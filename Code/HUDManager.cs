using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Player;
using System.Linq;

/// <summary>
/// Manages HUD updates and links the player to the HUD.
/// Based on SFO's HUDManagerComponent pattern.
/// Add this component to your player prefab!
/// </summary>
public sealed class HUDManager : Component
{
	private PlayerVeggaStats _playerStats;
	private PlayerHud _hudPanel;
	private Sandbox.UI.PlayerVeggaModularHud _modularHudPanel;

	protected override void OnStart()
	{
			// Only run on the local owner's instance.
			// On the host we have components for all players, but only the one
			// owned by Connection.Local should ever touch the local HUD.
			var localConn = Connection.Local;
			if ( localConn == null )
				return;

		// Get the player's stats component
		_playerStats = Components.Get<PlayerVeggaStats>();

			// If this stats component doesn't belong to our local connection,
			// do not bind it to the HUD on this client.
			if ( _playerStats == null || _playerStats.Network.Owner != localConn )
				return;

		// Ensure HUD layout edit mode is off when the local HUD comes online,
		// so we never spawn directly into layout mode from a previous session.
		VeggaHudLayoutState.SetActive( false );

		// Find the PlayerHud component in the scene (it should be on a ScreenPanel)
		_modularHudPanel = Scene.GetAllComponents<Sandbox.UI.PlayerVeggaModularHud>().FirstOrDefault();
		_hudPanel = Scene.GetAllComponents<PlayerHud>().FirstOrDefault();

		// Prefer modular HUD when available.
		if ( _modularHudPanel != null && _hudPanel != null && _hudPanel.Enabled )
		{
			_hudPanel.Enabled = false;
			Log.Info( "[HUDManager] Disabled legacy PlayerHud (modular HUD present)." );
		}

		// Link the player stats to the preferred HUD.
		if ( _playerStats != null )
		{
			if ( _modularHudPanel != null )
			{
				_modularHudPanel.PlayerStats = _playerStats;
				Log.Info( "✅ HUDManager: Linked player stats to modular HUD!" );
			}
			else if ( _hudPanel != null )
			{
				_hudPanel.PlayerStats = _playerStats;
				Log.Info( "✅ HUDManager: Linked player stats to legacy HUD!" );
			}
			else
			{
				Log.Warning( "⚠️ HUDManager: No HUD found to link." );
			}
		}
		else
		{
			Log.Warning( "⚠️ HUDManager: Player stats missing; cannot link HUD." );
		}
	}
}

