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
	/// Link to the HotbarHud component.
	/// </summary>
	[Property, Group( "Links" )]
	public HotbarHud HotbarPanel { get; set; }

	/// <summary>
	/// Link to the AmmoHud component (weapon ammo counter).
	/// </summary>
	[Property, Group( "Links" )]
	public AmmoHud AmmoPanel { get; set; }

	/// <summary>
	/// Link to the XPBar component. Set this in the inspector!
	/// </summary>
	[Property, Group( "Links" )]
	public XPBar XpBarPanel { get; set; }

	/// <summary>
	/// Link to the CashDropProgressBar component. Set this in the inspector!
	/// </summary>
	[Property, Group( "Links" )]
	public CashDropProgressBar CashDropProgressBarPanel { get; set; }

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
	/// Link to the VeggaBoxHud component (blue-box HUD surfaces).
	/// </summary>
	[Property, Group( "Links" )]
	public VeggaBoxHud BoxHudPanel { get; set; }

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

	/// <summary>
	/// Link to the SkillsPanel component. Set this in the inspector!
	/// </summary>
	[Property, Group( "Links" )]
	public SkillsPanel SkillsPanel { get; set; }

	/// <summary>
	/// Link to the InteractHint component (Press E to open furnace/storage).
	/// </summary>
	[Property, Group( "Links" )]
	public InteractHint InteractHintPanel { get; set; }

	/// <summary>
	/// Link to the FurnaceHud component.
	/// </summary>
	[Property, Group( "Links" )]
	public FurnaceHud FurnaceHudPanel { get; set; }

	/// <summary>
	/// Link to the StorageHud component.
	/// </summary>
	[Property, Group( "Links" )]
	public StorageHud StorageHudPanel { get; set; }

	private PlayerVeggaStats _currentPlayerStats;

	protected override void OnStart()
	{
		// Disable the experimental Box HUD by default.
		// Hotload can preserve static values, so force it off to avoid duplicate blue-box layers.
		VeggaHudLayoutState.BoxHudEnabled = false;

		// Auto-find HUD components
		if ( HudPanel == null )
		{
			HudPanel = Components.Get<PlayerHud>();
		}

		if ( ModularHudPanel == null )
		{
			// Keep modular HUD on the same ScreenPanel (UI Root) as the layout overlay.
			ModularHudPanel = Components.Get<Sandbox.UI.PlayerVeggaModularHud>();
			if ( ModularHudPanel == null )
			{
				var existing = Scene.GetAllComponents<Sandbox.UI.PlayerVeggaModularHud>().FirstOrDefault();
				if ( existing != null && existing.IsValid && existing.GameObject != GameObject )
				{
					existing.Enabled = false;
					Log.Info( "[HUDManagerScene] Disabled external PlayerVeggaModularHud (different GameObject)." );
				}
				ModularHudPanel = Components.Create<Sandbox.UI.PlayerVeggaModularHud>();
			}
		}

		// If both HUD implementations exist in the UI scene, prefer the modular HUD.
		// The legacy PlayerHud has fixed CSS placement and does not follow the layout system,
		// which makes it look like the real UI is ignoring the blue/pink overlay boxes.
		if ( ModularHudPanel != null && HudPanel != null && HudPanel.Enabled )
		{
			HudPanel.Enabled = false;
			Log.Info( "[HUDManagerScene] Disabled legacy PlayerHud (modular HUD present)." );
		}

		if ( InventoryPanel == null )
		{
			// Same-canvas preference so positioning math matches the editor.
			InventoryPanel = Components.Get<InventoryHud>();
			if ( InventoryPanel == null )
			{
				var existing = Scene.GetAllComponents<InventoryHud>().FirstOrDefault();
				if ( existing != null && existing.IsValid && existing.GameObject != GameObject )
				{
					existing.Enabled = false;
					Log.Info( "[HUDManagerScene] Disabled external InventoryHud (different GameObject)." );
				}
				InventoryPanel = Components.Create<InventoryHud>();
			}
		}

		if ( HotbarPanel == null )
		{
			HotbarPanel = Components.Get<HotbarHud>();
			if ( HotbarPanel == null )
			{
				var existing = Scene.GetAllComponents<HotbarHud>().FirstOrDefault();
				if ( existing != null && existing.IsValid && existing.GameObject != GameObject )
				{
					existing.Enabled = false;
					Log.Info( "[HUDManagerScene] Disabled external HotbarHud (different GameObject)." );
				}
				HotbarPanel = Components.Create<HotbarHud>();
			}
		}

		if ( AmmoPanel == null )
		{
			AmmoPanel = Components.Get<AmmoHud>();
			if ( AmmoPanel == null )
			{
				var existing = Scene.GetAllComponents<AmmoHud>().FirstOrDefault();
				if ( existing != null && existing.IsValid && existing.GameObject != GameObject )
				{
					existing.Enabled = false;
					Log.Info( "[HUDManagerScene] Disabled external AmmoHud (different GameObject)." );
				}
				AmmoPanel = Components.Create<AmmoHud>();
			}
		}

		if ( XpBarPanel == null )
		{
			// Same-canvas preference for XP bar.
			XpBarPanel = Components.Get<XPBar>();
			if ( XpBarPanel == null )
			{
				var existing = Scene.GetAllComponents<XPBar>().FirstOrDefault();
				if ( existing != null && existing.IsValid && existing.GameObject != GameObject )
				{
					existing.Enabled = false;
					Log.Info( "[HUDManagerScene] Disabled external XPBar (different GameObject)." );
				}
				XpBarPanel = Components.Create<XPBar>();
			}
		}

		if ( CashDropProgressBarPanel == null )
		{
			// Same-canvas preference for progress bar.
			CashDropProgressBarPanel = Components.Get<CashDropProgressBar>();
			if ( CashDropProgressBarPanel == null )
			{
				var existing = Scene.GetAllComponents<CashDropProgressBar>().FirstOrDefault();
				if ( existing != null && existing.IsValid && existing.GameObject != GameObject )
				{
					existing.Enabled = false;
					Log.Info( "[HUDManagerScene] Disabled external CashDropProgressBar (different GameObject)." );
				}
				CashDropProgressBarPanel = Components.Create<CashDropProgressBar>();
			}
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

		if ( BoxHudPanel == null )
		{
			BoxHudPanel = Components.Get<VeggaBoxHud>();
			// Only auto-create if the feature is enabled.
			if ( BoxHudPanel == null && VeggaHudLayoutState.BoxHudEnabled )
			{
				BoxHudPanel = Components.Create<VeggaBoxHud>();
			}
		}

		if ( TradeWindowPanel == null )
		{
			TradeWindowPanel = Components.Get<TradeWindow>();
		}

		if ( PickupHintPanel == null )
		{
			// Prefer keeping all HUD panels on the same ScreenPanel (UI Root) so layout coordinates match.
			PickupHintPanel = Components.Get<PickupHint>();
			if ( PickupHintPanel == null )
			{
				var existing = Scene.GetAllComponents<PickupHint>().FirstOrDefault();
				if ( existing != null && existing.IsValid && existing.GameObject != GameObject )
				{
					// Disable external instance to avoid duplicates and coordinate drift.
					existing.Enabled = false;
				}
				PickupHintPanel = Components.Create<PickupHint>();
			}
		}

		if ( ChatPanel == null )
		{
			// Same-canvas preference (see PickupHint/Skills).
			ChatPanel = Components.Get<VeggaChat>();
			if ( ChatPanel == null )
			{
				var existing = Scene.GetAllComponents<VeggaChat>().FirstOrDefault();
				if ( existing != null && existing.IsValid && existing.GameObject != GameObject )
				{
					existing.Enabled = false;
				}
				ChatPanel = Components.Create<VeggaChat>();
			}
		}

		if ( SkillsPanel == null )
		{
			SkillsPanel = Components.Get<SkillsPanel>();
			if ( SkillsPanel == null )
			{
				var existing = Scene.GetAllComponents<SkillsPanel>().FirstOrDefault();
				if ( existing != null && existing.IsValid && existing.GameObject != GameObject )
				{
					existing.Enabled = false;
				}
				SkillsPanel = Components.Create<SkillsPanel>();
			}
		}

		if ( InteractHintPanel == null )
		{
			InteractHintPanel = Components.Get<InteractHint>();
			if ( InteractHintPanel == null )
			{
				var existing = Scene.GetAllComponents<InteractHint>().FirstOrDefault();
				if ( existing != null && existing.IsValid && existing.GameObject != GameObject )
					existing.Enabled = false;
				InteractHintPanel = Components.Create<InteractHint>();
			}
		}

		if ( FurnaceHudPanel == null )
		{
			FurnaceHudPanel = Components.Get<FurnaceHud>();
			if ( FurnaceHudPanel == null )
			{
				var existing = Scene.GetAllComponents<FurnaceHud>().FirstOrDefault();
				if ( existing != null && existing.IsValid && existing.GameObject != GameObject )
					existing.Enabled = false;
				FurnaceHudPanel = Components.Create<FurnaceHud>();
			}
		}

		if ( StorageHudPanel == null )
		{
			StorageHudPanel = Components.Get<StorageHud>();
			if ( StorageHudPanel == null )
			{
				var existing = Scene.GetAllComponents<StorageHud>().FirstOrDefault();
				if ( existing != null && existing.IsValid && existing.GameObject != GameObject )
					existing.Enabled = false;
				StorageHudPanel = Components.Create<StorageHud>();
			}
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

