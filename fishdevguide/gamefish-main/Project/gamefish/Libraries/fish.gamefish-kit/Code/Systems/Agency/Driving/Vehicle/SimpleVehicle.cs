namespace GameFish;

/// <summary>
/// A stupidly simple vehicle that doesn't care about wheels.
/// </summary>
public partial class SimpleVehicle : Vehicle
{
	/// <summary>
	/// How much to push the vehicle forward from acceleration input.
	/// </summary>
	[Property]
	[Feature( VEHICLE ), Order( VEHICLE_ORDER )]
	public float DrivingSpeed { get; set; } = 800f;

	/// <summary>
	/// The relative direction to push the vehicle forward/back.
	/// </summary>
	[Normal]
	[Property]
	[Feature( VEHICLE ), Order( VEHICLE_ORDER )]
	public Vector3 DrivingAxis { get; set; } = Vector3.Forward;

	/// <summary>
	/// How much to spin the vehicle from steering input.
	/// </summary>
	[Property]
	[Feature( VEHICLE ), Order( VEHICLE_ORDER )]
	public float SteeringTorque { get; set; } = 6f;

	/// <summary>
	/// The relative direction to spin the vehicle around.
	/// </summary>
	[Normal]
	[Property]
	[Feature( VEHICLE ), Order( VEHICLE_ORDER )]
	public Vector3 SteeringAxis { get; set; } = Vector3.Up;

	protected override void ApplyForces( in float deltaTime, in bool isFixedUpdate )
	{
		if ( IsProxy || !Rigidbody.IsValid() )
			return;

		var rDrive = WorldRotation;

		if ( InputAcceleration != 0f )
		{
			var drivingAxis = rDrive * DrivingAxis;
			var speed = InputAcceleration * DrivingSpeed;

			Velocity += drivingAxis * speed * deltaTime;
		}

		if ( InputSteering != 0f )
		{
			var steeringAxis = rDrive * SteeringAxis;
			var torque = InputSteering * SteeringTorque;

			Rigidbody.AngularVelocity += steeringAxis * torque * deltaTime;
		}
	}
}
