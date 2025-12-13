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
	public Guid MessageId { get; set; } = Guid.NewGuid();
	public string SenderName { get; set; }
	public Guid SenderId { get; set; }
	public string Text { get; set; }
	public DateTime Time { get; set; }
	public ChatMessageType Type { get; set; }
	public bool IsPrivate { get; set; }
	public string TargetName { get; set; } // For PMs
	public Dictionary<string, int> Reactions { get; set; } = new();
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
	/// Event when any existing message changes (eg. reactions updated).
	/// </summary>
	public Action<ChatMessage> OnMessagesChanged;

	/// <summary>
	/// Local singleton (per-client) used by UI.
	/// </summary>
	private static VeggaChatManager _local;
	public static VeggaChatManager Local
	{
		get
		{
			var localConn = Connection.Local;
			if ( localConn != null && _local != null && _local.IsValid() && _local.Network.Owner == localConn )
				return _local;

			// Clear invalid cache
			if ( _local != null && !_local.IsValid() )
				_local = null;

			var scene = Game.ActiveScene;
			if ( scene is null ) return _local;

			// Prefer the chat manager owned by our local connection
			if ( localConn != null )
			{
				foreach ( var mgr in scene.GetAllComponents<VeggaChatManager>() )
				{
					if ( mgr.IsValid() && mgr.Network.Owner == localConn )
					{
						_local = mgr;
						return _local;
					}
				}
			}

			// Fallback: first valid instance (useful in editor/SP edge-cases)
			_local = scene.GetAllComponents<VeggaChatManager>().FirstOrDefault( m => m.IsValid() );
			return _local;
		}
	}

	/// <summary>
	/// Command registry.
	/// </summary>
	private static Dictionary<string, ChatCommand> _commands = new();
	private static bool _defaultCommandsRegistered;

	public static IReadOnlyCollection<ChatCommand> GetRegisteredCommands()
	{
		return _commands.Values.ToList();
	}

	protected override void OnStart()
	{
		// Register default commands once (static registry)
		if ( !_defaultCommandsRegistered )
		{
			_defaultCommandsRegistered = true;
			RegisterDefaultCommands();
		}
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

			if ( !TryResolvePmTarget( args, out var targetName, out var message ) )
			{
				AddLocalMessage( "Usage: /pm <player> <message>", ChatMessageType.Error );
				return;
			}

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
		var senderName = Connection.Local?.DisplayName;
		if ( string.IsNullOrWhiteSpace( senderName ) )
		{
			var playerStats = PlayerVeggaStats.Local;
			senderName = playerStats?.Network?.Owner?.DisplayName;
		}
		senderName ??= "Unknown";

		// Connection.Local?.Id is Guid? so fall back to Guid.Empty instead of an int
		// Use a stable message id so all clients can reference this message (eg. reactions)
		var messageId = Guid.NewGuid();
		BroadcastMessage( messageId, senderName, Connection.Local?.Id ?? Guid.Empty, text, ChatMessageType.Normal );
	}

	void ProcessCommand( string input )
	{
		// If the user typed only the prefix, show help.
		if ( input == "/" )
		{
			AddLocalMessage( "=== Chat Commands ===", ChatMessageType.System );
			foreach ( var chatCmd in _commands.Values.OrderBy( c => c.Name ) )
			{
				AddLocalMessage( $"/{chatCmd.Name} {chatCmd.Usage} - {chatCmd.Description}", ChatMessageType.System );
			}
			AddLocalMessage( "Admin: /hex <command> ... (or !hex <command> ...)", ChatMessageType.System );
			return;
		}
		if ( input == "!" )
		{
			AddLocalMessage( "Admin commands require !hex <command> ... (or /hex <command> ...) ", ChatMessageType.System );
			AddLocalMessage( "Type !hex help to list admin commands.", ChatMessageType.System );
			return;
		}

		// Remove prefix
		var text = input.Substring( 1 );
		var parts = text.Split( ' ', StringSplitOptions.RemoveEmptyEntries );

		if ( parts.Length == 0 ) return;

		var cmdName = parts[0].ToLower();
		var args = parts.Skip( 1 ).ToArray();

		// Admin commands: only via !hex or /hex
		// Exception: allow !pm as a convenience alias for /pm
		if ( input.StartsWith( "!" ) )
		{
			if ( cmdName == "pm" )
			{
				if ( _commands.TryGetValue( "pm", out var pmCmd ) )
				{
					pmCmd.Handler?.Invoke( args );
					return;
				}
			}

			if ( cmdName != "hex" )
			{
				AddLocalMessage( "Admin commands require !hex <command> ...", ChatMessageType.Error );
				return;
			}

			if ( args.Length == 0 )
			{
				AddLocalMessage( "Type !hex help to list admin commands.", ChatMessageType.System );
				return;
			}

			var adminCmd = args[0];
			var adminArgs = args.Skip( 1 ).ToArray();
			ProcessAdminCommand( adminCmd, adminArgs );
			return;
		}
		if ( input.StartsWith( "/" ) && cmdName == "hex" )
		{
			if ( args.Length == 0 )
			{
				AddLocalMessage( "Type /hex help to list admin commands.", ChatMessageType.System );
				return;
			}
			var adminCmd = args[0];
			var adminArgs = args.Skip( 1 ).ToArray();
			ProcessAdminCommand( adminCmd, adminArgs );
			return;
		}

		// Regular commands (/)
		if ( _commands.TryGetValue( cmdName, out var resolvedCmd ) )
		{
			if ( resolvedCmd.AdminOnly && !IsAdmin() )
			{
				AddLocalMessage( "You don't have permission to use this command.", ChatMessageType.Error );
				return;
			}

			resolvedCmd.Handler?.Invoke( args );
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
	void BroadcastMessage( Guid messageId, string senderName, Guid senderId, string text, ChatMessageType type )
	{
		var msg = new ChatMessage
		{
			MessageId = messageId,
			SenderName = senderName,
			SenderId = senderId,
			Text = text,
			Time = DateTime.Now,
			Type = type,
			IsPrivate = false
		};

		// IMPORTANT:
		// This RPC executes on the sender's replicated component instance on each client.
		// We want all messages to land in the *local* manager instance that the UI reads.
		// Otherwise remote clients store messages on a different per-player component and
		// the UI never sees them.
		var target = Local;
		if ( target != null )
		{
			target.AddMessage( msg );
		}
		else
		{
			AddMessage( msg );
		}
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
		var messageId = Guid.NewGuid();

		// Send to target
		SendPrivateMessageRpc( targetStats.Network.Owner.Id, messageId, senderName, message );

		// Show in our chat
		AddLocalMessage( $"[PM to {targetStats.Network.Owner.DisplayName}] {message}", ChatMessageType.Private );
	}

	[Rpc.Broadcast]
	void SendPrivateMessageRpc( Guid targetConnectionId, Guid messageId, string senderName, string message )
	{
		// Only process if we're the target
		if ( Connection.Local?.Id != targetConnectionId ) return;

		var msg = new ChatMessage
		{
			MessageId = messageId,
			SenderName = senderName,
			Text = message,
			Time = DateTime.Now,
			Type = ChatMessageType.Private,
			IsPrivate = true
		};

		var target = Local;
		if ( target != null )
		{
			target.AddMessage( msg );
		}
		else
		{
			AddMessage( msg );
		}
	}

	void SendActionMessage( string action )
	{
		var playerStats = PlayerVeggaStats.Local;
		var senderName = playerStats?.Network?.Owner?.DisplayName ?? "Unknown";
		var messageId = Guid.NewGuid();

		// Use Guid.Empty as fallback for missing connection id
		BroadcastMessage( messageId, senderName, Connection.Local?.Id ?? Guid.Empty, $"* {senderName} {action}", ChatMessageType.Action );
	}

	/// <summary>
	/// Add a local-only message (system, error, etc.)
	/// </summary>
	public void AddLocalMessage( string text, ChatMessageType type = ChatMessageType.System )
	{
		var msg = new ChatMessage
		{
			MessageId = Guid.NewGuid(),
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
		OnMessagesChanged?.Invoke( msg );
	}

	public void AddReaction( Guid messageId, string reactionKey )
	{
		if ( messageId == Guid.Empty ) return;
		if ( string.IsNullOrWhiteSpace( reactionKey ) ) return;

		AddReactionRpc( messageId, reactionKey );
	}

	[Rpc.Broadcast]
	void AddReactionRpc( Guid messageId, string reactionKey )
	{
		var target = Local ?? this;
		if ( target == null ) return;

		target.ApplyReaction( messageId, reactionKey );
	}

	void ApplyReaction( Guid messageId, string reactionKey )
	{
		var msg = Messages.FirstOrDefault( m => m.MessageId == messageId );
		if ( msg == null ) return;

		if ( msg.Reactions == null )
		{
			msg.Reactions = new Dictionary<string, int>();
		}

		if ( !msg.Reactions.TryGetValue( reactionKey, out var count ) )
		{
			count = 0;
		}

		msg.Reactions[reactionKey] = count + 1;
		OnMessagesChanged?.Invoke( msg );
	}

	bool TryResolvePmTarget( string[] args, out string targetName, out string message )
	{
		targetName = null;
		message = null;
		if ( args == null || args.Length < 2 ) return false;

		var scene = Game.ActiveScene;
		if ( scene is null ) return false;

		var displayNames = scene.GetAllComponents<PlayerVeggaStats>()
			.Select( s => s?.Network?.Owner?.DisplayName )
			.Where( n => !string.IsNullOrWhiteSpace( n ) )
			.Distinct()
			.ToList();

		// Try to match the longest possible target name prefix.
		for ( int take = args.Length - 1; take >= 1; take-- )
		{
			var candidate = string.Join( " ", args.Take( take ) );
			if ( displayNames.Any( n => string.Equals( n, candidate, System.StringComparison.OrdinalIgnoreCase ) ) )
			{
				targetName = candidate;
				message = string.Join( " ", args.Skip( take ) );
				return !string.IsNullOrWhiteSpace( message );
			}
		}

		// Fallback: partial match using the longest prefix first.
		for ( int take = args.Length - 1; take >= 1; take-- )
		{
			var candidate = string.Join( " ", args.Take( take ) );
			var resolved = FindPlayerByName( candidate );
			if ( resolved != null )
			{
				targetName = resolved.Network?.Owner?.DisplayName ?? candidate;
				message = string.Join( " ", args.Skip( take ) );
				return !string.IsNullOrWhiteSpace( message );
			}
		}

		return false;
	}

	PlayerVeggaStats FindPlayerByName( string name )
	{
		name = name.ToLower();

		var scene = Game.ActiveScene;
		if ( scene is null ) return null;

		// Find all players
		var allStats = scene.GetAllComponents<PlayerVeggaStats>();

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
