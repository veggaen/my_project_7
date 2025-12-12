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
		_hudPanel = Scene.GetAllComponents<PlayerHud>().FirstOrDefault();

		// Link the player stats to the HUD
		if ( _hudPanel != null && _playerStats != null )
		{
			_hudPanel.PlayerStats = _playerStats;
			Log.Info( "✅ HUDManager: Linked player stats to HUD!" );
		}
		else
		{
			Log.Warning( $"⚠️ HUDManager: Could not link HUD. HUD found: {_hudPanel != null}, Stats found: {_playerStats != null}" );
		}
	}
}

