using Sandbox;
using Sandbox.UI;
namespace Sandbox;

/// <summary>
/// Debug console commands for resetting HUD layout state.
/// </summary>
public static class VeggaHudLayoutDebugCommands
{
	/// <summary>
	/// Wipe the entire hud_layout.json (layouts + safe areas).
	/// </summary>
	[ConCmd( "vegga_hud_layout_wipe" )]
	public static void WipeLayoutFile()
	{
		VeggaHudLayoutState.WipeAllSavedLayoutData();
		Log.Info( "[HUD Layout] Wiped layout save file." );
	}
}
