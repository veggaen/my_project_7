using System;
using Sandbox;

namespace Sandbox.Money;

public static class VeggaCurrency
{
	// Fiat money item id (defined in VeggaItemRegistry)
	public const int CashItemId = 1;

	// Gold coin item id (defined in VeggaItemRegistry)
	public const int GoldCoinItemId = 2;

	public static int GetCash( VeggaInventory inventory )
	{
		return inventory?.GetItemCount( CashItemId ) ?? 0;
	}

	public static bool TryAddCash( GameObject player, int amount )
	{
		if ( player == null || amount <= 0 ) return false;
		var inventory = player.Components.Get<VeggaInventory>();
		if ( inventory == null ) return false;
		return inventory.AddItem( CashItemId, amount );
	}

	public static int GetGoldCoins( VeggaInventory inventory )
	{
		return inventory?.GetItemCount( GoldCoinItemId ) ?? 0;
	}

	public static bool TryAddGoldCoins( GameObject player, int amount )
	{
		if ( player == null || amount <= 0 ) return false;
		var inventory = player.Components.Get<VeggaInventory>();
		if ( inventory == null ) return false;
		return inventory.AddItem( GoldCoinItemId, amount );
	}

	public static bool TryRemoveGoldCoins( GameObject player, int amount )
	{
		if ( player == null || amount <= 0 ) return false;
		var inventory = player.Components.Get<VeggaInventory>();
		if ( inventory == null ) return false;
		return inventory.RemoveItem( GoldCoinItemId, amount );
	}

	public static bool TryRemoveCash( GameObject player, int amount )
	{
		if ( player == null || amount <= 0 ) return false;
		var inventory = player.Components.Get<VeggaInventory>();
		if ( inventory == null ) return false;
		return inventory.RemoveItem( CashItemId, amount );
	}

	public static string FormatCompact( int amount )
	{
		// Inventory/UI count formatting rules:
		// - Under 10,000: show full number (no separators)
		// - 10,000..999,999: show k (floor), e.g. 999,999 -> 999k
		// - 1,000,000..999,999,999: show M (floor), e.g. 2,147,483,647 -> 2147M
		// - 1,000,000,000+: show B (floor)
		if ( amount < 0 )
			return "-" + FormatCompact( -amount );

		if ( amount < 10_000 )
			return amount.ToString();

		if ( amount >= 1_000_000_000 )
			return $"{amount / 1_000_000_000}B";
		if ( amount >= 1_000_000 )
			return $"{amount / 1_000_000}M";
		return $"{amount / 1_000}k";
	}

	public static string FormatExact( int amount )
	{
		return amount.ToString( "N0" );
	}

	/// <summary>
	/// Cash model selection. Never uses one-sided single-bill models.
	/// </summary>
	public static string GetCashModelPathForAmount( int amount )
	{
		amount = Math.Clamp( amount, 1, int.MaxValue );

		// Requested tiers:
		// - Use batch models for low/medium values
		// - Use box model for 100k+ (100k money box)
		if ( amount <= 10_000 )
			return "models/money/batch_used.vmdl";
		if ( amount < 100_000 )
			return "models/money/batch_clean.vmdl";
		return "models/money/box.vmdl";
	}
}
