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
		// - Under 10,000: show full number (with separators)
		// - 10,000+: abbreviate with k/m/b
		// - k: round down to integer (987,886 -> 987k)
		// - m/b: round down to 1 decimal (2,147,483 -> 2.1m)
		if ( amount < 0 )
			return "-" + FormatCompact( -amount );

		const int fullThreshold = 10_000;
		if ( amount < fullThreshold )
			return amount.ToString( "N0" );

		if ( amount >= 1_000_000_000 )
		{
			float v = TruncateToDecimals( amount / 1_000_000_000f, 1 );
			return $"{v:0.#}b";
		}
		if ( amount >= 1_000_000 )
		{
			float v = TruncateToDecimals( amount / 1_000_000f, 1 );
			return $"{v:0.#}m";
		}

		// 10,000..999,999
		return $"{amount / 1_000}k";
	}

	static float TruncateToDecimals( float value, int decimals )
	{
		if ( decimals <= 0 )
			return MathF.Floor( value );
		float scale = 1f;
		for ( int i = 0; i < decimals; i++ )
			scale *= 10f;
		return MathF.Floor( value * scale ) / scale;
	}
}
