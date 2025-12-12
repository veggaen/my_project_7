using System;
using System.Collections.Generic;
using Sandbox;

namespace VEGGA.HEX;

/// <summary>
/// HEX Core System - Main controller for VEGGA ROLEPLAY's admin system
/// HEX = Hexagonal Execution X-System
/// The definitive admin, anti-cheat, and security suite
/// </summary>
public sealed class HEXCore : Component, Component.INetworkListener
{
	[Property, Group( "Configuration" )] 
	public string OwnerSteamId { get; set; } = "76561198050516440"; // Your Steam ID

	[Property, Group( "Configuration" )]
	public bool EnableAntiCheat { get; set; } = true;

	[Property, Group( "Configuration" )]
	public bool EnableLogging { get; set; } = true;

	[Property, Group( "Configuration" )]
	public bool EnableDiscordWebhook { get; set; } = false;

	[Property, Group( "Configuration" )]
	public string DiscordWebhookUrl { get; set; } = "";

	// Singleton
	public static HEXCore Instance { get; private set; }

	// Sub-systems
	public HEXPermissions Permissions { get; private set; }
	public HEXLogger Logger { get; private set; }
	public HEXCommandRegistry Commands { get; private set; }
	public HEXAntiCheat AntiCheat { get; private set; }

	// Active sessions
	private Dictionary<ulong, HEXPlayerSession> _activeSessions = new();

	protected override void OnAwake()
	{
		if ( Instance != null )
		{
			GameObject.Destroy();
			return;
		}

		Instance = this;
	}

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;

		Log.Info( "╔════════════════════════════════════════════════════════════╗" );
		Log.Info( "║                                                            ║" );
		Log.Info( "║   🔷 HEX ADMIN SYSTEM v1.0                                ║" );
		Log.Info( "║   Hexagonal Execution X-System                            ║" );
		Log.Info( "║   VEGGA ROLEPLAY's Next-Gen Admin Suite                   ║" );
		Log.Info( "║                                                            ║" );
		Log.Info( "╚════════════════════════════════════════════════════════════╝" );

		// Initialize sub-systems
		InitializeSubSystems();

		Log.Info( "✅ HEX Core: Initialized" );
		Log.Info( $"   Owner: {OwnerSteamId}" );
		Log.Info( $"   Anti-Cheat: {(EnableAntiCheat ? "ENABLED" : "DISABLED")}" );
		Log.Info( $"   Logging: {(EnableLogging ? "ENABLED" : "DISABLED")}" );
		Log.Info( $"   Discord Webhook: {(EnableDiscordWebhook ? "ENABLED" : "DISABLED")}" );
	}

	void InitializeSubSystems()
	{
		// Permissions system
		Permissions = Components.GetOrCreate<HEXPermissions>();
		Permissions.Initialize( this );

		// Logger
		Logger = Components.GetOrCreate<HEXLogger>();
		Logger.Initialize( this );

		// Command registry
		Commands = Components.GetOrCreate<HEXCommandRegistry>();
		Commands.Initialize( this );

		// Anti-cheat (if enabled)
		if ( EnableAntiCheat )
		{
			AntiCheat = Components.GetOrCreate<HEXAntiCheat>();
			AntiCheat.Initialize( this );
		}

		Log.Info( "✅ HEX Sub-systems: Initialized" );
	}

	// ---- Network Events ----

	void INetworkListener.OnActive( Connection connection )
	{
		if ( !Networking.IsHost ) return;

		// Player connected
		ulong steamId = connection.SteamId;
		
		var session = new HEXPlayerSession
		{
			SteamId = steamId,
			ConnectionTime = DateTime.UtcNow,
			Connection = connection
		};

		_activeSessions[steamId] = session;

		// Load rank
		string rank = Permissions.GetPlayerRank( steamId.ToString() );
		session.Rank = rank;

		Logger?.LogActivity( $"CONNECT | {connection.DisplayName} ({steamId}) | Rank: {rank}" );

		Log.Info( $"🔷 HEX: Player connected - {connection.DisplayName} ({steamId}) [{rank}]" );
	}

	void INetworkListener.OnDisconnected( Connection connection )
	{
		if ( !Networking.IsHost ) return;

		// Player disconnected
		ulong steamId = connection.SteamId;

		if ( _activeSessions.TryGetValue( steamId, out var session ) )
		{
			var duration = DateTime.UtcNow - session.ConnectionTime;
			Logger?.LogActivity( $"DISCONNECT | {connection.DisplayName} ({steamId}) | Duration: {duration.TotalMinutes:F1}m" );

			_activeSessions.Remove( steamId );
		}

		Log.Info( $"🔷 HEX: Player disconnected - {connection.DisplayName} ({steamId})" );
	}

	// ---- Public API ----

	public HEXPlayerSession GetSession( ulong steamId )
	{
		return _activeSessions.GetValueOrDefault( steamId );
	}

	public IEnumerable<HEXPlayerSession> GetAllSessions()
	{
		return _activeSessions.Values;
	}

	public bool IsOwner( ulong steamId )
	{
		return steamId.ToString() == OwnerSteamId;
	}
}

/// <summary>
/// Represents an active player session
/// </summary>
public class HEXPlayerSession
{
	public ulong SteamId { get; set; }
	public string Rank { get; set; } = "Guest";
	public DateTime ConnectionTime { get; set; }
	public Connection Connection { get; set; }
	public int WarningCount { get; set; } = 0;
	public bool IsWatchlisted { get; set; } = false;
	public bool IsFrozen { get; set; } = false;
	public bool IsGodMode { get; set; } = false;
	public bool IsNoclip { get; set; } = false;
	public bool IsCloaked { get; set; } = false;
}

