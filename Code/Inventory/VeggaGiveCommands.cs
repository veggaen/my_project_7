using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Debug/cheat-style commands to give yourself items and list the registry.
/// Host-only.
/// </summary>
public static class VeggaGiveCommands
{
	private const string LogPrefix = "[Give]";

	static bool EnsureHostAndInventory( out VeggaInventory inv )
	{
		inv = null;

		if ( !Networking.IsHost )
		{
			Log.Warning( $"{LogPrefix} Host-only (run it on the server/host)." );
			return false;
		}

		inv = VeggaInventory.Local;
		if ( inv == null || !inv.IsValid() )
		{
			Log.Warning( $"{LogPrefix} No local inventory found." );
			return false;
		}

		return true;
	}

	static int GetEffectiveMaxStack( VeggaItemDef def )
	{
		if ( def == null ) return 1;
		return VeggaInventory.GetEffectiveMaxStackForItem( def.Id, def );
	}

	// Keep vegga_give_all sane: currency stacks are effectively huge, but "give all" should not hand out billions.
	static int GetGiveAllDesiredAmount( VeggaItemDef def )
	{
		if ( def == null ) return 1;
		if ( !IsStackable( def ) ) return 1;

		int desired = GetEffectiveMaxStack( def );
		if ( VeggaInventory.IsCurrencyItemId( def.Id ) )
			desired = Math.Min( desired, VeggaInventory.MaxStackSize );
		return desired;
	}

	static bool IsStackable( VeggaItemDef def )
		=> GetEffectiveMaxStack( def ) > 1;

	static bool TryResolveItem( string token, out VeggaItemDef def )
	{
		def = null;
		if ( string.IsNullOrWhiteSpace( token ) )
			return false;

		token = token.Trim();

		// Numeric id support
		if ( int.TryParse( token, out int id ) )
		{
			def = VeggaItemRegistry.Get( id );
			return def != null;
		}

		// Name support (case-insensitive), with a normalized fallback.
		var all = VeggaItemRegistry.GetAll()?.ToList() ?? new List<VeggaItemDef>();
		def = all.FirstOrDefault( d => d != null && string.Equals( d.Name, token, StringComparison.OrdinalIgnoreCase ) );
		if ( def != null ) return true;

		static string Normalize( string s )
		{
			if ( string.IsNullOrWhiteSpace( s ) ) return string.Empty;
			var chars = s.Where( ch => char.IsLetterOrDigit( ch ) ).ToArray();
			return new string( chars ).ToLowerInvariant();
		}

		var needle = Normalize( token );
		if ( needle.Length == 0 ) return false;

		def = all.FirstOrDefault( d => d != null && Normalize( d.Name ) == needle );
		if ( def != null ) return true;

		// As a last resort, allow contains match (if unique).
		var matches = all
			.Where( d => d != null && Normalize( d.Name ).Contains( needle, StringComparison.Ordinal ) )
			.Take( 3 )
			.ToList();

		if ( matches.Count == 1 )
		{
			def = matches[0];
			return true;
		}

		if ( matches.Count > 1 )
			Log.Warning( $"{LogPrefix} Item name '{token}' is ambiguous. Try an item id instead." );
		return false;
	}

	static bool TryGive( VeggaInventory inv, VeggaItemDef def, int requestedAmount, bool clampNonStackableToOne, out int givenAmount )
	{
		givenAmount = 0;
		if ( inv == null || def == null ) return false;
		if ( def.Id <= 0 ) return false;

		int maxStack = GetEffectiveMaxStack( def );
		bool stackable = maxStack > 1;

		int amount = requestedAmount;
		if ( amount <= 0 ) amount = 1;
		if ( stackable ) amount = Math.Min( amount, maxStack );
		if ( !stackable && clampNonStackableToOne ) amount = 1;

		// Respect inventory space; skip if it can't fit.
		if ( !inv.CanFitItem( def.Id, amount ) )
			return false;

		if ( inv.AddItem( def.Id, amount ) )
		{
			givenAmount = amount;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Give a single item by id or name.
	/// Defaults: stackable =&gt; 100 (clamped to max stack), non-stackable =&gt; 1.
	/// Usage: vegga_give &lt;itemId|itemName&gt; [amount]
	/// Examples: vegga_give 100, vegga_give "Gold Bar", vegga_give log 100
	/// </summary>
	[ConCmd( "vegga_give", Help = "Give yourself an item. Usage: vegga_give <itemId|itemName> [amount]" )]
	public static void GiveCmd( string item, int amount = -1 )
	{
		if ( !EnsureHostAndInventory( out var inv ) ) return;

		if ( !TryResolveItem( item, out var def ) )
		{
			Log.Warning( $"{LogPrefix} Unknown item '{item}'." );
			return;
		}

		bool stackable = IsStackable( def );
		int defaultAmount = stackable ? 100 : 1;
		int requested = amount > 0 ? amount : defaultAmount;

		if ( TryGive( inv, def, requested, clampNonStackableToOne: true, out int given ) )
		{
			Log.Info( $"{LogPrefix} Added {given}x {def.Name} (id={def.Id})." );
			PlayerDataPersistence.SaveLocalNow();
		}
		else
		{
			Log.Warning( $"{LogPrefix} Could not add {def.Name} (inventory full?)" );
		}
	}

	[ConCmd( "hex_give", Help = "Alias for vegga_give. Usage: hex_give <itemId|itemName> [amount]" )]
	public static void HexGiveCmd( string item, int amount = -1 )
		=> GiveCmd( item, amount );

	/// <summary>
	/// Give one of every non-stackable item.
	/// Usage: vegga_give_unstackables
	/// </summary>
	[ConCmd( "vegga_give_unstackables", Help = "Give yourself one of every non-stackable item." )]
	public static void GiveUnstackablesCmd()
	{
		if ( !EnsureHostAndInventory( out var inv ) ) return;

		int givenKinds = 0;
		int skippedKinds = 0;

		foreach ( var def in VeggaItemRegistry.GetAll().OrderBy( d => d?.Id ?? 0 ) )
		{
			if ( def == null || def.Id <= 0 ) continue;
			if ( IsStackable( def ) ) continue;

			if ( TryGive( inv, def, 1, clampNonStackableToOne: true, out _ ) )
				givenKinds++;
			else
				skippedKinds++;
		}

		Log.Info( $"{LogPrefix} Unstackables: gave {givenKinds}, skipped {skippedKinds}." );
		PlayerDataPersistence.SaveLocalNow();
	}

	[ConCmd( "hex_give_unstackables", Help = "Alias for vegga_give_unstackables." )]
	public static void HexGiveUnstackablesCmd()
		=> GiveUnstackablesCmd();

	/// <summary>
	/// Give stackable items.
	/// - If amount omitted: give one full stack of each stackable (clamped to max stack).
	/// - If amount provided: give that amount of each (clamped to max stack).
	/// Usage: vegga_give_stackables [amount]
	/// </summary>
	[ConCmd( "vegga_give_stackables", Help = "Give yourself stackable items. Usage: vegga_give_stackables [amount]" )]
	public static void GiveStackablesCmd( int amount = -1 )
	{
		if ( !EnsureHostAndInventory( out var inv ) ) return;

		int givenKinds = 0;
		int skippedKinds = 0;

		foreach ( var def in VeggaItemRegistry.GetAll().OrderBy( d => d?.Id ?? 0 ) )
		{
			if ( def == null || def.Id <= 0 ) continue;
			if ( !IsStackable( def ) ) continue;

			// Default to 100 each (clamped), not "full max stack".
			int desired = amount > 0 ? amount : 100;
			if ( TryGive( inv, def, desired, clampNonStackableToOne: false, out _ ) )
				givenKinds++;
			else
				skippedKinds++;
		}

		Log.Info( $"{LogPrefix} Stackables: gave {givenKinds}, skipped {skippedKinds}." );
		PlayerDataPersistence.SaveLocalNow();
	}

	[ConCmd( "hex_give_stackables", Help = "Alias for vegga_give_stackables. Usage: hex_give_stackables [amount]" )]
	public static void HexGiveStackablesCmd( int amount = -1 )
		=> GiveStackablesCmd( amount );

	/// <summary>
	/// Give all items in the registry.
	/// Stackables are given as full stacks (max stack), unstackables as single.
	/// Usage: vegga_give_all
	/// </summary>
	[ConCmd( "vegga_give_all", Help = "Give yourself one of every item (stackables at max stack)." )]
	public static void GiveAllCmd()
	{
		if ( !EnsureHostAndInventory( out var inv ) ) return;

		int givenKinds = 0;
		int skippedKinds = 0;

		foreach ( var def in VeggaItemRegistry.GetAll().OrderBy( d => d?.Id ?? 0 ) )
		{
			if ( def == null || def.Id <= 0 ) continue;
			int desired = GetGiveAllDesiredAmount( def );

			if ( TryGive( inv, def, desired, clampNonStackableToOne: true, out _ ) )
				givenKinds++;
			else
				skippedKinds++;
		}

		Log.Info( $"{LogPrefix} All items: gave {givenKinds}, skipped {skippedKinds}." );
		PlayerDataPersistence.SaveLocalNow();
	}

	[ConCmd( "hex_give_all", Help = "Alias for vegga_give_all." )]
	public static void HexGiveAllCmd()
		=> GiveAllCmd();

	/// <summary>
	/// Print a clean list of items to console.
	/// Usage: vegga_items_list
	/// </summary>
	[ConCmd( "vegga_items_list", Help = "List all registered items." )]
	public static void ListItemsCmd()
	{
		var items = VeggaItemRegistry.GetAll()
			.Where( d => d != null && d.Id > 0 )
			.OrderBy( d => d.Category )
			.ThenBy( d => d.Id )
			.ToList();

		Log.Info( "[Items] ===== Registry =====" );
		foreach ( var def in items )
		{
			int maxStack = GetEffectiveMaxStack( def );
			string stackLabel = maxStack > 1 ? "stackable" : "unstackable";
			Log.Info( $"[Items] {def.Name} (id={def.Id}) - {stackLabel} - max: {maxStack}" );
		}
		Log.Info( $"[Items] Total: {items.Count}" );
	}

	[ConCmd( "hex_items_list", Help = "Alias for vegga_items_list." )]
	public static void HexListItemsCmd()
		=> ListItemsCmd();
}
