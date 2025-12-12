using System;
using Sandbox;

namespace VEGGA.HEX;

/// <summary>
/// HEX Logger - Activity and event logging system
/// </summary>
public sealed class HEXLogger : Component
{
	private HEXCore _core;
	private const string ActivityLogFile = "HEX/activity.log";
	private const string AntiCheatLogFile = "HEX/anticheat.log";
	private const string CommandLogFile = "HEX/commands.log";

	public void Initialize( HEXCore core )
	{
		_core = core;
		LogActivity( "SYSTEM | HEX Logger initialized" );
	}

	/// <summary>
	/// Log general activity
	/// </summary>
	public void LogActivity( string message )
	{
		if ( !_core.EnableLogging ) return;

		try
		{
			string timestamp = DateTime.UtcNow.ToString( "yyyy-MM-dd HH:mm:ss" );
			string logEntry = $"[{timestamp}] {message}\n";
			
			AppendToFile( ActivityLogFile, logEntry );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"⚠️ HEX Logger: Failed to log activity - {ex.Message}" );
		}
	}

	/// <summary>
	/// Log anti-cheat detection
	/// </summary>
	public void LogAntiCheat( string playerName, string steamId, string detection, string details )
	{
		if ( !_core.EnableLogging ) return;

		try
		{
			string timestamp = DateTime.UtcNow.ToString( "yyyy-MM-dd HH:mm:ss" );
			string logEntry = $"[{timestamp}] AC | {playerName} ({steamId}) | {detection} | {details}\n";
			
			AppendToFile( AntiCheatLogFile, logEntry );
			AppendToFile( ActivityLogFile, logEntry );

			// Discord webhook (if enabled)
			if ( _core.EnableDiscordWebhook && !string.IsNullOrEmpty( _core.DiscordWebhookUrl ) )
			{
				SendDiscordWebhook( $"🚨 **Anti-Cheat Detection**\n**Player:** {playerName} ({steamId})\n**Detection:** {detection}\n**Details:** {details}" );
			}
		}
		catch ( Exception ex )
		{
			Log.Warning( $"⚠️ HEX Logger: Failed to log anti-cheat - {ex.Message}" );
		}
	}

	/// <summary>
	/// Log command execution
	/// </summary>
	public void LogCommand( string executor, string steamId, string command, string target = "" )
	{
		if ( !_core.EnableLogging ) return;

		try
		{
			string timestamp = DateTime.UtcNow.ToString( "yyyy-MM-dd HH:mm:ss" );
			string targetStr = string.IsNullOrEmpty( target ) ? "" : $" → {target}";
			string logEntry = $"[{timestamp}] CMD | {executor} ({steamId}) | {command}{targetStr}\n";
			
			AppendToFile( CommandLogFile, logEntry );
			AppendToFile( ActivityLogFile, logEntry );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"⚠️ HEX Logger: Failed to log command - {ex.Message}" );
		}
	}

	/// <summary>
	/// Get recent activity log
	/// </summary>
	public string GetRecentActivity( int lineCount = 100 )
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

	/// <summary>
	/// Get recent anti-cheat log
	/// </summary>
	public string GetRecentAntiCheat( int lineCount = 50 )
	{
		try
		{
			if ( !FileSystem.Data.FileExists( AntiCheatLogFile ) )
				return "No anti-cheat detections logged.";

			string log = FileSystem.Data.ReadAllText( AntiCheatLogFile );
			var lines = log.Split( '\n' );
			var recentLines = lines.TakeLast( lineCount );
			
			return string.Join( "\n", recentLines );
		}
		catch ( Exception ex )
		{
			return $"Error reading log: {ex.Message}";
		}
	}

	// ---- Helper Methods ----

	void AppendToFile( string filePath, string content )
	{
		string existingContent = "";
		if ( FileSystem.Data.FileExists( filePath ) )
		{
			existingContent = FileSystem.Data.ReadAllText( filePath );
		}

		FileSystem.Data.WriteAllText( filePath, existingContent + content );
	}

	void SendDiscordWebhook( string message )
	{
		// TODO: Implement Discord webhook
		// For now, just log it
		Log.Info( $"📢 Discord Webhook: {message}" );
	}
}

