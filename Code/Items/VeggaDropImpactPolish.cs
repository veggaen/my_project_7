using Sandbox;
using System;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Lightweight collision polish for dropped items:
/// - boosts damping briefly after impacts so props settle (less jitter / less endless rolling)
/// - optionally plays an impact sound (broadcast)
///
/// Intended to be attached automatically to world-drop pickups.
/// </summary>
public sealed class VeggaDropImpactPolish : Component, Component.ICollisionListener
{
	[Property] public string ImpactSound { get; set; } = "";
	[Property] public float MinSpeedForImpact { get; set; } = 120f;
	[Property] public float ImpactCooldownSeconds { get; set; } = 0.08f;
	[Property] public float SettleSeconds { get; set; } = 0.25f;
	[Property] public float SettleLinearDamping { get; set; } = 0.08f;
	[Property] public float SettleAngularDamping { get; set; } = 1.2f;

	float _baseLinearDamping;
	float _baseAngularDamping;
	bool _capturedBase;
	float _settleUntil;
	float _nextSoundTime;

	protected override void OnStart()
	{
		CaptureBaseline();
	}

	public void RefreshBaseline()
	{
		CaptureBaseline();
	}

	protected override void OnUpdate()
	{
		if ( _settleUntil <= 0f )
			return;
		if ( Time.Now < _settleUntil )
			return;

		_settleUntil = 0f;
		RestoreBaseline();
	}

	void CaptureBaseline()
	{
		var rb = GetRigidbody();
		if ( rb == null )
			return;

		_baseLinearDamping = rb.LinearDamping;
		_baseAngularDamping = rb.AngularDamping;
		_capturedBase = true;
	}

	void RestoreBaseline()
	{
		if ( !_capturedBase )
			return;

		var rb = GetRigidbody();
		if ( rb == null )
			return;

		rb.LinearDamping = _baseLinearDamping;
		rb.AngularDamping = _baseAngularDamping;
	}

	Rigidbody GetRigidbody()
	{
		var rb = Components.Get<Rigidbody>() ?? Components.GetAll<Rigidbody>( FindMode.InDescendants ).FirstOrDefault();
		if ( rb == null || !rb.IsValid() || !rb.Enabled || !rb.MotionEnabled )
			return null;
		return rb;
	}

	void HandleImpact()
	{
		var rb = GetRigidbody();
		if ( rb == null )
			return;

		// Use rigidbody speed as a stable approximation.
		var speed = rb.Velocity.Length;
		if ( speed < MinSpeedForImpact )
			return;

		// Boost damping so the item settles after hits.
		rb.LinearDamping = MathF.Max( rb.LinearDamping, SettleLinearDamping );
		rb.AngularDamping = MathF.Max( rb.AngularDamping, SettleAngularDamping );
		_settleUntil = Time.Now + MathF.Max( 0f, SettleSeconds );

		if ( string.IsNullOrWhiteSpace( ImpactSound ) )
			return;
		if ( Time.Now < _nextSoundTime )
			return;

		_nextSoundTime = Time.Now + MathF.Max( 0.01f, ImpactCooldownSeconds );
		RpcPlaySoundAt( WorldPosition, ImpactSound );
	}

	public void OnCollisionStart( Collision collision ) => HandleImpact();
	public void OnCollisionUpdate( Collision collision ) => HandleImpact();
	public void OnCollisionStop( Collision collision ) { }

	[Rpc.Broadcast]
	static void RpcPlaySoundAt( Vector3 pos, string soundName )
	{
		if ( string.IsNullOrWhiteSpace( soundName ) )
			return;

		try
		{
			Sound.Play( soundName, pos );
		}
		catch
		{
			// Ignore missing/invalid sound events.
		}
	}
}
