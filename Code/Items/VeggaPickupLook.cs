using Sandbox;
using System;

namespace Sandbox;

public static class VeggaPickupLook
{
	const string LookAtRadiusCookieKey = "vegga_lookatradius";
	const string LookAtDistanceCookieKey = "vegga_lookatdistance";

	const float LookAtRadiusMin = 0.00001f;
	const float LookAtRadiusMax = 1.0f;
	const float LookAtRadiusWorldScale = 50f;
	const float LookAtDistanceMin = 1f;
	const float LookAtDistanceMax = 5000f;

	static float _lookAtRadius = 0.15f;
	static float _lookAtDistance = 200f;
	static bool _loaded;

	static void EnsureLoaded()
	{
		if ( _loaded )
			return;
		_loaded = true;

		try
		{
			LookAtRadius = Cookie.Get( LookAtRadiusCookieKey, _lookAtRadius );
			LookAtDistance = Cookie.Get( LookAtDistanceCookieKey, _lookAtDistance );
		}
		catch
		{
			// If cookies aren't available (e.g., server realm), fall back to defaults.
			LookAtRadius = _lookAtRadius;
			LookAtDistance = _lookAtDistance;
		}
	}

	static void Save()
	{
		try
		{
			Cookie.Set( LookAtRadiusCookieKey, _lookAtRadius );
			Cookie.Set( LookAtDistanceCookieKey, _lookAtDistance );
		}
		catch
		{
			// No-op if cookies aren't available.
		}
	}

	public static float LookAtRadius
	{
		get => _lookAtRadius;
		set => _lookAtRadius = value.Clamp( LookAtRadiusMin, LookAtRadiusMax );
	}

	public static float LookAtDistance
	{
		get => _lookAtDistance;
		set => _lookAtDistance = value.Clamp( LookAtDistanceMin, LookAtDistanceMax );
	}

	[ConCmd( "vegga_lookatradius", Help = "Set/get pickup look-at radius strength. Usage: vegga_lookatradius [0.00001..1.0]" )]
	public static void CmdLookAtRadius( float value = -1f )
	{
		EnsureLoaded();

		if ( value < 0f )
		{
			Log.Info( $"[PickupLook] vegga_lookatradius={_lookAtRadius:0.#####} (clamp {LookAtRadiusMin:0.#####}..{LookAtRadiusMax:0.#####})" );
			return;
		}

		LookAtRadius = value;
		Save();
		Log.Info( $"[PickupLook] vegga_lookatradius set to {_lookAtRadius:0.#####}" );
	}

	[ConCmd( "vegga.lookatradius", Help = "Alias for vegga_lookatradius (deprecated)." )]
	public static void CmdLookAtRadiusAlias( float value = -1f )
	{
		CmdLookAtRadius( value );
	}

	[ConCmd( "vegga_lookatdistance", Help = "Set/get max look-at trace distance (range). Usage: vegga_lookatdistance [1..5000]" )]
	public static void CmdLookAtDistance( float value = -1f )
	{
		EnsureLoaded();

		if ( value < 0f )
		{
			Log.Info( $"[PickupLook] vegga_lookatdistance={_lookAtDistance:0.##} (clamp {LookAtDistanceMin:0}..{LookAtDistanceMax:0})" );
			return;
		}

		LookAtDistance = value;
		Save();
		Log.Info( $"[PickupLook] vegga_lookatdistance set to {_lookAtDistance:0.##}" );
	}

	[ConCmd( "vegga.lookatdistance", Help = "Alias for vegga_lookatdistance (deprecated)." )]
	public static void CmdLookAtDistanceAlias( float value = -1f )
	{
		CmdLookAtDistance( value );
	}

	static float GetPickupApproxRadius( VeggaPickupItem pickup )
	{
		if ( pickup == null || !pickup.IsValid() )
			return 6f;

		var sphere = pickup.Components.Get<SphereCollider>();
		if ( sphere != null && sphere.IsValid() && sphere.Enabled && sphere.Radius > 0 )
			return sphere.Radius;

		var box = pickup.Components.Get<BoxCollider>();
		if ( box != null && box.IsValid() && box.Enabled )
		{
			var halfMax = MathF.Max( MathF.Max( MathF.Abs( box.Scale.x ), MathF.Abs( box.Scale.y ) ), MathF.Abs( box.Scale.z ) ) * 0.5f;
			return MathF.Max( 2f, halfMax );
		}

		return 6f;
	}

	static bool HasLineOfSightToPickup( Scene scene, Ray ray, VeggaPickupItem pickup )
	{
		if ( scene == null || pickup == null || !pickup.IsValid() )
			return false;

		var origin = ray.Position;
		var target = pickup.WorldPosition;
		var tr = scene.Trace.Ray( origin, target )
			.WithoutTags( "player", "trigger" )
			.Run();

		if ( !tr.Hit )
			return true;

		var hitPickup = tr.GameObject?.Components.GetInDescendantsOrSelf<VeggaPickupItem>();
		return hitPickup == pickup;
	}

	public static VeggaPickupItem GetLookedAtPickup( Scene scene )
	{
		EnsureLoaded();

		var camera = scene?.Camera;
		if ( camera == null )
			return null;

		var ray = camera.ScreenNormalToRay( new Vector2( 0.5f, 0.5f ) );
		var maxDistance = LookAtDistance;

		// Stage 1 (pixel-perfect): if you're actually hitting the pickup's collider,
		// always return it regardless of look-at radius.
		var directTrace = scene.Trace.Ray( ray, maxDistance )
			.WithoutTags( "player" )
			.Run();
		if ( directTrace.Hit )
		{
			var directPickup = directTrace.GameObject?.Components.GetInDescendantsOrSelf<VeggaPickupItem>();
			if ( directPickup != null && directPickup.IsValid() )
				return directPickup;
		}

		var origin = ray.Position;
		var forward = ray.Forward.Normal;
		var baseRadius = LookAtRadius * LookAtRadiusWorldScale;

		VeggaPickupItem best = null;
		var bestScore = float.NegativeInfinity;

		foreach ( var pickup in scene.GetAllComponents<VeggaPickupItem>() )
		{
			if ( pickup == null || !pickup.IsValid() )
				continue;

			var pos = pickup.WorldPosition;
			var to = pos - origin;
			var along = Vector3.Dot( to, forward );
			if ( along <= 0f || along > maxDistance )
				continue;

			var closest = origin + forward * along;
			var lateral = Vector3.DistanceBetween( pos, closest );

			var pickupRadius = GetPickupApproxRadius( pickup );
			var allowed = baseRadius + MathF.Min( 12f, pickupRadius );
			if ( lateral > allowed )
				continue;

			// Prefer the item you're aiming at (center), then closer along ray.
			var aimDot = (to.Normal).Dot( forward );
			var lateralScore = 1f - (lateral / allowed);
			var distanceScore = 1f - (along / maxDistance);
			var score = (aimDot * 2.0f) + (lateralScore * 2.0f) + (distanceScore * 0.15f);

			if ( score <= bestScore )
				continue;

			// Avoid selecting pickups behind walls/props.
			if ( !HasLineOfSightToPickup( scene, ray, pickup ) )
				continue;

			bestScore = score;
			best = pickup;
		}

		return best;
	}

	/// <summary>
	/// Returns +1 when the camera is on the player's right shoulder, -1 when on the left.
	/// Falls back to +1 when it can't determine.
	/// </summary>
	public static int GetCameraShoulderSide( Scene scene, GameObject player )
	{
		var camera = scene?.Camera;
		if ( camera == null || player == null || !player.IsValid )
			return 1;

		var playerPos = player.WorldPosition;
		var right = player.WorldRotation.Right;
		var camOffset = camera.Transform.Position - playerPos;
		var side = Vector3.Dot( camOffset, right );
		if ( side > 0.001f ) return 1;
		if ( side < -0.001f ) return -1;
		return 1;
	}
}
