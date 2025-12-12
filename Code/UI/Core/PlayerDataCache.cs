using System;
using System.Collections.Generic;
using Sandbox;

namespace Sandbox.UI;

/// <summary>
/// Centralized player data cache with event-driven updates.
/// Only updates UI when data actually changes (not every frame).
/// TypeScript equivalent: Reactive state management (like Redux/Zustand)
/// </summary>
public static class PlayerDataCache
{
	// ---- Events (TypeScript: EventEmitter) ----
	public static event Action<float> OnHealthChanged;
	public static event Action<float> OnArmorChanged;
	public static event Action<int> OnMoneyChanged;
	public static event Action<string> OnJobChanged;
	public static event Action<float> OnPrayerChanged;
	public static event Action<float> OnStaminaChanged;
	public static event Action<float> OnSpecialAttackChanged;
	
	// Skill events
	public static event Action<string, int, float> OnSkillChanged; // (skillName, level, xp)
	
	// Session stats events
	public static event Action<int> OnKillsChanged;
	public static event Action<int> OnDeathsChanged;
	public static event Action<int> OnQuestPointsChanged;

	// ---- Cached Values (TypeScript: private state) ----
	private static float _cachedHealth = 0f;
	private static float _cachedArmor = 0f;
	private static int _cachedMoney = 0;
	private static string _cachedJob = "";
	private static float _cachedPrayer = 0f;
	private static float _cachedStamina = 0f;
	private static float _cachedSpecialAttack = 0f;
	
	private static Dictionary<string, (int level, float xp)> _cachedSkills = new();
	private static int _cachedKills = 0;
	private static int _cachedDeaths = 0;
	private static int _cachedQuestPoints = 0;

	// ---- Public Getters (TypeScript: readonly properties) ----
	public static float Health => _cachedHealth;
	public static float Armor => _cachedArmor;
	public static int Money => _cachedMoney;
	public static string Job => _cachedJob;
	public static float Prayer => _cachedPrayer;
	public static float Stamina => _cachedStamina;
	public static float SpecialAttack => _cachedSpecialAttack;
	
	public static int Kills => _cachedKills;
	public static int Deaths => _cachedDeaths;
	public static int QuestPoints => _cachedQuestPoints;

	/// <summary>
	/// Update player stats (only triggers events if values changed)
	/// TypeScript: updateState(newState) { if (changed) emit('change') }
	/// </summary>
	public static void UpdatePlayerStats( PlayerVeggaStats stats )
	{
		if ( stats == null || !stats.IsValid() ) return;

		// TypeScript: if (newHealth !== cachedHealth) { ... }
		if ( Math.Abs( stats.Health - _cachedHealth ) > 0.01f )
		{
			_cachedHealth = stats.Health;
			OnHealthChanged?.Invoke( _cachedHealth );
		}

		if ( Math.Abs( stats.Armor - _cachedArmor ) > 0.01f )
		{
			_cachedArmor = stats.Armor;
			OnArmorChanged?.Invoke( _cachedArmor );
		}

		if ( stats.Money != _cachedMoney )
		{
			_cachedMoney = stats.Money;
			OnMoneyChanged?.Invoke( _cachedMoney );
		}

		if ( stats.JobName != _cachedJob )
		{
			_cachedJob = stats.JobName;
			OnJobChanged?.Invoke( _cachedJob );
		}

		if ( Math.Abs( stats.Prayer - _cachedPrayer ) > 0.01f )
		{
			_cachedPrayer = stats.Prayer;
			OnPrayerChanged?.Invoke( _cachedPrayer );
		}

		if ( Math.Abs( stats.Stamina - _cachedStamina ) > 0.01f )
		{
			_cachedStamina = stats.Stamina;
			OnStaminaChanged?.Invoke( _cachedStamina );
		}

		if ( Math.Abs( stats.SpecialAttack - _cachedSpecialAttack ) > 0.01f )
		{
			_cachedSpecialAttack = stats.SpecialAttack;
			OnSpecialAttackChanged?.Invoke( _cachedSpecialAttack );
		}
	}

	/// <summary>
	/// Update skill data (only triggers if changed)
	/// </summary>
	public static void UpdateSkill( string skillName, int level, float xp )
	{
		// TypeScript: const cached = skills[skillName] ?? { level: 0, xp: 0 };
		if ( !_cachedSkills.TryGetValue( skillName, out var cached ) )
		{
			cached = (0, 0f);
		}

		// TypeScript: if (level !== cached.level || xp !== cached.xp) { ... }
		if ( cached.level != level || Math.Abs( cached.xp - xp ) > 0.01f )
		{
			_cachedSkills[skillName] = (level, xp);
			OnSkillChanged?.Invoke( skillName, level, xp );
		}
	}

	/// <summary>
	/// Update session stats (only triggers if changed)
	/// </summary>
	public static void UpdateSessionStats( PlayerSessionStats stats )
	{
		if ( stats == null || !stats.IsValid() ) return;

		if ( stats.SessionKills != _cachedKills )
		{
			_cachedKills = stats.SessionKills;
			OnKillsChanged?.Invoke( _cachedKills );
		}

		if ( stats.SessionDeaths != _cachedDeaths )
		{
			_cachedDeaths = stats.SessionDeaths;
			OnDeathsChanged?.Invoke( _cachedDeaths );
		}

		if ( stats.TotalQuestPoints != _cachedQuestPoints )
		{
			_cachedQuestPoints = stats.TotalQuestPoints;
			OnQuestPointsChanged?.Invoke( _cachedQuestPoints );
		}
	}

	/// <summary>
	/// Clear cache (on disconnect, etc.)
	/// </summary>
	public static void Clear()
	{
		_cachedHealth = 0f;
		_cachedArmor = 0f;
		_cachedMoney = 0;
		_cachedJob = "";
		_cachedPrayer = 0f;
		_cachedStamina = 0f;
		_cachedSpecialAttack = 0f;
		_cachedSkills.Clear();
		_cachedKills = 0;
		_cachedDeaths = 0;
		_cachedQuestPoints = 0;
	}
}

