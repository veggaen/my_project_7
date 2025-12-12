using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace VEGGA.HEX;

/// <summary>
/// HEX Permissions System
/// Manages ranks and permissions for all players
/// </summary>
public sealed class HEXPermissions : Component
{
	private HEXCore _core;
	private Dictionary<string, HEXRank> _ranks = new();
	private Dictionary<string, string> _playerRanks = new(); // SteamID -> Rank name

	private const string RanksFile = "HEX/ranks.json";
	private const string PlayerRanksFile = "HEX/player_ranks.json";

	public void Initialize( HEXCore core )
	{
		_core = core;
		LoadRanks();
		LoadPlayerRanks();
	}

	void LoadRanks()
	{
		// Default ranks
		_ranks = new Dictionary<string, HEXRank>
		{
			["Guest"] = new HEXRank { Name = "Guest", Level = 0, Color = "#888888", Permissions = new() },
			["VIP"] = new HEXRank { Name = "VIP", Level = 1, Color = "#FFAA00", Permissions = new() { "hex.fun" } },
			["Moderator"] = new HEXRank { Name = "Moderator", Level = 2, Color = "#00AAFF", Permissions = new() { "hex.fun", "hex.kick", "hex.freeze", "hex.slay" } },
			["Admin"] = new HEXRank { Name = "Admin", Level = 3, Color = "#FF8800", Permissions = new() { "hex.*", "hex.ban", "hex.teleport", "hex.god" } },
			["Superadmin"] = new HEXRank { Name = "Superadmin", Level = 4, Color = "#FF4444", Permissions = new() { "hex.*", "hex.anticheat" } },
			["Owner"] = new HEXRank { Name = "Owner", Level = 5, Color = "#FF0000", Permissions = new() { "hex.*" } }
		};

		// Try to load from file
		try
		{
			if ( FileSystem.Data.FileExists( RanksFile ) )
			{
				var loadedRanks = FileSystem.Data.ReadJson<Dictionary<string, HEXRank>>( RanksFile );
				if ( loadedRanks != null )
				{
					_ranks = loadedRanks;
					Log.Info( $"✅ HEX Permissions: Loaded {_ranks.Count} ranks from file" );
				}
			}
			else
			{
				// Save defaults
				FileSystem.Data.WriteJson( RanksFile, _ranks );
				Log.Info( "✅ HEX Permissions: Created default ranks file" );
			}
		}
		catch ( Exception ex )
		{
			Log.Error( $"❌ HEX Permissions: Failed to load ranks - {ex.Message}" );
		}
	}

	void LoadPlayerRanks()
	{
		try
		{
			if ( FileSystem.Data.FileExists( PlayerRanksFile ) )
			{
				_playerRanks = FileSystem.Data.ReadJson<Dictionary<string, string>>( PlayerRanksFile );
				Log.Info( $"✅ HEX Permissions: Loaded {_playerRanks.Count} player ranks" );
			}
		}
		catch ( Exception ex )
		{
			Log.Error( $"❌ HEX Permissions: Failed to load player ranks - {ex.Message}" );
		}
	}

	void SavePlayerRanks()
	{
		try
		{
			FileSystem.Data.WriteJson( PlayerRanksFile, _playerRanks );
		}
		catch ( Exception ex )
		{
			Log.Error( $"❌ HEX Permissions: Failed to save player ranks - {ex.Message}" );
		}
	}

	// ---- Public API ----

	public string GetPlayerRank( string steamId )
	{
		// Owner always gets Owner rank
		if ( steamId == _core.OwnerSteamId )
			return "Owner";

		return _playerRanks.GetValueOrDefault( steamId, "Guest" );
	}

	public void SetPlayerRank( string steamId, string rankName )
	{
		if ( !_ranks.ContainsKey( rankName ) )
		{
			Log.Warning( $"⚠️ HEX Permissions: Rank '{rankName}' does not exist" );
			return;
		}

		_playerRanks[steamId] = rankName;
		SavePlayerRanks();

		_core.Logger?.LogActivity( $"RANK | Set {steamId} to {rankName}" );
		Log.Info( $"✅ HEX Permissions: Set {steamId} to {rankName}" );
	}

	public bool HasPermission( string steamId, string permission )
	{
		string rankName = GetPlayerRank( steamId );
		
		if ( !_ranks.TryGetValue( rankName, out var rank ) )
			return false;

		// Owner has all permissions
		if ( rankName == "Owner" )
			return true;

		// Check for wildcard
		if ( rank.Permissions.Contains( "hex.*" ) )
			return true;

		// Check specific permission
		return rank.Permissions.Contains( permission );
	}

	public int GetRankLevel( string rankName )
	{
		return _ranks.GetValueOrDefault( rankName )?.Level ?? 0;
	}

	public HEXRank GetRank( string rankName )
	{
		return _ranks.GetValueOrDefault( rankName );
	}

	public IEnumerable<HEXRank> GetAllRanks()
	{
		return _ranks.Values.OrderBy( r => r.Level );
	}
}

/// <summary>
/// Represents a rank with permissions
/// </summary>
[Serializable]
public class HEXRank
{
	public string Name { get; set; }
	public int Level { get; set; }
	public string Color { get; set; }
	public List<string> Permissions { get; set; } = new();
}

