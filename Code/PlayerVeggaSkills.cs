using System;
using System.Collections.Generic;
using Sandbox;

namespace Sandbox;

public sealed class PlayerVeggaSkills : Component
{
	public const int SkillCount = 23;

	// Networked skill data (fixed size). Host/owner writes; proxies receive.
	[Sync] private NetList<int> _xp { get; set; } = new();
	[Sync] private NetList<int> _level { get; set; } = new();

	// Combat is derived from levels; compute locally from synced data.
	[Property, Group( "Combat Info" ), ReadOnly]
	public int CombatLevel { get; private set; }

	[Property, Group( "Combat Info" ), ReadOnly]
	public string CombatType { get; private set; } = "Warrior";

	[Property, Group( "Combat Info" ), ReadOnly]
	public float CombatLevelPrecise { get; private set; }

	[Property, Group( "Combat Info" ), ReadOnly]
	public string NextCombatHint { get; private set; } = "";

	/// <summary>
	/// Link to the PlayerVeggaStats component so we can push Hitpoints -> MaxHealth.
	/// Assign this in the inspector on player_vegga, or it will auto-find on the same GameObject.
	/// </summary>
	[Property, Group( "Config" )] public PlayerVeggaStats Stats { get; set; }

	// -------- Inspector Display Properties (Read-Only) --------

	// Combat Skills
	[Property, Group( "Combat Skills" ), ReadOnly] public int AttackLevel => GetLevel( SkillId.Attack );
	[Property, Group( "Combat Skills" ), ReadOnly] public int AttackXP => GetXp( SkillId.Attack );

	[Property, Group( "Combat Skills" ), ReadOnly] public int StrengthLevel => GetLevel( SkillId.Strength );
	[Property, Group( "Combat Skills" ), ReadOnly] public int StrengthXP => GetXp( SkillId.Strength );

	[Property, Group( "Combat Skills" ), ReadOnly] public int DefenceLevel => GetLevel( SkillId.Defence );
	[Property, Group( "Combat Skills" ), ReadOnly] public int DefenceXP => GetXp( SkillId.Defence );

	[Property, Group( "Combat Skills" ), ReadOnly] public int RangedLevel => GetLevel( SkillId.Ranged );
	[Property, Group( "Combat Skills" ), ReadOnly] public int RangedXP => GetXp( SkillId.Ranged );

	[Property, Group( "Combat Skills" ), ReadOnly] public int PrayerLevel => GetLevel( SkillId.Prayer );
	[Property, Group( "Combat Skills" ), ReadOnly] public int PrayerXP => GetXp( SkillId.Prayer );

	[Property, Group( "Combat Skills" ), ReadOnly] public int MagicLevel => GetLevel( SkillId.Magic );
	[Property, Group( "Combat Skills" ), ReadOnly] public int MagicXP => GetXp( SkillId.Magic );

	[Property, Group( "Combat Skills" ), ReadOnly] public int HitpointsLevel => GetLevel( SkillId.Hitpoints );
	[Property, Group( "Combat Skills" ), ReadOnly] public int HitpointsXP => GetXp( SkillId.Hitpoints );

	// Gathering Skills
	[Property, Group( "Gathering Skills" ), ReadOnly] public int FarmingLevel => GetLevel( SkillId.Farming );
	[Property, Group( "Gathering Skills" ), ReadOnly] public int FarmingXP => GetXp( SkillId.Farming );

	[Property, Group( "Gathering Skills" ), ReadOnly] public int FishingLevel => GetLevel( SkillId.Fishing );
	[Property, Group( "Gathering Skills" ), ReadOnly] public int FishingXP => GetXp( SkillId.Fishing );

	[Property, Group( "Gathering Skills" ), ReadOnly] public int HunterLevel => GetLevel( SkillId.Hunter );
	[Property, Group( "Gathering Skills" ), ReadOnly] public int HunterXP => GetXp( SkillId.Hunter );

	[Property, Group( "Gathering Skills" ), ReadOnly] public int MiningLevel => GetLevel( SkillId.Mining );
	[Property, Group( "Gathering Skills" ), ReadOnly] public int MiningXP => GetXp( SkillId.Mining );

	[Property, Group( "Gathering Skills" ), ReadOnly] public int WoodcuttingLevel => GetLevel( SkillId.Woodcutting );
	[Property, Group( "Gathering Skills" ), ReadOnly] public int WoodcuttingXP => GetXp( SkillId.Woodcutting );

	// Production Skills
	[Property, Group( "Production Skills" ), ReadOnly] public int CookingLevel => GetLevel( SkillId.Cooking );
	[Property, Group( "Production Skills" ), ReadOnly] public int CookingXP => GetXp( SkillId.Cooking );

	[Property, Group( "Production Skills" ), ReadOnly] public int CraftingLevel => GetLevel( SkillId.Crafting );
	[Property, Group( "Production Skills" ), ReadOnly] public int CraftingXP => GetXp( SkillId.Crafting );

	[Property, Group( "Production Skills" ), ReadOnly] public int FletchingLevel => GetLevel( SkillId.Fletching );
	[Property, Group( "Production Skills" ), ReadOnly] public int FletchingXP => GetXp( SkillId.Fletching );

	[Property, Group( "Production Skills" ), ReadOnly] public int HerbloreLevel => GetLevel( SkillId.Herblore );
	[Property, Group( "Production Skills" ), ReadOnly] public int HerbloreXP => GetXp( SkillId.Herblore );

	[Property, Group( "Production Skills" ), ReadOnly] public int RunecraftLevel => GetLevel( SkillId.Runecraft );
	[Property, Group( "Production Skills" ), ReadOnly] public int RunecraftXP => GetXp( SkillId.Runecraft );

	[Property, Group( "Production Skills" ), ReadOnly] public int SmithingLevel => GetLevel( SkillId.Smithing );
	[Property, Group( "Production Skills" ), ReadOnly] public int SmithingXP => GetXp( SkillId.Smithing );

	[Property, Group( "Production Skills" ), ReadOnly] public int ConstructionLevel => GetLevel( SkillId.Construction );
	[Property, Group( "Production Skills" ), ReadOnly] public int ConstructionXP => GetXp( SkillId.Construction );

	[Property, Group( "Production Skills" ), ReadOnly] public int FiremakingLevel => GetLevel( SkillId.Firemaking );
	[Property, Group( "Production Skills" ), ReadOnly] public int FiremakingXP => GetXp( SkillId.Firemaking );

	// Utility Skills
	[Property, Group( "Utility Skills" ), ReadOnly] public int AgilityLevel => GetLevel( SkillId.Agility );
	[Property, Group( "Utility Skills" ), ReadOnly] public int AgilityXP => GetXp( SkillId.Agility );

	[Property, Group( "Utility Skills" ), ReadOnly] public int SlayerLevel => GetLevel( SkillId.Slayer );
	[Property, Group( "Utility Skills" ), ReadOnly] public int SlayerXP => GetXp( SkillId.Slayer );

	[Property, Group( "Utility Skills" ), ReadOnly] public int ThievingLevel => GetLevel( SkillId.Thieving );
	[Property, Group( "Utility Skills" ), ReadOnly] public int ThievingXP => GetXp( SkillId.Thieving );

	public event Action<SkillId, int>? OnSkillLevelUp;
	public event Action<SkillId, int>? OnXPGained;

	int _lastLevelHash;

	protected override void OnStart()
	{
		// Auto-find stats if not wired manually
		Stats ??= GameObject.Components.Get<PlayerVeggaStats>();

		if ( Stats == null )
		{
			Log.Warning( "PlayerVeggaSkills: No PlayerVeggaStats found on same GameObject." );
		}

		// Host/owner initializes the networked lists.
		if ( !Network.IsProxy )
		{
			EnsureSkillListsSizedAndDefaults();
			ApplyHitpointsToStats();
		}

		RecalculateCombatLevel();
	}

	protected override void OnUpdate()
	{
		// On clients/proxies, levels replicate over time. Detect changes and recompute derived combat.
		if ( Network.IsProxy )
		{
			int h = 17;
			int count = _level.Count;
			for ( int i = 0; i < count; i++ )
				h = (h * 31) + _level[i];

			if ( h != _lastLevelHash )
			{
				_lastLevelHash = h;
				RecalculateCombatLevel();
			}
		}
	}

	void EnsureSkillListsSizedAndDefaults()
	{
		if ( Network.IsProxy )
			return;

		bool wasEmpty = _level.Count == 0 && _xp.Count == 0;

		// Grow/shrink to fixed size
		while ( _level.Count < SkillCount ) _level.Add( 1 );
		while ( _xp.Count < SkillCount ) _xp.Add( 0 );
		while ( _level.Count > SkillCount ) _level.RemoveAt( _level.Count - 1 );
		while ( _xp.Count > SkillCount ) _xp.RemoveAt( _xp.Count - 1 );

		// If this is a fresh init, enforce OSRS-style defaults.
		if ( wasEmpty )
		{
			for ( int i = 0; i < SkillCount; i++ )
			{
				_level[i] = 1;
				_xp[i] = 0;
			}

			_level[(int)SkillId.Hitpoints] = 10;
			_xp[(int)SkillId.Hitpoints] = SkillXpTable.GetXpForLevel( 10 );
		}
		else
		{
			// Defensive: clamp any zeros to minimums.
			for ( int i = 0; i < SkillCount; i++ )
			{
				if ( _level[i] <= 0 ) _level[i] = 1;
				if ( _xp[i] < 0 ) _xp[i] = 0;
			}
		}
	}

	// -------- Public accessors --------

	static int DefaultLevelFor( SkillId skill )
	{
		return skill == SkillId.Hitpoints ? 10 : 1;
	}

	public int GetLevel( SkillId skill )
	{
		int idx = (int)skill;
		if ( idx < 0 || idx >= _level.Count )
			return DefaultLevelFor( skill );
		return _level[idx];
	}

	public int GetXp( SkillId skill )
	{
		int idx = (int)skill;
		if ( idx < 0 || idx >= _xp.Count )
			return SkillXpTable.GetXpForLevel( DefaultLevelFor( skill ) );
		return _xp[idx];
	}

	// -------- Core logic: set level / add XP --------

	public void SetSkillLevel( SkillId skill, int level )
	{
		if ( Network.IsProxy ) return;
		EnsureSkillListsSizedAndDefaults();

		level = level.Clamp( 1, SkillXpTable.MaxLevel );

		int idx = (int)skill;
		_level[idx] = level;
		_xp[idx] = SkillXpTable.GetXpForLevel( level );
		MarkPlayerDataDirty();

		// If Hitpoints changes, update HP/MaxHealth on stats
		if ( skill == SkillId.Hitpoints )
			ApplyHitpointsToStats();

		if ( IsCombatSkill( skill ) )
			RecalculateCombatLevel();
	}

	public void AddXp( SkillId skill, int amount )
	{
		if ( Network.IsProxy ) return;
		EnsureSkillListsSizedAndDefaults();

		if ( amount <= 0 ) return;

		int idx = (int)skill;
		int oldXp = _xp[idx];
		int maxXp = SkillXpTable.GetXpForLevel( SkillXpTable.MaxLevel );

		int newXp = (oldXp + amount).Clamp( 0, maxXp );
		_xp[idx] = newXp;
		MarkPlayerDataDirty();

		// Fire XP gained event
		OnXPGained?.Invoke( skill, amount );

		int oldLevel = _level[idx];
		int newLevel = SkillXpTable.GetLevelForXp( newXp );

		if ( newLevel > oldLevel )
		{
			_level[idx] = newLevel;
			OnSkillLevelUp?.Invoke( skill, newLevel );
			Log.Info( $"{GameObject.Name} leveled {skill} {oldLevel} -> {newLevel}" );

			// Level up in Hitpoints -> update HP/MaxHealth
			if ( skill == SkillId.Hitpoints )
				ApplyHitpointsToStats();
		}

		if ( IsCombatSkill( skill ) )
			RecalculateCombatLevel();
	}

	void MarkPlayerDataDirty()
	{
		if ( !Networking.IsHost )
			return;

		var owner = Network?.Owner;
		var steamId = owner?.SteamId.ToString();
		if ( string.IsNullOrWhiteSpace( steamId ) )
		{
			steamId = Connection.Local?.SteamId.ToString();
		}

		if ( !string.IsNullOrWhiteSpace( steamId ) )
			PlayerDataPersistence.MarkPlayerDataChanged( steamId );
	}

	// -------- Persistence helpers --------

	public void ExportSaveData( out List<int> levels, out List<int> xps )
	{
		levels = new List<int>( SkillCount );
		xps = new List<int>( SkillCount );

		// If we're a proxy, we still export what we have (best-effort), but the server/owner
		// should be the one actually saving.
		if ( !Network.IsProxy )
			EnsureSkillListsSizedAndDefaults();

		for ( int i = 0; i < SkillCount; i++ )
		{
			int lvl = (i >= 0 && i < _level.Count) ? _level[i] : (i == (int)SkillId.Hitpoints ? 10 : 1);
			int xp = (i >= 0 && i < _xp.Count) ? _xp[i] : SkillXpTable.GetXpForLevel( lvl );
			levels.Add( lvl );
			xps.Add( xp );
		}
	}

	public void LoadSaveData( IReadOnlyList<int> levels, IReadOnlyList<int> xps )
	{
		if ( Network.IsProxy )
			return;

		EnsureSkillListsSizedAndDefaults();

		bool hasLevels = levels != null && levels.Count == SkillCount;
		bool hasXps = xps != null && xps.Count == SkillCount;
		if ( !hasLevels && !hasXps )
			return;

		for ( int i = 0; i < SkillCount; i++ )
		{
			int targetXp;
			if ( hasXps )			// prefer XP as source of truth
			{
				targetXp = Math.Max( 0, xps[i] );
			}
			else
			{
				int lvl = levels[i].Clamp( 1, SkillXpTable.MaxLevel );
				targetXp = SkillXpTable.GetXpForLevel( lvl );
			}

			targetXp = targetXp.Clamp( 0, SkillXpTable.GetXpForLevel( SkillXpTable.MaxLevel ) );
			_xp[i] = targetXp;
			_level[i] = SkillXpTable.GetLevelForXp( targetXp ).Clamp( 1, SkillXpTable.MaxLevel );
		}

		ApplyHitpointsToStats();
		RecalculateCombatLevel();
	}


	/// <summary>
	/// Apply the Hitpoints level to PlayerVeggaStats.MaxHealth/Health.
	/// Uses the helper you added in PlayerVeggaStats.
	/// </summary>
	private void ApplyHitpointsToStats()
	{
		if ( Stats is null )
			return;

		int hpLevel = GetLevel( SkillId.Hitpoints );

		// refillHealth = false to keep same HP % when max changes
		Stats.SetMaxHealthFromHitpoints( hpLevel, refillHealth: false );
	}

	// -------- Combat level calculation --------

	private static bool IsCombatSkill( SkillId skill )
	{
		return skill == SkillId.Attack
			|| skill == SkillId.Strength
			|| skill == SkillId.Defence
			|| skill == SkillId.Hitpoints
			|| skill == SkillId.Prayer
			|| skill == SkillId.Ranged
			|| skill == SkillId.Magic;
	}

	private void RecalculateCombatLevel()
	{
		int att = GetLevel( SkillId.Attack );
		int str = GetLevel( SkillId.Strength );
		int def = GetLevel( SkillId.Defence );
		int hp = GetLevel( SkillId.Hitpoints );
		int pray = GetLevel( SkillId.Prayer );
		int rng = GetLevel( SkillId.Ranged );
		int mag = GetLevel( SkillId.Magic );

		double basePart = 0.25 * (def + hp + Math.Floor( pray / 2.0 ));
		double melee = 0.325 * (att + str);
		double range = 0.325 * Math.Floor( rng * 1.5 );
		double mage = 0.325 * Math.Floor( mag * 1.5 );

		double style = Math.Max( melee, Math.Max( range, mage ) );
		double raw = basePart + style;

		CombatLevelPrecise = (float)raw;
		CombatLevel = (int)Math.Floor( raw );

		if ( style == melee ) CombatType = "Warrior";
		else if ( style == range ) CombatType = "Ranger";
		else if ( style == mage ) CombatType = "Mage";
		else CombatType = "Hybrid";

		// Compute simple suggestions for next combat level
		NextCombatHint = BuildNextCombatHint( att, str, def, hp, pray, rng, mag, raw );
	}

	private string BuildNextCombatHint( int att, int str, int def, int hp, int pray, int rng, int mag, double currentRaw )
	{
		int currentFloor = (int)Math.Floor( currentRaw );

		// Helper to compute raw combat for given stats
		double Calc( int a, int s, int d, int h, int p, int r, int m )
		{
			double b = 0.25 * (d + h + Math.Floor( p / 2.0 ));
			double melee = 0.325 * (a + s);
			double range = 0.325 * Math.Floor( r * 1.5 );
			double mage = 0.325 * Math.Floor( m * 1.5 );
			double style = Math.Max( melee, Math.Max( range, mage ) );
			return b + style;
		}

		(int delta, string label) best = (999, "");

		void TrySkill( string name, Func<int, double> simulate )
		{
			for ( int i = 1; i <= 15; i++ )
			{
				double raw = simulate( i );
				if ( Math.Floor( raw ) > currentFloor )
				{
					if ( i < best.delta )
						best = (i, $"{name}+{i}");
					break;
				}
			}
		}

		TrySkill( "Att",  i => Calc( att + i, str, def, hp, pray, rng, mag ) );
		TrySkill( "Str",  i => Calc( att, str + i, def, hp, pray, rng, mag ) );
		TrySkill( "Def",  i => Calc( att, str, def + i, hp, pray, rng, mag ) );
		TrySkill( "HP",   i => Calc( att, str, def, hp + i, pray, rng, mag ) );
		TrySkill( "Pray", i => Calc( att, str, def, hp, pray + i, rng, mag ) );
		TrySkill( "Rng",  i => Calc( att, str, def, hp, pray, rng + i, mag ) );
		TrySkill( "Mag",  i => Calc( att, str, def, hp, pray, rng, mag + i ) );

		if ( string.IsNullOrEmpty( best.label ) )
			return "Max combat or no simple upgrade found.";

		return $"Next CB in ~{best.label}";
	}

	// -------- Inspector Testing Buttons --------

	[Button( "Add 100 Attack XP" ), Group( "Testing" )]
	public void TestAddAttackXP() => AddXp( SkillId.Attack, 100 );

	[Button( "Add 100 Strength XP" ), Group( "Testing" )]
	public void TestAddStrengthXP() => AddXp( SkillId.Strength, 100 );

	[Button( "Add 100 Defence XP" ), Group( "Testing" )]
	public void TestAddDefenceXP() => AddXp( SkillId.Defence, 100 );

	[Button( "Add 100 Hitpoints XP" ), Group( "Testing" )]
	public void TestAddHitpointsXP() => AddXp( SkillId.Hitpoints, 100 );

	[Button( "Add 1000 Mining XP" ), Group( "Testing" )]
	public void TestAddMiningXP() => AddXp( SkillId.Mining, 1000 );

	[Button( "Add 1000 Woodcutting XP" ), Group( "Testing" )]
	public void TestAddWoodcuttingXP() => AddXp( SkillId.Woodcutting, 1000 );

	[Button( "Set Attack to 99" ), Group( "Testing" )]
	public void TestMaxAttack() => SetSkillLevel( SkillId.Attack, 99 );

	[Button( "Set All Combat to 99" ), Group( "Testing" )]
	public void TestMaxAllCombat()
	{
		SetSkillLevel( SkillId.Attack, 99 );
		SetSkillLevel( SkillId.Strength, 99 );
		SetSkillLevel( SkillId.Defence, 99 );
		SetSkillLevel( SkillId.Hitpoints, 99 );
		SetSkillLevel( SkillId.Ranged, 99 );
		SetSkillLevel( SkillId.Magic, 99 );
		SetSkillLevel( SkillId.Prayer, 99 );
	}

	[Button( "Reset All Skills to 1" ), Group( "Testing" )]
	public void TestResetAllSkills()
	{
		for ( int i = 0; i < SkillCount; i++ )
		{
			_level[i] = 1;
			_xp[i] = 0;
		}
		SetSkillLevel( SkillId.Hitpoints, 10 ); // OSRS starting HP
		ApplyHitpointsToStats();
		RecalculateCombatLevel();
	}
}
