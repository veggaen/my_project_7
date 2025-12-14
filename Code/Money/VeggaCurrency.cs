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
		if ( amount >= 1_000_000_000 ) return $"{amount / 1_000_000_000f:0.#}B";
		if ( amount >= 1_000_000 ) return $"{amount / 1_000_000f:0.#}M";
		if ( amount >= 1_000 ) return $"{amount / 1_000f:0.#}k";
		return amount.ToString();
	}
}
