using Sandbox;
using Sandbox.Money;
using System;
using System.Linq;

namespace Sandbox;

/// <summary>
/// While a cash pallet is being built, we temporarily lock physics for large cash boxes so
/// newly-spawned pieces can't shove the entire stack around.
/// Once the build completes, the spawner unlocks these so players can push the pile.
/// Host-only.
/// </summary>
public sealed class VeggaCashBuildLock : Component
{
	// Safety: never leave props permanently locked if something goes wrong.
	[Property] public float FailSafeUnlockSeconds { get; set; } = 20f;
	// Typical behavior: stabilize each box only briefly after placement.
	[Property] public float AutoUnlockSeconds { get; set; } = 1.25f;
	[Property] public float StabilizeLinearDamping { get; set; } = 1.15f;
	[Property] public float StabilizeAngularDamping { get; set; } = 6.0f;
	[Property] public float MaxLinearSpeedWhileBuilding { get; set; } = 55.0f;
	[Property] public float MaxAngularSpeedWhileBuilding { get; set; } = 25.0f;

	float _failsafeUnlockAt;
	bool _locked;
	Rigidbody _rb;
	float _originalLinearDamping;
	float _originalAngularDamping;
	bool _originalLockPitch;
	bool _originalLockRoll;

	protected override void OnStart()
	{
		_failsafeUnlockAt = Time.Now + MathF.Max( 0.5f, FailSafeUnlockSeconds );
	}

	protected override void OnUpdate()
	{
		if ( Network.IsProxy )
			return;

		if ( _locked && Time.Now >= _failsafeUnlockAt )
			UnlockNow();
	}

	protected override void OnFixedUpdate()
	{
		if ( Network.IsProxy )
			return;
		if ( !_locked )
			return;
		if ( _rb == null || !_rb.IsValid() )
			return;

		// Keep it responsive (not kinematic), but clamp the "self-shove" energy while the pallet is building.
		var v = _rb.Velocity;
		float maxV = MathF.Max( 0f, MaxLinearSpeedWhileBuilding );
		if ( maxV > 0f && v.Length > maxV )
			_rb.Velocity = v.Normal * maxV;

		var av = _rb.AngularVelocity;
		float maxAV = MathF.Max( 0f, MaxAngularSpeedWhileBuilding );
		if ( maxAV > 0f && av.Length > maxAV )
			_rb.AngularVelocity = av.Normal * maxAV;
	}

	public void LockNow()
	{
		if ( Network.IsProxy )
			return;
		if ( _locked )
			return;

		_rb = Components.Get<Rigidbody>() ?? Components.GetAll<Rigidbody>( FindMode.InDescendants ).FirstOrDefault();
		if ( _rb == null || !_rb.IsValid() )
			return;

		_rb.Enabled = true;
		_rb.MotionEnabled = true;
		_rb.Gravity = true;

		_originalLinearDamping = _rb.LinearDamping;
		_originalAngularDamping = _rb.AngularDamping;
		try
		{
			var original = _rb.Locking;
			_originalLockPitch = original.Pitch;
			_originalLockRoll = original.Roll;
		}
		catch
		{
			_originalLockPitch = false;
			_originalLockRoll = false;
		}

		_rb.LinearDamping = MathF.Max( _rb.LinearDamping, StabilizeLinearDamping );
		_rb.AngularDamping = MathF.Max( _rb.AngularDamping, StabilizeAngularDamping );
		try
		{
			// Keep pallet boxes upright while building, but still pushable.
			var locking = _rb.Locking;
			locking.Pitch = true;
			locking.Roll = true;
			_rb.Locking = locking;
		}
		catch { }

		_rb.Velocity *= 0.35f;
		_rb.AngularVelocity *= 0.35f;

		// Auto-unlock soon so it stays pushable during the build.
		_failsafeUnlockAt = Time.Now + MathF.Max( 0.25f, AutoUnlockSeconds );
		_locked = true;
	}

	public void UnlockNow()
	{
		if ( Network.IsProxy )
			return;
		if ( !_locked )
			return;

		if ( _rb == null || !_rb.IsValid() )
		{
			_locked = false;
			return;
		}

		_rb.Enabled = true;
		_rb.MotionEnabled = true;
		_rb.Gravity = true;
		_rb.LinearDamping = _originalLinearDamping;
		_rb.AngularDamping = _originalAngularDamping;
		try
		{
			var locking = _rb.Locking;
			locking.Pitch = _originalLockPitch;
			locking.Roll = _originalLockRoll;
			_rb.Locking = locking;
		}
		catch { }
		VeggaCurrency.ApplyCashRigidbodyTuning( _rb, VeggaCurrency.CashBoxAmount );
		_locked = false;

		// This component is only needed during build.
		Destroy();
	}

	public static void UnlockAllNear( Scene scene, Vector3 center, float radius )
	{
		if ( !Networking.IsHost )
			return;
		if ( scene == null )
			scene = Game.ActiveScene;
		if ( scene == null )
			return;

		float r2 = radius * radius;
		foreach ( var l in scene.GetAllComponents<VeggaCashBuildLock>() )
		{
			if ( l == null || !l.IsValid() ) continue;
			if ( Vector3.DistanceBetweenSquared( l.WorldPosition, center ) > r2 ) continue;
			l.UnlockNow();
		}
	}
}
