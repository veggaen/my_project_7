using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Chat message data structure.
/// </summary>
public class ChatMessage
{
	public string SenderName { get; set; }
	public Guid SenderId { get; set; }
	public string Text { get; set; }
	public DateTime Time { get; set; }
	public ChatMessageType Type { get; set; }
	public bool IsPrivate { get; set; }
	public string TargetName { get; set; } // For PMs
}

public enum ChatMessageType
{
	Normal,
	System,
	Admin,
	Private,
	Action,  // /me
	Error
}

/// <summary>
/// Chat manager component. Handles sending/receiving messages and commands.
/// Attach to the player or scene.
/// </summary>
public sealed class VeggaChatManager : Component
{
	[Property] public int MaxMessages { get; set; } = 100;
	[Property] public float MessageFadeTime { get; set; } = 8f;

	/// <summary>
	/// All chat messages.
	/// </summary>
	public List<ChatMessage> Messages { get; private set; } = new();

	/// <summary>
	/// Event when a new message is received.
	/// </summary>
	public Action<ChatMessage> OnMessageReceived;

	/// <summary>
	/// Local singleton.
	/// </summary>
	public static VeggaChatManager Local { get; private set; }

	/// <summary>
	/// Command registry.
	/// </summary>
	private static Dictionary<string, ChatCommand> _commands = new();

	protected override void OnStart()
	{
		if ( !IsProxy )
		{
			Local = this;
		}

		// Register default commands
		RegisterDefaultCommands();
	}

	void RegisterDefaultCommands()
	{
		// PM command
		RegisterCommand( "pm", "Send private message", "<player> <message>", ( args ) =>
		{
			if ( args.Length < 2 )
			{
				AddLocalMessage( "Usage: /pm <player> <message>", ChatMessageType.Error );
				return;
			}

			var targetName = args[0];
			var message = string.Join( " ", args.Skip( 1 ) );
			SendPrivateMessage( targetName, message );
		} );

		// Me action
		RegisterCommand( "me", "Perform an action", "<action>", ( args ) =>
		{
			if ( args.Length == 0 )
			{
				AddLocalMessage( "Usage: /me <action>", ChatMessageType.Error );
				return;
			}

			var action = string.Join( " ", args );
			SendActionMessage( action );
		} );

		// Clear chat
		RegisterCommand( "clear", "Clear chat history", "", ( args ) =>
		{
			Messages.Clear();
			AddLocalMessage( "Chat cleared.", ChatMessageType.System );
		} );

		// Help
		RegisterCommand( "help", "Show available commands", "", ( args ) =>
		{
			AddLocalMessage( "=== Available Commands ===", ChatMessageType.System );
			foreach ( var cmd in _commands.Values.OrderBy( c => c.Name ) )
			{
				AddLocalMessage( $"/{cmd.Name} {cmd.Usage} - {cmd.Description}", ChatMessageType.System );
			}
		} );
	}

	/// <summary>
	/// Register a chat command.
	/// </summary>
	public static void RegisterCommand( string name, string description, string usage, Action<string[]> handler, bool adminOnly = false )
	{
		var cmd = new ChatCommand
		{
			Name = name.ToLower(),
			Description = description,
			Usage = usage,
			Handler = handler,
			AdminOnly = adminOnly
		};
		_commands[cmd.Name] = cmd;
	}

	/// <summary>
	/// Send a chat message.
	/// </summary>
	public void SendMessage( string text )
	{
		if ( string.IsNullOrWhiteSpace( text ) ) return;

		text = text.Trim();

		// Check for commands
		if ( text.StartsWith( "/" ) || text.StartsWith( "!" ) )
		{
			ProcessCommand( text );
			return;
		}

		// Normal message
		var playerStats = PlayerVeggaStats.Local;
		var senderName = playerStats?.Network?.Owner?.DisplayName ?? "Unknown";

		// Connection.Local?.Id is Guid? so fall back to Guid.Empty instead of an int
		BroadcastMessage( senderName, Connection.Local?.Id ?? Guid.Empty, text, ChatMessageType.Normal );
	}

	void ProcessCommand( string input )
	{
		// Remove prefix
		var text = input.Substring( 1 );
		var parts = text.Split( ' ', StringSplitOptions.RemoveEmptyEntries );

		if ( parts.Length == 0 ) return;

		var cmdName = parts[0].ToLower();
		var args = parts.Skip( 1 ).ToArray();

		// Check for admin commands (!)
		if ( input.StartsWith( "!" ) )
		{
			ProcessAdminCommand( cmdName, args );
			return;
		}

		// Regular commands (/)
		if ( _commands.TryGetValue( cmdName, out var cmd ) )
		{
			if ( cmd.AdminOnly && !IsAdmin() )
			{
				AddLocalMessage( "You don't have permission to use this command.", ChatMessageType.Error );
				return;
			}

			cmd.Handler?.Invoke( args );
		}
		else
		{
			AddLocalMessage( $"Unknown command: /{cmdName}. Type /help for available commands.", ChatMessageType.Error );
		}
	}

	void ProcessAdminCommand( string cmdName, string[] args )
	{
		// Check admin status
		if ( !IsAdmin() )
		{
			AddLocalMessage( "You don't have permission to use admin commands.", ChatMessageType.Error );
			return;
		}

		// Route to admin manager
		VeggaAdminManager.ExecuteCommand( cmdName, args );
	}

	[Rpc.Broadcast]
	void BroadcastMessage( string senderName, Guid senderId, string text, ChatMessageType type )
	{
		var msg = new ChatMessage
		{
			SenderName = senderName,
			SenderId = senderId,
			Text = text,
			Time = DateTime.Now,
			Type = type,
			IsPrivate = false
		};

		AddMessage( msg );
	}

	void SendPrivateMessage( string targetName, string message )
	{
		// Find target player
		var targetStats = FindPlayerByName( targetName );
		if ( targetStats == null )
		{
			AddLocalMessage( $"Player '{targetName}' not found.", ChatMessageType.Error );
			return;
		}

		var senderName = PlayerVeggaStats.Local?.Network?.Owner?.DisplayName ?? "Unknown";

		// Send to target
		SendPrivateMessageRpc( targetStats.Network.Owner.Id, senderName, message );

		// Show in our chat
		AddLocalMessage( $"[PM to {targetStats.Network.Owner.DisplayName}] {message}", ChatMessageType.Private );
	}

	[Rpc.Broadcast]
	void SendPrivateMessageRpc( Guid targetConnectionId, string senderName, string message )
	{
		// Only process if we're the target
		if ( Connection.Local?.Id != targetConnectionId ) return;

		var msg = new ChatMessage
		{
			SenderName = senderName,
			Text = message,
			Time = DateTime.Now,
			Type = ChatMessageType.Private,
			IsPrivate = true
		};

		AddMessage( msg );
	}

	void SendActionMessage( string action )
	{
		var playerStats = PlayerVeggaStats.Local;
		var senderName = playerStats?.Network?.Owner?.DisplayName ?? "Unknown";

		// Use Guid.Empty as fallback for missing connection id
		BroadcastMessage( senderName, Connection.Local?.Id ?? Guid.Empty, $"* {senderName} {action}", ChatMessageType.Action );
	}

	/// <summary>
	/// Add a local-only message (system, error, etc.)
	/// </summary>
	public void AddLocalMessage( string text, ChatMessageType type = ChatMessageType.System )
	{
		var msg = new ChatMessage
		{
			SenderName = "System",
			Text = text,
			Time = DateTime.Now,
			Type = type
		};

		AddMessage( msg );
	}

	void AddMessage( ChatMessage msg )
	{
		Messages.Add( msg );

		// Trim old messages
		while ( Messages.Count > MaxMessages )
		{
			Messages.RemoveAt( 0 );
		}

		OnMessageReceived?.Invoke( msg );
	}

	PlayerVeggaStats FindPlayerByName( string name )
	{
		name = name.ToLower();

		// Find all players
		var allStats = Scene.GetAllComponents<PlayerVeggaStats>();

		// Exact match first
		var exact = allStats.FirstOrDefault( p => p.Network?.Owner?.DisplayName?.ToLower() == name );
		if ( exact != null ) return exact;

		// Partial match
		return allStats.FirstOrDefault( p => p.Network?.Owner?.DisplayName?.ToLower()?.Contains( name ) == true );
	}

	bool IsAdmin()
	{
		// Check if local player is admin
		return VeggaAdminManager.IsAdmin( Connection.Local?.Id ?? Guid.Empty );
	}
}

public class ChatCommand
{
	public string Name { get; set; }
	public string Description { get; set; }
	public string Usage { get; set; }
	public Action<string[]> Handler { get; set; }
	public bool AdminOnly { get; set; }
}
