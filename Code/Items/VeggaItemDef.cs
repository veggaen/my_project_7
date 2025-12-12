using System.Collections.Generic;
using Sandbox;

namespace Sandbox;

/// <summary>
/// Defines an item type with all its properties.
/// </summary>
public class VeggaItemDef
{
	public int Id { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public string IconPath { get; set; }
	public string ModelPath { get; set; }
	public int Value { get; set; } = 0; // Base sell value
	public int MaxStack { get; set; } = 1;
	public bool Tradeable { get; set; } = true;
	public bool Droppable { get; set; } = true;
	public ItemCategory Category { get; set; } = ItemCategory.Misc;
	public ItemRarity Rarity { get; set; } = ItemRarity.Common;

	/// <summary>
	/// Optional: maximum grams / durability for this item.
	/// Used by smeltable items like gold bars so each instance
	/// can track remaining grams independently.
	/// </summary>
	public int MaxGrams { get; set; } = 0;

	/// <summary>
	/// Optional: Can this item be crafted into something else?
	/// </summary>
	public List<CraftRecipe> CraftInto { get; set; } = new();
}

public enum ItemCategory
{
	Misc,
	Currency,
	Material,
	Consumable,
	Equipment,
	Quest
}

public enum ItemRarity
{
	Common,     // White
	Uncommon,   // Green
	Rare,       // Blue
	Epic,       // Purple
	Legendary   // Orange/Gold
}

public class CraftRecipe
{
	public int OutputItemId { get; set; }
	public int OutputCount { get; set; } = 1;
	public string RecipeName { get; set; }
}

/// <summary>
/// Static registry of all item definitions.
/// </summary>
public static class VeggaItemRegistry
{
	private static Dictionary<int, VeggaItemDef> _items = new();
	private static bool _initialized = false;

	public static void Initialize()
	{
		if ( _initialized ) return;
		_initialized = true;

		// ---- CURRENCY ----
		Register( new VeggaItemDef
		{
			Id = 1,
			Name = "Gold Coin",
			Description = "A shiny gold coin worth $1.",
			IconPath = "ui/items/gold_coin.png",
			ModelPath = "models/items/gold_coin.vmdl",
			Value = 1,
			MaxStack = 999999,
			Category = ItemCategory.Currency,
			Rarity = ItemRarity.Common
		} );

		// ---- MATERIALS ----
		Register( new VeggaItemDef
		{
			Id = 100,
			Name = "200g Gold Bar",
			Description = "A solid gold bar weighing 200 grams. Can be slowly shaved by hand or rapidly smelted at a furnace.",
			IconPath = "ui/items/gold_bar.png",
			ModelPath = "goldbar/gold_bar.vmdl",
			// New design: 1 gram = $1. A 200g bar is worth $200.
			Value = 200,
			MaxStack = 1, // Non-stackable so each bar can track its own grams
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Rare,
			MaxGrams = 200,
			CraftInto = new List<CraftRecipe>
			{
				new CraftRecipe
				{
					OutputItemId = 1, // Gold Coin
					// This mirrors the value above so generic crafting UIs can read it
					OutputCount = 200,
					RecipeName = "Smelt into Gold Coins"
				}
			}
		} );

		Register( new VeggaItemDef
		{
			Id = 101,
			Name = "500g Gold Bar",
			Description = "A hefty gold bar weighing 500 grams. Worth about $500 in coins.",
			IconPath = "ui/items/gold_bar_large.png",
			ModelPath = "goldbar/gold_bar_large.vmdl",
			Value = 500,
			MaxStack = 50,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Epic,
			CraftInto = new List<CraftRecipe>
			{
				new CraftRecipe
				{
					OutputItemId = 1,
					OutputCount = 500,
					RecipeName = "Smelt into Gold Coins"
				}
			}
		} );

		Log.Info( $"✅ VeggaItemRegistry initialized with {_items.Count} items" );
	}

	public static void Register( VeggaItemDef item )
	{
		if ( _items.ContainsKey( item.Id ) )
		{
			Log.Warning( $"⚠️ Item ID {item.Id} already registered, overwriting" );
		}
		_items[item.Id] = item;
	}

	public static VeggaItemDef Get( int id )
	{
		Initialize();
		return _items.TryGetValue( id, out var item ) ? item : null;
	}

	public static IEnumerable<VeggaItemDef> GetAll()
	{
		Initialize();
		return _items.Values;
	}

	public static bool Exists( int id )
	{
		Initialize();
		return _items.ContainsKey( id );
	}
}
