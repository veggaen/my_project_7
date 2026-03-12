using Sandbox;
using Sandbox.Citizen;
using System;
using System.Linq;

public sealed class PlayerVeggaMovement : Component
{
	// Movement / physics

	[Property] public float GroundControl { get; set; } = 5.0f;
	[Property] public float AirControl { get; set; } = 0.4f;
	[Property] public float MaxForce { get; set; } = 120f;

	// Speeds (defaults – inspector values override these)
	[Property] public float BaseSpeed { get; set; } = 45.0f;  // WASD only
	[Property] public float RunSpeed { get; set; } = 65.0f;   // Shift (Run)
	[Property] public float CrouchSpeed { get; set; } = 8.0f; // Ctrl (Crouch)

	// Optional diagonal nerf (when pressing 2+ movement keys)
	[Property] public float DiagonalSpeedScale { get; set; } = 0.85f;

	// Old props kept so inspector doesn’t explode; not used directly
	[Property] public float Speed { get; set; } = 160.0f;
	[Property] public float WalkSpeed { get; set; } = 200.0f;

	[Property] public float JumpForce { get; set; } = 400.0f;

	// How quickly we accelerate toward target speed on ground.
	[Property] public float GroundAccelRate { get; set; } = 4.0f;

	// *** Body rotation tuning ***
	// Faster when moving, slower when idle
	[Property] public float BodyTurnSpeedMoving { get; set; } = 10.0f;
	[Property] public float BodyTurnSpeedIdle { get; set; } = 5.0f;

	// Tiny free head twist that doesn't move the body
	[Property] public float BodyFreeHeadAngle { get; set; } = 12.0f;

	// Angle where body is fully trying to catch up
	[Property] public float BodyMaxHeadAngle { get; set; } = 80.0f;

	// Max turning speed when head is far from body
	[Property] public float BodyMaxTurnSpeed { get; set; } = 28.0f;

	// Object References
	[Property] public GameObject Head { get; set; }
	[Property] public GameObject Body { get; set; }

	// 🎯 MULTIPLAYER: Static accessor for local player (for camera to find us)
	private static PlayerVeggaMovement _local;
	public static PlayerVeggaMovement Local
	{
		get
		{
			// Check if cached local is still valid AND still owned by our local connection
			var localConn = Connection.Local;
			if ( localConn != null && _local != null && _local.IsValid() && _local.Network.Owner == localConn )
				return _local;

			// Clear invalid cache
			_local = null;

			var scene = Game.ActiveScene;
			if ( scene is null || localConn is null ) return null;

			// Find our player - must be owned by our local connection
			foreach ( var movement in scene.GetAllComponents<PlayerVeggaMovement>() )
			{
				if ( movement.IsValid() && movement.Network.Owner == localConn )
				{
					_local = movement;
					return _local;
				}
			}

			return null;
		}
	}

	/// <summary>
	/// Clear the local cache. Call this when network state changes significantly.
	/// </summary>
	public static void ClearLocalCache()
	{
		_local = null;
	}

	// Member Variables
	public Vector3 WishVelocity = Vector3.Zero;

	// 🎯 MULTIPLAYER: Sync these so other clients see animations!
	[Sync] public bool IsCrouching { get; private set; } = false;
	[Sync] public bool IsSprinting { get; private set; } = false;

	// Synced movement velocity so the host can apply inertia to dropped items.
	[Sync] public Vector3 SyncedVelocity { get; private set; } = Vector3.Zero;

	// 🎯 MULTIPLAYER: Sync body AND head rotation so other clients see where you're facing
	[Sync] public Angles TargetBodyAngle { get; private set; } = Angles.Zero;
	[Sync] public Angles TargetHeadAngle { get; set; } = Angles.Zero; // Public set needed for CameraVeggaMovement

	private CharacterController characterController;
	private CitizenAnimationHelper animationHelper;
	private SkinnedModelRenderer _bodyRenderer;
	private PlayerVeggaAnimGraphDriver _animGraphDriver;

	// Standing vs crouch heights – to avoid *= 2 / 0.5f drift
	private float StandingHeight;
	private float CrouchHeight;

	protected override void OnAwake()
	{
		characterController = Components.Get<CharacterController>();
		animationHelper = Components.Get<CitizenAnimationHelper>();
		_animGraphDriver = Components.Get<PlayerVeggaAnimGraphDriver>();

		if ( Body != null )
		{
			_bodyRenderer = Body.Components.Get<SkinnedModelRenderer>();
		}

		if ( characterController is null )
		{
			Log.Error( "PlayerVeggaMovement: CharacterController missing on this GameObject." );
			return;
		}

		if ( Head is null )
			Log.Error( "PlayerVeggaMovement: Head property is not assigned." );

		StandingHeight = characterController.Height;
		CrouchHeight = StandingHeight * 0.5f;

		// We DON'T call UpdateBodyRendererVisibility() here because Network.IsProxy
		// might not be initialized yet during OnAwake on some clients. We instead
		// do it from OnUpdate where network state is stable.
	}

	protected override void OnUpdate()
	{
		// If HUD layout mode is active on this client, freeze local input so we
		// can edit the UI without walking the character around. Remote players
		// (proxies) should keep updating normally.
		if ( !IsProxy && Sandbox.UI.VeggaHudLayoutState.IsActive )
		{
			return;
		}

		// 🎯 MULTIPLAYER: Ensure body visibility matches proxy/local state.
		// This is cheap and guarantees that once Network.IsProxy is correct
		// (after spawn/ownership), remote players become fully visible while
		// the local player stays "shadows only".
		UpdateBodyRendererVisibility();

		if ( characterController is null || Head is null )
			return;

		// Make sure body visibility is correct for local vs other players
		UpdateBodyRendererVisibility();

		// 🎯 MULTIPLAYER: Only process input for local player
		if ( !IsProxy )
		{
			UpdateCrouch();
			IsSprinting = Input.Down( "Run" ) && !IsCrouching;

			if ( Input.Pressed( "Jump" ) )
				Jump();
		}

		// 🎯 MULTIPLAYER: Apply synced head rotation on proxy players FIRST
		// This must happen BEFORE RotateBody() and UpdateAnimation() so they use correct head rotation
		if ( IsProxy && Head != null )
		{
			Head.Transform.Rotation = TargetHeadAngle.ToRotation();
		}

		// 🎯 MULTIPLAYER: Run animations and body rotation on ALL clients
		// This uses the [Sync] variables set by the owner
		RotateBody();
		UpdateAnimation();
	}

	/// <summary>
	/// Toggle crouch with safety check before uncrouching.
	/// Hold-to-crouch: press = crouch, release = stand (if ceiling allows).
	/// </summary>
	void UpdateCrouch()
	{
		if ( characterController is null ) return;

		// Pressed: start crouching
		if ( Input.Pressed( "Crouch" ) && !IsCrouching )
		{
			IsCrouching = true;
			characterController.Height = CrouchHeight;
		}

		// Released: try to stand up (only if there is room)
		if ( Input.Released( "Crouch" ) && IsCrouching )
		{
			if ( CanStandUp() )
			{
				IsCrouching = false;
				characterController.Height = StandingHeight;
			}
			// else: keep crouching until user lets go again in a safe spot
		}
	}

	/// <summary>
	/// Trace above the player to see if we can safely return to StandingHeight.
	/// </summary>
	bool CanStandUp()
	{
		if ( characterController is null )
			return false;

		// Start roughly at the top of the crouched capsule
		var origin = characterController.WorldPosition + Vector3.Up * (CrouchHeight * 0.5f);

		// We need to grow from CrouchHeight to StandingHeight – add a little margin
		float extraHeight = (StandingHeight - CrouchHeight) + 2.0f;
		var end = origin + Vector3.Up * extraHeight;

		var tr = Scene.Trace
			.Ray( origin, end )
			.WithoutTags( "player", "trigger" )
			.Run();

		return !tr.Hit;
	}

	protected override void OnFixedUpdate()
	{
		// 🎯 MULTIPLAYER FIX: Don't simulate movement for other players!
		// IsProxy = true means this is someone else's player
		if ( IsProxy ) return;

		if ( characterController is null || Head is null )
			return;

		BuildWishVelocity();
		Move();
		SyncedVelocity = characterController.Velocity;
	}

	void BuildWishVelocity()
	{
		Vector3 moveDir = Vector3.Zero;
		int keysDown = 0;

		var rot = Head.Transform.Rotation;
		if ( Input.Down( "Forward" ) ) { moveDir += rot.Forward; keysDown++; }
		if ( Input.Down( "Backward" ) ) { moveDir += rot.Backward; keysDown++; }
		if ( Input.Down( "Left" ) ) { moveDir += rot.Left; keysDown++; }
		if ( Input.Down( "Right" ) ) { moveDir += rot.Right; keysDown++; }

		moveDir = moveDir.WithZ( 0 );
		if ( !moveDir.IsNearZeroLength )
			moveDir = moveDir.Normal;

		// --- Base speed by state ---
		float targetSpeed;
		if ( IsCrouching )
			targetSpeed = CrouchSpeed;
		else if ( IsSprinting )
			targetSpeed = RunSpeed;
		else
			targetSpeed = BaseSpeed;

		// --- Diagonal nerf: light when crouching/walking, heavier when sprinting ---
		if ( keysDown >= 2 && !moveDir.IsNearZeroLength )
		{
			if ( IsCrouching )
			{
				// almost no penalty while crouching – feel agile while slow
				targetSpeed *= 0.97f;
			}
			else if ( !IsSprinting )
			{
				// walking: tiny nerf
				targetSpeed *= 0.92f;
			}
			else
			{
				// sprint: heavier nerf
				targetSpeed *= DiagonalSpeedScale; // usually ~0.85
			}
		}

		float accelFactor = GroundAccelRate;
		float speedScale = 1.0f;

		// Previous horizontal wish direction
		var prevWish = WishVelocity.WithZ( 0 );
		bool hasPrev = !prevWish.IsNearZeroLength;
		bool hasInput = !moveDir.IsNearZeroLength;

		// --- Strong direction-change punishment ONLY when sprinting and already moving ---
		if ( IsSprinting && hasPrev && hasInput )
		{
			var oldDir = prevWish.Normal;
			float dot = Vector3.Dot( oldDir, moveDir ); // 1 = same, 0 = 90°, -1 = opposite

			if ( dot < -0.3f )
			{
				accelFactor *= 0.25f;
				speedScale = 0.45f;
			}
			else if ( dot < 0.1f )
			{
				accelFactor *= 0.4f;
				speedScale = 0.65f;
			}
			else if ( dot < 0.7f )
			{
				accelFactor *= 0.7f;
				speedScale = 0.85f;
			}
		}
		else
		{
			// Walk / crouch feel: loose & responsive
			if ( !hasInput )
			{
				accelFactor *= 0.8f; // come to a stop – not too sticky
			}
			else
			{
				accelFactor *= 1.15f; // walk/crouch: accelerate a bit faster
			}
		}

		targetSpeed *= speedScale;

		Vector3 targetVelocity = moveDir * targetSpeed;

		// Smooth acceleration / deceleration on the ground directionally
		WishVelocity = Vector3.Lerp( WishVelocity, targetVelocity, accelFactor * Time.Delta );
	}

	void Move()
	{
		var gravity = Scene.PhysicsWorld.Gravity;

		if ( characterController.IsOnGround )
		{
			characterController.Velocity = characterController.Velocity.WithZ( 0 );
			characterController.Accelerate( WishVelocity );
			characterController.ApplyFriction( GroundControl );
		}
		else
		{
			characterController.Velocity += gravity * Time.Delta * 0.5f;

			var airWish = WishVelocity.ClampLength( MaxForce );
			characterController.Accelerate( airWish );
			characterController.ApplyFriction( AirControl );
		}

		characterController.Move();

		if ( !characterController.IsOnGround )
			characterController.Velocity += gravity * Time.Delta * 0.5f;
		else
			characterController.Velocity = characterController.Velocity.WithZ( 0 );
	}

	void RotateBody()
	{
		if ( Body is null || Head is null || characterController is null )
			return;

		// 🎯 MULTIPLAYER FIX: Only the OWNER should set synced variables!
		// Proxies should just USE the already-synced TargetBodyAngle value
		if ( !IsProxy )
		{
			// Owner: calculate body angle from local head rotation
			TargetBodyAngle = new Angles( 0, Head.Transform.Rotation.Yaw(), 0 );
		}

		// Body follows head yaw with a free head-twist range and ramped catch-up.
		// Both owner and proxy use TargetBodyAngle (synced from owner)
		var targetRot = TargetBodyAngle.ToRotation();
		var currentRot = Body.Transform.Rotation;

		float yawDiff = currentRot.Distance( targetRot ); // degrees (0..180)

		// 1) DEADZONE: tiny head movements shouldn't move the body at all
		if ( yawDiff <= BodyFreeHeadAngle )
			return;

		float moveSpeed = characterController.Velocity.WithZ( 0 ).Length;
		float baseTurnSpeed = moveSpeed > 1f ? BodyTurnSpeedMoving : BodyTurnSpeedIdle;

		// 2) RAMP: between free angle and max angle, smoothly ramp from base speed to max
		float t = 0f;
		float range = BodyMaxHeadAngle - BodyFreeHeadAngle;
		if ( range > 0.001f )
		{
			t = (yawDiff - BodyFreeHeadAngle) / range;
		}

		t = Math.Max( 0f, Math.Min( 1f, t ) );

		float turnSpeed = baseTurnSpeed + (BodyMaxTurnSpeed - baseTurnSpeed) * t;

		Body.Transform.Rotation = Rotation.Lerp(
			currentRot,
			targetRot,
			Time.Delta * turnSpeed
		);
	}

	void Jump()
	{
		if ( !characterController.IsOnGround ) return;

		characterController.Punch( Vector3.Up * JumpForce );

		// 🎯 MULTIPLAYER: Broadcast jump animation to all clients
		BroadcastJumpAnimation();
	}

	/// <summary>
	/// 🎯 MULTIPLAYER: Play jump animation on all clients
	/// </summary>
	[Broadcast]
	void BroadcastJumpAnimation()
	{
		if ( animationHelper != null )
		{
			animationHelper.TriggerJump();
		}
	}

	void UpdateAnimation()
	{
		if ( animationHelper is null ) return;

		// NOTE: The PlayerVeggaAnimGraphDriver provides supplemental custom params
		// (lead_side, shoulder_swap_progress, etc.) but we ALWAYS let
		// CitizenAnimationHelper run below to drive the standard holdtype,
		// aim weights, velocity, etc. The driver's OnUpdate runs independently.

		var equipment = Components.Get<Sandbox.VeggaEquipmentController>( FindMode.InSelf | FindMode.InDescendants );
		var isAiming = equipment != null && equipment.IsValid() && equipment.IsAiming;

		if ( equipment != null && equipment.IsValid() )
		{
			// Temporary fallback: keep the stock CitizenAnimationHelper path stable.
			// Long-term lead-hand posing is driven by the custom animgraph parameters
			// written by PlayerVeggaAnimGraphDriver, not by runtime pose invention here.
			animationHelper.HoldType = equipment.HoldType switch
			{
				Sandbox.VeggaHoldType.Pistol => CitizenAnimationHelper.HoldTypes.Pistol,
				Sandbox.VeggaHoldType.Rifle => CitizenAnimationHelper.HoldTypes.Rifle,
				Sandbox.VeggaHoldType.Tool => CitizenAnimationHelper.HoldTypes.HoldItem,
				Sandbox.VeggaHoldType.Melee => CitizenAnimationHelper.HoldTypes.Punch,
				_ => CitizenAnimationHelper.HoldTypes.None
			};

			bool hasWeapon = equipment.HoldType != Sandbox.VeggaHoldType.None;
			animationHelper.AimEyesWeight = isAiming ? 1f : (hasWeapon ? 0.5f : 0f);
			animationHelper.AimHeadWeight = isAiming ? 1f : (hasWeapon ? 0.5f : 0f);
			animationHelper.AimBodyWeight = isAiming ? 0.8f : (hasWeapon ? 0.4f : 0f);
		}

		// 🎯 MULTIPLAYER: Use synced head rotation for animations
		var headRotation = TargetHeadAngle.ToRotation();

		animationHelper.WithWishVelocity( WishVelocity );
		animationHelper.WithVelocity( characterController.Velocity );
		animationHelper.AimAngle = headRotation;
		animationHelper.IsGrounded = characterController.IsOnGround;
		animationHelper.WithLook( headRotation.Forward, 1, 0.8f, 0.4f );

		animationHelper.MoveStyle = IsSprinting
			? CitizenAnimationHelper.MoveStyles.Run
			: CitizenAnimationHelper.MoveStyles.Walk;

		animationHelper.DuckLevel = IsCrouching ? 1f : 0f;

		// Reset body scale (remove any leftover mirror).
		if ( Body != null && Body.IsValid() )
			Body.WorldScale = Vector3.One;
	}

	/// <summary>
	/// 🎯 MULTIPLAYER: Control body renderer visibility.
	/// Local player: hide the body in first-person to avoid seeing eyes/mouth.
	/// Remote players: always visible.
	/// </summary>
	void UpdateBodyRendererVisibility()
	{
		if ( _bodyRenderer == null ) return;

		// Proxies should always be visible.
		if ( IsProxy )
		{
			_bodyRenderer.RenderType = ModelRenderer.ShadowRenderType.On;
			return;
		}

		bool firstPerson = false;
		try
		{
			var cam = Scene?.Components?.GetAll<CameraVeggaMovement>()?.FirstOrDefault();
			if ( cam != null && cam.IsValid() )
				firstPerson = cam.InFirstPerson;
		}
		catch
		{
			firstPerson = false;
		}

		_bodyRenderer.RenderType = firstPerson
			? ModelRenderer.ShadowRenderType.ShadowsOnly
			: ModelRenderer.ShadowRenderType.On;
	}
}
