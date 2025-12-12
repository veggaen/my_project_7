using System;
using Sandbox;
using Sandbox.Data;

namespace Sandbox;

/// <summary>
/// Debug console commands for testing the OSRS-style skill system and money persistence.
/// 
/// These commands are meant for local testing only – you can remove them or
/// guard them behind a dev flag later if you want.
/// 
/// Usage examples (in console):
///   vegga_set_skill_level Attack 30
///   vegga_add_skill_xp Mining 5000
///   vegga_print_combat
///   vegga_print_skill Fishing
///   vegga_money_info
///   vegga_add_money 100
///   vegga_save_now
///   vegga_check_save_file
/// </summary>
public static class SkillDebugCommands
{
	/// <summary>
	/// Helper: get the local player's PlayerVeggaSkills component.
	/// Returns null and logs a warning if anything is missing.
	/// </summary>
	private static PlayerVeggaSkills? GetLocalSkills()
	{
		var stats = PlayerVeggaStats.Local;
		if ( stats is null )
		{
			Log.Warning( "SkillDebugCommands: PlayerVeggaStats.Local is null � is a local player spawned?" );
			return null;
		}

		var skills = stats.Components.Get<PlayerVeggaSkills>();
		if ( skills is null )
		{
			Log.Warning( "SkillDebugCommands: No PlayerVeggaSkills component found on local player GameObject." );
			return null;
		}

		return skills;
	}

	/// <summary>
	/// Set a specific skill to an exact level.
	/// Example: vegga_set_skill_level Attack 30
	/// </summary>
	// If your SDK supports it you can uncomment the Help argument:
	// [ConCmd( "vegga_set_skill_level", Help = "Set a skill level. Usage: vegga_set_skill_level Attack 30" )]
	[ConCmd( "vegga_set_skill_level" )]
	public static void SetSkillLevelCmd( string skillName, int level )
	{
		var skills = GetLocalSkills();
		if ( skills is null )
			return;

		if ( !Enum.TryParse<SkillId>( skillName, true, out var skill ) )
		{
			Log.Warning( $"Unknown skill '{skillName}'. Valid skills: {string.Join( ", ", Enum.GetNames<SkillId>() )}" );
			return;
		}

		level = level.Clamp( 1, SkillXpTable.MaxLevel );
		skills.SetSkillLevel( skill, level );
		Log.Info( $"[Skills] Set {skill} level to {level}." );
	}

	/// <summary>
	/// Add a raw amount of XP to a skill (uses OSRS XP table to auto-level).
	/// Example: vegga_add_skill_xp Mining 5000
	/// </summary>
	// [ConCmd( "vegga_add_skill_xp", Help = "Add XP to a skill. Usage: vegga_add_skill_xp Mining 5000" )]
	[ConCmd( "vegga_add_skill_xp" )]
	public static void AddSkillXpCmd( string skillName, int amount )
	{
		var skills = GetLocalSkills();
		if ( skills is null )
			return;

		if ( !Enum.TryParse<SkillId>( skillName, true, out var skill ) )
		{
			Log.Warning( $"Unknown skill '{skillName}'. Valid skills: {string.Join( ", ", Enum.GetNames<SkillId>() )}" );
			return;
		}

		if ( amount <= 0 )
		{
			Log.Warning( "XP amount must be positive." );
			return;
		}

		skills.AddXp( skill, amount );
		Log.Info( $"[Skills] Added {amount} XP to {skill}. New level = {skills.GetLevel( skill )}, total XP = {skills.GetXp( skill )}." );
	}

	/// <summary>
	/// Print combat level, combat type and main combat skills.
	/// Example: vegga_print_combat
	/// </summary>
	// [ConCmd( "vegga_print_combat", Help = "Print combat level, type and main combat skills." )]
	[ConCmd( "vegga_print_combat" )]
	public static void PrintCombatCmd()
	{
		var skills = GetLocalSkills();
		if ( skills is null )
			return;

		Log.Info( $"[Combat] Combat Level: {skills.CombatLevel}   Type: {skills.CombatType}" );

		Log.Info(
			$"[Combat] " +
			$"Atk {skills.GetLevel( SkillId.Attack )}, " +
			$"Str {skills.GetLevel( SkillId.Strength )}, " +
			$"Def {skills.GetLevel( SkillId.Defence )}, " +
			$"HP {skills.GetLevel( SkillId.Hitpoints )}, " +
			$"Pray {skills.GetLevel( SkillId.Prayer )}, " +
			$"Rng {skills.GetLevel( SkillId.Ranged )}, " +
			$"Mag {skills.GetLevel( SkillId.Magic )}"
		);
	}

	/// <summary>
	/// Print the level and XP of a single skill.
	/// Example: vegga_print_skill Fishing
	/// </summary>
	// [ConCmd( "vegga_print_skill", Help = "Print level + XP for a skill. Usage: vegga_print_skill Fishing" )]
	[ConCmd( "vegga_print_skill" )]
	public static void PrintSkillCmd( string skillName )
	{
		var skills = GetLocalSkills();
		if ( skills is null )
			return;

		if ( !Enum.TryParse<SkillId>( skillName, true, out var skill ) )
		{
			Log.Warning( $"Unknown skill '{skillName}'. Valid skills: {string.Join( ", ", Enum.GetNames<SkillId>() )}" );
			return;
		}

		int level = skills.GetLevel( skill );
		int xp = skills.GetXp( skill );
		int nextLevel = (level < SkillXpTable.MaxLevel) ? level + 1 : level;
		int xpNext = SkillXpTable.GetXpForLevel( nextLevel );
		int toNext = (nextLevel > level) ? xpNext - xp : 0;

		Log.Info( $"[Skills] {skill}: Level {level}, XP {xp}, XP to next level {toNext}" );
	}

	// ==================== MONEY DEBUG COMMANDS ====================

	/// <summary>
	/// Print current money status and save file info.
	/// Example: vegga_money_info
	/// </summary>
	[ConCmd( "vegga_money_info" )]
	public static void MoneyInfoCmd()
	{
		var stats = PlayerVeggaStats.Local;
		if ( stats is null )
		{
			Log.Warning( "[Money] PlayerVeggaStats.Local is null – is a local player spawned?" );
			return;
		}

		Log.Info( $"[Money] ========== Money Debug Info ==========" );
		Log.Info( $"[Money] Current Money: ${stats.Money}" );
		Log.Info( $"[Money] StartMoney (config): ${stats.StartMoney}" );
		Log.Info( $"[Money] _moneyLoadedFromSave: {stats._moneyLoadedFromSave}" );
		Log.Info( $"[Money] _initialized: {stats._initialized}" );

		// Check save file
		var conn = Connection.Local;
		if ( conn != null )
		{
			var steamId = conn.SteamId.ToString();
			var filePath = $"PlayerData/{steamId}.json";
			var fullPath = FileSystem.Data.GetFullPath( filePath );
			
			Log.Info( $"[Money] SteamID: {steamId}" );
			Log.Info( $"[Money] Save file path: {fullPath}" );
			Log.Info( $"[Money] File exists: {FileSystem.Data.FileExists( filePath )}" );

			if ( FileSystem.Data.FileExists( filePath ) )
			{
				try
				{
					var data = FileSystem.Data.ReadJson<PlayerDataManager.PlayerData>( filePath );
					if ( data != null )
					{
						Log.Info( $"[Money] Saved money in file: ${data.Money}" );
						Log.Info( $"[Money] Saved name: {data.PreferredUsername}" );
						Log.Info( $"[Money] Last seen: {data.LastSeen}" );
					}
				}
				catch ( Exception ex )
				{
					Log.Error( $"[Money] Error reading save file: {ex.Message}" );
				}
			}
		}
		else
		{
			Log.Warning( "[Money] No local connection found (editor mode?)" );
		}

		Log.Info( $"[Money] ========================================" );
	}

	/// <summary>
	/// Add money to the local player and save immediately.
	/// Example: vegga_add_money 100
	/// </summary>
	[ConCmd( "vegga_add_money" )]
	public static void AddMoneyCmd( int amount )
	{
		var stats = PlayerVeggaStats.Local;
		if ( stats is null )
		{
			Log.Warning( "[Money] PlayerVeggaStats.Local is null – is a local player spawned?" );
			return;
		}

		var before = stats.Money;
		stats.AddMoney( amount );
		Log.Info( $"[Money] Added ${amount}: ${before} -> ${stats.Money}" );

		// Force save
		PlayerDataPersistence.SaveLocalNow();
	}

	/// <summary>
	/// Force save player data right now.
	/// Example: vegga_save_now
	/// </summary>
	[ConCmd( "vegga_save_now" )]
	public static void SaveNowCmd()
	{
		Log.Info( "[Money] Forcing save..." );
		PlayerDataPersistence.SaveLocalNow();
	}

	/// <summary>
	/// Check the contents of the save file directly.
	/// Example: vegga_check_save_file
	/// </summary>
	[ConCmd( "vegga_check_save_file" )]
	public static void CheckSaveFileCmd()
	{
		var conn = Connection.Local;
		if ( conn == null )
		{
			Log.Warning( "[Money] No local connection found." );
			return;
		}

		var steamId = conn.SteamId.ToString();
		var filePath = $"PlayerData/{steamId}.json";
		var fullPath = FileSystem.Data.GetFullPath( filePath );

		Log.Info( $"[Money] ========== Save File Check ==========" );
		Log.Info( $"[Money] Path: {fullPath}" );

		if ( !FileSystem.Data.FileExists( filePath ) )
		{
			Log.Warning( $"[Money] File does NOT exist!" );
			Log.Info( $"[Money] ========================================" );
			return;
		}

		try
		{
			var json = FileSystem.Data.ReadAllText( filePath );
			Log.Info( $"[Money] Raw JSON (first 500 chars):" );
			Log.Info( json.Length > 500 ? json.Substring( 0, 500 ) + "..." : json );
		}
		catch ( Exception ex )
		{
			Log.Error( $"[Money] Error reading file: {ex.Message}" );
		}

		Log.Info( $"[Money] ========================================" );
	}
}
