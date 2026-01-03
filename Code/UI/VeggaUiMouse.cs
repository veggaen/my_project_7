using Sandbox;

namespace Sandbox.UI;

public static class VeggaUiMouse
{
	public static bool WantsUiMouse
	{
		get
		{
			// Any UI that needs cursor interaction should set a global flag.
			return VeggaHudLayoutState.IsActive
				|| VeggaPauseMenu.IsOpenGlobal
				|| VeggaUiFreeCursor.IsActive
				|| VeggaChat.IsChatInputOpenGlobal
				|| CrossVeggaHairMenu.IsOpenGlobal
				|| (VeggaScoreboard.IsOpenGlobal && VeggaScoreboard.WantsCursorGlobal)
				|| (InventoryHud.IsOpenGlobal && VeggaHudLayoutState.GetCursorMode( VeggaHudLayoutState.KeyInventory, defaultValue: true ))
				|| (SkillsPanel.IsOpenGlobal && VeggaHudLayoutState.GetCursorMode( VeggaHudLayoutState.KeySkillsPanel, defaultValue: true ))
				|| (FurnaceHud.IsOpenGlobal && VeggaHudLayoutState.GetCursorMode( VeggaHudLayoutState.KeyFurnaceMenu, defaultValue: true ))
				|| (StorageHud.IsOpenGlobal && VeggaHudLayoutState.GetCursorMode( VeggaHudLayoutState.KeyStorageMenu, defaultValue: true ));
		}
	}

	public static void Apply()
	{
		// Don't stomp editor viewport tooling when not actively playing.
		if ( !Game.IsPlaying )
			return;

		// Free cursor toggle (default bind requested: | key). Use Input action so users can rebind.
		bool freeCursorPressed = Input.Pressed( "FreeCursorToggle" )
			|| Input.Pressed( "freecursortoggle" );

		if ( !VeggaChat.IsChatInputOpenGlobal && freeCursorPressed )
		{
			VeggaUiFreeCursor.Toggle();
		}

		// Centralized ESC behavior: close one UI per press.
		VeggaUiCloseStack.HandleEscapePress();

		Mouse.Visibility = WantsUiMouse ? MouseVisibility.Visible : MouseVisibility.Hidden;
	}
}
