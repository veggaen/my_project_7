namespace Sandbox;

/// <summary>
/// String constants for animgraph parameter/tag names.
/// These are intended to match the FishDev-style setup (holdtype selector, aiming, shoot/reload layers).
/// </summary>
public static class VeggaAnimGraphParams
{
	public const string HoldType = "holdtype";
	public const string IsAiming = "aim";
	public const string Shoot = "shoot";
	public const string Reload = "reload";

	// Optional tags you can use inside the animgraph to disable lookats/IK during reload.
	public const string TagDisableLookAt = "disable_lookat";
	public const string TagDisableIk = "disable_ik";
}
