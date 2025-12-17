using Sandbox;

namespace Sandbox;

public static class VeggaSfxSettings
{
	public static bool Enabled { get; private set; } = true;
	public static bool PickupEnabled { get; private set; } = true;
	public static bool SalaryEnabled { get; private set; } = true;

	public const string CoinSound = "igrotronika.coin1";

	[ConCmd( "vegga_sfx", Help = "Enable/disable all Vegga SFX. Usage: vegga_sfx [0/1]" )]
	public static void CmdAll( int enabled = -1 )
	{
		if ( enabled < 0 )
			Enabled = !Enabled;
		else
			Enabled = enabled != 0;

		Log.Info( $"[SFX] Enabled={(Enabled ? 1 : 0)}" );
	}

	[ConCmd( "vegga_sfx_pickup", Help = "Enable/disable pickup SFX. Usage: vegga_sfx_pickup [0/1]" )]
	public static void CmdPickup( int enabled = -1 )
	{
		if ( enabled < 0 )
			PickupEnabled = !PickupEnabled;
		else
			PickupEnabled = enabled != 0;

		Log.Info( $"[SFX] PickupEnabled={(PickupEnabled ? 1 : 0)}" );
	}

	[ConCmd( "vegga_sfx_salary", Help = "Enable/disable salary SFX. Usage: vegga_sfx_salary [0/1]" )]
	public static void CmdSalary( int enabled = -1 )
	{
		if ( enabled < 0 )
			SalaryEnabled = !SalaryEnabled;
		else
			SalaryEnabled = enabled != 0;

		Log.Info( $"[SFX] SalaryEnabled={(SalaryEnabled ? 1 : 0)}" );
	}
}
