using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace Sandbox;

/// <summary>
/// Player inventory system with 96-1024 slots.
/// Starts with 96 slots (12x8 grid), can expand to 1024 through upgrades.
/// </summary>
public sealed class VeggaInventory : Component
{
	static bool DebugNet => false;
	const float DropMaxDistance = 65f;
	const float DropAimTraceDistance = 2000f;
	// Allow third-person cameras to be behind the player while still rejecting totally bogus rays.
	const float DropRayOriginMaxError = 400f;

	// Hotbar (action bar) occupies slots 0..8.
	const int HotbarSlotCount = 9;

	static bool IsHotbarSlot( int slotIndex ) => slotIndex >= 0 && slotIndex < HotbarSlotCount;

	static bool IsHotbarBlockedItem( int itemId )
	{
		if ( itemId <= 0 ) return false;
		// Only allow items that are directly usable from the action bar.
		// (Meds/consumables, weapons/tools/equipment)
		var def = VeggaItemRegistry.Get( itemId );
		return def != null && def.Category is not ItemCategory.Consumable and not ItemCategory.Equipment;
	}

	int CountFreeSlotsForItem( int itemId )
	{
		int limit = Math.Min( _itemIds.Count, TotalSlots );
		if ( limit <= 0 ) return 0;

		bool blockHotbar = IsHotbarBlockedItem( itemId );
		int start = blockHotbar ? HotbarSlotCount : 0;
		if ( start < 0 ) start = 0;
		if ( start >= limit ) return 0;

		int free = 0;
		for ( int i = start; i < limit; i++ )
		{
			if ( IsEmptySlot( i ) )
				free++;
		}
		return free;
	}

	struct PredictedDrop
	{
		public int SlotIndex;
		public int ItemId;
		public int Count;
		public int Durability;
	}

	// Client-only: when dropping to world, we optimistically remove items so UI updates instantly.
	// The host spawns the world drop and acks/rejects the request.
	static readonly Dictionary<Guid, PredictedDrop> _predictedDrops = new();

	// ---- Constants ----
	public const int BaseSlots = 96; // 12x8 grid
	public const int MaxSlots = 1024; // Maximum possible slots
	// Global hard cap for stack sizes. Keep this sane to prevent overflow/UX issues.
	// Item defs can set lower MaxStack values per-item.
	public const int MaxStackSize = 100000;
	const int DurableStackMarker = 0; // durability==0 means "full" when the item uses durability

	internal static bool IsCurrencyItemId( int itemId )
		=> itemId == Sandbox.Money.VeggaCurrency.CashItemId || itemId == Sandbox.Money.VeggaCurrency.GoldCoinItemId;

	static List<int> SplitIntoWorldDropChunks( int itemId, int count, VeggaItemDef def )
	{
		var chunks = new List<int>();
		if ( count <= 0 )
			return chunks;

		// Cash: spawn exactly ONE world entity that represents the full amount.
		// (Visuals are still chosen by amount via VeggaCurrency.GetCashPrefabPathForAmount.)
		if ( itemId == Sandbox.Money.VeggaCurrency.CashItemId )
		{
			chunks.Add( count );
			return chunks;
		}

		int maxStack = GetEffectiveMaxStackForItem( itemId, def );
		if ( maxStack <= 0 ) maxStack = 1;
		int r = count;
		while ( r > maxStack )
		{
			chunks.Add( maxStack );
			r -= maxStack;
		}
		if ( r > 0 )
			chunks.Add( r );
		return chunks;
	}

	static Vector3 GetCashBoxPalletEndPos( Vector3 baseEndPos, Rotation basis, int boxIndex, int boxCount )
	{
		if ( boxCount <= 1 )
			return baseEndPos;

		// Make a compact pallet-style grid that grows with count.
		// For very large counts, increase footprint so the stack isn't absurdly tall.
		int targetGrid = (int)MathF.Ceiling( MathF.Sqrt( MathF.Min( boxCount, 100 ) ) );
		int cols = Math.Clamp( targetGrid, 3, 10 );
		int rows = cols;
		int perLayer = cols * rows;
		int layer = boxIndex / perLayer;
		int within = boxIndex % perLayer;
		int row = within / cols;
		int col = within % cols;

		// Tuned spacing for money box collider scale.
		// SpacingZ too high makes upper layers float; spacingX/Y too small makes boxes overlap.
		const float spacingX = 33.0f;
		const float spacingY = 19.0f;
		const float spacingZ = 30.2f;

		float centeredX = col - (cols - 1) * 0.5f;
		float centeredY = row - (rows - 1) * 0.5f;
		var offset = basis.Right * (centeredX * spacingX) + basis.Forward * (centeredY * spacingY) + Vector3.Up * (layer * spacingZ);

		// Lift slightly so boxes don't spawn intersecting terrain seams.
		return baseEndPos + offset + Vector3.Up * 3.0f;
	}

	internal static int GetEffectiveMaxStackForItem( int itemId, VeggaItemDef def = null )
	{
		if ( itemId <= 0 ) return 1;

		// OSRS-style: currency stacks effectively up to int.MaxValue.
		if ( IsCurrencyItemId( itemId ) )
			return int.MaxValue;

		// Noted items should behave like currency stacks (huge stacks) but are represented as a page in-world.
		def ??= VeggaItemRegistry.Get( itemId );
		if ( def != null && string.Equals( def.PrefabPath, "noted_page.prefab", StringComparison.OrdinalIgnoreCase ) )
			return int.MaxValue;

		int maxStack = def?.MaxStack ?? MaxStackSize;
		if ( maxStack <= 0 ) maxStack = 1;
		if ( maxStack > MaxStackSize ) maxStack = MaxStackSize;
		return maxStack;
	}

	// ---- Inventory Data ----
	[Sync] private NetList<int> _itemIds { get; set; } = new();
	[Sync] private NetList<int> _itemCounts { get; set; } = new();
	// Optional per-item durability/grams/etc. Index-aligned with _itemIds.
	// For items that don't use durability, this will be 0.
	[Sync] private NetList<int> _itemDurability { get; set; } = new();

	// ---- Slot Upgrades ----
	[Sync] public int BonusSlotsFromRank { get; private set; } = 0;
	[Sync] public int BonusSlotsFromQuests { get; private set; } = 0;
	[Sync] public int BonusSlotsFromPurchase { get; private set; } = 0;

	// ---- Change Tracking (UI) ----
	// Increment whenever inventory contents change so UI can re-render reliably.
	[Sync] public int Revision { get; private set; } = 0;

	// ---- Calculated Properties ----
	public int TotalSlots => Math.Min( BaseSlots + BonusSlotsFromRank + BonusSlotsFromQuests + BonusSlotsFromPurchase, MaxSlots );
	public int UsedSlots => CountUsedSlots();
	public int FreeSlots => TotalSlots - UsedSlots;

	private int CountUsedSlots()
	{
		int used = 0;
		int limit = Math.Min( _itemIds.Count, TotalSlots );
		for ( int i = 0; i < limit; i++ )
		{
			if ( _itemIds[i] > 0 && _itemCounts[i] > 0 )
				used++;
		}
		return used;
	}

	private void EnsureSlotArraysSized()
	{
		if ( Network.IsProxy ) return;

		int desired = TotalSlots;
		if ( desired < 0 ) desired = 0;
		if ( desired > MaxSlots ) desired = MaxSlots;

		// Grow
		while ( _itemIds.Count < desired )
		{
			_itemIds.Add( 0 );
			_itemCounts.Add( 0 );
			_itemDurability.Add( 0 );
		}

		// Shrink
		while ( _itemIds.Count > desired )
		{
			int last = _itemIds.Count - 1;
			_itemIds.RemoveAt( last );
			_itemCounts.RemoveAt( last );
			_itemDurability.RemoveAt( last );
		}
	}

	private bool IsEmptySlot( int slotIndex )
	{
		if ( slotIndex < 0 || slotIndex >= _itemIds.Count ) return true;
		return _itemIds[slotIndex] <= 0 || _itemCounts[slotIndex] <= 0;
	}

	private void ClearSlot( int slotIndex )
	{
		if ( slotIndex < 0 || slotIndex >= _itemIds.Count ) return;
		_itemIds[slotIndex] = 0;
		_itemCounts[slotIndex] = 0;
		_itemDurability[slotIndex] = 0;
	}

	/// <summary>
	/// Host-to-owner: add items to the owning client's inventory.
	/// This is used by server-authoritative world interactions (eg. pickups) where
	/// the host validates, then instructs the owner to mutate their [Sync] inventory.
	/// </summary>
	[Rpc.Broadcast]
	public void RpcGiveItemToOwner( Guid targetId, int itemId, int count )
	{
		if ( Connection.Local?.Id != targetId ) return;
		if ( Network.IsProxy ) return;
		if ( DebugNet ) Log.Info( $"[Inv] RpcGiveItemToOwner targetId={targetId} itemId={itemId} count={count}" );
		if ( itemId <= 0 || count <= 0 ) return;
		AddItem( itemId, count );
	}

	[Rpc.Broadcast]
	public void RpcGiveItemToOwnerWithDurability( Guid targetId, int itemId, int count, int durability )
	{
		if ( Connection.Local?.Id != targetId ) return;
		if ( Network.IsProxy ) return;
		if ( DebugNet ) Log.Info( $"[Inv] RpcGiveItemToOwnerWithDurability targetId={targetId} itemId={itemId} count={count} dur={durability}" );
		if ( itemId <= 0 || count <= 0 ) return;
		AddItemWithDurability( itemId, count, durability );
	}

	// ---- Local Singleton ----
	private static VeggaInventory _local;
	public static VeggaInventory Local
	{
		get
		{
			var localConn = Connection.Local;
			if ( _local is not null && _local.IsValid )
			{
				// Guard against stale caches across respawn/scene reloads.
				// If we return a proxy inventory, client-side actions (drop/move) can become delayed.
				if ( _local.Network?.IsProxy == false && _local.Network?.Owner == localConn )
					return _local;
				_local = null;
			}

			var scene = Game.ActiveScene;
			if ( scene != null && localConn != null )
			{
				foreach ( var inv in scene.GetAllComponents<VeggaInventory>() )
				{
					if ( inv != null && inv.IsValid() && inv.Network?.Owner == localConn )
					{
						_local = inv;
						return _local;
					}
				}
			}

			// Fallback: use local stats accessor.
			var player = PlayerVeggaStats.Local;
			if ( player != null )
				_local = player.GameObject.Components.Get<VeggaInventory>();

			return _local;
		}
	}

	protected override void OnStart()
	{
			if ( Networking.IsHost && !Network.IsProxy )
			{
				EnsureSlotArraysSized();
			}
		Log.Info( $"✅ VeggaInventory: {TotalSlots} slots available ({UsedSlots} used, {FreeSlots} free)" );
	}

	// ---- Add Item ----
	public bool AddItem( int itemId, int count = 1 )
	{
		if ( Network.IsProxy ) return false;
		if ( itemId <= 0 || count <= 0 ) return false;
			EnsureSlotArraysSized();

		bool blockHotbar = IsHotbarBlockedItem( itemId );

		var def = VeggaItemRegistry.Get( itemId );
		int maxStack = GetEffectiveMaxStackForItem( itemId, def );
		bool isStackable = maxStack > 1;
		bool usesDurability = def != null && def.MaxGrams > 0;
		int maxDurability = usesDurability ? def.MaxGrams : 0;

		// Non-stackable: each count consumes a slot.
		if ( !isStackable )
		{
			int freeSlots = blockHotbar ? CountFreeSlotsForItem( itemId ) : FreeSlots;
			if ( freeSlots < count )
			{
				Log.Warning( "❌ Inventory full!" );
				return false;
			}

			int remainingToPlace = count;
			int start = blockHotbar ? HotbarSlotCount : 0;
			for ( int i = start; i < _itemIds.Count && remainingToPlace > 0; i++ )
			{
				if ( !IsEmptySlot( i ) )
					continue;

				_itemIds[i] = itemId;
				_itemCounts[i] = 1;
				_itemDurability[i] = usesDurability ? maxDurability : 0;
				remainingToPlace--;
			}

			if ( remainingToPlace > 0 )
			{
				Log.Warning( "❌ Inventory full!" );
				return false;
			}

			Revision++;
			Log.Info( $"📦 Added item {itemId} x{count} to inventory" );
			return true;
		}

		// Stackable: pre-check that we can fit the whole amount.
		long remaining = count;
		long capacityInExisting = 0;
		for ( int i = 0; i < _itemIds.Count; i++ )
		{
			if ( blockHotbar && IsHotbarSlot( i ) )
				continue;
			if ( _itemIds[i] != itemId || _itemCounts[i] <= 0 )
				continue;

			// For durability items (eg. gold bars), we only stack into "full stacks".
			// Partial items stay as separate 1-count entries with durability > 0.
			if ( usesDurability )
			{
				int dur = i < _itemDurability.Count ? _itemDurability[i] : 0;
				bool isPartial = dur > 0 && dur < maxDurability;
				if ( isPartial )
					continue;
			}

			int current = _itemCounts[i];
			if ( current < 0 ) current = 0;
			if ( current > maxStack ) current = maxStack;
			capacityInExisting += (maxStack - current);
			if ( capacityInExisting >= remaining )
				break;
		}

		long needAfterExisting = Math.Max( 0, remaining - capacityInExisting );
		int newStacksNeeded = needAfterExisting > 0 ? (int)((needAfterExisting + (long)maxStack - 1) / (long)maxStack) : 0;
		int freeSlotsForItem = blockHotbar ? CountFreeSlotsForItem( itemId ) : FreeSlots;
		if ( freeSlotsForItem < newStacksNeeded )
		{
			Log.Warning( "❌ Inventory full!" );
			return false;
		}

		// Fill existing stacks first.
		for ( int i = 0; i < _itemIds.Count && remaining > 0; i++ )
		{
			if ( blockHotbar && IsHotbarSlot( i ) )
				continue;
			if ( _itemIds[i] != itemId || _itemCounts[i] <= 0 )
				continue;

			if ( usesDurability )
			{
				int dur = i < _itemDurability.Count ? _itemDurability[i] : 0;
				bool isPartial = dur > 0 && dur < maxDurability;
				if ( isPartial )
					continue;

				// If this is a single full durable item, normalize it into a stack marker.
				if ( dur >= maxDurability && _itemCounts[i] == 1 )
				{
					_itemDurability[i] = DurableStackMarker;
				}
			}

			int current = _itemCounts[i];
			if ( current < 0 ) current = 0;
			if ( current >= maxStack )
				continue;

			int add = (int)Math.Min( (long)(maxStack - current), remaining );
			_itemCounts[i] = current + add;
			remaining -= add;
		}

		// Add new stacks into empty slots as needed.
		for ( int i = 0; i < _itemIds.Count && remaining > 0; i++ )
		{
			if ( blockHotbar && IsHotbarSlot( i ) )
				continue;
			if ( !IsEmptySlot( i ) )
				continue;

			int add = (int)Math.Min( (long)maxStack, remaining );
			_itemIds[i] = itemId;
			_itemCounts[i] = add;
			_itemDurability[i] = usesDurability ? DurableStackMarker : 0;
			remaining -= add;
		}

		if ( remaining > 0 )
		{
			Log.Warning( "❌ Inventory full!" );
			return false;
		}

		Revision++;
		Log.Info( $"📦 Added item {itemId} x{count} to inventory" );
		return true;
	}

	/// <summary>
	/// Drag/drop support: request swapping two occupied slots.
	/// IMPORTANT (multiplayer): inventories are player-owned, so slot moves must execute on the owner.
	/// Best practice: validate on host, then instruct owner to apply the mutation locally so [Sync] replicates.
	/// </summary>
	public void RequestMoveSlot( int fromIndex, int toIndex )
	{
		if ( fromIndex == toIndex ) return;
		var requesterId = Connection.Local?.Id ?? Guid.Empty;
		if ( requesterId == Guid.Empty ) return;
		if ( DebugNet ) Log.Info( $"[Inv] RequestMoveSlot from={fromIndex} to={toIndex} requesterId={requesterId} host={Networking.IsHost} proxy={Network.IsProxy}" );
		RpcRequestMoveSlot( requesterId, fromIndex, toIndex );
	}

	[Rpc.Broadcast]
	private void RpcRequestMoveSlot( Guid requesterId, int fromIndex, int toIndex )
	{
		if ( !Networking.IsHost ) return;
		if ( requesterId == Guid.Empty ) return;
		if ( Network?.Owner?.Id != requesterId ) return;
		RpcExecuteMoveSlot( requesterId, fromIndex, toIndex );
	}

	[Rpc.Broadcast]
	private void RpcExecuteMoveSlot( Guid targetId, int fromIndex, int toIndex )
	{
		if ( Connection.Local?.Id != targetId ) return;
		if ( Network.IsProxy ) return;
		MoveSlotInternal( fromIndex, toIndex );
	}

	/// <summary>
	/// Split a stack: move <paramref name="count"/> from <paramref name="fromIndex"/> into an empty <paramref name="toIndex"/>.
	/// Intended for Shift-drag "half stack" behavior.
	/// </summary>
	public void RequestSplitMoveSlot( int fromIndex, int toIndex, int count )
	{
		if ( fromIndex == toIndex ) return;
		if ( count <= 0 ) return;
		var requesterId = Connection.Local?.Id ?? Guid.Empty;
		if ( requesterId == Guid.Empty ) return;
		if ( DebugNet ) Log.Info( $"[Inv] RequestSplitMoveSlot from={fromIndex} to={toIndex} count={count} requesterId={requesterId} host={Networking.IsHost} proxy={Network.IsProxy}" );
		RpcRequestSplitMoveSlot( requesterId, fromIndex, toIndex, count );
	}

	/// <summary>
	/// Move <paramref name="count"/> items from <paramref name="fromIndex"/> into <paramref name="toIndex"/>.
	/// Supports:
	/// - Empty target: creates/places a partial stack.
	/// - Same-item target: merges up to max stack size.
	/// For different-item occupied targets, this is a no-op (prevents accidental swaps).
	/// </summary>
	public void RequestMoveCount( int fromIndex, int toIndex, int count )
	{
		if ( fromIndex == toIndex ) return;
		if ( count <= 0 ) return;
		var requesterId = Connection.Local?.Id ?? Guid.Empty;
		if ( requesterId == Guid.Empty ) return;
		RpcRequestMoveCount( requesterId, fromIndex, toIndex, count );
	}

	[Rpc.Broadcast]
	private void RpcRequestSplitMoveSlot( Guid requesterId, int fromIndex, int toIndex, int count )
	{
		if ( !Networking.IsHost ) return;
		if ( requesterId == Guid.Empty ) return;
		if ( Network?.Owner?.Id != requesterId ) return;
		RpcExecuteSplitMoveSlot( requesterId, fromIndex, toIndex, count );
	}

	[Rpc.Broadcast]
	private void RpcExecuteSplitMoveSlot( Guid targetId, int fromIndex, int toIndex, int count )
	{
		if ( Connection.Local?.Id != targetId ) return;
		if ( Network.IsProxy ) return;
		SplitMoveSlotInternal( fromIndex, toIndex, count );
	}

	[Rpc.Broadcast]
	private void RpcRequestMoveCount( Guid requesterId, int fromIndex, int toIndex, int count )
	{
		if ( !Networking.IsHost ) return;
		if ( requesterId == Guid.Empty ) return;
		if ( Network?.Owner?.Id != requesterId ) return;
		RpcExecuteMoveCount( requesterId, fromIndex, toIndex, count );
	}

	[Rpc.Broadcast]
	private void RpcExecuteMoveCount( Guid targetId, int fromIndex, int toIndex, int count )
	{
		if ( Connection.Local?.Id != targetId ) return;
		if ( Network.IsProxy ) return;
		MoveCountInternal( fromIndex, toIndex, count );
	}

	private void SplitMoveSlotInternal( int fromIndex, int toIndex, int count )
	{
		if ( Network.IsProxy ) return;
		EnsureSlotArraysSized();
		if ( fromIndex < 0 || fromIndex >= TotalSlots ) return;
		if ( toIndex < 0 || toIndex >= TotalSlots ) return;
		if ( fromIndex == toIndex ) return;
		if ( IsEmptySlot( fromIndex ) ) return;
		if ( !IsEmptySlot( toIndex ) ) return;
		if ( count <= 0 ) return;

		int itemId = _itemIds[fromIndex];
		int available = _itemCounts[fromIndex];
		if ( itemId <= 0 || available <= 0 ) return;
		if ( IsHotbarSlot( toIndex ) && IsHotbarBlockedItem( itemId ) ) return;
		if ( count >= available ) return; // splitting should leave something behind

		var def = VeggaItemRegistry.Get( itemId );
		int maxStack = GetEffectiveMaxStackForItem( itemId, def );
		if ( maxStack <= 1 ) return; // non-stackable items can't be split like this
		if ( count > maxStack ) count = maxStack;

		_itemCounts[fromIndex] = available - count;
		_itemIds[toIndex] = itemId;
		_itemCounts[toIndex] = count;
		_itemDurability[toIndex] = _itemDurability[fromIndex];

		Revision++;
	}

	private void MoveCountInternal( int fromIndex, int toIndex, int count )
	{
		if ( Network.IsProxy ) return;
		EnsureSlotArraysSized();
		if ( fromIndex < 0 || fromIndex >= TotalSlots ) return;
		if ( toIndex < 0 || toIndex >= TotalSlots ) return;
		if ( fromIndex == toIndex ) return;
		if ( IsEmptySlot( fromIndex ) ) return;
		if ( count <= 0 ) return;

		int itemId = _itemIds[fromIndex];
		int available = _itemCounts[fromIndex];
		if ( itemId <= 0 || available <= 0 ) return;
		if ( IsHotbarSlot( toIndex ) && IsHotbarBlockedItem( itemId ) ) return;
		if ( count > available ) count = available;
		if ( count >= available )
		{
			// Full move becomes a normal slot move.
			MoveSlotInternal( fromIndex, toIndex );
			return;
		}

		var def = VeggaItemRegistry.Get( itemId );
		int maxStack = GetEffectiveMaxStackForItem( itemId, def );
		if ( maxStack <= 1 ) return;
		bool usesDurability = def != null && def.MaxGrams > 0;
		if ( usesDurability )
		{
			int durFrom = fromIndex < _itemDurability.Count ? _itemDurability[fromIndex] : 0;
			// Don't allow splitting partial durability items into fake stacks.
			if ( durFrom != DurableStackMarker )
				return;
		}

		// Empty target: create a new partial stack.
		if ( IsEmptySlot( toIndex ) )
		{
			_itemCounts[fromIndex] = available - count;
			_itemIds[toIndex] = itemId;
			_itemCounts[toIndex] = count;
			_itemDurability[toIndex] = _itemDurability[fromIndex];
			Revision++;
			return;
		}

		// Same-item target: merge.
		int toItemId = _itemIds[toIndex];
		if ( toItemId != itemId || _itemCounts[toIndex] <= 0 )
			return;
		if ( usesDurability )
		{
			int durTo = toIndex < _itemDurability.Count ? _itemDurability[toIndex] : 0;
			if ( durTo != DurableStackMarker )
				return;
		}

		long space = (long)maxStack - (long)_itemCounts[toIndex];
		if ( space <= 0 )
			return;

		int move = (int)Math.Min( (long)count, space );
		_itemCounts[toIndex] += move;
		_itemCounts[fromIndex] -= move;
		if ( _itemCounts[fromIndex] <= 0 )
			ClearSlot( fromIndex );
		Revision++;
	}

	/// <summary>
	/// Drop a quantity from a slot into the world as a pickup.
	/// Multiplayer: host spawns the world drop, then the owning client consumes from their inventory.
	/// </summary>
	public void RequestDropFromSlot( int slotIndex, int count )
	{
		if ( count <= 0 ) return;
		if ( DebugNet ) Log.Info( $"[Inv] RequestDropFromSlot slot={slotIndex} count={count} host={Networking.IsHost} proxy={Network.IsProxy} local={Connection.Local?.Id}" );

		// Host-owner (singleplayer / listen server): do it all locally.
		if ( Networking.IsHost && !Network.IsProxy )
		{
			if ( DebugNet ) Log.Info( "[Inv] DropFromSlot executing on host-local (singleplayer/host-owner)" );
			DropFromSlotInternal( slotIndex, count );
			return;
		}

		var requesterId = Connection.Local?.Id ?? Guid.Empty;
		if ( requesterId == Guid.Empty ) return;

		// If we own this inventory, predictively consume locally so the UI is instant.
		// Then ask host to spawn the world drop. Host will ACK/REJECT.
		if ( !Network.IsProxy )
		{
			EnsureSlotArraysSized();
			if ( slotIndex < 0 || slotIndex >= TotalSlots ) return;
			if ( IsEmptySlot( slotIndex ) ) return;

			int itemId = _itemIds[slotIndex];
			int available = _itemCounts[slotIndex];
			if ( itemId <= 0 || available <= 0 ) return;
			if ( count > available ) count = available;

			var def = VeggaItemRegistry.Get( itemId );
			if ( def != null && !def.Droppable ) return;

			var token = Guid.NewGuid();
			int durability = slotIndex < _itemDurability.Count ? _itemDurability[slotIndex] : 0;
			_predictedDrops[token] = new PredictedDrop { SlotIndex = slotIndex, ItemId = itemId, Count = count, Durability = durability };

			var ray = GetBestLocalDropRay();
			if ( DebugNet ) Log.Info( $"[Inv] DropFromSlot predicting token={token} slot={slotIndex} itemId={itemId} count={count}" );
			// Send request first to reduce race where host sees already-consumed state.
			RpcRequestDropFromSlot( requesterId, slotIndex, count, itemId, token, ray.Position, ray.Forward );
			RemoveFromSlot( slotIndex, count );
			return;
		}

		// Fallback: proxies can't mutate; still request a host-side spawn.
		var fallbackRay = new Ray( Vector3.Zero, Vector3.Forward );
		var camera = (Scene ?? Game.ActiveScene)?.Camera;
		if ( camera != null )
			fallbackRay = camera.ScreenNormalToRay( new Vector2( 0.5f, 0.5f ) );
		if ( DebugNet ) Log.Info( $"[Inv] DropFromSlot sending RPC (no prediction) requesterId={requesterId}" );
		RpcRequestDropFromSlot( requesterId, slotIndex, count, -1, Guid.Empty, fallbackRay.Position, fallbackRay.Forward );
	}

	[Rpc.Broadcast]
	private void RpcRequestDropFromSlot( Guid requesterId, int slotIndex, int count, int expectedItemId, Guid dropToken, Vector3 rayOrigin, Vector3 rayForward )
	{
		if ( DebugNet ) Log.Info( $"[Inv] RpcRequestDropFromSlot requesterId={requesterId} slot={slotIndex} count={count} expectedItemId={expectedItemId} token={dropToken} host={Networking.IsHost}" );
		if ( !Networking.IsHost ) return;
		if ( requesterId == Guid.Empty ) { if ( DebugNet ) Log.Warning( "[Inv] Drop RPC rejected: empty requesterId" ); return; }
		if ( count <= 0 ) { if ( DebugNet ) Log.Warning( "[Inv] Drop RPC rejected: count<=0" ); return; }

		// Validate the inventory belongs to the requester (don't rely on this component's Network.Owner).
		var scene = Scene ?? Game.ActiveScene;
		if ( scene == null ) { if ( DebugNet ) Log.Warning( "[Inv] Drop RPC rejected: no scene" ); return; }

		PlayerVeggaStats stats = null;
		foreach ( var s in scene.GetAllComponents<PlayerVeggaStats>() )
		{
			if ( s != null && s.IsValid() && s.Network?.Owner?.Id == requesterId )
			{
				stats = s;
				break;
			}
		}
		if ( stats == null ) { if ( DebugNet ) Log.Warning( "[Inv] Drop RPC rejected: requester stats not found" ); RpcDropRejected( requesterId, dropToken ); return; }
		if ( DebugNet ) Log.Info( $"[Inv] Drop RPC requester={stats.Network?.Owner?.DisplayName}" );

		var inv = stats.GameObject?.Components.Get<VeggaInventory>();
		if ( inv == null ) { if ( DebugNet ) Log.Warning( "[Inv] Drop RPC rejected: inv missing" ); RpcDropRejected( requesterId, dropToken ); return; }

		// Validate slot contents using the host's view of the owner's [Sync] state.
		if ( slotIndex < 0 || slotIndex >= inv.TotalSlots ) { if ( DebugNet ) Log.Warning( "[Inv] Drop RPC rejected: slotIndex out of range" ); RpcDropRejected( requesterId, dropToken ); return; }

		int itemId = 0;
		int available = 0;
		if ( !inv.IsEmptySlot( slotIndex ) )
		{
			itemId = inv._itemIds[slotIndex];
			available = inv._itemCounts[slotIndex];
		}

		// If we have a predicted drop token, allow spawning even if the host's replicated view is already consumed.
		// (The owner removed locally after sending the RPC.)
		if ( itemId <= 0 || available <= 0 )
		{
			if ( dropToken == Guid.Empty || expectedItemId <= 0 )
			{
				if ( DebugNet ) Log.Warning( "[Inv] Drop RPC rejected: empty slot (no prediction token)" );
				RpcDropRejected( requesterId, dropToken );
				return;
			}
			itemId = expectedItemId;
			available = count;
		}
		else
		{
			if ( expectedItemId > 0 && itemId != expectedItemId )
			{
				if ( DebugNet ) Log.Warning( "[Inv] Drop RPC rejected: expectedItemId mismatch" );
				RpcDropRejected( requesterId, dropToken );
				return;
			}
			if ( count > available ) count = available;
		}

		var def = VeggaItemRegistry.Get( itemId );
		if ( def != null && !def.Droppable ) { if ( DebugNet ) Log.Warning( "[Inv] Drop RPC rejected: item not droppable" ); RpcDropRejected( requesterId, dropToken ); return; }

		var spawnRot = stats.WorldRotation;
		var worldScene = stats.Scene ?? scene;
		if ( worldScene == null ) { if ( DebugNet ) Log.Warning( "[Inv] Drop RPC rejected: worldScene null" ); RpcDropRejected( requesterId, dropToken ); return; }

		var spawnPos = GetDropSpawnPosition( worldScene, stats, rayOrigin, rayForward, out var lookRot );
		if ( lookRot != default )
			spawnRot = lookRot;

		// Cash boxes should never inherit camera pitch (looking up/down) for rotation.
		// Keep cash facing based on BODY yaw only.
		Rotation cashFacingRot = default;
		if ( itemId == Sandbox.Money.VeggaCurrency.CashItemId )
		{
			var movementForCash = stats?.GameObject?.Components.Get<PlayerVeggaMovement>();
			float bodyYaw = movementForCash != null ? movementForCash.TargetBodyAngle.yaw : stats.WorldRotation.Yaw();
			cashFacingRot = new Angles( 0f, bodyYaw, 0f ).ToRotation();
		}

		// Ores and goblet mould use tight/simple colliders; drop a touch higher to avoid starting intersecting the ground.
		if ( VeggaOreVisuals.IsOre( itemId ) || itemId == VeggaItemIds.MouldGoblet )
			spawnPos += Vector3.Up * 8f;

		var lowerBack = FindLowerBack( stats?.GameObject );
		var startPos = lowerBack != null && lowerBack.IsValid() ? lowerBack.WorldPosition : (stats.WorldPosition + stats.WorldRotation.Backward * 6f + Vector3.Up * 30f);

		var dropVelocity = GetDropInertiaVelocity( stats, rayForward );

		var dropChunks = SplitIntoWorldDropChunks( itemId, count, def );
		if ( dropChunks.Count <= 0 )
		{
			RpcDropRejected( requesterId, dropToken );
			return;
		}

		// Gold bars are non-stackable. Enforce a single-bar drop.
		if ( itemId == VeggaItemIds.GoldBar200g )
		{
			dropChunks.Clear();
			dropChunks.Add( 1 );
		}

		int dropDurability = inv.GetSlotDurability( slotIndex );
		if ( VeggaOreVisuals.IsOre( itemId ) )
		{
			// If this is a legacy ore with no stored variant, choose one now.
			dropDurability = VeggaOreVisuals.EnsureVariant( itemId, dropDurability, allowRandomize: true );
		}

		for ( int i = 0; i < dropChunks.Count; i++ )
		{
			int chunkCount = dropChunks[i];
			// Default: all chunks land at the crosshair target; only throw timing is staggered.
			var endPos = spawnPos;
			float startDelaySeconds = dropChunks.Count > 1 ? i * 0.07f : 0f;

			if ( itemId == Sandbox.Money.VeggaCurrency.CashItemId )
			{
				var palletRot = cashFacingRot != default ? cashFacingRot : new Angles( 0f, spawnRot.Yaw(), 0f ).ToRotation();

				var vel = (chunkCount >= Sandbox.Money.VeggaCurrency.CashBoxAmount) ? Vector3.Zero : dropVelocity;

				var cashGo = Sandbox.Money.CashWorldDrop.Spawn( worldScene, startPos, palletRot, chunkCount, requesterId );
				if ( cashGo == null || !cashGo.IsValid() )
				{
					RpcDropRejected( requesterId, dropToken );
					return;
				}
				var anim = cashGo.Components.Create<VeggaDropThrowAnimator>();
				anim.Begin( stats.GameObject, rayOrigin, endPos, palletRot, vel,
					registerPersistence: false,
					itemId: Sandbox.Money.VeggaCurrency.CashItemId,
					count: chunkCount,
					durability: 0,
					prefabPath: null,
					droppedBy: requesterId,
					droppedAtUtcTicks: DateTime.UtcNow.Ticks,
					startDelaySeconds: startDelaySeconds );
				continue;
			}

			var go = TrySpawnPrefabDrop( worldScene, def, itemId, chunkCount, startPos, spawnRot, requesterId, Vector3.Zero );
			if ( go != null && go.IsValid() )
			{
				var rootPickup = go.Components.Get<VeggaPickupItem>();
				if ( rootPickup != null && rootPickup.IsValid() )
					rootPickup.Durability = dropDurability;
				foreach ( var pickup in go.Components.GetAll<VeggaPickupItem>( FindMode.InDescendants ) )
				{
					if ( pickup == null || !pickup.IsValid() ) continue;
					pickup.Durability = dropDurability;
				}
				VeggaOreVisuals.ApplyToWorldObject( go, itemId, dropDurability );
			}

			if ( go == null )
			{
				// If the item has a prefab, never fall back to a dynamic GameObject. Reject the drop.
				if ( def != null && !string.IsNullOrWhiteSpace( def.PrefabPath ) )
				{
					if ( DebugNet ) Log.Warning( $"[Inv] Drop RPC rejected: prefab spawn failed for itemId={itemId} prefab={def.PrefabPath}" );
					RpcDropRejected( requesterId, dropToken );
					return;
				}

				go = new GameObject( true, $"DroppedItem_{itemId}" );
				go.WorldPosition = startPos;
				go.WorldRotation = spawnRot;

				var renderer = go.Components.Create<ModelRenderer>();
				if ( VeggaOreVisuals.IsOre( itemId ) )
				{
					try { renderer.Model = Model.Load( VeggaOreVisuals.GetVariantModelPath( dropDurability ) ); } catch { }
					renderer.Tint = VeggaOreVisuals.GetTintForItem( itemId );
				}
				else if ( def != null && !string.IsNullOrWhiteSpace( def.ModelPath ) )
				{
					try { renderer.Model = Model.Load( def.ModelPath ); } catch { }
					TryApplyDropTint( renderer, itemId );
				}

				var pickup = go.Components.Create<VeggaPickupItem>();
				pickup.ItemId = itemId;
				pickup.Quantity = chunkCount;
				pickup.Durability = dropDurability;
				pickup.PickupDelay = 0.25f;
				pickup.DroppedById = requesterId;
				pickup.DroppedAtTime = Time.Now;
				pickup.DroppedAtUtcTicks = DateTime.UtcNow.Ticks;

				var rb = go.Components.Create<Rigidbody>();
				rb.Gravity = true;
				rb.Velocity = Vector3.Zero;

				if ( renderer != null && renderer.Model != null )
				{
					var collider = go.Components.Create<ModelCollider>();
					try { collider.Model = renderer.Model; } catch { }
					collider.IsTrigger = false;
				}
				else
				{
					var collider = go.Components.Create<SphereCollider>();
					collider.Radius = 12f;
					collider.IsTrigger = false;
				}
			}

			if ( go != null && go.IsValid() )
			{
				var droppedAtUtcTicks = DateTime.UtcNow.Ticks;
				var pickup = go.Components.Get<VeggaPickupItem>();
				if ( pickup != null && pickup.IsValid() )
				{
					pickup.Durability = dropDurability;
					pickup.DroppedById = requesterId;
					pickup.DroppedAtUtcTicks = droppedAtUtcTicks;
				}
				VeggaOreVisuals.ApplyToWorldObject( go, itemId, dropDurability );

				var anim = go.Components.Create<VeggaDropThrowAnimator>();
				anim.Begin( stats.GameObject, rayOrigin, endPos, spawnRot, dropVelocity,
					registerPersistence: def != null && !string.IsNullOrWhiteSpace( def.PrefabPath ),
					itemId: itemId,
					count: chunkCount,
					durability: dropDurability,
					prefabPath: def?.PrefabPath,
					droppedBy: requesterId,
					droppedAtUtcTicks: droppedAtUtcTicks,
					startDelaySeconds: startDelaySeconds );
			}
		}

		static void TryApplyDropTint( ModelRenderer renderer, int itemId )
		{
			if ( renderer == null || !renderer.IsValid() ) return;

			// Subtle tints so the same rock mesh reads as different metal.
			switch ( itemId )
			{
				case VeggaItemIds.TinOre:
				case VeggaItemIds.TinBar:
					renderer.Tint = new Color( 0.70f, 0.70f, 0.75f, 1.0f );
					break;
				case VeggaItemIds.CopperOre:
				case VeggaItemIds.CopperBar:
					renderer.Tint = new Color( 0.90f, 0.55f, 0.25f, 1.0f );
					break;
				case VeggaItemIds.IronOre:
				case VeggaItemIds.IronBar:
					renderer.Tint = new Color( 0.45f, 0.45f, 0.47f, 1.0f );
					break;
				case VeggaItemIds.BronzeBar:
					renderer.Tint = new Color( 0.82f, 0.62f, 0.28f, 1.0f );
					break;
				case VeggaItemIds.CoalOre:
					renderer.Tint = new Color( 0.20f, 0.20f, 0.20f, 1.0f );
					break;
				case VeggaItemIds.SteelBar:
					renderer.Tint = new Color( 0.50f, 0.52f, 0.56f, 1.0f );
					break;
			}
		}

		// Tell the owning client to consume items from their inventory.
		// Also consume on the host (when the host still sees the items) so persistence
		// snapshots taken immediately after a drop don't resurrect items on scene reset.
		// This is a best-effort sync: if the host view is already consumed, do nothing.
		if ( slotIndex >= 0 && slotIndex < inv._itemIds.Count && inv._itemIds[slotIndex] == itemId && inv._itemCounts[slotIndex] > 0 )
		{
			inv.RemoveFromSlot( slotIndex, count );
			var steamId = stats.Network?.Owner?.SteamId.ToString();
			if ( !string.IsNullOrWhiteSpace( steamId ) )
				PlayerDataPersistence.MarkPlayerDataChanged( steamId );
		}

		if ( dropToken != Guid.Empty )
		{
			if ( DebugNet ) Log.Info( $"[Inv] Drop RPC spawned world drop itemId={itemId} count={count}; ACK token={dropToken}" );
			inv.RpcDropAck( requesterId, dropToken );
		}
		else
		{
			if ( DebugNet ) Log.Info( $"[Inv] Drop RPC spawned world drop itemId={itemId} count={count}; consuming on owner" );
			inv.RpcConsumeDroppedFromSlot( requesterId, slotIndex, count, itemId );
		}
	}

	[Rpc.Broadcast]
	private void RpcDropAck( Guid targetId, Guid dropToken )
	{
		if ( Connection.Local?.Id != targetId ) return;
		if ( dropToken == Guid.Empty ) return;
		_predictedDrops.Remove( dropToken );
	}

	[Rpc.Broadcast]
	private void RpcDropRejected( Guid targetId, Guid dropToken )
	{
		if ( Connection.Local?.Id != targetId ) return;
		if ( Network.IsProxy ) return;
		if ( dropToken == Guid.Empty ) return;

		if ( !_predictedDrops.TryGetValue( dropToken, out var predicted ) )
			return;
		_predictedDrops.Remove( dropToken );

		if ( predicted.ItemId <= 0 || predicted.Count <= 0 ) return;
		EnsureSlotArraysSized();

		if ( predicted.SlotIndex >= 0 && predicted.SlotIndex < TotalSlots && IsEmptySlot( predicted.SlotIndex ) )
		{
			_itemIds[predicted.SlotIndex] = predicted.ItemId;
			_itemCounts[predicted.SlotIndex] = predicted.Count;
			_itemDurability[predicted.SlotIndex] = predicted.Durability;
			Revision++;
			return;
		}

		AddItem( predicted.ItemId, predicted.Count );
	}

	[Rpc.Broadcast]
	private void RpcConsumeDroppedFromSlot( Guid targetId, int slotIndex, int count, int expectedItemId )
	{
		// Only the owning client should mutate their [Sync] inventory state.
		if ( Connection.Local?.Id != targetId ) return;
		if ( Network.IsProxy ) return;
		if ( DebugNet ) Log.Info( $"[Inv] RpcConsumeDroppedFromSlot targetId={targetId} slot={slotIndex} count={count} expectedItemId={expectedItemId}" );
		if ( count <= 0 ) return;
		EnsureSlotArraysSized();
		if ( slotIndex < 0 || slotIndex >= TotalSlots ) return;
		if ( _itemIds[slotIndex] != expectedItemId ) return;
		RemoveFromSlot( slotIndex, count );
	}

	[Rpc.Broadcast]
	public void RpcConsumeFromSlot( Guid targetId, int slotIndex, int count, int expectedItemId )
	{
		// Only the owning client should mutate their [Sync] inventory state.
		if ( Connection.Local?.Id != targetId ) return;
		if ( Network.IsProxy ) return;
		if ( count <= 0 ) return;
		EnsureSlotArraysSized();
		if ( slotIndex < 0 || slotIndex >= TotalSlots ) return;
		if ( _itemIds[slotIndex] != expectedItemId ) return;
		RemoveFromSlot( slotIndex, count );
	}

	/// <summary>
	/// Can this inventory fit an item quantity? Works for proxies (no mutation).
	/// Used by host-side pickup validation.
	/// </summary>
	public bool CanFitItem( int itemId, int count )
	{
		if ( itemId <= 0 || count <= 0 ) return false;
		int limit = Math.Min( _itemIds.Count, TotalSlots );
		if ( limit <= 0 ) return false;

		var def = VeggaItemRegistry.Get( itemId );
		int maxStack = GetEffectiveMaxStackForItem( itemId, def );
		bool isStackable = maxStack > 1;

		if ( !isStackable )
		{
			return FreeSlots >= count;
		}

		long remaining = count;
		long capacityInExisting = 0;
		for ( int i = 0; i < limit; i++ )
		{
			if ( _itemIds[i] != itemId || _itemCounts[i] <= 0 )
				continue;

			int current = _itemCounts[i];
			if ( current < 0 ) current = 0;
			if ( current > maxStack ) current = maxStack;
			capacityInExisting += (maxStack - current);
			if ( capacityInExisting >= remaining )
				break;
		}

		long needAfterExisting = Math.Max( 0, remaining - capacityInExisting );
		int newStacksNeeded = needAfterExisting > 0 ? (int)((needAfterExisting + (long)maxStack - 1) / (long)maxStack) : 0;
		return FreeSlots >= newStacksNeeded;
	}

	private void DropFromSlotInternal( int slotIndex, int count )
	{
		if ( Network.IsProxy ) return;
		EnsureSlotArraysSized();
		if ( slotIndex < 0 || slotIndex >= TotalSlots ) return;
		if ( IsEmptySlot( slotIndex ) ) return;
		if ( count <= 0 ) return;

		int itemId = _itemIds[slotIndex];
		int available = _itemCounts[slotIndex];
		if ( itemId <= 0 || available <= 0 ) return;
		if ( count > available ) count = available;

		var def = VeggaItemRegistry.Get( itemId );
		if ( def != null && !def.Droppable ) return;

		var scene = Scene ?? Game.ActiveScene;
		if ( scene == null ) return;

		// Drop at crosshair (capped distance). In singleplayer/host-owner we can use the scene camera.
		var stats = GameObject.Components.Get<PlayerVeggaStats>();
		var ray = GetBestLocalDropRay();
		var pos = GetDropSpawnPosition( scene, stats, ray.Position, ray.Forward, out var rot );
		if ( rot == default ) rot = GameObject.WorldRotation;

		// Cash should not inherit camera pitch/roll. Force yaw-only rotation.
		if ( itemId == Sandbox.Money.VeggaCurrency.CashItemId )
		{
			var movementForCash = (stats?.GameObject ?? GameObject)?.Components.Get<PlayerVeggaMovement>();
			float bodyYaw = movementForCash != null ? movementForCash.TargetBodyAngle.yaw : (stats != null ? stats.WorldRotation.Yaw() : GameObject.WorldRotation.Yaw());
			rot = new Angles( 0f, bodyYaw, 0f ).ToRotation();
		}
		// Ores and goblet mould use tight/simple colliders; drop a touch higher to avoid starting intersecting the ground.
		if ( VeggaOreVisuals.IsOre( itemId ) || itemId == VeggaItemIds.MouldGoblet )
			pos += Vector3.Up * 8f;
		var dropVelocity = GetDropInertiaVelocity( stats, ray.Forward );
		var lowerBack = FindLowerBack( stats?.GameObject ?? GameObject );
		var startPos = lowerBack != null && lowerBack.IsValid() ? lowerBack.WorldPosition : (GameObject.WorldPosition + GameObject.WorldRotation.Backward * 6f + Vector3.Up * 30f);

		var dropperId = stats?.Network?.Owner?.Id ?? Connection.Local?.Id ?? Guid.Empty;
		var dropChunks = SplitIntoWorldDropChunks( itemId, count, def );
		if ( dropChunks.Count <= 0 )
			return;

		// Gold bars are non-stackable. Enforce a single-bar drop.
		if ( itemId == VeggaItemIds.GoldBar200g )
		{
			dropChunks.Clear();
			dropChunks.Add( 1 );
			count = 1;
		}

		int dropDurability = GetSlotDurability( slotIndex );
		if ( VeggaOreVisuals.IsOre( itemId ) )
		{
			// If this is a legacy ore with no stored variant, choose one now.
			dropDurability = VeggaOreVisuals.EnsureVariant( itemId, dropDurability, allowRandomize: true );
		}

		for ( int i = 0; i < dropChunks.Count; i++ )
		{
			int chunkCount = dropChunks[i];
			// All chunks land at the normal crosshair target; only the throw timing is staggered.
			var endPos = pos;
			float startDelaySeconds = dropChunks.Count > 1 ? i * 0.07f : 0f;

			if ( itemId == Sandbox.Money.VeggaCurrency.CashItemId )
			{
				var cashGo = Sandbox.Money.CashWorldDrop.Spawn( scene, startPos, rot, chunkCount, dropperId );
				if ( cashGo == null || !cashGo.IsValid() )
					return;
				var anim = cashGo.Components.Create<VeggaDropThrowAnimator>();
				var vel = (chunkCount >= Sandbox.Money.VeggaCurrency.CashBoxAmount) ? Vector3.Zero : dropVelocity;
				anim.Begin( stats?.GameObject ?? GameObject, ray.Position, endPos, rot, vel,
					registerPersistence: false,
					itemId: Sandbox.Money.VeggaCurrency.CashItemId,
					count: chunkCount,
					durability: 0,
					prefabPath: null,
					droppedBy: dropperId,
					droppedAtUtcTicks: DateTime.UtcNow.Ticks,
					startDelaySeconds: startDelaySeconds );
				continue;
			}

			var go = TrySpawnPrefabDrop( scene, def, itemId, chunkCount, startPos, rot, dropperId, Vector3.Zero );
			if ( go == null )
			{
				// If the item has a prefab, never fall back to a dynamic GameObject.
				if ( def != null && !string.IsNullOrWhiteSpace( def.PrefabPath ) )
					return;

				go = new GameObject( true, $"DroppedItem_{itemId}" );
				go.WorldPosition = startPos;
				go.WorldRotation = rot;

				var renderer = go.Components.Create<ModelRenderer>();
				if ( def != null && !string.IsNullOrWhiteSpace( def.ModelPath ) )
				{
					try { renderer.Model = Model.Load( def.ModelPath ); } catch { }
				}

				var pickup = go.Components.Create<VeggaPickupItem>();
				pickup.ItemId = itemId;
				pickup.Quantity = chunkCount;
				pickup.PickupDelay = 0.25f;
				if ( dropperId != Guid.Empty )
					pickup.DroppedById = dropperId;
				pickup.DroppedAtTime = Time.Now;

				var rb = go.Components.Create<Rigidbody>();
				rb.Gravity = true;
				rb.Velocity = Vector3.Zero;

				if ( renderer != null && renderer.Model != null )
				{
					var collider = go.Components.Create<ModelCollider>();
					try { collider.Model = renderer.Model; } catch { }
					collider.IsTrigger = false;
				}
				else
				{
					var collider = go.Components.Create<SphereCollider>();
					collider.Radius = 12f;
					collider.IsTrigger = false;
				}
			}

			if ( go != null && go.IsValid() )
			{
				var droppedAtUtcTicks = DateTime.UtcNow.Ticks;
				var rootPickup = go.Components.Get<VeggaPickupItem>();
				if ( rootPickup != null && rootPickup.IsValid() )
				{
					rootPickup.Durability = dropDurability;
					rootPickup.DroppedById = dropperId;
					rootPickup.DroppedAtUtcTicks = droppedAtUtcTicks;
				}
				foreach ( var pickup in go.Components.GetAll<VeggaPickupItem>( FindMode.InDescendants ) )
				{
					if ( pickup == null || !pickup.IsValid() ) continue;
					pickup.Durability = dropDurability;
					pickup.DroppedById = dropperId;
					pickup.DroppedAtUtcTicks = droppedAtUtcTicks;
				}
				VeggaOreVisuals.ApplyToWorldObject( go, itemId, dropDurability );

				var anim = go.Components.Create<VeggaDropThrowAnimator>();
				anim.Begin( stats?.GameObject ?? GameObject, ray.Position, endPos, rot, dropVelocity,
					registerPersistence: def != null && !string.IsNullOrWhiteSpace( def.PrefabPath ),
					itemId: itemId,
					count: chunkCount,
					durability: dropDurability,
					prefabPath: def?.PrefabPath,
					droppedBy: dropperId,
					droppedAtUtcTicks: droppedAtUtcTicks,
					startDelaySeconds: startDelaySeconds );
			}
		}

		// Remove from inventory after successful spawn.
		RemoveFromSlot( slotIndex, count );

		// Editor/host-only: persist immediately so Stop→Play doesn't resurrect items.
		// (Autosave interval can be minutes; this keeps iteration tight.)
		try
		{
			var key = PlayerDataPersistence.GetLocalPersistenceKey();
			PlayerDataPersistence.MarkPlayerDataChanged( key );
			PlayerDataPersistence.SaveLocalNow();
		}
		catch
		{
			// Best-effort only.
		}
	}

	private Ray GetBestLocalDropRay()
	{
		var scene = Scene ?? Game.ActiveScene;
		var camera = scene?.Camera;
		if ( camera != null )
			return camera.ScreenNormalToRay( new Vector2( 0.5f, 0.5f ) );

		var movement = GameObject?.Components.Get<PlayerVeggaMovement>();
		if ( movement != null && movement.Head != null && movement.Head.IsValid() )
		{
			var forward = movement.Head.WorldRotation.Forward;
			if ( forward.LengthSquared > 0.0001f )
				return new Ray( movement.Head.WorldPosition, forward.Normal );
		}

		return new Ray( GameObject.WorldPosition + Vector3.Up * 60f, GameObject.WorldRotation.Forward );
	}

	static GameObject FindLowerBack( GameObject root )
	{
		if ( root == null || !root.IsValid )
			return null;
		if ( string.Equals( root.Name, "LowerBack", StringComparison.OrdinalIgnoreCase ) )
			return root;
		foreach ( var child in root.Children )
		{
			var found = FindLowerBack( child );
			if ( found != null && found.IsValid )
				return found;
		}
		return null;
	}

	static Vector3 GetDropInertiaVelocity( PlayerVeggaStats stats, Vector3 rayForward )
	{
		var movement = stats?.GameObject?.Components.Get<PlayerVeggaMovement>();
		var vel = movement != null ? movement.SyncedVelocity : Vector3.Zero;
		vel = vel.WithZ( 0 );

		// Small forward bias so drops don't feel like they glue straight down.
		var forward = rayForward.WithZ( 0 );
		if ( forward.LengthSquared > 0.0001f )
			forward = forward.Normal;
		else
			forward = (stats?.WorldRotation.Forward ?? Vector3.Forward).WithZ( 0 ).Normal;

		return vel + forward * 60f;
	}

	static GameObject TrySpawnPrefabDrop( Scene scene, VeggaItemDef def, int itemId, int count, Vector3 pos, Rotation rot, Guid droppedById, Vector3 dropVelocity )
	{
		if ( scene == null )
			return null;
		if ( def == null || string.IsNullOrWhiteSpace( def.PrefabPath ) )
			return null;
		if ( itemId <= 0 || count <= 0 )
			return null;

		GameObject go = null;
		try
		{
			go = GameObject.Clone( def.PrefabPath, new Transform( pos, rot ), scene, startEnabled: false, name: $"DroppedItem_{itemId}" );
		}
		catch
		{
			go = null;
		}

		if ( go == null || !go.IsValid() )
			return null;

		// Override pickup metadata on prefab (root or descendants).
		IEnumerable<VeggaPickupItem> SelfAndDescendantsPickups()
		{
			var root = go.Components.Get<VeggaPickupItem>();
			if ( root != null )
				yield return root;
			foreach ( var child in go.Components.GetAll<VeggaPickupItem>( FindMode.InDescendants ) )
				yield return child;
		}

		void ApplyPickupOverrides()
		{
			foreach ( var pickup in SelfAndDescendantsPickups() )
			{
				if ( pickup == null ) continue;
				pickup.ItemId = itemId;
				pickup.Quantity = count;
				if ( pickup.PickupDelay <= 0 )
					pickup.PickupDelay = 0.25f;
				if ( droppedById != Guid.Empty )
					pickup.DroppedById = droppedById;
				pickup.DroppedAtTime = Time.Now;
			}
		}

		ApplyPickupOverrides();

		// Ensure physics is actually enabled (ores were reported as “no rigidbody” / weightless in-world).
		var rb = go.Components.Get<Rigidbody>()
			?? go.Components.GetAll<Rigidbody>( FindMode.InDescendants ).FirstOrDefault();
		if ( rb == null )
		{
			rb = go.Components.Create<Rigidbody>();
		}
		if ( rb != null )
		{
			rb.Enabled = true;
			rb.MotionEnabled = true;
			rb.Gravity = true;
			if ( VeggaOreVisuals.IsOre( itemId ) )
			{
				// Match the "Gold Bar" baseline so ore always falls and settles.
				rb.MassOverride = 25;
				rb.LinearDamping = 0.25f;
				rb.AngularDamping = 1f;
			}
			else if ( itemId == VeggaItemIds.MouldGoblet )
			{
				rb.MassOverride = 25;
				rb.LinearDamping = 0.35f;
				rb.AngularDamping = 4f;
			}
			rb.Velocity = dropVelocity;
		}

		var modelCollider = go.Components.Get<ModelCollider>()
			?? go.Components.GetAll<ModelCollider>( FindMode.InDescendants ).FirstOrDefault();
		if ( modelCollider != null )
		{
			var renderer = go.Components.Get<ModelRenderer>()
				?? go.Components.GetAll<ModelRenderer>( FindMode.InDescendants ).FirstOrDefault();
			if ( renderer != null && renderer.IsValid() && renderer.Model != null )
				modelCollider.Model = renderer.Model;

			modelCollider.Enabled = true;
			modelCollider.IsTrigger = false;
			modelCollider.Static = false;
		}

		// IMPORTANT: some item models don't have a usable collision mesh.
		// Guarantee a simple collider so the rigidbody actually simulates and the drop falls.
		bool isGobletMould = itemId == VeggaItemIds.MouldGoblet;
		bool needsFallbackCollider = VeggaOreVisuals.IsOre( itemId ) || isGobletMould;
		if ( needsFallbackCollider )
		{
			if ( isGobletMould )
			{
				// Prevent weird rolling by using a box collider instead of a sphere.
				var sphere = go.Components.Get<SphereCollider>()
					?? go.Components.GetAll<SphereCollider>( FindMode.InDescendants ).FirstOrDefault();
				if ( sphere != null ) sphere.Enabled = false;

				if ( modelCollider != null ) modelCollider.Enabled = false;

				var box = go.Components.Get<BoxCollider>()
					?? go.Components.GetAll<BoxCollider>( FindMode.InDescendants ).FirstOrDefault();
				if ( box == null )
					box = go.Components.Create<BoxCollider>();
				if ( box != null )
				{
					box.Enabled = true;
					box.IsTrigger = false;
					box.Scale = new Vector3( 12f, 12f, 10f );
				}
			}
			else
			{
				// Ore model collision can be flaky; prefer the simple sphere collider.
				if ( modelCollider != null ) modelCollider.Enabled = false;

				var sphere = go.Components.Get<SphereCollider>()
					?? go.Components.GetAll<SphereCollider>( FindMode.InDescendants ).FirstOrDefault();
				if ( sphere == null )
					sphere = go.Components.Create<SphereCollider>();
				if ( sphere != null )
				{
					sphere.Enabled = true;
					sphere.IsTrigger = false;
					// Slightly larger than before to avoid slipping through terrain seams.
					if ( sphere.Radius <= 0 || sphere.Radius > 16f ) sphere.Radius = 10f;
				}
			}
		}

		go.Enabled = true;
		ApplyPickupOverrides();
		return go;
	}

	static Vector3 GetDropSpawnPosition( Scene scene, PlayerVeggaStats stats, Vector3 rayOrigin, Vector3 rayForward, out Rotation lookRotation )
	{
		lookRotation = default;
		if ( scene == null )
			return stats != null ? stats.WorldPosition : Vector3.Zero;

		var movement = stats?.GameObject?.Components.Get<PlayerVeggaMovement>();
		Vector3 headPos;
		Rotation headRot;
		if ( movement != null && movement.Head != null && movement.Head.IsValid() )
		{
			headPos = movement.Head.WorldPosition;
			headRot = movement.Head.WorldRotation;
		}
		else if ( stats != null )
		{
			headPos = stats.WorldPosition + Vector3.Up * 60f;
			headRot = stats.WorldRotation;
		}
		else
		{
			headPos = Vector3.Zero;
			headRot = Rotation.Identity;
		}

		// 1) Camera/crosshair trace to determine what the player is aiming at.
		Ray cameraRay;
		bool useCameraRay = rayForward.LengthSquared > 0.0001f;
		if ( useCameraRay )
		{
			// Sanity check: camera ray origin should be near the player (allows 3rd person distance).
			if ( stats != null && Vector3.DistanceBetween( headPos, rayOrigin ) > DropRayOriginMaxError )
				useCameraRay = false;
		}

		if ( useCameraRay )
			cameraRay = new Ray( rayOrigin, rayForward.Normal );
		else
			cameraRay = new Ray( headPos, headRot.Forward );

		var cameraTr = scene.Trace.Ray( cameraRay, DropAimTraceDistance )
			.WithoutTags( "player", "trigger" )
			.Run();

		Vector3 aimPoint = cameraTr.Hit
			? cameraTr.HitPosition
			: cameraRay.Position + cameraRay.Forward * DropAimTraceDistance;

		// 2) Head trace towards aim point, capped at DropMaxDistance from head.
		var toAim = aimPoint - headPos;
		if ( toAim.LengthSquared < 0.0001f )
			toAim = headRot.Forward;
		var toAimDir = toAim.Normal;
		float desiredDistance = Math.Min( DropMaxDistance, toAim.Length );
		var desiredEnd = headPos + toAimDir * desiredDistance;

		var headTr = scene.Trace.Ray( headPos, desiredEnd )
			.WithoutTags( "player", "trigger" )
			.Run();

		Vector3 pos = desiredEnd;
		if ( headTr.Hit )	
		{
			pos = headTr.HitPosition + headTr.Normal * 4f;
		}

		// 3) Optional floor snap (drop onto ground) but never exceed DropMaxDistance from head.
		var floorStart = pos + Vector3.Up * 10f;
		var floorEnd = pos + Vector3.Down * 200f;
		var floorTr = scene.Trace.Ray( floorStart, floorEnd )
			.WithoutTags( "player", "trigger" )
			.Run();
		if ( floorTr.Hit )
		{
			var snapped = floorTr.HitPosition + floorTr.Normal * 2f;
			if ( Vector3.DistanceBetween( headPos, snapped ) <= DropMaxDistance + 1f )
				pos = snapped;
		}

		// Keep it slightly above the surface to avoid z-fighting.
		pos += Vector3.Up * 2f;
		lookRotation = Rotation.LookAt( toAimDir, Vector3.Up );
		return pos;
	}

	private void MoveSlotInternal( int fromIndex, int toIndex )
	{
		if ( Network.IsProxy ) return;
			EnsureSlotArraysSized();
			if ( fromIndex < 0 || fromIndex >= TotalSlots ) return;
			if ( toIndex < 0 || toIndex >= TotalSlots ) return;
		if ( fromIndex == toIndex ) return;
			if ( IsEmptySlot( fromIndex ) ) return;

		// Hard rule: certain items can never be placed into hotbar slots.
		int fromItemCheck = _itemIds[fromIndex];
		int toItemCheck = !IsEmptySlot( toIndex ) ? _itemIds[toIndex] : 0;
		if ( IsHotbarSlot( toIndex ) && IsHotbarBlockedItem( fromItemCheck ) )
			return;
		// If swapping with a hotbar slot, don't allow moving a blocked item into it.
		if ( IsHotbarSlot( fromIndex ) && IsHotbarBlockedItem( toItemCheck ) )
			return;

		// Merge stacks when dragging onto the same item.
		if ( !IsEmptySlot( toIndex ) )
		{
			int fromItemId = _itemIds[fromIndex];
			int toItemId = _itemIds[toIndex];
			if ( fromItemId > 0 && fromItemId == toItemId && _itemCounts[fromIndex] > 0 && _itemCounts[toIndex] > 0 )
			{
				var def = VeggaItemRegistry.Get( fromItemId );
				int maxStack = GetEffectiveMaxStackForItem( fromItemId, def );
				bool usesDurability = def != null && def.MaxGrams > 0;
				if ( maxStack > 1 )
				{
					// For durability items, only merge true stack markers (durability==0).
					if ( usesDurability )
					{
						int durA = fromIndex < _itemDurability.Count ? _itemDurability[fromIndex] : 0;
						int durB = toIndex < _itemDurability.Count ? _itemDurability[toIndex] : 0;
						if ( durA != DurableStackMarker || durB != DurableStackMarker )
							goto DoSwap;
					}

					long space = (long)maxStack - (long)_itemCounts[toIndex];
					if ( space > 0 )
					{
						int move = (int)Math.Min( space, (long)_itemCounts[fromIndex] );
						_itemCounts[toIndex] += move;
						_itemCounts[fromIndex] -= move;
						if ( _itemCounts[fromIndex] <= 0 )
							ClearSlot( fromIndex );
						Revision++;
						return;
					}
				}
			}
		}

		DoSwap:

			// True slotted inventory: allow swapping with empty slots.
			(_itemIds[fromIndex], _itemIds[toIndex]) = (_itemIds[toIndex], _itemIds[fromIndex]);
			(_itemCounts[fromIndex], _itemCounts[toIndex]) = (_itemCounts[toIndex], _itemCounts[fromIndex]);
			(_itemDurability[fromIndex], _itemDurability[toIndex]) = (_itemDurability[toIndex], _itemDurability[fromIndex]);

		Revision++;
	}

	// ---- Remove Item ----
	public bool RemoveItem( int itemId, int count = 1 )
	{
		if ( Network.IsProxy ) return false;
		if ( itemId <= 0 || count <= 0 ) return false;
			EnsureSlotArraysSized();

		int remaining = count;
		bool changed = false;

		for ( int i = 0; i < _itemIds.Count && remaining > 0; i++ )
		{
			if ( _itemIds[i] != itemId )
				continue;
			if ( _itemCounts[i] <= 0 )
				continue;

			int take = Math.Min( remaining, _itemCounts[i] );
			_itemCounts[i] -= take;
			remaining -= take;
			changed = true;

			if ( _itemCounts[i] <= 0 )
			{
				ClearSlot( i );
			}
		}

		if ( !changed )
		{
			Log.Warning( $"❌ Item {itemId} not found in inventory" );
			return false;
		}

		Revision++;
		if ( remaining > 0 )
		{
			Log.Warning( $"❌ Not enough of item {itemId} to remove {count} (missing {remaining})" );
			return false;
		}

		Log.Info( $"📦 Removed {count}x item {itemId}" );
		return true;
	}

	/// <summary>
	/// Remove a specific slot index (optionally reducing stack count).
	/// Returns false if out of range.
	/// </summary>
	public bool RemoveFromSlot( int slotIndex, int count = 1 )
	{
		if ( Network.IsProxy ) return false;
			EnsureSlotArraysSized();
			if ( slotIndex < 0 || slotIndex >= _itemIds.Count ) return false;
		if ( count <= 0 ) return false;
			if ( _itemCounts[slotIndex] <= 0 || _itemIds[slotIndex] <= 0 ) return false;

		_itemCounts[slotIndex] -= count;
		if ( _itemCounts[slotIndex] <= 0 )
		{
			ClearSlot( slotIndex );
		}

		Revision++;
		return true;
	}

	/// <summary>
	/// For durability items that are stored as large stacks, split out a single
	/// count=1 durable slot (durability=max) so systems like smelting can track
	/// partial progress without breaking stackability.
	/// </summary>
	public bool TryExtractOneDurable( int itemId, int maxDurability, out int newSlotIndex )
	{
		newSlotIndex = -1;
		if ( Network.IsProxy ) return false;
		if ( itemId <= 0 || maxDurability <= 0 ) return false;
			EnsureSlotArraysSized();

		for ( int i = 0; i < _itemIds.Count; i++ )
		{
			if ( _itemIds[i] != itemId )
				continue;
			if ( _itemCounts[i] <= 0 )
				continue;

			int dur = i < _itemDurability.Count ? _itemDurability[i] : 0;
			bool isPartial = dur > 0 && dur < maxDurability;
			if ( isPartial )
				continue;

			if ( _itemCounts[i] <= 1 )
			{
				// Single bar slot: normalize it into a durable slot.
				_itemDurability[i] = maxDurability;
				Revision++;
				newSlotIndex = i;
				return true;
			}

			// Split one out of the stack into an empty slot.
			int empty = -1;
			for ( int j = 0; j < _itemIds.Count; j++ )
			{
				if ( IsEmptySlot( j ) ) { empty = j; break; }
			}
			if ( empty < 0 )
				return false;

			_itemCounts[i] -= 1;
			_itemIds[empty] = itemId;
			_itemCounts[empty] = 1;
			_itemDurability[empty] = maxDurability;
			Revision++;
			newSlotIndex = empty;
			return true;
		}

		return false;
	}

	public bool AddItemWithDurability( int itemId, int count, int durability )
	{
		if ( Network.IsProxy ) return false;
		if ( itemId <= 0 || count <= 0 ) return false;
		EnsureSlotArraysSized();

		var def = VeggaItemRegistry.Get( itemId );
		int maxStack = def?.MaxStack ?? MaxStackSize;
		if ( maxStack <= 0 ) maxStack = 1;
		if ( maxStack > MaxStackSize ) maxStack = MaxStackSize;
		bool isStackable = maxStack > 1;
		bool usesDurability = def != null && def.MaxGrams > 0;
		int maxDurability = usesDurability ? def.MaxGrams : 0;

		// Stackables: durability isn’t meaningful, route to normal add.
		if ( isStackable )
			return AddItem( itemId, count );

		int freeSlots = FreeSlots;
		if ( freeSlots < count )
			return false;

		int remainingToPlace = count;
		for ( int i = 0; i < _itemIds.Count && remainingToPlace > 0; i++ )
		{
			if ( !IsEmptySlot( i ) )
				continue;

			_itemIds[i] = itemId;
			_itemCounts[i] = 1;
			if ( usesDurability )
			{
				int d = durability;
				if ( d <= 0 ) d = maxDurability;
				if ( d < 0 ) d = 0;
				if ( maxDurability > 0 && d > maxDurability ) d = maxDurability;
				_itemDurability[i] = d;
			}
			else
			{
				_itemDurability[i] = durability;
			}

			remainingToPlace--;
		}

		return remainingToPlace <= 0;
	}

	// ---- Get Item Count ----
	public int GetItemCount( int itemId )
	{
		if ( itemId <= 0 ) return 0;
		long total = 0;
			for ( int i = 0; i < _itemIds.Count; i++ )
		{
			if ( _itemIds[i] != itemId )
				continue;

			int c = i < _itemCounts.Count ? _itemCounts[i] : 0;
			if ( c > 0 )
				total += c;
		}

		if ( total <= 0 ) return 0;
		return total >= int.MaxValue ? int.MaxValue : (int)total;
	}

	public void ClearAll()
	{
		if ( Network.IsProxy ) return;
			EnsureSlotArraysSized();
			for ( int i = 0; i < _itemIds.Count; i++ )
			{
				_itemIds[i] = 0;
				_itemCounts[i] = 0;
				_itemDurability[i] = 0;
			}
		Revision++;
	}

	public void ExportSaveData( out List<int> itemIds, out List<int> itemCounts, out List<int> itemDurability )
	{
			EnsureSlotArraysSized();
			itemIds = new List<int>( _itemIds.Count );
			itemCounts = new List<int>( _itemCounts.Count );
			itemDurability = new List<int>( _itemDurability.Count );

			for ( int i = 0; i < _itemIds.Count; i++ )
				itemIds.Add( _itemIds[i] );
			for ( int i = 0; i < _itemCounts.Count; i++ )
				itemCounts.Add( _itemCounts[i] );
			for ( int i = 0; i < _itemDurability.Count; i++ )
				itemDurability.Add( _itemDurability[i] );
	}

	public void LoadSaveData( List<int> itemIds, List<int> itemCounts, List<int> itemDurability )
	{
		if ( Network.IsProxy ) return;
			EnsureSlotArraysSized();
			ClearAll();

		if ( itemIds == null || itemCounts == null )
		{
			Revision++;
			return;
		}

		int count = Math.Min( _itemIds.Count, Math.Min( itemIds.Count, itemCounts.Count ) );
		for ( int i = 0; i < count; i++ )
		{
			int id = itemIds[i];
			int c = itemCounts[i];
			if ( id <= 0 || c <= 0 )
			{
				ClearSlot( i );
				continue;
			}

			_itemIds[i] = id;
			_itemCounts[i] = c;
		}

		// Durability is optional; align as best-effort.
		if ( itemDurability != null )
		{
			for ( int i = 0; i < count; i++ )
			{
				int d = i < itemDurability.Count ? itemDurability[i] : 0;
				_itemDurability[i] = d;
			}
		}
		else
		{
			for ( int i = 0; i < count; i++ )
				_itemDurability[i] = 0;
		}

		Revision++;
	}

	/// <summary>
	/// Get remaining durability/grams for a given slot index.
	/// Returns 0 if out of range or unused.
	/// </summary>
	public int GetSlotDurability( int slotIndex )
	{
		if ( slotIndex < 0 || slotIndex >= _itemDurability.Count )
			return 0;

		return _itemDurability[slotIndex];
	}

	/// <summary>
	/// Set remaining durability/grams for a given slot index.
	/// Clamped to [0, MaxGrams] based on the item definition.
	/// </summary>
	public void SetSlotDurability( int slotIndex, int durability )
	{
		if ( Network.IsProxy ) return;
			EnsureSlotArraysSized();
			if ( slotIndex < 0 || slotIndex >= _itemIds.Count || slotIndex >= _itemDurability.Count )
			return;
			if ( _itemIds[slotIndex] <= 0 || _itemCounts[slotIndex] <= 0 )
				return;

		int itemId = _itemIds[slotIndex];
		var def = VeggaItemRegistry.Get( itemId );
		if ( def != null && def.MaxGrams > 0 )
		{
			_itemDurability[slotIndex] = Math.Clamp( durability, 0, def.MaxGrams );
		}
		else
		{
			_itemDurability[slotIndex] = 0;
		}

		Revision++;
	}

	/// <summary>
	/// Get the itemId for a given slot index, or 0 if empty/out of range.
	/// </summary>
	public int GetSlotItemId( int slotIndex )
	{
		if ( slotIndex < 0 || slotIndex >= _itemIds.Count )
			return 0;
			if ( _itemCounts[slotIndex] <= 0 ) return 0;
			return _itemIds[slotIndex];
	}

	/// <summary>
	/// Get the item count for a given slot index, or 0 if empty/out of range.
	/// </summary>
	public int GetSlotCount( int slotIndex )
	{
		if ( slotIndex < 0 || slotIndex >= _itemCounts.Count )
			return 0;
			if ( _itemIds[slotIndex] <= 0 ) return 0;
			return _itemCounts[slotIndex];
	}

	/// <summary>
	/// Find the first slot index containing the given itemId, or -1.
	/// </summary>
	public int FindFirstSlot( int itemId )
	{
			for ( int i = 0; i < _itemIds.Count; i++ )
		{
				if ( _itemIds[i] == itemId && _itemCounts[i] > 0 )
				return i;
		}
		return -1;
	}

	// ---- Add Bonus Slots ----
	public void AddBonusSlotsFromRank( int amount )
	{
		if ( Network.IsProxy ) return;
		BonusSlotsFromRank += amount;
			EnsureSlotArraysSized();
		Log.Info( $"📦 +{amount} inventory slots from rank! Total: {TotalSlots}" );
	}

	public void AddBonusSlotsFromQuest( int amount )
	{
		if ( Network.IsProxy ) return;
		BonusSlotsFromQuests += amount;
			EnsureSlotArraysSized();
		Log.Info( $"📦 +{amount} inventory slots from quest! Total: {TotalSlots}" );
	}

	public void AddBonusSlotsFromPurchase( int amount )
	{
		if ( Network.IsProxy ) return;
		BonusSlotsFromPurchase += amount;
			EnsureSlotArraysSized();
		Log.Info( $"📦 +{amount} inventory slots from purchase! Total: {TotalSlots}" );
	}

	// ---- Testing Buttons ----
	[Button( "Add Test Item" ), Group( "Testing" )]
	public void TestAddItem()
	{
		AddItem( 1001, 1 ); // Add item ID 1001
	}

	[Button( "Add 100 Slots (Rank)" ), Group( "Testing" )]
	public void TestAddRankSlots()
	{
		AddBonusSlotsFromRank( 100 );
	}
}

