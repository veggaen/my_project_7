using System;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Rust-like furnace with: 3 fuel slots, 2 input slots, 1 mould slot, 4 output slots.
/// Server-authoritative, persists contents via WorldContainerPersistence.
/// </summary>
public sealed class VeggaFurnace : Component
{
	public const int FuelSlot0 = 0;
	public const int InputSlotA = 1;
	public const int InputSlotB = 2;
	public const int MouldSlot = 3;
	public const int OutputSlot0 = 4;
	public const int OutputSlotCount = 4;
	public const int OutputSlotEndExclusive = OutputSlot0 + OutputSlotCount;
	public const int FuelSlot1 = OutputSlotEndExclusive;
	public const int FuelSlot2 = OutputSlotEndExclusive + 1;
	public const int SlotCount = OutputSlotEndExclusive + 2;

	// Back-compat alias (older code/UI referenced a single FuelSlot).
	public const int FuelSlot = FuelSlot0;

	static readonly int[] FuelSlots = new[] { FuelSlot0, FuelSlot1, FuelSlot2 };

	[Property] public float InteractRange { get; set; } = 160f;
	[Property] public float SmeltGramsPerSecond { get; set; } = 25f;
	[Property] public float FuelSecondsPerLog { get; set; } = 600f;
	[Property] public float FuelSecondsPerChoppedLog { get; set; } = 600f;

	// Sounds (as provided)
	[Property] public string IgniteSound { get; set; } = "sounds/furnace/start_furnace_sfx.mp3";
	[Property] public string BurnLoopSound { get; set; } = "sounds/furnace/loop_burning_sfx.mp3";
	[Property] public string ExtinguishSound { get; set; } = "sounds/furnace/stop_furnace_sfx.mp3";
	[Property] public float BurnLoopIntervalSeconds { get; set; } = 120f;

	[Sync] public Guid ContainerId { get; private set; } = Guid.Empty;

	[Sync] public bool IsOn { get; private set; }
	[Sync] public float FuelSecondsRemaining { get; private set; }

	[Sync] private NetList<int> _itemIds { get; set; } = new();
	[Sync] private NetList<int> _counts { get; set; } = new();
	[Sync] private NetList<int> _durability { get; set; } = new();

	// Casting/conversion buffer for multi-step jobs (coins/goblet).
	[Sync] public int CastBufferGrams { get; private set; }

	// Exposed for UI (progress bar + small status).
	[Sync] public FurnaceJobKind ActiveJob { get; private set; }
	[Sync] public float ActiveJobProgress01 { get; private set; }

	private float _smeltProgress;
	private float _burnLoopTimer;
	private Guid _lastInteractorId;
	private float _nextHintAt;

	protected override void OnStart()
	{
		EnsureSlotsSized();

		var idComp = Components.Get<VeggaWorldContainerId>() ?? Components.Create<VeggaWorldContainerId>();
		if ( Networking.IsHost && idComp.ContainerId == Guid.Empty )
			idComp.ContainerId = GameObject.Id;
		ContainerId = idComp.ContainerId;

		if ( Networking.IsHost )
		{
			var loadResult = TryLoad();
			// Avoid overwriting unknown/incompatible save formats.
			if ( loadResult != LoadResult.Incompatible )
				Save();
		}
	}

	static bool IsFuelItem( int itemId )
	{
		return itemId == VeggaItemIds.LogFull || itemId == VeggaItemIds.LogChopped;
	}

	static bool IsFuelSlotIndex( int slot )
	{
		return slot == FuelSlot0 || slot == FuelSlot1 || slot == FuelSlot2;
	}

	static bool IsOutputSlotIndex( int slot )
	{
		return slot >= OutputSlot0 && slot < OutputSlotEndExclusive;
	}

	void EnsureSlotsSized()
	{
		if ( _itemIds.Count == SlotCount ) return;
		if ( Network.IsProxy ) return;

		_itemIds.Clear();
		_counts.Clear();
		_durability.Clear();
		for ( int i = 0; i < SlotCount; i++ )
		{
			_itemIds.Add( 0 );
			_counts.Add( 0 );
			_durability.Add( 0 );
		}
	}

	public int GetSlotItemId( int slot ) => slot >= 0 && slot < SlotCount ? _itemIds[slot] : 0;
	public int GetSlotCount( int slot ) => slot >= 0 && slot < SlotCount ? _counts[slot] : 0;
	public int GetSlotDurability( int slot ) => slot >= 0 && slot < SlotCount ? _durability[slot] : 0;

	protected override void OnUpdate()
	{
		// Client-side: allow pressing E to open (including listen-server host).
		if ( Connection.Local != null )
			TryClientInteractOpen();

		if ( !Networking.IsHost )
			return;

		TickFuelAndSmelt();
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

		// Hit this object or any descendant.
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
		_lastInteractorId = requesterId;

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
		Sandbox.UI.FurnaceHud.Open( containerId );
	}

	public void RequestDepositFromPlayer( int fromSlot, int count )
	{
		var requesterId = Connection.Local?.Id ?? Guid.Empty;
		if ( requesterId == Guid.Empty ) return;
		RpcRequestDeposit( requesterId, fromSlot, count );
	}

	public void RequestDepositFromPlayerToSlot( int fromSlot, int count, int targetFurnaceSlot )
	{
		var requesterId = Connection.Local?.Id ?? Guid.Empty;
		if ( requesterId == Guid.Empty ) return;
		RpcRequestDepositToSlot( requesterId, fromSlot, count, targetFurnaceSlot );
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
		if ( itemId <= 0 || available <= 0 ) return;

		int move = Math.Min( available, count );
		if ( move <= 0 ) return;

		int moved = TryDepositItem( itemId, move, inv.GetSlotDurability( fromSlot ) );
		if ( moved <= 0 ) return;

		inv.RpcConsumeFromSlot( requesterId, fromSlot, moved, itemId );
		Save();
	}

	[Rpc.Broadcast]
	void RpcRequestDepositToSlot( Guid requesterId, int fromSlot, int count, int targetFurnaceSlot )
	{
		if ( !Networking.IsHost ) return;
		if ( requesterId == Guid.Empty ) return;
		if ( count <= 0 ) return;
		if ( targetFurnaceSlot < 0 || targetFurnaceSlot >= SlotCount ) return;
		// Never allow deposits directly into output slots.
		if ( IsOutputSlotIndex( targetFurnaceSlot ) ) return;

		var stats = FindPlayerStatsById( requesterId );
		if ( stats == null ) return;
		float dist = Vector3.DistanceBetween( stats.WorldPosition, WorldPosition );
		if ( dist > InteractRange + 10f ) return;

		var inv = FindPlayerInventoryById( requesterId );
		if ( inv == null ) return;
		if ( fromSlot < 0 || fromSlot >= inv.TotalSlots ) return;

		int itemId = inv.GetSlotItemId( fromSlot );
		int available = inv.GetSlotCount( fromSlot );
		if ( itemId <= 0 || available <= 0 ) return;

		int move = Math.Min( available, count );
		if ( move <= 0 ) return;

		int moved = TryDepositItemToSpecificSlot( itemId, move, inv.GetSlotDurability( fromSlot ), targetFurnaceSlot );
		if ( moved <= 0 ) return;

		inv.RpcConsumeFromSlot( requesterId, fromSlot, moved, itemId );
		Save();
	}

	int TryDepositItemToSpecificSlot( int itemId, int count, int durability, int targetSlot )
	{
		if ( count <= 0 ) return 0;
		if ( targetSlot < 0 || targetSlot >= SlotCount ) return 0;
		if ( IsOutputSlotIndex( targetSlot ) ) return 0;

		// Fuel
		if ( IsFuelSlotIndex( targetSlot ) )
		{
			if ( !IsFuelItem( itemId ) )
				return 0;
			return AddToSlotStackable( targetSlot, itemId, count );
		}

		// Mould
		if ( targetSlot == MouldSlot )
		{
			if ( itemId != VeggaItemIds.MouldCoin && itemId != VeggaItemIds.MouldGoblet )
				return 0;
			if ( _itemIds[MouldSlot] != 0 ) return 0;
			_itemIds[MouldSlot] = itemId;
			_counts[MouldSlot] = 1;
			_durability[MouldSlot] = 0;
			return 1;
		}

		// Inputs
		if ( targetSlot == InputSlotA || targetSlot == InputSlotB )
		{
			if ( itemId != VeggaItemIds.GoldOre
				&& itemId != VeggaItemIds.TinOre
				&& itemId != VeggaItemIds.CopperOre
				&& itemId != VeggaItemIds.IronOre
				&& itemId != VeggaItemIds.CoalOre
				&& itemId != VeggaItemIds.GoldBar200g
				&& itemId != VeggaItemIds.TinBar
				&& itemId != VeggaItemIds.CopperBar )
				return 0;

			if ( _itemIds[targetSlot] != 0 && _itemIds[targetSlot] != itemId )
				return 0;

			int moved = AddToSlotStackable( targetSlot, itemId, count );
			if ( moved > 0 )
			{
				var def = VeggaItemRegistry.Get( itemId );
				int maxStack = def?.MaxStack ?? VeggaInventory.MaxStackSize;
				// Preserve durability/variant only when this slot represents a single, non-stackable item.
				if ( maxStack <= 1 && _counts[targetSlot] == 1 )
					_durability[targetSlot] = durability;
				else if ( _counts[targetSlot] > 1 )
					_durability[targetSlot] = 0;
			}
			return moved;
		}

		return 0;
	}

	public void RequestWithdrawToPlayer( int fromFurnaceSlot, int count )
	{
		var requesterId = Connection.Local?.Id ?? Guid.Empty;
		if ( requesterId == Guid.Empty ) return;
		RpcRequestWithdraw( requesterId, fromFurnaceSlot, count );
	}

	[Rpc.Broadcast]
	void RpcRequestWithdraw( Guid requesterId, int fromFurnaceSlot, int count )
	{
		if ( !Networking.IsHost ) return;
		if ( requesterId == Guid.Empty ) return;
		if ( fromFurnaceSlot < 0 || fromFurnaceSlot >= SlotCount ) return;
		if ( count <= 0 ) return;

		var inv = FindPlayerInventoryById( requesterId );
		if ( inv == null ) return;

		int itemId = _itemIds[fromFurnaceSlot];
		int available = _counts[fromFurnaceSlot];
		int dur = _durability[fromFurnaceSlot];
		if ( itemId <= 0 || available <= 0 ) return;

		int take = Math.Min( available, count );
		if ( !inv.CanFitItem( itemId, take ) ) return;

		_counts[fromFurnaceSlot] = available - take;
		if ( _counts[fromFurnaceSlot] <= 0 )
		{
			_itemIds[fromFurnaceSlot] = 0;
			_durability[fromFurnaceSlot] = 0;
		}

		inv.RpcGiveItemToOwnerWithDurability( requesterId, itemId, take, dur );
		Save();
	}

	public void RequestToggleOn()
	{
		var requesterId = Connection.Local?.Id ?? Guid.Empty;
		if ( requesterId == Guid.Empty ) return;
		RpcToggleOn( requesterId );
	}

	[Rpc.Broadcast]
	void RpcToggleOn( Guid requesterId )
	{
		if ( !Networking.IsHost ) return;
		if ( requesterId == Guid.Empty ) return;
		_lastInteractorId = requesterId;
		IsOn = !IsOn;

		// If you toggled ON but we can't light due to no fuel, immediately toggle back off
		// and tell the player why.
		if ( IsOn && FuelSecondsRemaining <= 0f && !HasFuelItem() )
		{
			IsOn = false;
			TrySendHintToLastInteractor( "Add wood (log/chopped log) to the FUEL slot." );
		}
		Save();
	}

	bool HasFuelItem()
	{
		foreach ( var slot in FuelSlots )
		{
			int itemId = _itemIds[slot];
			int count = _counts[slot];
			if ( itemId <= 0 || count <= 0 )
				continue;
			if ( IsFuelItem( itemId ) )
				return true;
		}
		return false;
	}

	void TurnOff( bool playSound )
	{
		if ( !IsOn ) return;
		IsOn = false;
		_smeltProgress = 0f;
		ActiveJob = FurnaceJobKind.None;
		ActiveJobProgress01 = 0f;
		if ( playSound )
			RpcPlaySoundAt( WorldPosition, ExtinguishSound );
		Save();
	}

	void TrySendHintToLastInteractor( string message )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return;
		if ( _lastInteractorId == Guid.Empty )
			return;

		var scene = Scene ?? Game.ActiveScene;
		if ( scene == null )
			return;

		var mgr = scene.GetAllComponents<VeggaChatManager>()
			.FirstOrDefault( m => m != null && m.IsValid() );
		mgr?.RpcSendSystemMessage( _lastInteractorId, message, ChatMessageType.System );
	}

	int TryDepositItem( int itemId, int count, int durability )
	{
		if ( count <= 0 )
			return 0;

		// Fuel
		if ( IsFuelItem( itemId ) )
		{
			int moved = 0;
			int remaining = count;
			foreach ( var slot in FuelSlots )
			{
				if ( remaining <= 0 ) break;
				int added = AddToSlotStackable( slot, itemId, remaining );
				moved += added;
				remaining -= added;
			}
			return moved;
		}

		// Moulds
		if ( itemId == VeggaItemIds.MouldCoin || itemId == VeggaItemIds.MouldGoblet )
		{
			if ( _itemIds[MouldSlot] != 0 ) return 0;
			_itemIds[MouldSlot] = itemId;
			_counts[MouldSlot] = 1;
			_durability[MouldSlot] = 0;
			return 1;
		}

		// Inputs
		if ( itemId == VeggaItemIds.GoldOre
			|| itemId == VeggaItemIds.TinOre
			|| itemId == VeggaItemIds.CopperOre
			|| itemId == VeggaItemIds.IronOre
			|| itemId == VeggaItemIds.CoalOre
			|| itemId == VeggaItemIds.GoldBar200g
			|| itemId == VeggaItemIds.TinBar
			|| itemId == VeggaItemIds.CopperBar )
		{
			var def = VeggaItemRegistry.Get( itemId );
			int maxStack = def?.MaxStack ?? VeggaInventory.MaxStackSize;
			if ( maxStack <= 0 ) maxStack = 1;
			if ( maxStack > VeggaInventory.MaxStackSize ) maxStack = VeggaInventory.MaxStackSize;

			int moved = 0;
			int remaining = count;

			// Pass 1: top up existing stacks.
			if ( maxStack > 1 )
			{
				if ( remaining > 0 && _itemIds[InputSlotA] == itemId && _counts[InputSlotA] > 0 )
				{
					int add = AddToSlotStackable( InputSlotA, itemId, remaining );
					moved += add;
					remaining -= add;
					if ( add > 0 )
						_durability[InputSlotA] = 0;
				}
				if ( remaining > 0 && _itemIds[InputSlotB] == itemId && _counts[InputSlotB] > 0 )
				{
					int add = AddToSlotStackable( InputSlotB, itemId, remaining );
					moved += add;
					remaining -= add;
					if ( add > 0 )
						_durability[InputSlotB] = 0;
				}
			}

			// Pass 2: fill empty input slots.
			if ( remaining > 0 && _itemIds[InputSlotA] == 0 )
			{
				int add = AddToSlotStackable( InputSlotA, itemId, remaining );
				moved += add;
				remaining -= add;
				if ( add > 0 )
					_durability[InputSlotA] = (maxStack <= 1 && _counts[InputSlotA] == 1) ? durability : 0;
			}
			if ( remaining > 0 && _itemIds[InputSlotB] == 0 )
			{
				int add = AddToSlotStackable( InputSlotB, itemId, remaining );
				moved += add;
				remaining -= add;
				if ( add > 0 )
					_durability[InputSlotB] = (maxStack <= 1 && _counts[InputSlotB] == 1) ? durability : 0;
			}

			// If these are stackable items, durability should never persist.
			if ( maxStack > 1 )
			{
				if ( _counts[InputSlotA] > 1 ) _durability[InputSlotA] = 0;
				if ( _counts[InputSlotB] > 1 ) _durability[InputSlotB] = 0;
			}

			return moved;
		}

		return 0;
	}

	int AddToSlotStackable( int slot, int itemId, int count )
	{
		if ( count <= 0 ) return 0;
		if ( _itemIds[slot] != 0 && _itemIds[slot] != itemId ) return 0;

		var def = VeggaItemRegistry.Get( itemId );
		int maxStack = def?.MaxStack ?? VeggaInventory.MaxStackSize;
		if ( maxStack <= 0 ) maxStack = 1;
		if ( maxStack > VeggaInventory.MaxStackSize ) maxStack = VeggaInventory.MaxStackSize;

		// Non-stackable items (maxStack=1) can only occupy this slot if it's empty.
		if ( maxStack <= 1 )
		{
			if ( _itemIds[slot] != 0 && _counts[slot] > 0 )
				return 0;
			_itemIds[slot] = itemId;
			_counts[slot] = 1;
			return 1;
		}

		_itemIds[slot] = itemId;
		int current = _counts[slot];
		if ( current < 0 ) current = 0;
		if ( current >= maxStack ) return 0;

		int add = Math.Min( count, maxStack - current );
		_counts[slot] = current + add;
		return add;
	}

	void TickFuelAndSmelt()
	{
		float dt = Time.Delta;
		if ( dt <= 0f ) return;

		if ( IsOn )
		{
			// Consume fuel if needed.
			if ( FuelSecondsRemaining <= 0f )
			{
				if ( TryConsumeOneFuelItem( out float secondsAdded ) )
				{
					FuelSecondsRemaining = secondsAdded;
					_burnLoopTimer = 0f;
					RpcPlaySoundAt( WorldPosition, IgniteSound );
					RpcPlaySoundAt( WorldPosition, BurnLoopSound );
				}
				else
				{
					// Out of fuel: shut off so UI state updates.
					TrySendHintToLastInteractor( "Out of fuel. Add wood to keep the furnace running." );
					TurnOff( playSound: true );
					return;
				}
			}

			if ( FuelSecondsRemaining > 0f )
			{
				FuelSecondsRemaining = Math.Max( 0f, FuelSecondsRemaining - dt );

				// Loop the burning audio by replaying the loop clip on an interval.
				// (If you later switch to a proper .sound looping event, this can be removed.)
				_burnLoopTimer += dt;
				if ( BurnLoopIntervalSeconds > 0f && _burnLoopTimer >= BurnLoopIntervalSeconds )
				{
					_burnLoopTimer = 0f;
					RpcPlaySoundAt( WorldPosition, BurnLoopSound );
				}

				TickSmelting( dt );

				// If fuel just ran out and there's no more to consume, shut off.
				if ( FuelSecondsRemaining <= 0f && !HasFuelItem() )
				{
					TrySendHintToLastInteractor( "Out of fuel. Add wood to keep the furnace running." );
					TurnOff( playSound: true );
					return;
				}
			}
			else
			{
				_smeltProgress = 0f;
				ActiveJob = FurnaceJobKind.None;
				ActiveJobProgress01 = 0f;
			}
		}
	}

	bool TryConsumeOneFuelItem( out float secondsAdded )
	{
		secondsAdded = 0f;
		foreach ( var slot in FuelSlots )
		{
			int itemId = _itemIds[slot];
			int count = _counts[slot];
			if ( itemId <= 0 || count <= 0 )
				continue;

			if ( itemId == VeggaItemIds.LogFull ) secondsAdded = FuelSecondsPerLog;
			else if ( itemId == VeggaItemIds.LogChopped ) secondsAdded = FuelSecondsPerChoppedLog;
			else
				continue;

			_counts[slot] = count - 1;
			if ( _counts[slot] <= 0 )
			{
				_itemIds[slot] = 0;
				_durability[slot] = 0;
			}

			return secondsAdded > 0f;
		}

		return false;
	}

	void TickSmelting( float dt )
	{
		if ( SmeltGramsPerSecond <= 0f ) return;

		int mould = _itemIds[MouldSlot];

		// New economy (no lumps):
		// - No mould: ore -> bar, bronze (tin bar + copper bar), steel (iron ore + 2 coal ore)
		// - Coin mould: gold bar -> 7 coins
		// - Goblet mould: 2 gold bars -> goblet
		bool canGoblet = mould == VeggaItemIds.MouldGoblet && CountInput( VeggaItemIds.GoldBar200g ) >= 2;
		bool canCoins = mould == VeggaItemIds.MouldCoin && HasAnyInput( VeggaItemIds.GoldBar200g );

		bool canBronze = mould == 0 && HasAnyInput( VeggaItemIds.TinBar ) && HasAnyInput( VeggaItemIds.CopperBar );
		bool canSteel = mould == 0 && CountInput( VeggaItemIds.IronOre ) >= 1 && CountInput( VeggaItemIds.CoalOre ) >= 2;

		bool canGoldBar = mould == 0 && HasAnyInput( VeggaItemIds.GoldOre );
		bool canTinBar = mould == 0 && HasAnyInput( VeggaItemIds.TinOre );
		bool canCopperBar = mould == 0 && HasAnyInput( VeggaItemIds.CopperOre );
		bool canIronBar = mould == 0 && HasAnyInput( VeggaItemIds.IronOre );

		// Priority: bronze first, then steel, then mould conversions, then ore->bars.
		FurnaceJobKind job = FurnaceJobKind.None;
		if ( canBronze ) job = FurnaceJobKind.BarsToBronzeBar;
		else if ( canSteel ) job = FurnaceJobKind.OreToSteelBar;
		else if ( canGoblet ) job = FurnaceJobKind.BarsToGoblet;
		else if ( canCoins ) job = FurnaceJobKind.GoldBarToCoins;
		else if ( canGoldBar ) job = FurnaceJobKind.OreToGoldBar;
		else if ( canTinBar ) job = FurnaceJobKind.OreToTinBar;
		else if ( canCopperBar ) job = FurnaceJobKind.OreToCopperBar;
		else if ( canIronBar ) job = FurnaceJobKind.OreToIronBar;
		else
		{
			_smeltProgress = 0f;
			ActiveJob = FurnaceJobKind.None;
			ActiveJobProgress01 = 0f;
			TrySendNoRecipeHint();
			return;
		}

		if ( ActiveJob != job )
		{
			// Don't let progress from one recipe bleed into another.
			CastBufferGrams = 0;
		}
		ActiveJob = job;

		_smeltProgress += dt * SmeltGramsPerSecond;
		int unitsThisTick = (int)_smeltProgress;
		if ( unitsThisTick <= 0 ) return;
		_smeltProgress -= unitsThisTick;

		switch ( job )
		{
			case FurnaceJobKind.OreToGoldBar:
				ProcessOreToBar( unitsThisTick, VeggaItemIds.GoldOre, VeggaItemIds.GoldBar200g );
				break;
			case FurnaceJobKind.OreToTinBar:
				ProcessOreToBar( unitsThisTick, VeggaItemIds.TinOre, VeggaItemIds.TinBar );
				break;
			case FurnaceJobKind.OreToCopperBar:
				ProcessOreToBar( unitsThisTick, VeggaItemIds.CopperOre, VeggaItemIds.CopperBar );
				break;
			case FurnaceJobKind.OreToIronBar:
				ProcessOreToBar( unitsThisTick, VeggaItemIds.IronOre, VeggaItemIds.IronBar );
				break;
			case FurnaceJobKind.BarsToBronzeBar:
				ProcessBronzeBar( unitsThisTick );
				break;
			case FurnaceJobKind.OreToSteelBar:
				ProcessSteelBar( unitsThisTick );
				break;
			case FurnaceJobKind.GoldBarToCoins:
				ProcessGoldBarToCoins( unitsThisTick );
				break;
			case FurnaceJobKind.BarsToGoblet:
				ProcessGoldBarsToGoblet( unitsThisTick );
				break;
		}

		ActiveJobProgress01 = ComputeActiveProgress01( job );
		Save();
	}

	float ComputeActiveProgress01( FurnaceJobKind job )
	{
		switch ( job )
		{
			case FurnaceJobKind.GoldBarToCoins:
				return Math.Clamp( CastBufferGrams / 1f, 0f, 1f );
			case FurnaceJobKind.BarsToGoblet:
				return Math.Clamp( CastBufferGrams / 2f, 0f, 1f );
			default:
				return Math.Clamp( _smeltProgress, 0f, 1f );
		}
	}

	void ProcessOreToBar( int units, int oreItemId, int barItemId )
	{
		for ( int i = 0; i < units; i++ )
		{
			if ( !TryConsumeInput( oreItemId, 1 ) ) return;
			if ( !TryAddToOutput( barItemId, 1 ) ) { RollbackInput( oreItemId, 1 ); return; }
		}
	}

	void ProcessBronzeBar( int units )
	{
		for ( int i = 0; i < units; i++ )
		{
			if ( !TryConsumeInput( VeggaItemIds.CopperBar, 1 ) ) return;
			if ( !TryConsumeInput( VeggaItemIds.TinBar, 1 ) ) { RollbackInput( VeggaItemIds.CopperBar, 1 ); return; }
			if ( !TryAddToOutput( VeggaItemIds.BronzeBar, 1 ) )
			{
				RollbackInput( VeggaItemIds.TinBar, 1 );
				RollbackInput( VeggaItemIds.CopperBar, 1 );
				return;
			}
		}
	}

	void ProcessSteelBar( int units )
	{
		for ( int i = 0; i < units; i++ )
		{
			if ( !TryConsumeInput( VeggaItemIds.IronOre, 1 ) ) return;
			if ( !TryConsumeInput( VeggaItemIds.CoalOre, 2 ) ) { RollbackInput( VeggaItemIds.IronOre, 1 ); return; }
			if ( !TryAddToOutput( VeggaItemIds.SteelBar, 1 ) )
			{
				RollbackInput( VeggaItemIds.CoalOre, 2 );
				RollbackInput( VeggaItemIds.IronOre, 1 );
				return;
			}
		}
	}

	void ProcessGoldBarToCoins( int units )
	{
		for ( int i = 0; i < units; i++ )
		{
			CastBufferGrams += 1;
			if ( CastBufferGrams < 1 ) continue;

			if ( !TryConsumeInput( VeggaItemIds.GoldBar200g, 1 ) ) { CastBufferGrams = 0; return; }
			if ( !TryAddToOutput( VeggaItemIds.GoldCoin, 7 ) )
			{
				RollbackInput( VeggaItemIds.GoldBar200g, 1 );
				CastBufferGrams = 1;
				return;
			}

			CastBufferGrams -= 1;
		}
	}

	void ProcessGoldBarsToGoblet( int units )
	{
		for ( int i = 0; i < units; i++ )
		{
			CastBufferGrams += 1;
			if ( CastBufferGrams < 2 ) continue;

			if ( !TryConsumeInput( VeggaItemIds.GoldBar200g, 2 ) ) { CastBufferGrams = 2; return; }
			if ( !TryAddToOutput( VeggaItemIds.GoldGoblet, 1 ) )
			{
				RollbackInput( VeggaItemIds.GoldBar200g, 2 );
				CastBufferGrams = 2;
				return;
			}

			CastBufferGrams -= 2;
		}
	}

	void TrySendNoRecipeHint()
	{
		// Rate limit so holding the furnace ON doesn't spam.
		if ( Time.Now < _nextHintAt )
			return;
		_nextHintAt = Time.Now + 2.0f;

		int mould = _itemIds[MouldSlot];
		bool hasOre = HasAnyInput( VeggaItemIds.GoldOre )
			|| HasAnyInput( VeggaItemIds.TinOre )
			|| HasAnyInput( VeggaItemIds.CopperOre )
			|| HasAnyInput( VeggaItemIds.IronOre )
			|| HasAnyInput( VeggaItemIds.CoalOre );
		bool hasGoldBar = HasAnyInput( VeggaItemIds.GoldBar200g );
		bool hasBars = hasGoldBar || HasAnyInput( VeggaItemIds.TinBar ) || HasAnyInput( VeggaItemIds.CopperBar );

		if ( mould == 0 )
		{
			if ( hasOre )		{ TrySendHintToLastInteractor( "No mould: smelt ore into bars. Steel: 1 iron ore + 2 coal ore." ); return; }
			if ( hasBars )		{ TrySendHintToLastInteractor( "No mould: bronze = 1 copper bar + 1 tin bar. Coin mould: gold bar → coins." ); return; }
		}
		else if ( mould == VeggaItemIds.MouldCoin )
		{
			if ( hasGoldBar ) { TrySendHintToLastInteractor( "Coin Mould: 1 gold bar → 7 coins." ); return; }
			TrySendHintToLastInteractor( "Coin Mould needs a gold bar in INPUT." );
			return;
		}
		else if ( mould == VeggaItemIds.MouldGoblet )
		{
			TrySendHintToLastInteractor( "Goblet Mould: needs 2 gold bars." );
			return;
		}

		TrySendHintToLastInteractor( "No valid recipe. Check mould + inputs." );
	}

	bool TryAddToOutput( int itemId, int count )
	{
		if ( itemId <= 0 || count <= 0 ) return false;

		var def = VeggaItemRegistry.Get( itemId );
		int maxStack = def?.MaxStack ?? 1;
		bool isStackable = maxStack > 1;

		if ( isStackable )
		{
			// Try stack into existing output slot.
			for ( int s = OutputSlot0; s < OutputSlotEndExclusive; s++ )
			{
				if ( _itemIds[s] != itemId ) continue;
				_counts[s] += count;
				return true;
			}

			// Find empty.
			for ( int s = OutputSlot0; s < OutputSlotEndExclusive; s++ )
			{
				if ( _itemIds[s] != 0 ) continue;
				_itemIds[s] = itemId;
				_counts[s] = count;
				_durability[s] = 0;
				return true;
			}

			return false;
		}

		// Non-stackable: require one empty output slot per item.
		int remaining = count;
		for ( int s = OutputSlot0; s < OutputSlotEndExclusive && remaining > 0; s++ )
		{
			if ( _itemIds[s] != 0 ) continue;
			_itemIds[s] = itemId;
			_counts[s] = 1;
			_durability[s] = 0;
			remaining--;
		}
		return remaining <= 0;
	}

	bool HasAnyInput( int itemId ) => _itemIds[InputSlotA] == itemId && _counts[InputSlotA] > 0
		|| _itemIds[InputSlotB] == itemId && _counts[InputSlotB] > 0;

	int CountInput( int itemId )
	{
		int total = 0;
		if ( _itemIds[InputSlotA] == itemId ) total += _counts[InputSlotA];
		if ( _itemIds[InputSlotB] == itemId ) total += _counts[InputSlotB];
		return total;
	}

	bool TryConsumeInput( int itemId, int count )
	{
		if ( count <= 0 ) return true;
		int remaining = count;
		if ( _itemIds[InputSlotA] == itemId && _counts[InputSlotA] > 0 )
		{
			int take = Math.Min( remaining, _counts[InputSlotA] );
			_counts[InputSlotA] -= take;
			remaining -= take;
			if ( _counts[InputSlotA] <= 0 ) { _itemIds[InputSlotA] = 0; _durability[InputSlotA] = 0; }
		}
		if ( remaining > 0 && _itemIds[InputSlotB] == itemId && _counts[InputSlotB] > 0 )
		{
			int take = Math.Min( remaining, _counts[InputSlotB] );
			_counts[InputSlotB] -= take;
			remaining -= take;
			if ( _counts[InputSlotB] <= 0 ) { _itemIds[InputSlotB] = 0; _durability[InputSlotB] = 0; }
		}
		return remaining <= 0;
	}

	void RollbackInput( int itemId, int count )
	{
		// Best-effort: put back into first input slot, otherwise second.
		if ( count <= 0 ) return;
		if ( _itemIds[InputSlotA] == 0 || _itemIds[InputSlotA] == itemId )
		{
			_itemIds[InputSlotA] = itemId;
			_counts[InputSlotA] += count;
			return;
		}
		if ( _itemIds[InputSlotB] == 0 || _itemIds[InputSlotB] == itemId )
		{
			_itemIds[InputSlotB] = itemId;
			_counts[InputSlotB] += count;
		}
	}

	enum LoadResult
	{
		NotFound = 0,
		Loaded = 1,
		Incompatible = 2
	}

	LoadResult TryLoad()
	{
		if ( ContainerId == Guid.Empty ) return LoadResult.NotFound;
		if ( !WorldContainerPersistence.TryLoad( ContainerId, out var save ) )
			return LoadResult.NotFound;
		if ( save?.Kind != "furnace" || save.ItemIds == null )
			return LoadResult.Incompatible;

		const int LegacySlotCount = 8;
		if ( save.ItemIds.Length != SlotCount && save.ItemIds.Length != LegacySlotCount )
		{
			Log.Warning( $"Furnace {ContainerId} save slot count {save.ItemIds.Length} is incompatible with current slot count {SlotCount}." );
			return LoadResult.Incompatible;
		}

		EnsureSlotsSized();

		// Copy legacy slots 0..7 as-is; new extra fuel slots start empty.
		int copyLen = Math.Min( save.ItemIds.Length, OutputSlotEndExclusive );
		for ( int i = 0; i < copyLen; i++ )
		{
			_itemIds[i] = save.ItemIds[i];
			_counts[i] = (save.Counts != null && i < save.Counts.Length) ? save.Counts[i] : 0;
			_durability[i] = (save.Durability != null && i < save.Durability.Length) ? save.Durability[i] : 0;
		}

		for ( int i = OutputSlotEndExclusive; i < SlotCount; i++ )
		{
			_itemIds[i] = 0;
			_counts[i] = 0;
			_durability[i] = 0;
		}

		FuelSecondsRemaining = save.FuelSecondsRemaining;
		IsOn = save.IsOn;
		_smeltProgress = save.JobProgress;
		CastBufferGrams = save.CastBufferGrams;
		return LoadResult.Loaded;
	}

	void Save()
	{
		if ( !Networking.IsHost ) return;
		if ( ContainerId == Guid.Empty ) return;

		WorldContainerPersistence.Store( ContainerId, new WorldContainerPersistence.ContainerSave
		{
			Kind = "furnace",
			ItemIds = _itemIds.ToArray(),
			Counts = _counts.ToArray(),
			Durability = _durability.ToArray(),
			FuelSecondsRemaining = FuelSecondsRemaining,
			IsOn = IsOn,
			JobProgress = _smeltProgress,
			CastBufferGrams = CastBufferGrams
		} );
	}

	protected override void OnDisabled()
	{
		if ( Networking.IsHost )
		{
			Save();
			if ( IsOn )
				RpcPlaySoundAt( WorldPosition, ExtinguishSound );
		}
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

	[Rpc.Broadcast]
	static void RpcPlaySoundAt( Vector3 pos, string soundName )
	{
		if ( string.IsNullOrWhiteSpace( soundName ) ) return;
		Sound.Play( soundName, pos );
	}

	public enum FurnaceJobKind
	{
		None = 0,
		OreToGoldBar = 1,
		OreToTinBar = 2,
		OreToCopperBar = 3,
		OreToIronBar = 4,
		BarsToBronzeBar = 5,
		OreToSteelBar = 6,
		GoldBarToCoins = 7,
		BarsToGoblet = 8
	}
}
