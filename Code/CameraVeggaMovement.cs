using Sandbox;
using System;
using System.Linq;
using Sandbox.UI;

public sealed class CameraVeggaMovement : Component
{
	// References (auto-populated from local player)
	private GameObject Head { get; set; }
	private GameObject Body { get; set; }
	private PlayerVeggaMovement Player { get; set; }

	// Third person settings
	[Property] public float ThirdPersonDistance { get; set; } = 180f;
	[Property] public float ShoulderOffset { get; set; } = 18f; // slightly less so body hugs edge more
	[Property] public float VerticalOffset { get; set; } = 12f;

	// Extra offset when crouched (negative = lower camera)
	[Property] public float CrouchVerticalOffset { get; set; } = -8f;

	// Smoothness controls
	[Property] public float DistanceLerpSpeed { get; set; } = 10f;   // FP <-> TP distance blend
	[Property] public float PositionLerpSpeed { get; set; } = 14f;   // cam pos smoothing (a bit snappier)
	[Property] public float ShoulderLerpSpeed { get; set; } = 14f;   // slide left<->right

	// Shoulder mid-zoom (helps camera go "around" the character instead of through centre)
	// 0.10 = up to +10% extra distance when blend is near 0 (mid swap)
	[Property] public float ShoulderMidZoomFactor { get; set; } = 0.10f;

	// FOV (fixed – you can still tweak)
	[Property] public float BaseFov { get; set; } = 70f;

	// Input actions
	[Property] public string ToggleViewAction { get; set; } = "FPView";
	[Property] public string ShoulderSwapAction { get; set; } = "ShoulderSwap";

	private CameraComponent _camera;
	private ModelRenderer _bodyRenderer;
	private ModelRenderer[] _playerRenderers;
	private float _currentFov;

	private GameObject _viewModelObject;
	private ModelRenderer _viewModelRenderer;

	private float _currentDistance;
	private float _targetDistance;

	// Shoulder: blend between -1 (left) and +1 (right)
	private float _shoulderBlend = 1f;
	private int _targetShoulderSide = 1; // -1 or +1

	private bool IsFirstPersonTarget => _targetDistance <= 0.01f;
	private bool IsFirstPerson => _currentDistance <= 0.05f;
	public bool InFirstPerson => IsFirstPerson;

	protected override void OnAwake()
	{
		_camera = Components.Get<CameraComponent>();

		// Start in third person, right shoulder by default
		_currentDistance = ThirdPersonDistance;
		_targetDistance = ThirdPersonDistance;
		_shoulderBlend = _targetShoulderSide = 1;

		_currentFov = BaseFov;
		if ( _camera is not null )
			_camera.FieldOfView = _currentFov;
	}

	protected override void OnUpdate()
	{
		// 🎯 MULTIPLAYER: Find local player if we don't have references yet
		if ( Player == null )
		{
			Player = PlayerVeggaMovement.Local;
			if ( Player != null )
			{
				Head = Player.Head;
				Body = Player.Body;

				CachePlayerRenderers();

				Log.Info( $"📷 Camera linked to local player: Head={Head?.Name}, Body={Body?.Name}" );
			}
			else
			{
				// No local player yet, wait
				return;
			}
		}

		// 🎯 MULTIPLAYER FIX: Double-check we still own this player!
		// The Player reference could become stale if network ownership changes
		if ( Player == null || !Player.IsValid() || Player.IsProxy )
		{
			// Lost ownership or player became invalid - reset references
			Player = null;
			Head = null;
			Body = null;
			_bodyRenderer = null;
			_playerRenderers = null;
			return;
		}

		if ( _camera is null || Head is null )
			return;

		// Keep mouse mode consistent across all UI flows.
		VeggaUiMouse.Apply();

		var eyeAngles = Head.Transform.Rotation.Angles();

		// If any UI needs the cursor, suppress camera look input (but keep camera tracking).
		if ( !VeggaUiMouse.WantsUiMouse )
		{
			HandleInput();

			// Mouse look: instant & snappy – pure FPS feeling
			eyeAngles.pitch -= Input.MouseDelta.y * -0.009f;
			eyeAngles.yaw -= Input.MouseDelta.x * 0.009f;
			eyeAngles.roll = 0f;
			eyeAngles.pitch = eyeAngles.pitch.Clamp( -89.9f, 89.9f );
			Head.Transform.Rotation = eyeAngles.ToRotation();

			// 🎯 MULTIPLAYER: Sync head rotation to PlayerVeggaMovement (only for owner!)
			Player.TargetHeadAngle = eyeAngles;
		}

		// Smooth FP <-> TP distance
		_currentDistance = _currentDistance + (_targetDistance - _currentDistance) * DistanceLerpSpeed * Time.Delta;
		if ( _currentDistance < 0.001f ) _currentDistance = 0f;

		// Smooth shoulder side (-1 <-> +1)
		_shoulderBlend = _shoulderBlend + (_targetShoulderSide - _shoulderBlend) * ShoulderLerpSpeed * Time.Delta;

		UpdateCameraTransform( eyeAngles );
		UpdateAimFov();
		UpdateWeaponViewModel();
	}

	private void UpdateWeaponViewModel()
	{
		// Only show viewmodel for the local player in first person.
		if ( Player == null || !Player.IsValid() || Player.IsProxy || _camera is null )
		{
			DestroyViewModel();
			return;
		}

		if ( !IsFirstPerson )
		{
			DestroyViewModel();
			return;
		}

		var equipment = Player.Components.Get<Sandbox.VeggaEquipmentController>( FindMode.InSelf | FindMode.InDescendants );
		if ( equipment == null || !equipment.IsValid() )
		{
			DestroyViewModel();
			return;
		}

		var itemId = equipment.GetEquippedItemId();
		if ( !VeggaEquipmentCatalog.TryGetWeaponSpec( itemId, out var spec ) || string.IsNullOrWhiteSpace( spec.ViewModelPath ) )
		{
			DestroyViewModel();
			return;
		}

		EnsureViewModel();
		_viewModelRenderer.Model = Model.Load( spec.ViewModelPath );
	}

	private void EnsureViewModel()
	{
		if ( _viewModelObject != null && _viewModelObject.IsValid() && _viewModelRenderer != null && _viewModelRenderer.IsValid() )
			return;

		DestroyViewModel();

		_viewModelObject = new GameObject( true, "weapon_viewmodel" );
		_viewModelObject.Parent = GameObject;
		// Most s&box viewmodels are authored to sit at the camera origin.
		_viewModelObject.Transform.LocalPosition = Vector3.Zero;
		_viewModelObject.Transform.LocalRotation = Rotation.Identity;

		_viewModelRenderer = _viewModelObject.Components.Create<ModelRenderer>();
		_viewModelRenderer.RenderType = ModelRenderer.ShadowRenderType.On;
	}

	private void DestroyViewModel()
	{
		if ( _viewModelObject != null && _viewModelObject.IsValid() )
			_viewModelObject.Destroy();
		_viewModelObject = null;
		_viewModelRenderer = null;
	}

	private void CachePlayerRenderers()
	{
		_playerRenderers = null;
		_bodyRenderer = null;

		if ( Player == null || !Player.IsValid() )
			return;

		var root = Player.GameObject;
		if ( root == null || !root.IsValid() )
			return;

		var list = root.Components.GetAll<ModelRenderer>( FindMode.InDescendants ).ToList();
		var rootR = root.Components.Get<ModelRenderer>();
		if ( rootR != null && rootR.IsValid() )
			list.Insert( 0, rootR );

		_playerRenderers = list.Where( r => r != null && r.IsValid() ).Distinct().ToArray();

		if ( Body is not null )
			_bodyRenderer = Body.Components.Get<ModelRenderer>()
				?? Body.Components.GetAll<ModelRenderer>( FindMode.InDescendants ).FirstOrDefault();
	}

	private void SetPlayerRenderType( ModelRenderer.ShadowRenderType type )
	{
		if ( _playerRenderers == null || _playerRenderers.Length == 0 )
			CachePlayerRenderers();

		if ( _playerRenderers == null )
			return;

		foreach ( var r in _playerRenderers )
		{
			if ( r == null || !r.IsValid() )
				continue;
			r.RenderType = type;
		}
	}

	private void UpdateAimFov()
	{
		if ( _camera is null )
			return;

		var targetFov = BaseFov;
		var equipment = Player != null && Player.IsValid()
			? Player.Components.Get<Sandbox.VeggaEquipmentController>( FindMode.InSelf | FindMode.InDescendants )
			: null;

		// "1x" ADS: slight FOV tighten for feel (not a scope).
		if ( !VeggaUiMouse.WantsUiMouse && equipment != null && equipment.IsValid() && equipment.CanAim() && Input.Down( "Attack2" ) )
			targetFov = MathF.Max( 10f, BaseFov * 0.85f );

		_currentFov = _currentFov + (targetFov - _currentFov) * (12f * Time.Delta).Clamp( 0f, 1f );
		_camera.FieldOfView = _currentFov;
	}

	private void HandleInput()
	{
		// Toggle FP <-> TP
		if ( !string.IsNullOrEmpty( ToggleViewAction ) && Input.Pressed( ToggleViewAction ) )
		{
			if ( IsFirstPersonTarget )
				_targetDistance = ThirdPersonDistance; // go TP
			else
				_targetDistance = 0f;                  // go FP
		}

		// Shoulder swap only in TP mode
		if ( !string.IsNullOrEmpty( ShoulderSwapAction ) && Input.Pressed( ShoulderSwapAction ) )
		{
			if ( !IsFirstPersonTarget )
			{
				_targetShoulderSide *= -1; // flip target side, blend handled in OnUpdate
			}
		}
	}

	private void UpdateCameraTransform( Angles eyeAngles )
	{
		var headPos = Head.Transform.Position;
		var rot = eyeAngles.ToRotation();
		var forward = rot.Forward;
		var right = rot.Right;
		var up = rot.Up;

		// Base vertical offset
		float vertical = VerticalOffset;

		// If we have a player ref and they are crouching, nudge camera down a bit
		if ( Player != null && Player.IsCrouching )
		{
			vertical += CrouchVerticalOffset;
		}

		Vector3 targetCamPos;

		if ( IsFirstPerson )
		{
			// First person: at the head
			targetCamPos = headPos + up * vertical;

			SetPlayerRenderType( ModelRenderer.ShadowRenderType.ShadowsOnly );
		}
		else
		{
			// --- Third person: over-the-shoulder ---

			// Extra zoom when shoulder blend is near 0 (mid swap / high movement)
			float shoulderMidT = 1f - Math.Min( 1f, Math.Abs( _shoulderBlend ) ); // 1 at centre, 0 at edges
			float distanceMul = 1f + ShoulderMidZoomFactor * shoulderMidT;

			float effectiveDistance = _currentDistance * distanceMul;

			var desiredCamPos =
				headPos
				+ up * vertical
				- forward * effectiveDistance
				+ right * ShoulderOffset * _shoulderBlend;

			// Collision check head -> desiredCamPos
			var camTrace = Scene.Trace
				.Ray( headPos, desiredCamPos )
				.WithoutTags( "player", "trigger" )
				.Run();

			if ( camTrace.Hit )
				targetCamPos = camTrace.HitPosition + camTrace.Normal;
			else
				targetCamPos = desiredCamPos;

			SetPlayerRenderType( ModelRenderer.ShadowRenderType.On );
		}

		// Smooth camera position a bit so motion feels weighted but still responsive
		var currentPos = _camera.Transform.Position;
		if ( currentPos == default )
			currentPos = targetCamPos;

		var posLerp = (PositionLerpSpeed * Time.Delta).Clamp( 0f, 1f );
		var newPos = Vector3.Lerp( currentPos, targetCamPos, posLerp );

		_camera.Transform.Position = newPos;
		_camera.Transform.Rotation = rot; // keep aim/crosshair perfectly crisp
	}
}
