using Sandbox;
using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Emergency escape hatch to get back to mouse-look.
/// Useful while iterating on UI input flows.
/// </summary>
public static class VeggaUiPanicCommands
{
	[ConCmd( "vegga_ui_panic" )]
	public static void PanicCloseAllUi()
	{
		VeggaHudLayoutState.SetActive( false );

		// Close UI layers that can hold mouse cursor.
		Sandbox.UI.InventoryHud.ForceClose();
		Sandbox.UI.VeggaChat.ForceClose();
		Sandbox.UI.VeggaPauseMenu.ForceClose();

		Log.Info( "[UI] Panic close: layout/chat/bag/pause" );
	}
}
