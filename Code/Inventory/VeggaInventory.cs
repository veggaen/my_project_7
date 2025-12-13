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
	public int UsedSlots => _itemIds.Count;
	public int FreeSlots => TotalSlots - UsedSlots;

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
		Log.Info( $"✅ VeggaInventory: {TotalSlots} slots available ({UsedSlots} used, {FreeSlots} free)" );
	}

	// ---- Add Item ----
	public bool AddItem( int itemId, int count = 1 )
	{
		if ( Network.IsProxy ) return false;
		if ( itemId <= 0 || count <= 0 ) return false;

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

			for ( int n = 0; n < count; n++ )
			{
				_itemIds.Add( itemId );
				_itemCounts.Add( 1 );
				_itemDurability.Add( usesDurability ? maxDurability : 0 );
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
			if ( _itemIds[i] != itemId )
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
			if ( _itemIds[i] != itemId )
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

		// Add new stacks as needed.
		while ( remaining > 0 )
		{
			int add = (int)Math.Min( (long)maxStack, remaining );
			_itemIds.Add( itemId );
			_itemCounts.Add( add );
			_itemDurability.Add( usesDurability ? DurableStackMarker : 0 );
			remaining -= add;
		}

		Revision++;
		Log.Info( $"📦 Added item {itemId} x{count} to inventory" );
		return true;
	}

	// ---- Remove Item ----
	public bool RemoveItem( int itemId, int count = 1 )
	{
		if ( Network.IsProxy ) return false;
		if ( itemId <= 0 || count <= 0 ) return false;

		int remaining = count;
		bool changed = false;

		for ( int i = 0; i < _itemIds.Count && remaining > 0; i++ )
		{
			if ( _itemIds[i] != itemId )
				continue;

			int take = Math.Min( remaining, _itemCounts[i] );
			_itemCounts[i] -= take;
			remaining -= take;
			changed = true;

			if ( _itemCounts[i] <= 0 )
			{
				_itemIds.RemoveAt( i );
				_itemCounts.RemoveAt( i );
				if ( i < _itemDurability.Count )	_itemDurability.RemoveAt( i );
				i--; // account for removal
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
		if ( slotIndex < 0 || slotIndex >= _itemIds.Count ) return false;
		if ( count <= 0 ) return false;

		_itemCounts[slotIndex] -= count;
		if ( _itemCounts[slotIndex] <= 0 )
		{
			_itemIds.RemoveAt( slotIndex );
			_itemCounts.RemoveAt( slotIndex );
			if ( slotIndex < _itemDurability.Count )
				_itemDurability.RemoveAt( slotIndex );
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

		for ( int i = 0; i < _itemIds.Count; i++ )
		{
			if ( _itemIds[i] != itemId )
				continue;

			int dur = i < _itemDurability.Count ? _itemDurability[i] : 0;
			bool isPartial = dur > 0 && dur < maxDurability;
			if ( isPartial )
				continue;

			if ( _itemCounts[i] <= 1 )
			{
				// Single bar slot: normalize it into a durable slot.
				if ( i < _itemDurability.Count )
					_itemDurability[i] = maxDurability;
				else
					_itemDurability.Add( maxDurability );
				Revision++;
				newSlotIndex = i;
				return true;
			}

			// Split one out of the stack.
			_itemCounts[i] -= 1;
			_itemIds.Add( itemId );
			_itemCounts.Add( 1 );
			_itemDurability.Add( maxDurability );
			Revision++;
			newSlotIndex = _itemIds.Count - 1;
			return true;
		}

		return false;
	}

	// ---- Get Item Count ----
	public int GetItemCount( int itemId )
	{
		for ( int i = 0; i < _itemIds.Count; i++ )
		{
			if ( _itemIds[i] == itemId )
				return _itemCounts[i];
		}
		return 0;
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
		if ( slotIndex < 0 || slotIndex >= _itemIds.Count || slotIndex >= _itemDurability.Count )
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
		return _itemIds[slotIndex];
	}

	/// <summary>
	/// Get the item count for a given slot index, or 0 if empty/out of range.
	/// </summary>
	public int GetSlotCount( int slotIndex )
	{
		if ( slotIndex < 0 || slotIndex >= _itemCounts.Count )
			return 0;
		return _itemCounts[slotIndex];
	}

	/// <summary>
	/// Find the first slot index containing the given itemId, or -1.
	/// </summary>
	public int FindFirstSlot( int itemId )
	{
		for ( int i = 0; i < _itemIds.Count; i++ )
		{
			if ( _itemIds[i] == itemId )
				return i;
		}
		return -1;
	}

	// ---- Add Bonus Slots ----
	public void AddBonusSlotsFromRank( int amount )
	{
		if ( Network.IsProxy ) return;
		BonusSlotsFromRank += amount;
		Log.Info( $"📦 +{amount} inventory slots from rank! Total: {TotalSlots}" );
	}

	public void AddBonusSlotsFromQuest( int amount )
	{
		if ( Network.IsProxy ) return;
		BonusSlotsFromQuests += amount;
		Log.Info( $"📦 +{amount} inventory slots from quest! Total: {TotalSlots}" );
	}

	public void AddBonusSlotsFromPurchase( int amount )
	{
		if ( Network.IsProxy ) return;
		BonusSlotsFromPurchase += amount;
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

