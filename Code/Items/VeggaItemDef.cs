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
	/// <summary>
	/// Optional: Prefab to use when dropping this item into the world.
	/// If set, drops should clone the prefab instance (preserves __Prefab).
	/// Use a project-relative prefab name like "goldbar_200g.prefab".
	/// </summary>
	public string PrefabPath { get; set; }
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

	public static void Initialize()
	{
		// NOTE:
		// Hotload can preserve static fields. We re-register the defaults every time
		// Initialize() is called so edits to item defs apply immediately without
		// requiring a full restart.
		// This is cheap (small item set) and prevents stale data like PrefabPath.

		_items ??= new();

		// ---- CURRENCY ----
		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.Cash,
			Name = "Money",
			Description = "Fiat currency. Stackable.",
			ModelPath = "models/money/batch_used.vmdl",
			Value = 1,
			MaxStack = 100000,
			Category = ItemCategory.Currency,
			Rarity = ItemRarity.Common
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.GoldCoin,
			Name = "Gold Coin",
			Description = "Gold currency. Stackable.",
			PrefabPath = "goldcoin.prefab",
			ModelPath = "models/goldcoin/goldcoin.vmdl",
			Value = 0,
			MaxStack = 100000,
			Category = ItemCategory.Currency,
			Rarity = ItemRarity.Common
		} );

		// ---- MATERIALS ----
		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.GoldBar200g,
			Name = "Gold Bar",
			Description = "A refined gold bar. Can be turned into coins at a furnace.",
			ModelPath = "goldbar/gold_bar.vmdl",
			PrefabPath = "goldbar_200g.prefab",
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Rare
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.GoldOre,
			Name = "Gold Ore",
			Description = "Unrefined ore. Smelt into a gold bar.",
			ModelPath = "models/props/rock_scatter/rock_scatter_03.vmdl",
			PrefabPath = "ore_pickup.prefab",
			// Use model thumbnail (SVGs were producing error/blank icons for some ore items).
			IconPath = null,
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Common
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.ClayOre,
			Name = "Clay Ore",
			Description = "Soft mineral-rich clay. (Not smeltable yet.)",
			ModelPath = "models/props/rock_scatter/rock_scatter_01.vmdl",
			PrefabPath = "ore_pickup.prefab",
			IconPath = null,
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Common
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.TinOre,
			Name = "Tin Ore",
			Description = "Dark heavy ore. Smelt into a tin bar.",
			ModelPath = "models/props/rock_scatter/rock_scatter_01.vmdl",
			PrefabPath = "ore_pickup.prefab",
			IconPath = null,
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Uncommon
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.CopperOre,
			Name = "Copper Ore",
			Description = "Reddish-brown ore. Smelt into a copper bar.",
			ModelPath = "models/props/rock_scatter/rock_scatter_03.vmdl",
			PrefabPath = "ore_pickup.prefab",
			IconPath = null,
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Uncommon
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.IronOre,
			Name = "Iron Ore",
			Description = "Common ore. Smelt into an iron bar.",
			ModelPath = "models/props/rock_scatter/rock_scatter_01.vmdl",
			PrefabPath = "ore_pickup.prefab",
			IconPath = null,
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Common
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.CoalOre,
			Name = "Coal Ore",
			Description = "Fuel-rich ore used for making steel.",
			ModelPath = "models/props/rock_scatter/rock_scatter_01.vmdl",
			PrefabPath = "ore_pickup.prefab",
			IconPath = null,
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Common
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.TinBar,
			Name = "Tin Bar",
			Description = "A refined tin bar.",
			ModelPath = "goldbar/gold_bar.vmdl",
			PrefabPath = "tinbar_200g.prefab",
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Uncommon
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.CopperBar,
			Name = "Copper Bar",
			Description = "A refined copper bar.",
			ModelPath = "goldbar/gold_bar.vmdl",
			PrefabPath = "copperbar_200g.prefab",
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Uncommon
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.BronzeBar,
			Name = "Bronze Bar",
			Description = "A bronze alloy bar made from copper + tin.",
			ModelPath = "goldbar/gold_bar.vmdl",
			PrefabPath = "bronzebar_200g.prefab",
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Rare
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.IronBar,
			Name = "Iron Bar",
			Description = "A refined iron bar.",
			ModelPath = "goldbar/gold_bar.vmdl",
			PrefabPath = "ironbar_200g.prefab",
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Common
		} );

		// ---- WEAPONS / TOOLS ----
		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.Pistol9mm,
			Name = "P250",
			Description = "A simple semi-auto pistol (9mm).",
			ModelPath = "models/weapons/sbox_pistol_usp/w_usp.vmdl",
			PrefabPath = "pistol_9mm.prefab",
			Value = 0,
			MaxStack = 1,
			MaxGrams = 12, // magazine capacity
			Category = ItemCategory.Equipment,
			Rarity = ItemRarity.Uncommon
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.Rifle556,
			Name = "MP5",
			Description = "A compact SMG (9mm).",
			ModelPath = "models/weapons/sbox_smg_mp5/w_mp5.vmdl",
			PrefabPath = "rifle_556.prefab",
			Value = 0,
			MaxStack = 1,
			MaxGrams = 30, // magazine capacity
			Category = ItemCategory.Equipment,
			Rarity = ItemRarity.Rare
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.DualPistols9mm,
			Name = "Dual P250s",
			Description = "A paired 9mm pistol setup. Mouse1 fires the camera-side pistol, Mouse2 fires the opposite pistol. No ADS.",
			ModelPath = "models/weapons/sbox_pistol_usp/w_usp.vmdl",
			PrefabPath = "pistol_9mm.prefab",
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Equipment,
			Rarity = ItemRarity.Rare
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.Knife,
			Name = "Knife",
			Description = "A close-range melee weapon.",
			ModelPath = "models/dev/knife.vmdl",
			PrefabPath = "knife.prefab",
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Equipment,
			Rarity = ItemRarity.Common
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.BuildHammer,
			Name = "Build Hammer",
			Description = "A tool item (build system stub).",
			ModelPath = "models/items/tools/hammer/wood_hammer.vmdl",
			PrefabPath = "wood_hammer.prefab",
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Equipment,
			Rarity = ItemRarity.Common
		} );

		// ---- AMMO ----
		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.Ammo9mm,
			Name = "9mm Rounds",
			Description = "Ammunition for 9mm weapons.",
			ModelPath = "models/weapons/sbox_ammo/9mm_ammobox/ammobox_9mm.vmdl",
			PrefabPath = "ammo_9mm.prefab",
			Value = 0,
			MaxStack = 200,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Common
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.Ammo556,
			Name = "5.56 Rounds",
			Description = "Ammunition for 5.56 weapons.",
			ModelPath = "models/items/ammo.vmdl",
			PrefabPath = "ammo_556.prefab",
			Value = 0,
			MaxStack = 200,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Common
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.SteelBar,
			Name = "Steel Bar",
			Description = "A steel bar made from iron and coal.",
			ModelPath = "goldbar/gold_bar.vmdl",
			PrefabPath = "steelbar_200g.prefab",
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Uncommon
		} );

		// ---- WOOD / FUEL ----
		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.LogFull,
			Name = "Log",
			Description = "A full log. Can be used as furnace fuel.",
			PrefabPath = "logs.prefab",
			ModelPath = "models/log/saunalog.vmdl",
			// Use model thumbnail instead of remote fish URL (fixes blank icons if fish fetch fails).
			IconPath = null,
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Common
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.LogChopped,
			Name = "Chopped Log",
			Description = "Half a log. Can be used as furnace fuel.",
			PrefabPath = "logchopped.prefab",
			// Match the chopped-log prefab model so the thumbnail is distinct.
			ModelPath = "models/logchopped/logchopped.vmdl",
			// Use model thumbnail instead of remote fish URL (fixes blank icons if fish fetch fails).
			IconPath = null,
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Common
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.NotedLogFull,
			Name = "Noted Log",
			Description = "A bank note representing a log. Exchange at a bank.",
			PrefabPath = "noted_page.prefab",
			// Inventory thumbnail should look like the underlying item; world-drop uses the page prefab.
			ModelPath = "models/log/saunalog.vmdl",
			IconPath = "ui/items/log.svg",
			Value = 0,
			MaxStack = 10000,
			Tradeable = true,
			Droppable = true,
			Category = ItemCategory.Misc,
			Rarity = ItemRarity.Common
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.NotedLogChopped,
			Name = "Noted Chopped Log",
			Description = "A bank note representing a chopped log. Exchange at a bank.",
			PrefabPath = "noted_page.prefab",
			// Inventory thumbnail should look like the underlying item; world-drop uses the page prefab.
			ModelPath = "models/logchopped/logchopped.vmdl",
			IconPath = "ui/items/log_chopped.svg",
			Value = 0,
			MaxStack = 10000,
			Tradeable = true,
			Droppable = true,
			Category = ItemCategory.Misc,
			Rarity = ItemRarity.Common
		} );

		// ---- MOULDS ----
		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.MouldBar,
			Name = "Bar Mould",
			Description = "A mould used for casting bars.",
			PrefabPath = "mould_bar.prefab",
			ModelPath = "models/food-kit/foodkit_plate-rectangle.vmdl",
			IconPath = "ui/items/mould.svg",
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Uncommon
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.MouldCoin,
			Name = "Coin Mould",
			Description = "Insert into furnace to mint gold coins from a gold bar.",
			PrefabPath = "mould_coin.prefab",
			ModelPath = "models/game_room/deep_plate.vmdl",
			IconPath = "ui/items/mould.svg",
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Uncommon
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.MouldGoblet,
			Name = "Goblet Mould",
			Description = "Insert into furnace to cast a gold goblet (consumes 2 gold bars).",
			PrefabPath = "mould_goblet.prefab",
			ModelPath = "models/rug/ceramic_vase_02_1k.vmdl",
			IconPath = "ui/items/mould.svg",
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Rare
		} );

		Register( new VeggaItemDef
		{
			Id = VeggaItemIds.GoldGoblet,
			Name = "Gold Goblet",
			Description = "A fancy gold goblet.",
			PrefabPath = "goldgoblet.prefab",
			// Let the inventory UI generate a model thumbnail (avoids trying to fetch a non-image URL).
			IconPath = null,
			// If this model exists via mounted content/packages, the thumbnail + world drop will use it.
			ModelPath = "models/3dmodelscc0/medievalpropspack/medieval_goblet/medieval_goblet.vmdl",
			Value = 0,
			MaxStack = 1,
			Category = ItemCategory.Material,
			Rarity = ItemRarity.Epic
		} );
	}

	public static void Register( VeggaItemDef item )
	{
		// Overwrite silently: Initialize() intentionally re-registers defaults.
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
