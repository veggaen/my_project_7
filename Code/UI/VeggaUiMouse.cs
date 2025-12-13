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
				|| VeggaChat.IsChatInputOpenGlobal
				|| InventoryHud.IsOpenGlobal;
		}
	}

	public static void Apply()
	{
		// Don't stomp editor viewport tooling when not actively playing.
		if ( !Game.IsPlaying )
			return;

		Mouse.Visibility = WantsUiMouse ? MouseVisibility.Visible : MouseVisibility.Hidden;
	}
}
