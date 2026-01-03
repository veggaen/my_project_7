using Sandbox;

namespace Sandbox.UI;

public static class VeggaUiFreeCursor
{
	public const string CloseStackKey = "free_cursor";

	public static bool IsActive { get; private set; }

	public static void Toggle()
	{
		Set( !IsActive );
	}

	public static void Set( bool active )
	{
		if ( IsActive == active )
			return;

		IsActive = active;

		if ( IsActive )
			VeggaUiCloseStack.SetOpen( CloseStackKey, true, Disable );
		else
			VeggaUiCloseStack.Remove( CloseStackKey );
	}

	public static void Disable()
	{
		IsActive = false;
		VeggaUiCloseStack.Remove( CloseStackKey );
	}
}
