namespace GameFish;

partial class Projectile
{
	/// <summary>
	/// The speed to go if not otherwise specified.
	/// </summary>
	[Property]
	[Feature( PROJECTILE ), Group( MOVEMENT )]
	[Range( 0f, 10000f, clamped: false ), Step( 1 )]
	public float DefaultSpeed { get; set; } = 1000f;

	public override Vector3 Velocity
	{
		get => ProjectileVelocity;
		set => ProjectileVelocity = value;
	}

	[Sync]
	protected Vector3 ProjectileVelocity { get; set; }

	/// <summary>
	/// Overrides what speed the projectile should be moving.
	/// </summary>
	[Sync]
	public float? ProjectileTargetSpeed { get; set; }

	[Property]
	[Feature( PROJECTILE )]
	[ToggleGroup( nameof( IsHoming ), Label = HOMING )]
	public bool IsHoming { get; set; } = false;

	/// <summary>
	/// <c>Min</c> = distance moving the farthest <br />
	/// <c>Max</c> = distance moving the slowest
	/// </summary>
	[Property]
	[Title( "Distance" )]
	[Feature( PROJECTILE )]
	[ToggleGroup( nameof( IsHoming ) )]
	public FloatRange HomingDistance { get; set; } = new( 250f, 1500f );

	/// <summary>
	/// <c>Min</c> = turning speed while farthest <br />
	/// <c>Max</c> = turning speed while closest
	/// </summary>
	[Property]
	[Title( "Speed" )]
	[Feature( PROJECTILE )]
	[ToggleGroup( nameof( IsHoming ) )]
	public FloatRange HomingSpeed { get; set; } = new( 0f, 1000f );

	protected override void Move( in float deltaTime, in bool isFixedUpdate )
	{
		var startPos = WorldPosition;
		var move = Velocity * deltaTime;

		var trAll = TraceSettings.RunAll( GameObject, startPos, startPos + move );

		foreach ( var tr in trAll )
		{
			if ( !IsCollision( in tr ) )
				continue;

			if ( TryCollide( new ImpactData( GameObject, tr ) ) )
				if ( IsFinished() )
					return;
		}

		if ( GameObject.IsValid() )
			WorldPosition += move;
	}

	protected virtual void UpdateVelocity( in float deltaTime )
	{
		// Homing missiles.
		if ( !IsHoming )
			return;

		// Find the nearest enemy.
		var projPos = Center;

		var (target, dist) = GetEnemiesWithin( projPos, HomingDistance.Max )
			.Where( enemy => enemy.IsValid() && enemy.Active )
			.Select( enemy => (enemy, projPos.Distance( enemy.Center )) )
			.OrderBy( tuple => tuple.Item2 )
			.FirstOrDefault();

		if ( !target.IsValid() || dist > HomingDistance.Max )
			return;

		var targetPos = target.Center + (target.Velocity * deltaTime);
		var dir = Center.Direction( targetPos );

		var projSpeed = ProjectileTargetSpeed ??= DefaultSpeed;
		var homingSpeed = HomingDistance.Remap( dist, HomingSpeed.Max, HomingSpeed.Min );

		var homingVel = dir * homingSpeed * deltaTime;

		var oldVel = Velocity;

		Velocity = (oldVel + homingVel).Normal * projSpeed;
	}
}
