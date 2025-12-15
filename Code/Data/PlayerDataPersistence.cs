using Sandbox;
using Sandbox.Data;
using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Handles automatic saving/loading of player data on join/disconnect
/// Uses EVENT-BASED saving (not intervals) for better performance
/// Smart backup system: last/hourly/daily/weekly (only 4 files per player)
/// </summary>
public sealed class PlayerDataPersistence : Component, Component.INetworkListener
{
	private static PlayerDataPersistence _instance;

	/// <summary>
	/// Safety auto-save interval (only saves if data changed)
	/// </summary>
	[Property] public float AutoSaveInterval { get; set; } = 300f; // 5 minutes

	/// <summary>
	/// Track last save time per player
	/// </summary>
	private Dictionary<string, RealTimeSince> _lastSaveTime = new();

	/// <summary>
	/// Track if player data has changed since last save
	/// </summary>
	private Dictionary<string, bool> _dataChanged = new();

	protected override void OnAwake()
	{
		// Prefer the most recently created instance so hot-reloads / scene
		// changes don't leave us with a stale component whose Scene is null.
		if ( _instance != null && _instance != this )
		{
			Log.Warning( "⚠️ Multiple PlayerDataPersistence instances detected! Replacing previous instance." );
			if ( _instance.IsValid )
			{
				_instance.GameObject?.Destroy();
			}
		}

		_instance = this;
		Log.Info( "💾 PlayerDataPersistence: Event-based saving enabled!" );
		Log.Info( $"💾 Safety auto-save: Every {AutoSaveInterval}s (only if changed)" );
	}

	protected override void OnDestroy()
	{
		// Best-effort flush on shutdown/hot-reload.
		// This helps ensure skills/inventory changes persist even if the session ends
		// without clean disconnect events firing.
		if ( Networking.IsHost )
		{
			try
			{
				foreach ( var steamId in _lastSaveTime.Keys.ToArray() )
				{
					SavePlayerNow( steamId );
				}
			}
			catch ( Exception ex )
			{
				Log.Warning( ex, "[PlayerDataPersistence] Flush-on-destroy failed" );
			}
		}

		base.OnDestroy();
		if ( _instance == this )
		{
			_instance = null;
		}
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;

		// Safety auto-save (only if data changed)
		foreach ( var kvp in _lastSaveTime.ToArray() )
		{
			var steamId = kvp.Key;
			var timeSince = kvp.Value;

			if ( timeSince > AutoSaveInterval && _dataChanged.GetValueOrDefault( steamId, false ) )
			{
				SavePlayerNow( steamId );
				_lastSaveTime[steamId] = 0;
				_dataChanged[steamId] = false;
			}
		}
	}

	/// <summary>
	/// Called when a player becomes active (fully connected)
	/// </summary>
	void INetworkListener.OnActive( Connection connection )
	{
		if ( !Networking.IsHost ) return;

		var steamId = connection.SteamId.ToString();
		Log.Info( $"📥 Loading player data for {connection.DisplayName} ({steamId})" );

		// Load player data from JSON
		var data = PlayerDataManager.LoadPlayerData( steamId );

		// Find the player's components
		var playerStats = FindPlayerStats( connection );
		var sessionStats = FindSessionStats( connection );

		if ( playerStats != null && sessionStats != null )
		{
			// Load data into components
			LoadDataIntoPlayer( data, playerStats, sessionStats );
			Log.Info( $"✅ Loaded data for {data.PreferredUsername}: ${data.Money}, {data.TotalPlaytime:F0}s playtime" );

			// Track this player for auto-save
			_lastSaveTime[steamId] = 0;
			_dataChanged[steamId] = false;
		}
		else
		{
			Log.Warning( $"⚠️ Could not find player components for {connection.DisplayName}" );
		}
	}

	/// <summary>
	/// Called when a player disconnects
	/// </summary>
	void INetworkListener.OnDisconnected( Connection connection )
	{
		if ( !Networking.IsHost ) return;

		var steamId = connection.SteamId.ToString();
		Log.Info( $"💾 Saving player data for {connection.DisplayName} ({steamId})" );

		SavePlayerNow( steamId );

		// Remove from tracking
		_lastSaveTime.Remove( steamId );
		_dataChanged.Remove( steamId );
	}

	/// <summary>
	/// Save a specific player's data immediately
	/// </summary>
	private void SavePlayerNow( string steamId )
	{
		Log.Info( $"💾 [SavePlayerNow] Starting save for SteamId: {steamId}" );

		// Find connection by SteamId
		Connection connection = null;
		foreach ( var conn in Connection.All )
		{
			if ( conn.SteamId.ToString() == steamId )
			{
				connection = conn;
				break;
			}
		}

		if ( connection == null )
		{
			Log.Warning( $"⚠️ [SavePlayerNow] Could not find connection for SteamId {steamId}" );
			return;
		}

		// Find the player's components
		var playerStats = FindPlayerStats( connection );
		var sessionStats = FindSessionStats( connection );

		if ( playerStats != null && sessionStats != null )
		{
			Log.Info( $"💾 [SavePlayerNow] Found components. playerStats.Money = ${playerStats.Money}" );

			// Save data from components
			var data = CreateDataFromPlayer( steamId, playerStats, sessionStats );

			Log.Info( $"💾 [SavePlayerNow] Created data object. data.Money = ${data.Money}" );

			bool success = PlayerDataManager.SavePlayerData( steamId, data );
			if ( success )
			{
				Log.Info( $"✅ [SavePlayerNow] Saved data for {data.PreferredUsername}: ${data.Money}, {data.TotalPlaytime:F0}s playtime" );
			}
			else
			{
				Log.Error( $"❌ [SavePlayerNow] Failed to save data for {steamId}" );
			}
		}
		else
		{
			Log.Warning( $"⚠️ [SavePlayerNow] Could not find player components for SteamId {steamId}" );
			Log.Warning( $"⚠️ [SavePlayerNow] playerStats={playerStats != null}, sessionStats={sessionStats != null}" );
		}
	}

	/// <summary>
	/// Mark player data as changed (will trigger auto-save)
	/// Call this when money/inventory/stats change
	/// </summary>
	public static void MarkPlayerDataChanged( string steamId )
	{
		if ( _instance == null ) return;
		_instance._dataChanged[steamId] = true;
	}

	/// <summary>
	/// Force an immediate save for the local player's data on the host.
	/// Useful for testing (e.g. after giving yourself money) so you can be
	/// sure the JSON file is updated without waiting for auto-save or disconnect.
	/// </summary>
	public static void SaveLocalNow()
	{
		Log.Info( "💾 [SaveLocalNow] Called!" );

		if ( _instance == null || !_instance.IsValid || _instance.Scene == null )
		{
			Log.Warning( "💾 SaveLocalNow: No valid PlayerDataPersistence instance in scene." );
			return;
		}

		if ( !Networking.IsHost )
		{
			// For now this is host-only. In the future we can add an RPC so
			// clients can request a save on the server.
			Log.Warning( "💾 SaveLocalNow: Only the host can force a save right now." );
			return;
		}

		var conn = Connection.Local;
		if ( conn == null )
		{
			Log.Warning( "💾 SaveLocalNow: No local connection found." );
			return;
		}

		Log.Info( $"💾 [SaveLocalNow] Saving for SteamId: {conn.SteamId}" );
		_instance.SavePlayerNow( conn.SteamId.ToString() );
	}

	/// <summary>
	/// Find PlayerVeggaStats for a connection.
	/// Tries multiple strategies so it works both in multiplayer and
	/// in editor single-player where Network.Owner may be null.
	/// </summary>
	private PlayerVeggaStats FindPlayerStats( Connection connection )
	{
		// In some editor/testing flows (e.g. StatsTest buttons) this component
		// can exist without being properly attached to a live Scene yet.
		// Guard against a null Scene so we don't throw when calling GetAllComponents.
		if ( Scene == null )
		{
			// Best effort: fall back to the global Local cache if available.
			if ( PlayerVeggaStats.Local != null && PlayerVeggaStats.Local.IsValid() )
			{
				Log.Warning( "[PlayerDataPersistence] FindPlayerStats: Scene is null, using PlayerVeggaStats.Local fallback." );
				return PlayerVeggaStats.Local;
			}

			Log.Warning( "[PlayerDataPersistence] FindPlayerStats: Scene is null and no Local stats available." );
			return null;
		}

		// 1) Prefer exact Network.Owner match (normal multiplayer case)
		foreach ( var stats in Scene.GetAllComponents<PlayerVeggaStats>() )
		{
			if ( !stats.IsValid() )
				continue;

			var net = stats.Network;
			if ( net != null && net.Owner == connection )
				return stats;
		}

		// 2) Fallback to the Local cache (used by HUD etc.)
		if ( PlayerVeggaStats.Local != null && PlayerVeggaStats.Local.IsValid() )
		{
			Log.Info( "[PlayerDataPersistence] FindPlayerStats: Using PlayerVeggaStats.Local fallback." );
			return PlayerVeggaStats.Local;
		}

		// 3) Final fallback – any non-proxy stats in the scene
		foreach ( var stats in Scene.GetAllComponents<PlayerVeggaStats>() )
		{
			if ( !stats.IsValid() )
				continue;

			var net = stats.Network;
			if ( net == null || !net.IsProxy )
				return stats;
		}

		Log.Warning( "[PlayerDataPersistence] FindPlayerStats: Could not find stats for connection." );
		return null;
	}

	/// <summary>
	/// Find PlayerSessionStats for a connection with similar fallbacks.
	/// </summary>
	private PlayerSessionStats FindSessionStats( Connection connection )
	{
		if ( Scene == null )
		{
			Log.Warning( "[PlayerDataPersistence] FindSessionStats: Scene is null, cannot search for session stats." );
			return null;
		}

		// 1) Prefer exact Network.Owner match
		foreach ( var stats in Scene.GetAllComponents<PlayerSessionStats>() )
		{
			if ( !stats.IsValid() )
				continue;

			var net = stats.Network;
			if ( net != null && net.Owner == connection )
				return stats;
		}

		// 2) Fallback – any non-proxy session stats
		foreach ( var stats in Scene.GetAllComponents<PlayerSessionStats>() )
		{
			if ( !stats.IsValid() )
				continue;

			var net = stats.Network;
			if ( net == null || !net.IsProxy )
				return stats;
		}

		Log.Warning( "[PlayerDataPersistence] FindSessionStats: Could not find session stats for connection." );
		return null;
	}

	/// <summary>
	/// Load data from JSON into player components
	/// </summary>
	private void LoadDataIntoPlayer( PlayerDataManager.PlayerData data, PlayerVeggaStats playerStats, PlayerSessionStats sessionStats )
	{
		Log.Info( $"[PlayerDataPersistence] LoadDataIntoPlayer() called. data.Money={data.Money}" );

		// Load identity (fallback to current connection name if save has no names yet)
		var connection = sessionStats.Network.Owner;

		// Prefer saved values if present, otherwise use connection.DisplayName
		var displayName = connection?.DisplayName ?? "Player";

		sessionStats.SteamName = string.IsNullOrWhiteSpace( data.SteamName )
			? displayName
			: data.SteamName;

		sessionStats.PreferredUsername = string.IsNullOrWhiteSpace( data.PreferredUsername )
			? sessionStats.SteamName
			: data.PreferredUsername;

		// HEX Owner override: if SteamID is listed as Owner, always show Owner rank
		var __steamId = connection?.SteamId.ToString();
		if ( !string.IsNullOrWhiteSpace( __steamId ) && Sandbox.Admin.HexPermissions.IsOwnerSteamId( __steamId ) )
		{
			sessionStats.Rank = "Owner";
		}
		else
		{
			sessionStats.Rank = string.IsNullOrWhiteSpace( data.Rank ) ? "Guest" : data.Rank;
		}

		// Load stats
		sessionStats.TotalKills = data.TotalKills;
		sessionStats.TotalDeaths = data.TotalDeaths;
		sessionStats.TotalArrests = data.TotalArrests;
		sessionStats.TotalQuestsCompleted = data.TotalQuestsCompleted;
		sessionStats.TotalPlaytime = data.TotalPlaytime;
		sessionStats.TotalConnects = data.TotalConnects + 1; // Increment connects
		sessionStats.TotalPropsSpawned = data.TotalPropsSpawned;

		// ---- Inventory + cash migration ----
		var inventory = playerStats.GameObject?.Components.Get<VeggaInventory>();
		if ( inventory == null )
		{
			Log.Warning( "[PlayerDataPersistence] Player has no VeggaInventory component; cannot load cash-as-item." );
		}
		else
		{
			// Migrate legacy saves (Money int) into inventory cash stack.
			if ( data.SaveVersion < 2 )
			{
				int legacyMoney = Math.Max( 0, data.Money );
				if ( legacyMoney > 0 )
				{
					data.ItemIds ??= new List<int>();
					data.ItemCounts ??= new List<int>();
					data.ItemDurability ??= new List<int>();
					data.ItemIds.Add( 1 );
					data.ItemCounts.Add( legacyMoney );
					data.ItemDurability.Add( 0 );
				}

				data.Money = 0;
				data.SaveVersion = 2;
			}

			inventory.LoadSaveData( data.ItemIds, data.ItemCounts, data.ItemDurability );
		}

		// ---- Skills ----
		var skills = playerStats.GameObject?.Components.Get<PlayerVeggaSkills>();
		if ( skills == null || !skills.IsValid() )
		{
			// Skills are optional on some prefabs, so don't hard-fail.
			Log.Warning( "[PlayerDataPersistence] Player has no PlayerVeggaSkills component; skipping skills load." );
		}
		else
		{
			// Initialize lists for older saves.
			if ( data.SaveVersion < 3 )
			{
				data.SkillLevels ??= new List<int>();
				data.SkillXps ??= new List<int>();
				data.SaveVersion = 3;
			}

			skills.LoadSaveData( data.SkillLevels, data.SkillXps );
		}

		// Mark that persistence ran (prevents fallback)
		playerStats._moneyLoadedFromSave = true;

		// Ensure new players have starting cash if they ended up with none.
		if ( inventory != null && inventory.IsValid() )
		{
			int cash = playerStats.Money;
			if ( cash <= 0 )
			{
				playerStats.AddMoney( playerStats.StartMoney );
			}
		}

		// Increment total connects
		data.TotalConnects++;

		// Mark initialization complete
		playerStats.FinalizeMoneyInit();
	}

	/// <summary>
	/// Create PlayerData from player components
	/// </summary>
	private PlayerDataManager.PlayerData CreateDataFromPlayer( string steamId, PlayerVeggaStats playerStats, PlayerSessionStats sessionStats )
	{
		var ids = new List<int>();
		var counts = new List<int>();
		var durability = new List<int>();
		var skillLevels = new List<int>();
		var skillXps = new List<int>();

		var inventory = playerStats.GameObject?.Components.Get<VeggaInventory>();
		if ( inventory != null && inventory.IsValid() )
		{
			inventory.ExportSaveData( out ids, out counts, out durability );
		}

		var skills = playerStats.GameObject?.Components.Get<PlayerVeggaSkills>();
		if ( skills != null && skills.IsValid() )
		{
			skills.ExportSaveData( out skillLevels, out skillXps );
		}

		return new PlayerDataManager.PlayerData
		{
			SaveVersion = 3,
			SteamId = steamId,
			SteamName = sessionStats.SteamName,
			PreferredUsername = sessionStats.PreferredUsername,
			PlayerName = sessionStats.PreferredUsername,
			Rank = sessionStats.Rank,

			// Stats
			TotalKills = sessionStats.TotalKills,
			TotalDeaths = sessionStats.TotalDeaths,
			TotalArrests = sessionStats.TotalArrests,
			TotalQuestsCompleted = sessionStats.TotalQuestsCompleted,
			TotalPlaytime = sessionStats.TotalPlaytime + sessionStats.SessionPlaytime, // Add current session
			TotalConnects = sessionStats.TotalConnects,
			TotalDisconnects = sessionStats.TotalDisconnects + 1, // Increment disconnects
			TotalPropsSpawned = sessionStats.TotalPropsSpawned,

			// Money
			Money = 0,
			BankBalance = 0, // TODO: Implement bank

			// Inventory
			InventorySlots = inventory?.TotalSlots ?? 96,
			ItemIds = ids,
			ItemCounts = counts,
			ItemDurability = durability,

			// Skills
			SkillLevels = skillLevels,
			SkillXps = skillXps,

			// Timestamps
			LastSeen = DateTime.UtcNow
		};
	}
}

