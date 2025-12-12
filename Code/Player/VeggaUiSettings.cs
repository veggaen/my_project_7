using System.Linq;
using Sandbox;

namespace Sandbox;

/// <summary>
/// ElvUI-inspired UI customization settings.
/// Allows players to customize HUD layout, inventory grid, scaling, etc.
/// </summary>
public sealed class VeggaUiSettings : Component
{
	// ---- Inventory Settings ----
	[Property, Group( "Inventory" ), Title( "Columns" )] 
	public int InventoryColumns { get; set; } = 12;
	
	[Property, Group( "Inventory" ), Title( "Rows" )] 
	public int InventoryRows { get; set; } = 8;
	
	[Property, Group( "Inventory" ), Title( "Item Size (px)" )] 
	public int InventoryItemSize { get; set; } = 40;

	// Calculated property
	public int InventoryPrimarySlots => InventoryColumns * InventoryRows; // 96 by default

	// ---- HUD Settings ----
	[Property, Group( "HUD" ), Title( "Global Scale" ), Range( 0.5f, 2.0f )] 
	public float HudScale { get; set; } = 1.0f;

	[Property, Group( "HUD" ), Title( "Show XP Bar" )] 
	public bool ShowXpBar { get; set; } = true;

	[Property, Group( "HUD" ), Title( "Show XP Drops" )] 
	public bool ShowXpDrops { get; set; } = true;

	// ---- Anchor Positions (0.0 to 1.0) ----
	[Property, Group( "Anchors" ), Title( "Inventory X" ), Range( 0f, 1f )] 
	public float InventoryAnchorX { get; set; } = 0.02f; // Left side

	[Property, Group( "Anchors" ), Title( "Inventory Y" ), Range( 0f, 1f )] 
	public float InventoryAnchorY { get; set; } = 0.90f; // Bottom

	[Property, Group( "Anchors" ), Title( "Player HUD X" ), Range( 0f, 1f )] 
	public float PlayerHudAnchorX { get; set; } = 0.02f; // Left side

	[Property, Group( "Anchors" ), Title( "Player HUD Y" ), Range( 0f, 1f )] 
	public float PlayerHudAnchorY { get; set; } = 0.02f; // Top

	// ---- Action Bar Settings ----
	[Property, Group( "Action Bars" ), Title( "Number of Bars" ), Range( 1, 6 )] 
	public int ActionBarCount { get; set; } = 2;

	[Property, Group( "Action Bars" ), Title( "Slots per Bar" ), Range( 6, 12 )] 
	public int ActionBarSlots { get; set; } = 12;

	[Property, Group( "Action Bars" ), Title( "Button Size (px)" )] 
	public int ActionBarButtonSize { get; set; } = 36;

	// ---- Local Singleton ----
	private static VeggaUiSettings _local;
	
	public static VeggaUiSettings Local
	{
		get
		{
			if ( _local is not null && _local.IsValid )
				return _local;

			_local = Game.ActiveScene
				.GetAllComponents<VeggaUiSettings>()
				.FirstOrDefault();

			return _local;
		}
	}

	protected override void OnStart()
	{
		Log.Info( "✅ VeggaUiSettings: Initialized!" );
		Log.Info( $"📦 Inventory: {InventoryColumns}x{InventoryRows} = {InventoryPrimarySlots} slots" );
		Log.Info( $"🎨 HUD Scale: {HudScale}" );
	}

	// ---- Save/Load Settings (Future) ----
	// TODO: Save to JSON file or Sandbox preferences
	// TODO: Load on start
	// TODO: UI config menu to edit these live
}

