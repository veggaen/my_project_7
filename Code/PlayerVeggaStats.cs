using System;
using System.Linq;
using Sandbox;
using Sandbox.Money;
using Sandbox.UI;

namespace Sandbox;

public sealed class PlayerVeggaStats : Component
{
	// -------- Inspector / starting values --------
	[Property, Group( "Config" )] public float MaxHealth { get; set; } = 100f;
	[Property, Group( "Config" )] public float InitHealth { get; set; } = 100f;

	[Property, Group( "Config" )] public float MaxArmor { get; set; } = 100f;
	[Property, Group( "Config" )] public float InitArmor { get; set; } = 50f;

	[Property, Group( "Config" )] public int StartMoney { get; set; } = 500;
	[Property, Group( "Config" )] public string StartJobName { get; set; } = "Citizen";
	[Property, Group( "Config" )] public float MaxPrayer { get; set; } = 99f;
	[Property, Group( "Config" )] public float MaxStamina { get; set; } = 100f;
	[Property, Group( "Config" )] public float MaxSpecialAttack { get; set; } = 100f;

	// -------- Runtime networked state --------
	[Sync] public float Health { get; private set; }
	[Sync] public float Armor { get; private set; }
	[Sync] public string JobName { get; private set; } = "Citizen";
	[Sync] public float Prayer { get; private set; }
	[Sync] public float Stamina { get; private set; }
	[Sync] public float SpecialAttack { get; private set; }

	// ---- Cash drop (pallet build) UI ----
	// Host updates these while spawning a cash pallet so the local HUD can show progress.
	[Sync] public bool CashDropBuildActive { get; private set; }
	[Sync] public float CashDropBuildProgress01 { get; private set; }

	internal void SetCashDropBuildProgress( bool active, float progress01 )
	{
		if ( Network.IsProxy )
			return;

		CashDropBuildActive = active;
		CashDropBuildProgress01 = progress01.Clamp( 0f, 1f );
	}

	public int Money
	{
		get
		{
			var inv = GameObject?.Components.Get<VeggaInventory>();
			return VeggaCurrency.GetCash( inv );
		}
	}

	/// <summary>
	/// True while the player is standing near at least one active furnace aura.
	/// </summary>
	[Sync] public bool HasForgeBuff { get; private set; }

	/// <summary>
	/// Current effective smelt speed multiplier.
	/// - 1.0 when hand-crafting away from any forge.
	/// - &gt; 1.0 when near a forge, scaled further by Smithing level.
	/// This value is recalculated whenever forge auras change or
	/// when the Smithing level changes.
	/// </summary>
	[Sync] public float SmeltSpeedMultiplier { get; private set; } = 1f;

	// Events
	public event Action OnHealthChanged;

	// -------- Inspector Display (Read-Only) --------
	[Property, Group( "Runtime Stats" ), ReadOnly, Title( "Current HP" )]
	public float CurrentHealth => Health;

	[Property, Group( "Runtime Stats" ), ReadOnly, Title( "Current Armor" )]
	public float CurrentArmor => Armor;

	[Property, Group( "Runtime Stats" ), ReadOnly, Title( "Current Money" )]
	public int CurrentMoney => Money;

	[Property, Group( "Runtime Stats" ), ReadOnly, Title( "Current Job" )]
	public string CurrentJob => JobName;

	[Property, Group( "Runtime Stats" ), ReadOnly, Title( "Current Prayer" )]
	public float CurrentPrayer => Prayer;

	[Property, Group( "Runtime Stats" ), ReadOnly, Title( "Current Stamina" )]
	public float CurrentStamina => Stamina;

	[Property, Group( "Runtime Stats" ), ReadOnly, Title( "Current Special" )]
	public float CurrentSpecial => SpecialAttack;

	/// <summary>
	/// Convenience accessor for "my" stats on the local client.
	/// Uses Connection.Local to make sure we always pick the correct player
	/// when multiple players exist in the scene.
	/// </summary>
	private static PlayerVeggaStats _local;
	public static PlayerVeggaStats Local
	{
		get
		{
			// Check if cached local is still valid AND still owned by our local connection
			var localConn = Connection.Local;
			if ( localConn != null && _local != null && _local.IsValid() && _local.Network.Owner == localConn )
				return _local;

			// Clear invalid cache
			_local = null;

			var scene = Game.ActiveScene;
			if ( scene is null ) return null;

			// 1) Prefer exact Network.Owner match (normal multiplayer case)
			if ( localConn is not null )
			{
				foreach ( var stats in scene.GetAllComponents<PlayerVeggaStats>() )
				{
					if ( stats.IsValid() && stats.Network.Owner == localConn )
					{
						_local = stats;
						return _local;
					}
				}
			}

			// 2) Editor / transient network states: owner can be null. Prefer a non-proxy.
			foreach ( var stats in scene.GetAllComponents<PlayerVeggaStats>() )
			{
				if ( !stats.IsValid() )
					continue;

				var net = stats.Network;
				if ( net != null && net.Owner == null && !net.IsProxy )
				{
					_local = stats;
					return _local;
				}
			}

			// 3) Final fallback: any non-proxy stats in the scene.
			foreach ( var stats in scene.GetAllComponents<PlayerVeggaStats>() )
			{
				if ( !stats.IsValid() )
					continue;

				var net = stats.Network;
				if ( net == null || !net.IsProxy )
				{
					_local = stats;
					return _local;
				}
			}

			return null;
		}
	}

	/// <summary>
	/// Clear the local cache. Call this when network state changes significantly.
	/// </summary>
	public static void ClearLocalCache()
	{
		_local = null;
	}

	/// <summary>
	/// Flag set by PlayerDataPersistence when it loads money.
	/// Prevents fallback from overwriting loaded money.
	/// </summary>
	internal bool _moneyLoadedFromSave = false;

	/// <summary>
	/// Flag to track if OnStart has completed initialization.
	/// </summary>
	internal bool _initialized = false;

	/// <summary>
	/// Timer for fallback money initialization (in case persistence doesn't run).
	/// </summary>
	private RealTimeSince _timeSinceStart;
	private bool _fallbackApplied = false;

	/// <summary>
	/// Active forge auras affecting this player on the host.
	/// Not synced directly; we only sync HasForgeBuff/SmeltSpeedMultiplier.
	/// </summary>
	private readonly HashSet<PlayerForgeAura> _activeForges = new();

	// Initialize on the simulating side only
	protected override void OnStart()
	{
		if ( Network.IsProxy )
			return;

		Log.Info( $"[PlayerVeggaStats] OnStart() called. Money={Money}, _moneyLoadedFromSave={_moneyLoadedFromSave}" );

		Health = InitHealth;
		Armor = InitArmor;

		// 🎯 DON'T set money here - let PlayerDataPersistence handle it
		// A fallback in OnUpdate() will apply StartMoney if persistence doesn't run

		JobName = StartJobName;
		Prayer = MaxPrayer;
		Stamina = MaxStamina;
		SpecialAttack = MaxSpecialAttack;

		_initialized = true;
		_timeSinceStart = 0;
		Log.Info( $"[PlayerVeggaStats] OnStart() completed. Waiting for PlayerDataPersistence..." );
	}

	protected override void OnUpdate()
	{
		if ( Network.IsProxy ) return;

		// Starter cash is handled by PlayerDataManager/PlayerDataPersistence.
		// Avoid runtime fallbacks here; they cause repeated grants in editor Stop->Play.

		// TypeScript: updateCache(this) - only emits events if changed
		PlayerDataCache.UpdatePlayerStats( this );
	}

	/// <summary>
	/// Called by PlayerDataPersistence after loading data.
	/// </summary>
	internal void FinalizeMoneyInit()
	{
		if ( Network.IsProxy ) return;

		_fallbackApplied = true; // Disable the fallback since persistence ran
		Log.Info( $"💰 [FinalizeMoneyInit] Money ready: ${Money} (loadedFromSave={_moneyLoadedFromSave})" );
	}

	// ---- Simple helpers you can call from other code ----

	public void Damage( float amount, float armorPenetration = 0f )
	{
		if ( Network.IsProxy || amount <= 0f ) return;

		var oldHealth = Health;
		var oldArmor = Armor;

		// Armor penetration (0.0 = no pen, 1.0 = 100% pen)
		armorPenetration = armorPenetration.Clamp( 0f, 1f );

		// Damage split: 10% to HP, 90% to Armor (before armor pen)
		float hpDamage = amount * 0.1f;
		float armorDamage = amount * 0.9f;

		// Apply armor penetration (bypasses armor, goes straight to HP)
		float penDamage = armorDamage * armorPenetration;
		armorDamage -= penDamage;
		hpDamage += penDamage;

		// Apply HP damage (always takes 10% + any armor pen damage)
		Health -= hpDamage;

		// Apply armor damage (takes 90% if armor available)
		if ( Armor > 0f )
		{
			float armorAbsorbed = Math.Min( Armor, armorDamage );
			Armor -= armorAbsorbed;

			// Overflow damage to HP if armor breaks
			float overflow = armorDamage - armorAbsorbed;
			if ( overflow > 0f )
			{
				Health -= overflow;
			}
		}
		else
		{
			// If no armor at all, the 90% also goes to HP
			Health -= armorDamage;
		}

		// Clamp health to 0
		Health = Math.Max( 0f, Health );

		// Trigger health changed event if health changed
		if ( Health != oldHealth )
		{
			OnHealthChanged?.Invoke();
		}

		Log.Info( $"💥 Damage: {amount} (Pen: {armorPenetration*100:F0}%) | HP: {oldHealth:F1} → {Health:F1} | Armor: {oldArmor:F1} → {Armor:F1}" );
	}

	public void Heal( float amount )
	{
		if ( Network.IsProxy || amount <= 0f ) return;

		var oldHealth = Health;
		Health = Math.Min( MaxHealth, Health + amount );

		if ( Health != oldHealth )
		{
			OnHealthChanged?.Invoke();
		}
	}

	/// <summary>
	/// Set health directly (for respawn, etc.)
	/// </summary>
	public void SetHealth( float value )
	{
		if ( Network.IsProxy ) return;
		var oldHealth = Health;
		Health = value.Clamp( 0f, MaxHealth );

		if ( Health != oldHealth )
		{
			OnHealthChanged?.Invoke();
		}
	}

	/// <summary>
	/// Set armor directly (for respawn, etc.)
	/// </summary>
	public void SetArmor( float value )
	{
		if ( Network.IsProxy ) return;
		Armor = value.Clamp( 0f, MaxArmor );
	}

	/// <summary>
	/// Called by the Hitpoints skill when that level changes.
	/// </summary>
	public void SetMaxHealthFromHitpoints( int hpLevel, bool refillHealth )
	{
		if ( Network.IsProxy )
			return;

		if ( hpLevel <= 0 )
			hpLevel = 1;

		float oldMax = MaxHealth;
		float oldHealth = Health;

		// Our design: 1 HP per level, starting at 10 (Hp lvl 10 -> 10 HP)
		MaxHealth = hpLevel;
		InitHealth = hpLevel;

		if ( refillHealth || oldMax <= 0f )
		{
			// e.g. on first setup or after big respec – go to full HP
			Health = MaxHealth;
		}
		else
		{
			// Keep the same percentage of HP when max changes
			float pct = oldMax > 0f ? oldHealth / oldMax : 1f;
			Health = (MaxHealth * pct).Clamp( 0f, MaxHealth );
		}
	}

	// ---- Inspector Buttons for Testing ----

	[Property, Group( "Testing" ), Title( "Damage Amount" )]
	public float TestDamageAmount { get; set; } = 10f;

	[Property, Group( "Testing" ), Range( 0f, 1f ), Title( "Armor Penetration %" )]
	public float TestArmorPen { get; set; } = 0f;

	[Button, Group( "Testing" )]
	public void DealDamage()
	{
		Damage( TestDamageAmount, TestArmorPen );
	}

	// Dynamic button title that shows current values
	public string DealDamage_Title => $"Deal {TestDamageAmount:F1} Damage ({TestArmorPen*100:F0}% Pen)";

	[Button( "Heal to Full" ), Group( "Testing" )]
	public void TestHealFull()
	{
		Health = MaxHealth;
	}
	
	[Button( "Add $100" ), Group( "Testing" )]
	public void TestAddMoney100()
	{
		try
		{
			Log.Info( $"[StatsTest] ========== Add $100 Button Pressed ==========" );
			Log.Info( $"[StatsTest] Before: Money = ${Money}, _moneyLoadedFromSave = {_moneyLoadedFromSave}" );
			var before = Money;
			AddMoney( 100 );
			Log.Info( $"[StatsTest] After AddMoney: ${before} -> ${Money}" );
			Log.Info( $"[StatsTest] Calling SaveLocalNow()..." );
			PlayerDataPersistence.SaveLocalNow();
			Log.Info( $"[StatsTest] ========== Done ==========" );
		}
		catch ( Exception ex )
		{
			Log.Error( $"[StatsTest] EXCEPTION: {ex.Message}" );
			Log.Error( $"[StatsTest] Stack: {ex.StackTrace}" );
		}
	}

	[Button( "Add $1000" ), Group( "Testing" )]
	public void TestAddMoney1000()
	{
		try
		{
			Log.Info( $"[StatsTest] ========== Add $1000 Button Pressed ==========" );
			Log.Info( $"[StatsTest] Before: Money = ${Money}, _moneyLoadedFromSave = {_moneyLoadedFromSave}" );
			var before = Money;
			AddMoney( 1000 );
			Log.Info( $"[StatsTest] After AddMoney: ${before} -> ${Money}" );
			Log.Info( $"[StatsTest] Calling SaveLocalNow()..." );
			PlayerDataPersistence.SaveLocalNow();
			Log.Info( $"[StatsTest] ========== Done ==========" );
		}
		catch ( Exception ex )
		{
			Log.Error( $"[StatsTest] EXCEPTION: {ex.Message}" );
			Log.Error( $"[StatsTest] Stack: {ex.StackTrace}" );
		}
	}

	[Button( "Set Full Armor" ), Group( "Testing" )]
	public void TestSetFullArmor()
	{
		Armor = MaxArmor;
	}

	[Button( "Remove All Armor" ), Group( "Testing" )]
	public void TestRemoveArmor()
	{
		Armor = 0f;
	}

	[Button( "Kill Player" ), Group( "Testing" )]
	public void TestKill()
	{
		Log.Info( "[StatsTest] Kill Player test button pressed." );
		SetHealth( 0f );
		SetArmor( 0f );
		PlayerDataPersistence.SaveLocalNow();
	}

	// ---- Public Methods ----

	/// <summary>
	/// Set money to a specific value
	/// </summary>
	public void SetMoney( int value )
	{
		if ( Network.IsProxy ) return;
		value = Math.Max( 0, value );
		int current = Money;
		if ( value == current )
			return;

		int delta = value - current;
		if ( delta > 0 )
		{
			VeggaCurrency.TryAddCash( GameObject, delta );
		}
		else
		{
			VeggaCurrency.TryRemoveCash( GameObject, -delta );
		}

		// Mark data as changed for auto-save
		MarkDataChanged();
	}

	public void AddMoney( int amount )
	{
		if ( Network.IsProxy ) return;
		if ( amount == 0 ) return;
		if ( amount > 0 )
		{
			VeggaCurrency.TryAddCash( GameObject, amount );
		}
		else
		{
			VeggaCurrency.TryRemoveCash( GameObject, -amount );
		}

		// Mark data as changed for auto-save
		MarkDataChanged();
	}

	/// <summary>
	/// Mark this player's data as changed (triggers auto-save)
	/// </summary>
	private void MarkDataChanged()
	{
		if ( Network.IsProxy ) return;

		var connection = Network.Owner;
		if ( connection != null )
		{
			PlayerDataPersistence.MarkPlayerDataChanged( connection.SteamId.ToString() );
		}
	}

	public bool TrySpendMoney( int amount )
	{
		if ( Network.IsProxy ) return false;
		if ( amount < 0 || Money < amount ) return false;
		var ok = VeggaCurrency.TryRemoveCash( GameObject, amount );
		if ( ok )
		{
			MarkDataChanged();
		}
		return ok;
	}

	/// <summary>
	/// Called by PlayerForgeAura when this player enters its radius.
	/// Host-only; clients receive the resulting synced properties.
	/// </summary>
	public void RegisterForgeAura( PlayerForgeAura aura )
	{
		if ( Network.IsProxy || aura == null || !aura.IsValid() )
			return;

		_activeForges.Add( aura );
		RecalculateForgeBuff();
	}

	/// <summary>
	/// Called by PlayerForgeAura when this player leaves its radius
	/// or when the aura is disabled/destroyed.
	/// </summary>
	public void UnregisterForgeAura( PlayerForgeAura aura )
	{
		if ( Network.IsProxy || aura == null )
			return;

		_activeForges.Remove( aura );
		RecalculateForgeBuff();
	}

	/// <summary>
	/// Recalculate HasForgeBuff and SmeltSpeedMultiplier based on
	/// active auras and Smithing level. Hand crafting uses 1x; standing
	/// in a forge uses the strongest aura times a smooth Smithing bonus.
	/// </summary>
	public void RecalculateForgeBuff()
	{
		if ( Network.IsProxy ) return;

		// Determine highest base forge multiplier from all active auras
		float baseForgeMultiplier = 1f;
		foreach ( var aura in _activeForges )
		{
			if ( aura != null && aura.IsValid() )
			{
				baseForgeMultiplier = Math.Max( baseForgeMultiplier, aura.BaseForgeMultiplier );
			}
		}

		HasForgeBuff = baseForgeMultiplier > 1f;

		// Hand crafting speed is always 1x when not buffed.
		if ( !HasForgeBuff )
		{
			SmeltSpeedMultiplier = 1f;
			return;
		}

		// Skill scaling: use Smithing as the forge/smelting skill.
		// Level 1 => 100% of base aura, level 99 => ~2.5x of base, etc.
		int smithingLevel = 1;
		var skills = GameObject.Components.Get<PlayerVeggaSkills>();
		if ( skills != null )
		{
			smithingLevel = Math.Max( 1, skills.GetLevel( SkillId.Smithing ) );
		}

		// Simple smooth curve: +1.5x over 98 levels (~1.5% per level).
		float skillFactor = 1f + (smithingLevel - 1) * 0.015f;
		SmeltSpeedMultiplier = baseForgeMultiplier * skillFactor;
	}

	/// <summary>
	/// Helper for smelting code. When useForgeSpeed is false, you get
	/// plain hand speed (1x). When true, you get the current forge
	/// multiplier (including skill scaling) if any, otherwise 1x.
	/// </summary>
	public float GetSmeltSpeedMultiplier( bool useForgeSpeed )
	{
		if ( !useForgeSpeed )
			return 1f;

		return SmeltSpeedMultiplier <= 0f ? 1f : SmeltSpeedMultiplier;
	}
}
