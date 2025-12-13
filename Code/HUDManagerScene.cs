using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Player;
using System.Linq;

/// <summary>
/// Manages HUD in the scene. Add this to the UI Root GameObject in your scene.
/// It will automatically find the local player and link their stats to the HUD.
/// </summary>
public sealed class HUDManagerScene : Component
{
	/// <summary>
	/// Link to the PlayerHud component. Set this in the inspector!
	/// </summary>
	[Property, Group( "Links" )]
	public PlayerHud HudPanel { get; set; }

	/// <summary>
	/// Link to the new modular player HUD component (preferred). Set this in the inspector or let it auto-find.
	/// </summary>
	[Property, Group( "Links" )]
	public Sandbox.UI.PlayerVeggaModularHud ModularHudPanel { get; set; }

	/// <summary>
	/// Link to the InventoryHud component. Set this in the inspector!
	/// </summary>
	[Property, Group( "Links" )]
	public InventoryHud InventoryPanel { get; set; }

	/// <summary>
	/// Link to the CrossVeggaHair component. Set this in the inspector!
	/// </summary>
	[Property, Group( "Links" )]
	public CrossVeggaHair CrosshairPanel { get; set; }

	/// <summary>
	/// Link to the CrossVeggaHairMenu component. Set this in the inspector!
	/// </summary>
	[Property, Group( "Links" )]
	public CrossVeggaHairMenu CrosshairMenuPanel { get; set; }

	/// <summary>
	/// Link to the VeggaPauseMenu component. Set this in the inspector!
	/// </summary>
	[Property, Group( "Links" )]
	public VeggaPauseMenu PauseMenuPanel { get; set; }

	/// <summary>
	/// Link to the VeggaHudLayoutOverlay component. Set this in the inspector!
	/// </summary>
	[Property, Group( "Links" )]
	public VeggaHudLayoutOverlay HudLayoutOverlay { get; set; }

	/// <summary>
	/// Link to the TradeWindow component. Set this in the inspector!
	/// </summary>
	[Property, Group( "Links" )]
	public TradeWindow TradeWindowPanel { get; set; }

	/// <summary>
	/// Link to the PickupHint component. Set this in the inspector!
	/// </summary>
	[Property, Group( "Links" )]
	public PickupHint PickupHintPanel { get; set; }

	/// <summary>
	/// Link to the VeggaChat component. Set this in the inspector!
	/// </summary>
	[Property, Group( "Links" )]
	public VeggaChat ChatPanel { get; set; }

	private PlayerVeggaStats _currentPlayerStats;

	protected override void OnStart()
	{
		// Auto-find HUD components
		if ( HudPanel == null )
		{
			HudPanel = Components.Get<PlayerHud>();
		}

		if ( ModularHudPanel == null )
		{
			ModularHudPanel = Components.Get<Sandbox.UI.PlayerVeggaModularHud>();
		}

		if ( InventoryPanel == null )
		{
			InventoryPanel = Components.Get<InventoryHud>();
		}

		if ( CrosshairPanel == null )
		{
			CrosshairPanel = Components.Get<CrossVeggaHair>();
		}

		if ( CrosshairMenuPanel == null )
		{
			CrosshairMenuPanel = Components.Get<CrossVeggaHairMenu>();
		}

		if ( PauseMenuPanel == null )
		{
			PauseMenuPanel = Components.Get<VeggaPauseMenu>();
		}

		if ( HudLayoutOverlay == null )
		{
			HudLayoutOverlay = Components.Get<VeggaHudLayoutOverlay>();
		}

		if ( TradeWindowPanel == null )
		{
			TradeWindowPanel = Components.Get<TradeWindow>();
		}

		if ( PickupHintPanel == null )
		{
			PickupHintPanel = Components.Get<PickupHint>() ?? Scene.GetAllComponents<PickupHint>().FirstOrDefault();
			if ( PickupHintPanel == null )
			{
				// If the scene doesn't have one, create it so pickup hints always work.
				var go = new GameObject( true, "UI_PickupHint" );
				go.Components.Create<ScreenPanel>();
				PickupHintPanel = go.Components.Create<PickupHint>();
			}
		}

		if ( ChatPanel == null )
		{
			ChatPanel = Components.Get<VeggaChat>();
		}

		// Ensure layout mode is off when scene loads
		VeggaHudLayoutState.SetActive( false );
	}

	protected override void OnUpdate()
	{
		// Find the local player's stats
		var playerStats = PlayerVeggaStats.Local;

		// If we found a new player, link it
		if ( playerStats != null && playerStats != _currentPlayerStats )
		{
			_currentPlayerStats = playerStats;

			if ( ModularHudPanel != null )
			{
				ModularHudPanel.PlayerStats = playerStats;
			}

			if ( HudPanel != null )
			{
				HudPanel.PlayerStats = playerStats;
			}

			if ( InventoryPanel != null )
			{
				InventoryPanel.PlayerStats = playerStats;
			}
		}
	}
}

