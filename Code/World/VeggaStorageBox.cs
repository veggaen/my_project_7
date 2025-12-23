using System;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Simple persistent storage container. Server-authoritative.
/// </summary>
public sealed class VeggaStorageBox : Component
{
	[Property] public float InteractRange { get; set; } = 160f;
	[Property] public int Slots { get; set; } = 24;

	[Sync] public Guid ContainerId { get; private set; } = Guid.Empty;
	[Sync] private NetList<int> _itemIds { get; set; } = new();
	[Sync] private NetList<int> _counts { get; set; } = new();
	[Sync] private NetList<int> _durability { get; set; } = new();

	public int GetSlotItemId( int slot ) => slot >= 0 && slot < Slots ? _itemIds[slot] : 0;
	public int GetSlotCount( int slot ) => slot >= 0 && slot < Slots ? _counts[slot] : 0;
	public int GetSlotDurability( int slot ) => slot >= 0 && slot < Slots ? _durability[slot] : 0;

	protected override void OnStart()
	{
		EnsureSlotsSized();

		var idComp = Components.Get<VeggaWorldContainerId>() ?? Components.Create<VeggaWorldContainerId>();
		if ( Networking.IsHost && idComp.ContainerId == Guid.Empty )
			idComp.ContainerId = GameObject.Id;
		ContainerId = idComp.ContainerId;

		if ( Networking.IsHost )
		{
			TryLoad();
			Save();
		}
	}

	void EnsureSlotsSized()
	{
		if ( _itemIds.Count == Slots ) return;
		if ( Network.IsProxy ) return;

		_itemIds.Clear();
		_counts.Clear();
		_durability.Clear();
		for ( int i = 0; i < Slots; i++ )
		{
			_itemIds.Add( 0 );
			_counts.Add( 0 );
			_durability.Add( 0 );
		}
	}

	protected override void OnUpdate()
	{
		// Client-side: allow pressing E to open (including listen-server host).
		if ( Connection.Local != null )
			TryClientInteractOpen();
	}

	void TryClientInteractOpen()
	{
		if ( Sandbox.UI.VeggaChat.IsChatInputOpenGlobal )
			return;

		var local = PlayerVeggaStats.Local;
		if ( local == null || !local.IsValid() )
			return;

		var scene = Scene ?? Game.ActiveScene;
		var cam = scene?.Camera;
		if ( cam == null )
			return;

		var ray = cam.ScreenNormalToRay( new Vector2( 0.5f, 0.5f ) );
		var tr = scene.Trace.Ray( ray, InteractRange + 40f )
			.WithoutTags( "trigger", "particles" )
			.Run();

		if ( !tr.Hit )
			return;

		var hitObj = tr.GameObject;
		if ( hitObj == null )
			return;

		bool isThis = false;
		for ( var obj = hitObj; obj != null; obj = obj.Parent )
		{
			if ( obj == GameObject ) { isThis = true; break; }
		}
		if ( !isThis ) return;

		float dist = Vector3.DistanceBetween( local.WorldPosition, WorldPosition );
		if ( dist > InteractRange )
			return;

		if ( Input.Pressed( "use" ) )
		{
			var id = Connection.Local?.Id ?? Guid.Empty;
			RpcRequestOpen( id );
		}
	}

	[Rpc.Broadcast]
	void RpcRequestOpen( Guid requesterId )
	{
		if ( !Networking.IsHost ) return;
		if ( requesterId == Guid.Empty ) return;

		var stats = FindPlayerStatsById( requesterId );
		if ( stats == null ) return;

		float dist = Vector3.DistanceBetween( stats.WorldPosition, WorldPosition );
		if ( dist > InteractRange + 10f ) return;

		RpcOpenUi( requesterId, ContainerId );
	}

	[Rpc.Broadcast]
	static void RpcOpenUi( Guid targetId, Guid containerId )
	{
		if ( Connection.Local?.Id != targetId ) return;
		Sandbox.UI.StorageHud.Open( containerId );
	}

	public void RequestDepositFromPlayer( int fromSlot, int count )
	{
		var requesterId = Connection.Local?.Id ?? Guid.Empty;
		if ( requesterId == Guid.Empty ) return;
		RpcRequestDeposit( requesterId, fromSlot, count );
	}

	[Rpc.Broadcast]
	void RpcRequestDeposit( Guid requesterId, int fromSlot, int count )
	{
		if ( !Networking.IsHost ) return;
		if ( requesterId == Guid.Empty ) return;
		if ( count <= 0 ) return;

		var inv = FindPlayerInventoryById( requesterId );
		if ( inv == null ) return;
		if ( fromSlot < 0 || fromSlot >= inv.TotalSlots ) return;

		int itemId = inv.GetSlotItemId( fromSlot );
		int available = inv.GetSlotCount( fromSlot );
		int dur = inv.GetSlotDurability( fromSlot );
		if ( itemId <= 0 || available <= 0 ) return;

		int move = Math.Min( available, count );
		int moved = TryDepositItem( itemId, move, dur );
		if ( moved <= 0 ) return;

		inv.RpcConsumeFromSlot( requesterId, fromSlot, moved, itemId );
		Save();
	}

	public void RequestWithdrawToPlayer( int fromSlot, int count )
	{
		var requesterId = Connection.Local?.Id ?? Guid.Empty;
		if ( requesterId == Guid.Empty ) return;
		RpcRequestWithdraw( requesterId, fromSlot, count );
	}

	[Rpc.Broadcast]
	void RpcRequestWithdraw( Guid requesterId, int fromSlot, int count )
	{
		if ( !Networking.IsHost ) return;
		if ( requesterId == Guid.Empty ) return;
		if ( fromSlot < 0 || fromSlot >= Slots ) return;
		if ( count <= 0 ) return;

		var inv = FindPlayerInventoryById( requesterId );
		if ( inv == null ) return;

		int itemId = _itemIds[fromSlot];
		int available = _counts[fromSlot];
		int dur = _durability[fromSlot];
		if ( itemId <= 0 || available <= 0 ) return;

		int take = Math.Min( available, count );
		if ( !inv.CanFitItem( itemId, take ) ) return;

		_counts[fromSlot] = available - take;
		if ( _counts[fromSlot] <= 0 )
		{
			_itemIds[fromSlot] = 0;
			_durability[fromSlot] = 0;
		}

		inv.RpcGiveItemToOwnerWithDurability( requesterId, itemId, take, dur );
		Save();
	}

	int TryDepositItem( int itemId, int count, int durability )
	{
		if ( itemId <= 0 || count <= 0 ) return 0;

		var def = VeggaItemRegistry.Get( itemId );
		int maxStack = def?.MaxStack ?? VeggaInventory.MaxStackSize;
		if ( maxStack <= 0 ) maxStack = 1;
		if ( maxStack > VeggaInventory.MaxStackSize ) maxStack = VeggaInventory.MaxStackSize;
		bool isStackable = maxStack > 1;

		int remaining = count;

		// Stack into existing (only for stackables).
		if ( isStackable )
		{
			for ( int i = 0; i < Slots && remaining > 0; i++ )
			{
				if ( _itemIds[i] != itemId || _counts[i] <= 0 ) continue;
				int current = _counts[i];
				if ( current < 0 ) current = 0;
				if ( current >= maxStack ) continue;

				int add = Math.Min( remaining, maxStack - current );
				_counts[i] = current + add;
				remaining -= add;
			}
		}

		// Empty slot.
		for ( int i = 0; i < Slots && remaining > 0; i++ )
		{
			if ( _itemIds[i] != 0 ) continue;
			_itemIds[i] = itemId;
			int add = isStackable ? Math.Min( remaining, maxStack ) : 1;
			_counts[i] = add;
			_durability[i] = durability;
			remaining -= add;
		}

		return count - remaining;
	}

	void TryLoad()
	{
		if ( ContainerId == Guid.Empty ) return;
		if ( WorldContainerPersistence.TryLoad( ContainerId, out var save ) )
		{
			if ( save?.Kind == "storage" && save.ItemIds?.Length == Slots )
			{
				EnsureSlotsSized();
				for ( int i = 0; i < Slots; i++ )
				{
					_itemIds[i] = save.ItemIds[i];
					_counts[i] = save.Counts?[i] ?? 0;
					_durability[i] = save.Durability?[i] ?? 0;
				}
			}
		}
	}

	void Save()
	{
		if ( !Networking.IsHost ) return;
		if ( ContainerId == Guid.Empty ) return;

		WorldContainerPersistence.Store( ContainerId, new WorldContainerPersistence.ContainerSave
		{
			Kind = "storage",
			ItemIds = _itemIds.ToArray(),
			Counts = _counts.ToArray(),
			Durability = _durability.ToArray()
		} );
	}

	protected override void OnDisabled()
	{
		if ( Networking.IsHost )
			Save();
	}

	static PlayerVeggaStats FindPlayerStatsById( Guid id )
	{
		var scene = Game.ActiveScene;
		if ( scene == null ) return null;
		return scene.GetAllComponents<PlayerVeggaStats>()
			.FirstOrDefault( s => s != null && s.IsValid() && s.Network?.Owner?.Id == id );
	}

	static VeggaInventory FindPlayerInventoryById( Guid id )
	{
		var scene = Game.ActiveScene;
		if ( scene == null ) return null;
		return scene.GetAllComponents<VeggaInventory>()
			.FirstOrDefault( inv => inv != null && inv.IsValid() && inv.Network?.Owner?.Id == id );
	}
}
