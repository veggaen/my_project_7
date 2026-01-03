using System;
using System.Linq;
using Sandbox;

namespace Sandbox.Money;

public static class VeggaCurrency
{
	// Fiat money item id (defined in VeggaItemRegistry)
	public const int CashItemId = 1;

	// Gold coin item id (defined in VeggaItemRegistry)
	public const int GoldCoinItemId = 2;

	// Cash visual tiers for world drops (prefab-based):
	// - 1..99,999 uses moneyveggabundle.prefab
	// - 100,000..4,999,999 uses moneyveggapile.prefab
	// - 5,000,000+ uses moneyveggamassivebundle.prefab
	public const int CashPileMinAmount = 100_000;
	public const int CashBoxMinAmount = 5_000_000;

	// Legacy tier constants still referenced across the codebase.
	// Keep them so older logic compiles, but align them to the new design.
	public const int CashBillAmount = 100_000;
	public const int CashBundleAmount = CashPileMinAmount;
	public const int CashBoxAmount = CashBoxMinAmount;

	public static Vector3 GetCashBoxColliderScaleForAmount( int amount )
	{
		// Legacy helper: kept for compatibility.
		// Prefer using author-authored BoxCollider scales in the money prefabs.
		amount = Math.Clamp( amount, 1, int.MaxValue );
		if ( amount >= CashBoxAmount )
			return new Vector3( 16f, 31f, 15f );
		if ( amount >= CashBundleAmount )
			return new Vector3( 4f, 9f, 2f );
		return new Vector3( 2f, 5f, 1f );
	}

	public static void ApplyCashRigidbodyTuning( Rigidbody rb, int amount )
	{
		if ( rb == null || !rb.IsValid() )
			return;

		amount = Math.Clamp( amount, 1, int.MaxValue );
		if ( amount >= CashBoxAmount )
		{
			// Massive bundle: heavy, settles quickly, stays pushable.
			rb.MassOverride = 70f;
			rb.LinearDamping = MathF.Max( rb.LinearDamping, 0.75f );
			rb.AngularDamping = MathF.Max( rb.AngularDamping, 5.0f );
			try
			{
				var locking = rb.Locking;
				locking.Pitch = true;
				locking.Roll = true;
				rb.Locking = locking;
			}
			catch { }
			return;
		}

		// Medium pile.
		if ( amount >= CashBundleAmount )
		{
			rb.MassOverride = 38f;
			rb.LinearDamping = MathF.Max( rb.LinearDamping, 0.60f );
			rb.AngularDamping = MathF.Max( rb.AngularDamping, 3.4f );
			return;
		}

		// Small bundle.
		rb.MassOverride = 24f;
		rb.LinearDamping = MathF.Max( rb.LinearDamping, 0.45f );
		rb.AngularDamping = MathF.Max( rb.AngularDamping, 2.2f );
	}

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
		if ( amount >= CashBoxAmount )
			return "models/money/box.vmdl";
		if ( amount >= CashBundleAmount )
			return "models/money/batch_used.vmdl";
		return "models/money/batch_clean.vmdl";
	}

	public static string GetCashPrefabPathForAmount( int amount )
	{
		amount = Math.Clamp( amount, 1, int.MaxValue );
		// Three-prefab setup:
		// - 1..99,999 => moneyveggabundle.prefab
		// - 100,000..4,999,999 => moneyveggapile.prefab
		// - 5,000,000+ => moneyveggamassivebundle.prefab
		if ( amount >= CashBoxAmount )
			return "moneyveggamassivebundle.prefab";
		if ( amount >= CashBundleAmount )
			return "moneyveggapile.prefab";
		return "moneyveggabundle.prefab";
	}
}
