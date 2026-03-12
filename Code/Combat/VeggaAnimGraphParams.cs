namespace Sandbox;

/// <summary>
/// String constants for animgraph parameter/tag names.
/// These are intended to match the FishDev-style setup (holdtype selector, aiming, shoot/reload layers).
/// </summary>
public static class VeggaAnimGraphParams
{
	public const string HoldType = "holdtype";
	public const string HoldTypeHandedness = "holdtype_handedness";
	public const string IsAiming = "aim";
	public const string Shoot = "shoot";
	public const string Reload = "reload";
	public const string LeadSide = "lead_side";
	public const string ShoulderSwapProgress = "shoulder_swap_progress";
	public const string IsThirdPerson = "third_person";
	public const string AimWeight = "aim_weight";
	public const string MoveSpeed = "move_speed";
	public const string MoveForward = "move_forward";
	public const string MoveRight = "move_right";
	public const string IsGrounded = "grounded";
	public const string IsCrouching = "crouching";
	public const string IsSprinting = "sprinting";
	public const string AimYaw = "aim_yaw";
	public const string AimPitch = "aim_pitch";
	public const string IsShoulderSwapping = "shoulder_swapping";
	public const string HasWeapon = "has_weapon";
	public const string SupportHandWeight = "support_hand_weight";

	// Optional tags you can use inside the animgraph to disable lookats/IK during reload.
	public const string TagDisableLookAt = "disable_lookat";
	public const string TagDisableIk = "disable_ik";
}
