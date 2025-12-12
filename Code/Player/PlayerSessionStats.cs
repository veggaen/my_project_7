using System;
using Sandbox;

namespace Sandbox;

/// <summary>
/// Tracks player stats for the current session.
/// Resets when player disconnects (unless saved to persistent stats).
/// </summary>
public sealed class PlayerSessionStats : Component
{
	// Session stats (reset on disconnect)
	[Sync] public int SessionKills { get; private set; } = 0;
	[Sync] public int SessionDeaths { get; private set; } = 0;
	[Sync] public int SessionArrests { get; private set; } = 0;
	[Sync] public int SessionQuestsCompleted { get; private set; } = 0;
	[Sync] public float SessionPlaytime { get; private set; } = 0f;
	[Sync] public int SessionMoneyEarned { get; private set; } = 0;
	[Sync] public int SessionMoneySpent { get; private set; } = 0;

	// Persistent stats (loaded from file)
	[Sync] public int TotalKills { get; set; } = 0;
	[Sync] public int TotalDeaths { get; set; } = 0;
	[Sync] public int TotalArrests { get; set; } = 0;
	[Sync] public int TotalQuestsCompleted { get; set; } = 0;
	[Sync] public int TotalQuestPoints { get; set; } = 0;
	[Sync] public int TotalPropsSpawned { get; set; } = 0;
	[Sync] public float TotalPlaytime { get; set; } = 0f;
	[Sync] public int TotalConnects { get; set; } = 0;
	[Sync] public int TotalDisconnects { get; set; } = 0;

	// Player identity
	[Sync] public string PreferredUsername { get; set; } = "";
	[Sync] public string SteamName { get; set; } = "";
	[Sync] public string Rank { get; set; } = "Guest";

	// Admin settings
	[Sync] public bool IsStealthMode { get; set; } = false;
	[Sync] public string StealthName { get; set; } = "";

	// Setup & Consent
	[Sync] public bool HasCompletedSetup { get; set; } = false;
	[Sync] public bool ConsentToDataCollection { get; set; } = false;
	[Sync] public DateTime ConsentDate { get; set; } = DateTime.MinValue;

	// References
	private PlayerVeggaStats _stats;
	private float _sessionStartTime;

	// Local singleton
	private static PlayerSessionStats _local;
	public static PlayerSessionStats Local
	{
		get
		{
			// Check if cached local is still valid and still owned by our local connection
			var localConn = Connection.Local;
			if ( localConn is not null && _local is not null && _local.IsValid() && _local.Network.Owner == localConn )
				return _local;

			// Clear invalid cache
			_local = null;

			var scene = Game.ActiveScene;
			if ( scene is null || localConn is null )
				return null;

			// Find the session stats owned by our local connection
			foreach ( var session in scene.GetAllComponents<PlayerSessionStats>() )
			{
				if ( session.IsValid() && session.Network.Owner == localConn )
				{
					_local = session;
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

	protected override void OnStart()
	{
		_stats = Components.Get<PlayerVeggaStats>();
		_sessionStartTime = Time.Now;

		Log.Info( "📊 PlayerSessionStats: Started tracking!" );
	}

	protected override void OnUpdate()
	{
		// Update session playtime
		if ( !Network.IsProxy )
		{
			SessionPlaytime = Time.Now - _sessionStartTime;
		}
	}

	// ---- Session Stat Tracking ----

	public void AddKill()
	{
		if ( Network.IsProxy ) return;
		SessionKills++;
		TotalKills++;
		Log.Info( $"💀 Kill recorded! Session: {SessionKills} | Total: {TotalKills}" );
	}

	public void AddDeath()
	{
		if ( Network.IsProxy ) return;
		SessionDeaths++;
		TotalDeaths++;
		Log.Info( $"☠️ Death recorded! Session: {SessionDeaths} | Total: {TotalDeaths}" );
	}

	public void AddArrest()
	{
		if ( Network.IsProxy ) return;
		SessionArrests++;
		TotalArrests++;
		Log.Info( $"👮 Arrest recorded! Session: {SessionArrests} | Total: {TotalArrests}" );
	}

	public void AddQuestCompleted( int questPoints = 1 )
	{
		if ( Network.IsProxy ) return;
		SessionQuestsCompleted++;
		TotalQuestsCompleted++;
		TotalQuestPoints += questPoints;
		Log.Info( $"✅ Quest completed! Session: {SessionQuestsCompleted} | Total: {TotalQuestsCompleted} | Points: {TotalQuestPoints}" );
	}

	public void AddPropSpawned()
	{
		if ( Network.IsProxy ) return;
		TotalPropsSpawned++;
		Log.Info( $"🔨 Prop spawned! Total: {TotalPropsSpawned}" );
	}

	public void AddMoneyEarned( int amount )
	{
		if ( Network.IsProxy ) return;
		SessionMoneyEarned += amount;
		Log.Info( $"💰 Money earned: ${amount} | Session total: ${SessionMoneyEarned}" );
	}

	public void AddMoneySpent( int amount )
	{
		if ( Network.IsProxy ) return;
		SessionMoneySpent += amount;
		Log.Info( $"💸 Money spent: ${amount} | Session total: ${SessionMoneySpent}" );
	}

	// ---- Calculated Stats ----

	public float GetKDRatio()
	{
		if ( TotalDeaths == 0 ) return TotalKills;
		return (float)TotalKills / TotalDeaths;
	}

	public int GetNetworth()
	{
		// TypeScript style: return stats?.Money ?? 0;
		if ( _stats == null || !_stats.IsValid() ) return 0;
		return _stats.Money; // TODO: Add bank balance when implemented
	}

	public string GetPlaytimeFormatted()
	{
		// Include current session playtime in the formatted output
		var totalSeconds = (int)(TotalPlaytime + SessionPlaytime);
		var hours = totalSeconds / 3600;
		var minutes = (totalSeconds % 3600) / 60;
		return $"{hours}h {minutes}m";
	}

	public string GetDisplayName()
	{
		// TypeScript style: if (isStealthMode && stealthName) return stealthName;
		if ( IsStealthMode && !string.IsNullOrEmpty( StealthName ) )
			return StealthName;

		// TypeScript style: return preferredUsername || steamName || "Player";
		if ( !string.IsNullOrEmpty( PreferredUsername ) )
			return PreferredUsername;

		if ( !string.IsNullOrEmpty( SteamName ) )
			return SteamName;

		// Fallback (TypeScript: ?? "Player")
		return "Player";
	}

	public int GetPing()
	{
		// TODO: Implement actual ping calculation
		return 0;
	}

	// ---- Testing Buttons ----

	[Button( "Add Test Kill" ), Group( "Testing" )]
	public void TestAddKill() => AddKill();

	[Button( "Add Test Death" ), Group( "Testing" )]
	public void TestAddDeath() => AddDeath();

	[Button( "Add Test Arrest" ), Group( "Testing" )]
	public void TestAddArrest() => AddArrest();

	[Button( "Add Test Quest" ), Group( "Testing" )]
	public void TestAddQuest() => AddQuestCompleted();
}

