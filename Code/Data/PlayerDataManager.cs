using System;
using System.Collections.Generic;
using System.Text.Json;
using Sandbox;

#nullable enable

namespace Sandbox.Data;

/// <summary>
/// Manages saving and loading player data to/from JSON files.
/// Features:
/// - Daily backups (1 day retention)
/// - Weekly backups (7 days retention)
/// - Activity logging
/// - Auto-save system
/// Uses s&box FileSystem.Data for safe file access.
/// </summary>
public static class PlayerDataManager
{
	private const string DataFolder = "PlayerData";
	private const string BackupFolder = "PlayerData/Backups";
	private const string DailyBackupFolder = "PlayerData/Backups/Daily";
	private const string WeeklyBackupFolder = "PlayerData/Backups/Weekly";
	private const string ActivityLogFile = "PlayerData/activity.log";

	[Serializable]
	public class PlayerData
	{
		// Identity
		public string SteamId { get; set; } = "";
		public string SteamName { get; set; } = ""; // Steam display name
		public string PreferredUsername { get; set; } = ""; // Custom username
		public string PlayerName { get; set; } = ""; // Display name (for compatibility)
		public string Rank { get; set; } = "Guest"; // Guest, VIP, Moderator, Admin, Superadmin, Owner

		// Connection Stats
		public int TotalConnects { get; set; }
		public int TotalDisconnects { get; set; }
		public DateTime FirstSeen { get; set; }
		public DateTime LastSeen { get; set; }

		// Combat Stats
		public int TotalKills { get; set; }
		public int TotalDeaths { get; set; }
		public int TotalArrests { get; set; }

		// Quest Stats
		public int TotalQuestsCompleted { get; set; }
		public int TotalQuestPoints { get; set; } // Quest reward points

		// Activity Stats
		public float TotalPlaytime { get; set; }
		public int TotalPropsSpawned { get; set; }

		// Money
		public int Money { get; set; }
		public int BankBalance { get; set; }

		// Inventory
		public int InventorySlots { get; set; }
		public List<int> ItemIds { get; set; } = new();
		public List<int> ItemCounts { get; set; } = new();

		// Admin Settings
		public bool IsStealthMode { get; set; } = false; // Admin invisible mode
		public string StealthName { get; set; } = ""; // Fake name when in stealth

		// Preferences
		public bool ShowSteamName { get; set; } = true;
		public string ScoreboardSortMode { get; set; } = "money";
		public bool ScoreboardSortDescending { get; set; } = true;
		public string AvatarType { get; set; } = "steam"; // steam, upload, default

		// Privacy & Consent
		public bool HasCompletedSetup { get; set; } = false;
		public bool ConsentToDataCollection { get; set; } = false;
		public DateTime ConsentDate { get; set; } = DateTime.MinValue;
	}

	/// <summary>
	/// Save player data to JSON file using s&box FileSystem
	/// Also creates daily backup
	/// </summary>
	public static bool SavePlayerData( string steamId, PlayerData data )
	{
		try
		{
			// Validate
			if ( string.IsNullOrEmpty( steamId ) )
			{
				Log.Warning( "⚠️ Cannot save: SteamID is empty" );
				return false;
			}

			// Update last seen
			data.LastSeen = DateTime.UtcNow;

			string filePath = $"{DataFolder}/{steamId}.json";

			// Save main file
			FileSystem.Data.WriteJson( filePath, data );

			// Get full path for logging
			string fullPath = FileSystem.Data.GetFullPath( filePath );

			// Create daily backup
			CreateDailyBackup( steamId, data );

			// Log activity with FULL PATH
			LogActivity( $"SAVE | {data.PlayerName} ({steamId}) | Money: ${data.Money} | Playtime: {data.TotalPlaytime:F0}s" );
			Log.Info( $"💾 Saved to: {fullPath}" );

			return true;
		}
		catch ( Exception ex )
		{
			Log.Error( $"❌ Save failed for {steamId}: {ex.Message}" );
			LogActivity( $"ERROR | Save failed for {steamId}: {ex.Message}" );
			return false;
		}
	}

	/// <summary>
	/// Load player data from JSON file using s&box FileSystem
	/// Falls back to daily backup if main file is corrupted
	/// </summary>
	public static PlayerData LoadPlayerData( string steamId )
	{
		try
		{
			string filePath = $"{DataFolder}/{steamId}.json";
			string fullPath = FileSystem.Data.GetFullPath( filePath );

			if ( !FileSystem.Data.FileExists( filePath ) )
			{
				LogActivity( $"LOAD | New player: {steamId}" );
				Log.Info( $"📂 New player - no data file found at: {fullPath}" );

				// Return new player data with $500 starting money
				return new PlayerData
				{
					SteamId = steamId,
					Money = 500, // Starting money for new players
					FirstSeen = DateTime.UtcNow,
					LastSeen = DateTime.UtcNow
				};
			}

			// Try to load main file
			var data = FileSystem.Data.ReadJson<PlayerData>( filePath );

			if ( data != null )
			{
				LogActivity( $"LOAD | {data.PlayerName} ({steamId}) | Money: ${data.Money}" );
				Log.Info( $"📖 Loaded from: {fullPath}" );
				return data;
			}

			// Main file corrupted, try daily backup
			Log.Warning( $"⚠️ Main file corrupted for {steamId}, trying backup..." );
			return LoadFromBackup( steamId );
		}
		catch ( Exception ex )
		{
			Log.Error( $"❌ Load failed for {steamId}: {ex.Message}" );

			// Try backup
			try
			{
				return LoadFromBackup( steamId );
			}
			catch
			{
				LogActivity( $"ERROR | Load failed for {steamId}: {ex.Message}" );
				return new PlayerData { SteamId = steamId };
			}
		}
	}

	/// <summary>
	/// [DEPRECATED] Old save method - Use PlayerDataPersistence instead!
	/// This method is kept for backwards compatibility but should not be used.
	/// </summary>
	[Obsolete( "Use PlayerDataPersistence.SavePlayerNow() instead" )]
	public static void SavePlayer( PlayerVeggaStats stats, PlayerSessionStats sessionStats )
	{
		Log.Warning( "⚠️ SavePlayer() is deprecated! Use PlayerDataPersistence instead." );
		// This method is no longer used - PlayerDataPersistence handles all saving
	}

	/// <summary>
	/// Load player stats into component
	/// </summary>
	public static void LoadPlayer( PlayerVeggaStats stats, PlayerSessionStats sessionStats, string steamId )
	{
		if ( stats == null || sessionStats == null ) return;

		var data = LoadPlayerData( steamId );

		// Load stats
		sessionStats.TotalKills = data.TotalKills;
		sessionStats.TotalDeaths = data.TotalDeaths;
		sessionStats.TotalArrests = data.TotalArrests;
		sessionStats.TotalQuestsCompleted = data.TotalQuestsCompleted;
		sessionStats.TotalPlaytime = data.TotalPlaytime;

		// Load money
		// stats.Money = data.Money; // Don't auto-load money (anti-cheat)

		Log.Info( $"✅ Loaded player: {data.PlayerName} | K/D: {data.TotalKills}/{data.TotalDeaths}" );
	}

	// ---- SMART BACKUP SYSTEM (Only 4 files per player!) ----

	/// <summary>
	/// Create smart backups: last/hourly/daily/weekly
	/// Only overwrites existing files - NO cleanup needed!
	/// Uses LastSeen timestamp from data to determine if backup should update
	/// </summary>
	private static void CreateDailyBackup( string steamId, PlayerData data )
	{
		try
		{
			var now = DateTime.UtcNow;

			// 1. LAST backup (on every save)
			string lastBackupPath = $"{BackupFolder}/{steamId}_last.json";
			FileSystem.Data.WriteJson( lastBackupPath, data );

			// 2. HOURLY backup (once per hour)
			string hourlyBackupPath = $"{BackupFolder}/{steamId}_hourly.json";
			if ( ShouldUpdateBackupByTimestamp( hourlyBackupPath, data.LastSeen, TimeSpan.FromHours( 1 ) ) )
			{
				FileSystem.Data.WriteJson( hourlyBackupPath, data );
			}

			// 3. DAILY backup (once per day)
			string dailyBackupPath = $"{BackupFolder}/{steamId}_daily.json";
			if ( ShouldUpdateBackupByTimestamp( dailyBackupPath, data.LastSeen, TimeSpan.FromDays( 1 ) ) )
			{
				FileSystem.Data.WriteJson( dailyBackupPath, data );
			}

			// 4. WEEKLY backup (once per week)
			string weeklyBackupPath = $"{BackupFolder}/{steamId}_weekly.json";
			if ( ShouldUpdateBackupByTimestamp( weeklyBackupPath, data.LastSeen, TimeSpan.FromDays( 7 ) ) )
			{
				FileSystem.Data.WriteJson( weeklyBackupPath, data );
			}
		}
		catch ( Exception ex )
		{
			Log.Warning( $"⚠️ Backup failed: {ex.Message}" );
		}
	}

	/// <summary>
	/// Check if backup file should be updated based on timestamp in the JSON data
	/// Uses s&box FileSystem API (no System.IO allowed)
	/// </summary>
	private static bool ShouldUpdateBackupByTimestamp( string filePath, DateTime currentTimestamp, TimeSpan maxAge )
	{
		try
		{
			// Check if file exists using s&box FileSystem
			if ( !FileSystem.Data.FileExists( filePath ) )
				return true; // File doesn't exist, create it

			// Read the existing backup file
			var existingData = FileSystem.Data.ReadJson<PlayerData>( filePath );
			if ( existingData == null )
				return true; // Corrupted file, recreate it

			// Compare timestamps
			var age = currentTimestamp - existingData.LastSeen;
			return age > maxAge;
		}
		catch
		{
			return true; // If we can't check, update it
		}
	}



	/// <summary>
	/// Load from most recent backup (smart backup system)
	/// </summary>
	private static PlayerData LoadFromBackup( string steamId )
	{
		try
		{
			// Try backups in order: last → hourly → daily → weekly
			string[] backupPaths = new[]
			{
				$"{BackupFolder}/{steamId}_last.json",
				$"{BackupFolder}/{steamId}_hourly.json",
				$"{BackupFolder}/{steamId}_daily.json",
				$"{BackupFolder}/{steamId}_weekly.json"
			};

			foreach ( var backupPath in backupPaths )
			{
				if ( FileSystem.Data.FileExists( backupPath ) )
				{
					var data = FileSystem.Data.ReadJson<PlayerData>( backupPath );
					if ( data != null )
					{
						Log.Info( $"✅ Restored from backup: {backupPath}" );
						LogActivity( $"RESTORE | Restored {steamId} from {backupPath}" );
						return data;
					}
				}
			}

			Log.Warning( $"⚠️ No backups found for {steamId}" );
			return new PlayerData { SteamId = steamId };
		}
		catch ( Exception ex )
		{
			Log.Error( $"❌ Backup restore failed: {ex.Message}" );
			return new PlayerData { SteamId = steamId };
		}
	}

	/// <summary>
	/// Manually rollback to a specific backup
	/// </summary>
	public static PlayerData RollbackToBackup( string steamId, string backupType )
	{
		try
		{
			string backupPath = $"{BackupFolder}/{steamId}_{backupType}.json";

			if ( !FileSystem.Data.FileExists( backupPath ) )
			{
				Log.Warning( $"⚠️ Backup not found: {backupPath}" );
				return null;
			}

			var data = FileSystem.Data.ReadJson<PlayerData>( backupPath );
			if ( data != null )
			{
				// Save as current data
				SavePlayerData( steamId, data );
				Log.Info( $"✅ Rolled back to {backupType} backup for {steamId}" );
				LogActivity( $"ROLLBACK | {steamId} rolled back to {backupType}" );
			}

			return data;
		}
		catch ( Exception ex )
		{
			Log.Error( $"❌ Rollback failed: {ex.Message}" );
			return null;
		}
	}



	// ---- ACTIVITY LOGGING ----

	/// <summary>
	/// Log important activities to text file
	/// </summary>
	public static void LogActivity( string message )
	{
		try
		{
			string timestamp = DateTime.UtcNow.ToString( "yyyy-MM-dd HH:mm:ss" );
			string logEntry = $"[{timestamp}] {message}\n";

			// Append to log file
			string existingLog = "";
			if ( FileSystem.Data.FileExists( ActivityLogFile ) )
			{
				existingLog = FileSystem.Data.ReadAllText( ActivityLogFile );
			}

			FileSystem.Data.WriteAllText( ActivityLogFile, existingLog + logEntry );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"⚠️ Activity log failed: {ex.Message}" );
		}
	}

	/// <summary>
	/// Get recent activity log (last N lines)
	/// </summary>
	public static string GetRecentActivity( int lineCount = 100 )
	{
		try
		{
			if ( !FileSystem.Data.FileExists( ActivityLogFile ) )
				return "No activity logged yet.";

			string log = FileSystem.Data.ReadAllText( ActivityLogFile );
			var lines = log.Split( '\n' );
			var recentLines = lines.TakeLast( lineCount );

			return string.Join( "\n", recentLines );
		}
		catch ( Exception ex )
		{
			return $"Error reading log: {ex.Message}";
		}
	}
}

