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
	// ---- Constants ----
	public const int BaseSlots = 96; // 12x8 grid
	public const int MaxSlots = 1024; // Maximum possible slots
	public const int MaxStackSize = int.MaxValue; // 2,147,483,647
	const int DurableStackMarker = 0; // durability==0 means "full" when the item uses durability

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

	// ---- Local Singleton ----
	private static VeggaInventory _local;
	public static VeggaInventory Local
	{
		get
		{
			if ( _local is not null && _local.IsValid )
				return _local;

			var player = PlayerVeggaStats.Local;
			if ( player != null )
			{
				_local = player.GameObject.Components.Get<VeggaInventory>();
			}

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

		var def = VeggaItemRegistry.Get( itemId );
		int maxStack = def?.MaxStack ?? MaxStackSize;
		if ( maxStack <= 0 ) maxStack = 1;
		if ( maxStack > MaxStackSize ) maxStack = MaxStackSize;
		bool isStackable = maxStack > 1;
		bool usesDurability = def != null && def.MaxGrams > 0;
		int maxDurability = usesDurability ? def.MaxGrams : 0;

		// Non-stackable: each count consumes a slot.
		if ( !isStackable )
		{
			if ( FreeSlots < count )
			{
				Log.Warning( "❌ Inventory full!" );
				return false;
			}

			int remainingToPlace = count;
			for ( int i = 0; i < _itemIds.Count && remainingToPlace > 0; i++ )
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
		if ( FreeSlots < newStacksNeeded )
		{
			Log.Warning( "❌ Inventory full!" );
			return false;
		}

		// Fill existing stacks first.
		for ( int i = 0; i < _itemIds.Count && remaining > 0; i++ )
		{
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
	/// Host executes immediately; clients request via RPC.
	/// </summary>
	public void RequestMoveSlot( int fromIndex, int toIndex )
	{
		if ( fromIndex == toIndex ) return;
		if ( Networking.IsHost && !Network.IsProxy )
		{
			MoveSlotInternal( fromIndex, toIndex );
			return;
		}

		var requesterId = Connection.Local?.Id ?? Guid.Empty;
		RpcRequestMoveSlot( requesterId, fromIndex, toIndex );
	}

	[Rpc.Broadcast]
	private void RpcRequestMoveSlot( Guid requesterId, int fromIndex, int toIndex )
	{
		if ( !Networking.IsHost ) return;
		if ( requesterId == Guid.Empty ) return;
		if ( Network?.Owner?.Id != requesterId ) return;

		MoveSlotInternal( fromIndex, toIndex );
	}

	private void MoveSlotInternal( int fromIndex, int toIndex )
	{
		if ( Network.IsProxy ) return;
			EnsureSlotArraysSized();
			if ( fromIndex < 0 || fromIndex >= TotalSlots ) return;
			if ( toIndex < 0 || toIndex >= TotalSlots ) return;
		if ( fromIndex == toIndex ) return;
			if ( IsEmptySlot( fromIndex ) ) return;

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

