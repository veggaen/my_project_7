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

		// Check if we have space
		if ( UsedSlots >= TotalSlots )
		{
			Log.Warning( "❌ Inventory full!" );
			return false;
		}

		var def = VeggaItemRegistry.Get( itemId );
		bool isStackable = def == null || def.MaxStack > 1;

		// Try to stack with existing item for stackable items only
		if ( isStackable )
		{
			for ( int i = 0; i < _itemIds.Count; i++ )
			{
				if ( _itemIds[i] == itemId )
				{
					_itemCounts[i] += count;
					Log.Info( $"📦 Stacked item {itemId} x{count} (now {_itemCounts[i]})" );
					return true;
				}
			}
		}

		// Add new stack / instance
		_itemIds.Add( itemId );
		_itemCounts.Add( count );

		// Initialize durability/grams for this slot
		if ( def != null && def.MaxGrams > 0 )
		{
			// For non-stackable smeltable items like gold bars, store remaining grams.
			_itemDurability.Add( def.MaxGrams );
		}
		else
		{
			_itemDurability.Add( 0 );
		}

		Log.Info( $"📦 Added item {itemId} x{count} to inventory" );
		return true;
	}

	// ---- Remove Item ----
	public bool RemoveItem( int itemId, int count = 1 )
	{
		if ( Network.IsProxy ) return false;

		for ( int i = 0; i < _itemIds.Count; i++ )
		{
			if ( _itemIds[i] == itemId )
			{
				_itemCounts[i] -= count;

				if ( _itemCounts[i] <= 0 )
				{
					_itemIds.RemoveAt( i );
					_itemCounts.RemoveAt( i );
					if ( i < _itemDurability.Count )
					{
						_itemDurability.RemoveAt( i );
					}
					Log.Info( $"📦 Removed item {itemId} (stack depleted)" );
				}
				else
				{
					Log.Info( $"📦 Removed {count}x item {itemId} ({_itemCounts[i]} remaining)" );
				}

				return true;
			}
		}

		Log.Warning( $"❌ Item {itemId} not found in inventory" );
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

