using System.Linq;
using Sandbox;

namespace Sandbox;

/// <summary>
/// Drives custom animgraph parameters for the player body renderer.
/// This lets the project migrate away from pure CitizenAnimationHelper-driven posing
/// while keeping the existing gameplay systems intact.
/// </summary>
public sealed class PlayerVeggaAnimGraphDriver : Component
{
	[Property] public GameObject Body { get; set; }
	[Property] public SkinnedModelRenderer TargetRenderer { get; set; }
	[Property] public bool RequireCustomAnimationGraph { get; set; } = false;

	public bool IsCustomAnimationActive => TargetRenderer != null
		&& TargetRenderer.IsValid()
		&& TargetRenderer.AnimationGraph != null;

	private PlayerVeggaMovement _movement;
	private VeggaEquipmentController _equipment;
	private CharacterController _characterController;
	private int _lastShotSequence = -1;
	private int _lastReloadSequence = -1;

	protected override void OnStart()
	{
		ResolveReferences();
	}

	protected override void OnUpdate()
	{
		ResolveReferences();

		if ( TargetRenderer == null || !TargetRenderer.IsValid() )
			return;

		if ( RequireCustomAnimationGraph && TargetRenderer.AnimationGraph == null )
			return;

		// Self-resetting animgraph triggers should be low unless explicitly pulsed this frame.
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.Shoot, false );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.Reload, false );

		var cam = Scene?.Components?.GetAll<CameraVeggaMovement>()?.FirstOrDefault();
		var inThirdPerson = cam != null && cam.IsValid() && !cam.InFirstPerson;
		var leadSide = inThirdPerson ? cam.TargetShoulderSide : 1;
		var shoulderSwapProgress = inThirdPerson
			? (((-cam.ShoulderBlend) + 1f) * 0.5f).Clamp( 0f, 1f )
			: 0f;
		var isShoulderSwapping = inThirdPerson && cam.TimeSinceShoulderSwap < 0.35f;

		var holdType = _equipment != null && _equipment.IsValid()
			? (int)_equipment.HoldType
			: (int)VeggaHoldType.None;
		var hasWeapon = holdType == (int)VeggaHoldType.Pistol || holdType == (int)VeggaHoldType.Rifle;
		var isAiming = _equipment != null && _equipment.IsValid() && _equipment.IsAiming;
		var isDualWield = _equipment != null && _equipment.IsValid() && _equipment.IsDualWielding;
		var fireSide = _equipment != null && _equipment.IsValid() ? _equipment.LastFireSide : leadSide;
		var velocity = _movement != null && _movement.IsValid()
			? _movement.SyncedVelocity
			: (_characterController != null && _characterController.IsValid() ? _characterController.Velocity : Vector3.Zero);
		var planarVelocity = velocity.WithZ( 0 );
		var moveSpeed = planarVelocity.Length;
		var bodyRotation = _movement != null && _movement.IsValid()
			? _movement.TargetBodyAngle.ToRotation()
			: (Body != null && Body.IsValid() ? Body.WorldRotation : Rotation.Identity);
		var moveForward = Vector3.Dot( planarVelocity, bodyRotation.Forward );
		var moveRight = Vector3.Dot( planarVelocity, bodyRotation.Right );
		var isGrounded = _characterController != null && _characterController.IsValid() && _characterController.IsOnGround;
		var isCrouching = _movement != null && _movement.IsValid() && _movement.IsCrouching;
		var isSprinting = _movement != null && _movement.IsValid() && _movement.IsSprinting;
		var aimYaw = 0f;
		var aimPitch = 0f;
		if ( _movement != null && _movement.IsValid() )
		{
			aimYaw = Angles.NormalizeAngle( _movement.TargetHeadAngle.yaw - _movement.TargetBodyAngle.yaw );
			aimPitch = Angles.NormalizeAngle( _movement.TargetHeadAngle.pitch );
		}

		// Parameter-only runtime: code chooses gameplay state, the animgraph owns body poses.
		// The future custom graph should read these values to select right/left lead,
		// hipfire/ADS, and shoulder-swap handoff states without any camera-space IK hacks.
		var aimWeight = isAiming ? 1f : 0f;
		var supportHandWeight = (!isDualWield && isAiming && hasWeapon && !isShoulderSwapping) ? 1f : 0f;

		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.HoldType, holdType );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.HoldTypeHandedness, leadSide );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.IsAiming, isAiming );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.LeadSide, leadSide );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.ShoulderSwapProgress, shoulderSwapProgress );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.IsShoulderSwapping, isShoulderSwapping );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.IsThirdPerson, inThirdPerson );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.AimWeight, aimWeight );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.HasWeapon, hasWeapon );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.SupportHandWeight, supportHandWeight );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.IsDualWield, isDualWield );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.FireSide, fireSide );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.MoveSpeed, moveSpeed );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.MoveForward, moveForward );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.MoveRight, moveRight );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.IsGrounded, isGrounded );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.IsCrouching, isCrouching );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.IsSprinting, isSprinting );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.AimYaw, aimYaw );
		AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.AimPitch, aimPitch );

		if ( _equipment == null || !_equipment.IsValid() )
			return;

		if ( _equipment.ShotSequence != _lastShotSequence )
		{
			_lastShotSequence = _equipment.ShotSequence;
			AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.Shoot, true );
		}

		if ( _equipment.ReloadSequence != _lastReloadSequence )
		{
			_lastReloadSequence = _equipment.ReloadSequence;
			AnimationParameterWriter.Set( TargetRenderer, VeggaAnimGraphParams.Reload, true );
		}
	}

	private void ResolveReferences()
	{
		_movement ??= Components.Get<PlayerVeggaMovement>();
		_equipment ??= Components.Get<VeggaEquipmentController>();
		_characterController ??= Components.Get<CharacterController>();

		if ( (Body == null || !Body.IsValid()) && _movement != null && _movement.IsValid() )
		{
			Body = _movement.Body;
		}

		if ( (TargetRenderer == null || !TargetRenderer.IsValid()) && Body != null && Body.IsValid() )
		{
			TargetRenderer = Body.Components.Get<SkinnedModelRenderer>()
				?? Body.Components.GetAll<SkinnedModelRenderer>( FindMode.InDescendants ).FirstOrDefault();
		}
	}

}