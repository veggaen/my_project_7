using System;
using Sandbox;

namespace Sandbox;

public sealed class VeggaProjectile : Component
{
	[Sync] public Vector3 Velocity { get; set; }
	[Sync] public float Damage { get; set; } = 10f;
	[Sync] public float LifetimeSeconds { get; set; } = 3.0f;
	[Sync] public Guid ShooterId { get; set; }

	private GameObject _shooter;
	private TimeSince _sinceSpawn;

	public void SetShooter( GameObject shooter )
	{
		_shooter = shooter;
	}

	protected override void OnStart()
	{
		_sinceSpawn = 0;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( _sinceSpawn > LifetimeSeconds )
		{
			GameObject.Destroy();
			return;
		}

		var scene = Scene;
		if ( scene is null )
		{
			GameObject.Destroy();
			return;
		}

		var from = WorldPosition;
		var to = from + Velocity * Time.Delta;

		var tr = scene.Trace
			.Ray( from, to )
			.WithoutTags( "trigger" );

		if ( _shooter != null && _shooter.IsValid() )
			tr = tr.IgnoreGameObjectHierarchy( _shooter );

		var hit = tr.Run();
		if ( hit.Hit )
		{
			TryApplyDamage( hit.GameObject );
			WorldPosition = hit.HitPosition;
			GameObject.Destroy();
			return;
		}

		WorldPosition = to;
	}

	private void TryApplyDamage( GameObject hitObject )
	{
		if ( hitObject == null || !hitObject.IsValid() )
			return;

		// Try direct hit object, then ancestors (common for player child bones).
		const FindMode findMode = FindMode.InSelf | FindMode.InAncestors;
		if ( hitObject.Components.TryGet<PlayerVeggaStats>( out var stats, findMode ) )
		{
			stats.Damage( Damage );
		}
	}
}
