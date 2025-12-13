using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Admin;

namespace Sandbox;

/// <summary>
/// Admin management system. Handles admin commands like !kick, !ban, !freeze, etc.
/// </summary>
public static class VeggaAdminManager
{
	private static HashSet<Guid> _admins = new();
	private static Dictionary<string, AdminCommand> _commands = new();

	/// <summary>
	/// Initialize admin commands.
	/// </summary>
	static VeggaAdminManager()
	{
		RegisterDefaultCommands();
	}

	static void RegisterDefaultCommands()
	{
		// Menu command
		Register( "menu", "Open admin menu", "", ( executor, args ) =>
		{
			// TODO: Open admin menu UI
			ChatMsg( executor, "Admin menu coming soon!", ChatMessageType.Admin );
		} );

		// Kick command
		Register( "kick", "Kick a player", "<player> [reason]", ( executor, args ) =>
		{
			if ( args.Length < 1 )
			{
				ChatMsg( executor, "Usage: !hex kick <player> [reason]", ChatMessageType.Error );
				return;
			}

			var target = FindPlayer( args[0] );
			if ( target == null )
			{
				ChatMsg( executor, $"Player '{args[0]}' not found.", ChatMessageType.Error );
				return;
			}

			if ( IsProtectedOwner( target, executor ) ) return;

			var reason = args.Length > 1 ? string.Join( " ", args.Skip( 1 ) ) : "Kicked by admin";
			KickPlayer( target, reason );
			ChatMsgAll( $"{target.Network.Owner.DisplayName} was kicked: {reason}", ChatMessageType.Admin );
		} );

		// Freeze command
		Register( "freeze", "Freeze a player", "<player>", ( executor, args ) =>
		{
			if ( args.Length < 1 )
			{
				ChatMsg( executor, "Usage: !hex freeze <player>", ChatMessageType.Error );
				return;
			}

			var target = FindPlayer( args[0] );
			if ( target == null )
			{
				ChatMsg( executor, $"Player '{args[0]}' not found.", ChatMessageType.Error );
				return;
			}

			if ( IsProtectedOwner( target, executor ) ) return;

			var movement = target.Components.Get<PlayerVeggaMovement>();
			if ( movement != null )
			{
				movement.Enabled = false;
				ChatMsgAll( $"{target.Network.Owner.DisplayName} was frozen by admin.", ChatMessageType.Admin );
			}
		} );

		// Unfreeze command
		Register( "unfreeze", "Unfreeze a player", "<player>", ( executor, args ) =>
		{
			if ( args.Length < 1 )
			{
				ChatMsg( executor, "Usage: !hex unfreeze <player>", ChatMessageType.Error );
				return;
			}

			var target = FindPlayer( args[0] );
			if ( target == null )
			{
				ChatMsg( executor, $"Player '{args[0]}' not found.", ChatMessageType.Error );
				return;
			}

			if ( IsProtectedOwner( target, executor ) ) return;

			var movement = target.Components.Get<PlayerVeggaMovement>();
			if ( movement != null )
			{
				movement.Enabled = true;
				ChatMsgAll( $"{target.Network.Owner.DisplayName} was unfrozen by admin.", ChatMessageType.Admin );
			}
		} );

		// Heal command
		Register( "heal", "Heal a player", "<player> [amount]", ( executor, args ) =>
		{
			if ( args.Length < 1 )
			{
				ChatMsg( executor, "Usage: !hex heal <player> [amount]", ChatMessageType.Error );
				return;
			}

			var target = FindPlayer( args[0] );
			if ( target == null )
			{
				ChatMsg( executor, $"Player '{args[0]}' not found.", ChatMessageType.Error );
				return;
			}

			if ( IsProtectedOwner( target, executor ) ) return;

			int amount = 100;
			if ( args.Length > 1 && int.TryParse( args[1], out int parsed ) )
			{
				amount = parsed;
			}

			target.Heal( amount );
			ChatMsg( executor, $"Healed {target.Network.Owner.DisplayName} for {amount} HP.", ChatMessageType.Admin );
		} );

		// Damage command
		Register( "damage", "Damage a player", "<player> <amount> [armorpen%]", ( executor, args ) =>
		{
			if ( args.Length < 2 )
			{
				ChatMsg( executor, "Usage: !hex damage <player> <amount> [armorpen%]", ChatMessageType.Error );
				return;
			}

			var target = FindPlayer( args[0] );
			if ( target == null )
			{
				ChatMsg( executor, $"Player '{args[0]}' not found.", ChatMessageType.Error );
				return;
			}

			if ( IsProtectedOwner( target, executor ) ) return;

			if ( !int.TryParse( args[1], out int damage ) )
			{
				ChatMsg( executor, "Invalid damage amount.", ChatMessageType.Error );
				return;
			}

			float armorPen = 0f;
			if ( args.Length > 2 && float.TryParse( args[2], out float pen ) )
			{
				armorPen = pen / 100f;
			}

			// PlayerVeggaStats exposes Damage(amount, armorPen) instead of TakeDamage
			target.Damage( damage, armorPen );
			ChatMsg( executor, $"Dealt {damage} damage to {target.Network.Owner.DisplayName} ({armorPen * 100}% armor pen).", ChatMessageType.Admin );
		} );

		// Give money
		Register( "givemoney", "Give money to a player", "<player> <amount>", ( executor, args ) =>
		{
			if ( args.Length < 2 )
			{
				ChatMsg( executor, "Usage: !hex givemoney <player> <amount>", ChatMessageType.Error );
				return;
			}

			var target = FindPlayer( args[0] );
			if ( target == null )
			{
				ChatMsg( executor, $"Player '{args[0]}' not found.", ChatMessageType.Error );
				return;
			}

			if ( IsProtectedOwner( target, executor ) ) return;

			if ( !int.TryParse( args[1], out int amount ) )
			{
				ChatMsg( executor, "Invalid amount.", ChatMessageType.Error );
				return;
			}

			target.AddMoney( amount );
			ChatMsg( executor, $"Gave ${amount} to {target.Network.Owner.DisplayName}.", ChatMessageType.Admin );
		} );

		// Set money (use SetMoney instead of assigning Money directly)
		Register( "setmoney", "Set player's money", "<player> <amount>", ( executor, args ) =>
		{
			if ( args.Length < 2 )
			{
				ChatMsg( executor, "Usage: !hex setmoney <player> <amount>", ChatMessageType.Error );
				return;
			}

			var target = FindPlayer( args[0] );
			if ( target == null )
			{
				ChatMsg( executor, $"Player '{args[0]}' not found.", ChatMessageType.Error );
				return;
			}

			if ( IsProtectedOwner( target, executor ) ) return;

			if ( !int.TryParse( args[1], out int amount ) )
			{
				ChatMsg( executor, "Invalid amount.", ChatMessageType.Error );
				return;
			}

			// PlayerVeggaStats.Money has a private setter; use SetMoney helper
			target.SetMoney( amount );
			ChatMsg( executor, $"Set {target.Network.Owner.DisplayName}'s money to ${amount}.", ChatMessageType.Admin );
		} );

		// Slap command
		Register( "slap", "Slap a player (applies upward force)", "<player> [force]", ( executor, args ) =>
		{
			if ( args.Length < 1 )
			{
				ChatMsg( executor, "Usage: !hex slap <player> [force]", ChatMessageType.Error );
				return;
			}

			var target = FindPlayer( args[0] );
			if ( target == null )
			{
				ChatMsg( executor, $"Player '{args[0]}' not found.", ChatMessageType.Error );
				return;
			}

			if ( IsProtectedOwner( target, executor ) ) return;

			float force = 300f;
			if ( args.Length > 1 && float.TryParse( args[1], out float f ) )
			{
				force = f;
			}

			var cc = target.Components.Get<CharacterController>();
			if ( cc != null )
			{
				cc.Punch( Vector3.Up * force + Vector3.Random * force * 0.3f );
			}

			// Use Damage instead of TakeDamage
			target.Damage( 5, 0f );
			ChatMsgAll( $"{target.Network.Owner.DisplayName} was slapped!", ChatMessageType.Admin );
		} );

		// Teleport
		Register( "tp", "Teleport to a player", "<player>", ( executor, args ) =>
		{
			if ( args.Length < 1 )
			{
				ChatMsg( executor, "Usage: !hex tp <player>", ChatMessageType.Error );
				return;
			}

			var target = FindPlayer( args[0] );
			if ( target == null )
			{
				ChatMsg( executor, $"Player '{args[0]}' not found.", ChatMessageType.Error );
				return;
			}

			var localPlayer = PlayerVeggaStats.Local;
			if ( localPlayer != null )
			{
				localPlayer.WorldPosition = target.WorldPosition + Vector3.Up * 10f;
				ChatMsg( executor, $"Teleported to {target.Network.Owner.DisplayName}.", ChatMessageType.Admin );
			}
		} );

		// Bring
		Register( "bring", "Bring a player to you", "<player>", ( executor, args ) =>
		{
			if ( args.Length < 1 )
			{
				ChatMsg( executor, "Usage: !hex bring <player>", ChatMessageType.Error );
				return;
			}

			var target = FindPlayer( args[0] );
			if ( target == null )
			{
				ChatMsg( executor, $"Player '{args[0]}' not found.", ChatMessageType.Error );
				return;
			}

			if ( IsProtectedOwner( target, executor ) ) return;

			var localPlayer = PlayerVeggaStats.Local;
			if ( localPlayer != null )
			{
				target.WorldPosition = localPlayer.WorldPosition + localPlayer.WorldRotation.Forward * 100f;
				ChatMsg( executor, $"Brought {target.Network.Owner.DisplayName} to you.", ChatMessageType.Admin );
			}
		} );

		// God mode
		Register( "god", "Toggle god mode", "[player]", ( executor, args ) =>
		{
			var target = args.Length > 0 ? FindPlayer( args[0] ) : PlayerVeggaStats.Local;
			if ( target == null )
			{
				ChatMsg( executor, $"Player not found.", ChatMessageType.Error );
				return;
			}

			// Toggle god mode (set max health very high)
			if ( target.MaxHealth > 10000 )
			{
				target.MaxHealth = 100;
				// Use SetHealth helper instead of writing Health directly
				target.SetHealth( 100 );
				ChatMsg( executor, $"God mode disabled for {target.Network?.Owner?.DisplayName ?? "you"}.", ChatMessageType.Admin );
			}
			else
			{
				target.MaxHealth = 999999;
				// Use SetHealth helper instead of writing Health directly
				target.SetHealth( 999999 );
				ChatMsg( executor, $"God mode enabled for {target.Network?.Owner?.DisplayName ?? "you"}.", ChatMessageType.Admin );
			}
		} );

		// NOTE: setjob and noclip commands are temporarily disabled until
		// a proper job-changing API and modern noclip implementation are added.

		// Respawn
		Register( "respawn", "Respawn a player", "<player>", ( executor, args ) =>
		{
			if ( args.Length < 1 )
			{
				ChatMsg( executor, "Usage: !hex respawn <player>", ChatMessageType.Error );
				return;
			}

			var target = FindPlayer( args[0] );
			if ( target == null )
			{
				ChatMsg( executor, $"Player '{args[0]}' not found.", ChatMessageType.Error );
				return;
			}

			if ( IsProtectedOwner( target, executor ) ) return;

			var death = target.Components.Get<PlayerVeggaDeath>();
			if ( death != null )
			{
				death.ForceRespawn();
				ChatMsg( executor, $"Respawned {target.Network.Owner.DisplayName}.", ChatMessageType.Admin );
			}
		} );

		// Help
		Register( "help", "Show admin commands", "", ( executor, args ) =>
		{
			ChatMsg( executor, "=== HEX Admin Commands ===", ChatMessageType.Admin );
			foreach ( var cmd in _commands.Values.OrderBy( c => c.Name ) )
			{
				var usage = string.IsNullOrWhiteSpace( cmd.Usage ) ? "" : $" {cmd.Usage}";
				ChatMsg( executor, $"!hex {cmd.Name}{usage} - {cmd.Description}", ChatMessageType.Admin );
			}
			ChatMsg( executor, "Also works as: /hex <command> ...", ChatMessageType.Admin );
		} );

		// Give item command
		Register( "give", "Give item to player", "<player|ent> <itemId> [amount]", ( executor, args ) =>
		{
			if ( args.Length < 2 )
			{
				ChatMsg( executor, "Usage: !hex give <player|ent> <itemId> [amount]", ChatMessageType.Error );
				ChatMsg( executor, "Examples: !hex give ent 100 (gold bar to self), !hex give v3gga 100 5 (5 gold bars to v3gga)", ChatMessageType.Error );
				return;
			}

			// Parse target - "ent" means self
			PlayerVeggaStats target;
			if ( args[0].ToLower() == "ent" || args[0].ToLower() == "me" || args[0].ToLower() == "self" )
			{
				target = PlayerVeggaStats.Local;
			}
			else
			{
				target = FindPlayer( args[0] );
			}

			if ( target == null )
			{
				ChatMsg( executor, $"Player '{args[0]}' not found.", ChatMessageType.Error );
				return;
			}

			if ( IsProtectedOwner( target, executor ) ) return;

			// Parse item ID
			if ( !int.TryParse( args[1], out int itemId ) )
			{
				ChatMsg( executor, "Invalid item ID.", ChatMessageType.Error );
				return;
			}

			// Check if item exists
			var itemDef = VeggaItemRegistry.Get( itemId );
			if ( itemDef == null )
			{
				ChatMsg( executor, $"Item ID {itemId} not found in registry.", ChatMessageType.Error );
				ChatMsg( executor, "Known items: 1=Gold Coin, 100=200g Gold Bar, 101=500g Gold Bar", ChatMessageType.Error );
				return;
			}

			// Parse amount
			int amount = 1;
			if ( args.Length > 2 && int.TryParse( args[2], out int parsedAmount ) )
			{
				amount = parsedAmount;
			}

			// Get inventory and add item
			var inventory = target.Components.Get<VeggaInventory>();
			if ( inventory == null )
			{
				ChatMsg( executor, $"Player has no inventory!", ChatMessageType.Error );
				return;
			}

			if ( inventory.AddItem( itemId, amount ) )
			{
				var targetName = target == PlayerVeggaStats.Local ? "yourself" : target.Network?.Owner?.DisplayName ?? "player";
				ChatMsg( executor, $"Gave {amount}x {itemDef.Name} to {targetName}.", ChatMessageType.Admin );
			}
			else
			{
				ChatMsg( executor, "Failed to add item - inventory may be full.", ChatMessageType.Error );
			}
		} );

		// List items command
		Register( "items", "List all available items", "", ( executor, args ) =>
		{
			ChatMsg( executor, "=== Available Items ===", ChatMessageType.Admin );
			foreach ( var item in VeggaItemRegistry.GetAll() )
			{
				ChatMsg( executor, $"ID {item.Id}: {item.Name} (${item.Value}) - {item.Category}", ChatMessageType.Admin );
			}
		} );

		// Spawn item in world command
		Register( "spawnitem", "Spawn item in world at your position", "<itemId> [amount]", ( executor, args ) =>
		{
			if ( args.Length < 1 )
			{
				ChatMsg( executor, "Usage: !hex spawnitem <itemId> [amount]", ChatMessageType.Error );
				return;
			}

			if ( !int.TryParse( args[0], out int itemId ) )
			{
				ChatMsg( executor, "Invalid item ID.", ChatMessageType.Error );
				return;
			}

			var itemDef = VeggaItemRegistry.Get( itemId );
			if ( itemDef == null )
			{
				ChatMsg( executor, $"Item ID {itemId} not found.", ChatMessageType.Error );
				return;
			}

			int amount = 1;
			if ( args.Length > 1 && int.TryParse( args[1], out int a ) )
			{
				amount = a;
			}

			// Spawn at player position
			var player = PlayerVeggaStats.Local;
			if ( player == null ) return;

			var spawnPos = player.WorldPosition + player.WorldRotation.Forward * 50f + Vector3.Up * 30f;

			// Create pickup GameObject
			var go = new GameObject( true, $"Pickup_{itemDef.Name}" );
			go.WorldPosition = spawnPos;

			// Add pickup component
			var pickup = go.Components.Create<VeggaPickupItem>();
			pickup.ItemId = itemId;
			pickup.Quantity = amount;

			// Add model if available
			if ( !string.IsNullOrEmpty( itemDef.ModelPath ) )
			{
				var renderer = go.Components.Create<ModelRenderer>();
				renderer.Model = Model.Load( itemDef.ModelPath );
			}

			// Add physics
			var rb = go.Components.Create<Rigidbody>();
			rb.Gravity = true;

			var collider = go.Components.Create<BoxCollider>();
			collider.Scale = new Vector3( 10, 10, 10 );

			ChatMsg( executor, $"Spawned {amount}x {itemDef.Name} in world.", ChatMessageType.Admin );
		} );

		// Smelt 200g gold bar(s) into coins over time
		Register( "smeltbar", "Smelt 200g Gold Bar into coins", "<player|ent> [bars]", ( executor, args ) =>
		{
			if ( args.Length < 1 )
			{
				ChatMsg( executor, "Usage: !hex smeltbar <player|ent> [bars]", ChatMessageType.Error );
				ChatMsg( executor, "Example: !hex smeltbar ent 1 (smelt one bar for yourself)", ChatMessageType.Error );
				return;
			}

			// Resolve target player ("ent"/"me"/"self" = local)
			PlayerVeggaStats target;
			if ( args[0].ToLower() == "ent" || args[0].ToLower() == "me" || args[0].ToLower() == "self" )
			{
				target = PlayerVeggaStats.Local;
			}
			else
			{
				target = FindPlayer( args[0] );
			}

			if ( target == null )
			{
				ChatMsg( executor, $"Player '{args[0]}' not found.", ChatMessageType.Error );
				return;
			}

			if ( IsProtectedOwner( target, executor ) ) return;

			int barsToSmelt = 1;
			if ( args.Length > 1 && !int.TryParse( args[1], out barsToSmelt ) )
			{
				ChatMsg( executor, "Invalid bar count.", ChatMessageType.Error );
				return;
			}

			if ( barsToSmelt <= 0 )
			{
				ChatMsg( executor, "Bar count must be positive.", ChatMessageType.Error );
				return;
			}

			const int GoldBarItemId = 100; // 200g Gold Bar
			var inventory = target.Components.Get<VeggaInventory>();
			if ( inventory == null )
			{
				ChatMsg( executor, "Player has no inventory!", ChatMessageType.Error );
				return;
			}

			int availableBars = inventory.GetItemCount( GoldBarItemId );
			if ( availableBars <= 0 )
			{
				ChatMsg( executor, "Player has no 200g Gold Bars to smelt.", ChatMessageType.Error );
				return;
			}

			if ( availableBars < barsToSmelt )
			{
				ChatMsg( executor, $"Player only has {availableBars}x 200g Gold Bar(s).", ChatMessageType.Error );
				return;
			}

			var barDef = VeggaItemRegistry.Get( GoldBarItemId );
			if ( barDef == null )
			{
				ChatMsg( executor, "Gold bar item definition not found.", ChatMessageType.Error );
				return;
			}

			// Admin helper: just start smelting the first bar using forge speed
			// regardless of furnace presence – useful for testing.
			var smelter = target.GameObject.Components.Get<PlayerGoldSmelter>() ?? target.GameObject.Components.Create<PlayerGoldSmelter>();
			smelter.StartSmeltingFirstGoldBar( useForgeSpeed: true );

			var targetName = target == PlayerVeggaStats.Local ? "yourself" : target.Network?.Owner?.DisplayName ?? "player";
			ChatMsg( executor, $"Smelting gold bar for {targetName} (admin fast mode).", ChatMessageType.Admin );
		} );
		// === HEX OWNER MANAGEMENT ===
		Register( "setowner", "Set a player to Owner rank (HEX)", "<player>", ( executor, args ) =>
		{
			if ( args.Length < 1 ) { ChatMsg( executor, "Usage: !hex setowner <player>", ChatMessageType.Error ); return; }
			var target = FindPlayer( args[0] );
			if ( target == null ) { ChatMsg( executor, "Player not found.", ChatMessageType.Error ); return; }
			var steamId = target.Network?.Owner?.SteamId.ToString();
			if ( string.IsNullOrEmpty( steamId ) ) { ChatMsg( executor, "Target has no steamid.", ChatMessageType.Error ); return; }

			HexPermissions.AddOwnerSteamId( steamId );

			// Persist rank change into player data
			var data = Sandbox.Data.PlayerDataManager.LoadPlayerData( steamId );
			data.Rank = "Owner";
			Sandbox.Data.PlayerDataManager.SavePlayerData( steamId, data );

			// Update live session if available
			var session = target.GameObject?.Components.Get<PlayerSessionStats>();
			if ( session != null ) session.Rank = "Owner";

			ChatMsgAll( $"{target.Network?.Owner?.DisplayName} is now OWNER (HEX).", ChatMessageType.Admin );
		} );

		Register( "ownerbypass", "Temporarily allow targeting owners (hours)", "<hours>", ( executor, args ) =>
		{
			if ( args.Length < 1 || !int.TryParse( args[0], out var hours ) )
			{ ChatMsg( executor, "Usage: !hex ownerbypass <hours>", ChatMessageType.Error ); return; }

			// Only an owner can toggle this
			var execSteamId = Connection.Local?.SteamId.ToString();
			if ( string.IsNullOrEmpty( execSteamId ) || !HexPermissions.IsOwnerSteamId( execSteamId ) )
			{ ChatMsg( executor, "Only an OWNER can toggle bypass.", ChatMessageType.Error ); return; }

			HexPermissions.EnableOwnerBypass( TimeSpan.FromHours( Math.Max( 1, hours ) ) );
			ChatMsgAll( $"HEX: Owner protection bypass enabled for {hours}h by {Connection.Local?.DisplayName}", ChatMessageType.Admin );
		} );
	}

	static bool IsProtectedOwner( PlayerVeggaStats target, Guid executor )
	{
		var steamId = target?.Network?.Owner?.SteamId.ToString();
		if ( string.IsNullOrEmpty( steamId ) ) return false;

		if ( HexPermissions.IsTargetOwnerProtected( steamId ) )
		{
			ChatMsg( executor, "HEX: Target is protected OWNER. Bypass is not active.", ChatMessageType.Error );
			return true;
		}

		return false;
	}

	/// <summary>
	/// Register an admin command.
	/// </summary>
	public static void Register( string name, string description, string usage, Action<Guid, string[]> handler )
	{
		var cmd = new AdminCommand
		{
			Name = name.ToLower(),
			Description = description,
			Usage = usage,
			Handler = handler
		};
		_commands[cmd.Name] = cmd;
	}

	/// <summary>
	/// Execute an admin command.
	/// </summary>
	public static void ExecuteCommand( string name, string[] args )
	{
		var executor = Connection.Local?.Id ?? Guid.Empty;

		if ( !IsAdmin( executor ) )
		{
			ChatMsg( executor, "You don't have permission to use admin commands.", ChatMessageType.Error );
			return;
		}

		name = name.ToLower();

		if ( _commands.TryGetValue( name, out var cmd ) )
		{
			cmd.Handler?.Invoke( executor, args );
		}
		else
		{
			ChatMsg( executor, $"Unknown admin command: {name}. Type !hex help for available commands.", ChatMessageType.Error );
		}
	}

	public static IReadOnlyCollection<AdminCommand> GetCommands()
	{
		return _commands.Values.ToList();
	}

	/// <summary>
	/// Check if a player is admin.
	/// </summary>
	public static bool IsAdmin( Guid connectionId )
	{
		// For now, everyone is admin in dev mode
		// TODO: Implement proper admin system with SteamID checks
		return true; // Development mode - everyone is admin
	}

	/// <summary>
	/// Add someone as admin.
	/// </summary>
	public static void AddAdmin( Guid connectionId )
	{
		_admins.Add( connectionId );
	}

	/// <summary>
	/// Remove admin status.
	/// </summary>
	public static void RemoveAdmin( Guid connectionId )
	{
		_admins.Remove( connectionId );
	}

	static PlayerVeggaStats FindPlayer( string name )
	{
		name = name.ToLower();

		var scene = Game.ActiveScene;
		if ( scene == null ) return null;

		var allStats = scene.GetAllComponents<PlayerVeggaStats>();

		// Exact match
		var exact = allStats.FirstOrDefault( p => p.Network?.Owner?.DisplayName?.ToLower() == name );
		if ( exact != null ) return exact;

		// Partial match
		return allStats.FirstOrDefault( p => p.Network?.Owner?.DisplayName?.ToLower()?.Contains( name ) == true );
	}

	static void KickPlayer( PlayerVeggaStats player, string reason )
	{
		// TODO: Implement proper kick via networking
		Log.Info( $"[Admin] Kicking {player.Network?.Owner?.DisplayName}: {reason}" );
	}

	static void ChatMsg( Guid executor, string message, ChatMessageType type )
	{
		var chat = VeggaChatManager.Local;
		chat?.AddLocalMessage( message, type );
	}

	static void ChatMsgAll( string message, ChatMessageType type )
	{
		// Broadcast to all players
		var chat = VeggaChatManager.Local;
		chat?.AddLocalMessage( message, type );
		// TODO: Use RPC to broadcast to all
	}
}

public class AdminCommand
{
	public string Name { get; set; }
	public string Description { get; set; }
	public string Usage { get; set; }
	public Action<Guid, string[]> Handler { get; set; }
}
