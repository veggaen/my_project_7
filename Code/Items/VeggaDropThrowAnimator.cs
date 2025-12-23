using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Animates a newly dropped pickup from the player's LowerBack out to the final drop position.
/// Host-only: disables physics/collisions during the animation, then restores and applies velocity.
/// </summary>
public sealed class VeggaDropThrowAnimator : Component
{
	const float MinThrowSeconds = 0.18f;
	const float MaxThrowSeconds = 1.8f;

	GameObject _player;
	Vector3 _startPos;
	Vector3 _endPos;
	Rotation _endRot;
	Vector3 _dropVelocity;
	float _startTime;
	float _duration;
	Vector3 _startScale;
	int _sideSign;
	float _phase;

	bool _cached;
	readonly List<Collider> _colliders = new();
	readonly List<bool> _colliderEnabled = new();
	readonly List<Rigidbody> _rigidbodies = new();
	readonly List<bool> _rigidbodyEnabled = new();

	// Optional persistence
	bool _registerPersistence;
	int _persistItemId;
	int _persistCount;
	int _persistDurability;
	string _persistPrefabPath;
	Guid _persistDroppedBy;
	long _persistDroppedAtUtcTicks;

	public void Begin(
		GameObject player,
		Vector3 cameraOrigin,
		Vector3 endPos,
		Rotation endRot,
		Vector3 dropVelocity,
		bool registerPersistence,
		int itemId,
		int count,
		int durability,
		string prefabPath,
		Guid droppedBy,
		long droppedAtUtcTicks )
	{
		_player = player;
		_endPos = endPos;
		_endRot = endRot;
		_dropVelocity = dropVelocity;
		_registerPersistence = registerPersistence;
		_persistItemId = itemId;
		_persistCount = count;
		_persistDurability = durability;
		_persistPrefabPath = prefabPath;
		_persistDroppedBy = droppedBy;
		_persistDroppedAtUtcTicks = droppedAtUtcTicks;

		_startScale = WorldScale;
		_startTime = Time.Now;

		var playerPos = player != null && player.IsValid ? player.WorldPosition : WorldPosition;
		var playerRot = player != null && player.IsValid ? player.WorldRotation : Rotation.Identity;
		var right = playerRot.Right;
		var sideDot = Vector3.Dot( cameraOrigin - playerPos, right );
		_sideSign = sideDot >= 0 ? 1 : -1;

		var seed = HashCode.Combine( itemId, count, endPos.GetHashCode(), droppedAtUtcTicks.GetHashCode() );
		_phase = seed * 0.001f;

		_startPos = FindLowerBack( player )?.WorldPosition ?? (playerPos + playerRot.Backward * 6f + Vector3.Up * 30f);
		WorldPosition = _startPos;
		WorldRotation = endRot;

		var dist = Vector3.DistanceBetween( _startPos, _endPos );
		_duration = (0.32f + dist / 520f).Clamp( MinThrowSeconds, MaxThrowSeconds );

		CacheAndDisablePhysicsAndCollisions();
		foreach ( var pickup in Components.GetAll<VeggaPickupItem>( FindMode.InDescendants ) )
		{
			if ( pickup == null || !pickup.IsValid() ) continue;
			pickup.BeginDropThrow();
		}
		var rootPickup = Components.Get<VeggaPickupItem>();
		if ( rootPickup != null && rootPickup.IsValid() )
			rootPickup.BeginDropThrow();
	}

	static GameObject FindLowerBack( GameObject root )
	{
		if ( root == null || !root.IsValid )
			return null;
		if ( string.Equals( root.Name, "LowerBack", StringComparison.OrdinalIgnoreCase ) )
			return root;
		foreach ( var child in root.Children )
		{
			var found = FindLowerBack( child );
			if ( found != null && found.IsValid )
				return found;
		}
		return null;
	}

	void CacheAndDisablePhysicsAndCollisions()
	{
		if ( _cached )
			return;
		_cached = true;

		_colliders.Clear();
		_colliderEnabled.Clear();
		foreach ( var c in Components.GetAll<Collider>( FindMode.InDescendants ) )
		{
			if ( c == null || !c.IsValid() ) continue;
			_colliders.Add( c );
			_colliderEnabled.Add( c.Enabled );
			c.Enabled = false;
		}
		var rootCollider = Components.Get<Collider>();
		if ( rootCollider != null && rootCollider.IsValid() && !_colliders.Contains( rootCollider ) )
		{
			_colliders.Add( rootCollider );
			_colliderEnabled.Add( rootCollider.Enabled );
			rootCollider.Enabled = false;
		}

		_rigidbodies.Clear();
		_rigidbodyEnabled.Clear();
		foreach ( var rb in Components.GetAll<Rigidbody>( FindMode.InDescendants ) )
		{
			if ( rb == null || !rb.IsValid() ) continue;
			_rigidbodies.Add( rb );
			_rigidbodyEnabled.Add( rb.Enabled );
			rb.Enabled = false;
		}
		var rootRb = Components.Get<Rigidbody>();
		if ( rootRb != null && rootRb.IsValid() && !_rigidbodies.Contains( rootRb ) )
		{
			_rigidbodies.Add( rootRb );
			_rigidbodyEnabled.Add( rootRb.Enabled );
			rootRb.Enabled = false;
		}
	}

	void RestorePhysicsAndCollisions()
	{
		for ( int i = 0; i < _colliders.Count; i++ )
		{
			var c = _colliders[i];
			if ( c == null || !c.IsValid() ) continue;
			c.Enabled = _colliderEnabled[i];
		}

		// Determine if this is a "problem" drop that must keep physics disabled until the
		// primitive collider fix is applied (ores + goblet mould).
		var rootPickup = Components.Get<VeggaPickupItem>();
		var descendantPickups = Components.GetAll<VeggaPickupItem>( FindMode.InDescendants );
		bool hasPickup = (rootPickup != null && rootPickup.IsValid()) || descendantPickups.Any( p => p != null && p.IsValid() );
		bool isProblemDrop = false;
		if ( rootPickup != null && rootPickup.IsValid() )
			isProblemDrop |= VeggaOreVisuals.IsOre( rootPickup.ItemId ) || rootPickup.ItemId == VeggaItemIds.MouldGoblet;
		foreach ( var p in descendantPickups )
		{
			if ( p == null || !p.IsValid() ) continue;
			if ( VeggaOreVisuals.IsOre( p.ItemId ) || p.ItemId == VeggaItemIds.MouldGoblet )
			{
				isProblemDrop = true;
				break;
			}
		}

		if ( !hasPickup )
		{
			for ( int i = 0; i < _rigidbodies.Count; i++ )
			{
				var rb = _rigidbodies[i];
				if ( rb == null || !rb.IsValid() ) continue;
				rb.Enabled = _rigidbodyEnabled[i];
				rb.MotionEnabled = true;
				rb.Gravity = true;
			}
		}
		else if ( !isProblemDrop )
		{
			// Normal items: restore physics now. This avoids breaking prefabs where the pickup
			// component is on a different node than the Rigidbody.
			for ( int i = 0; i < _rigidbodies.Count; i++ )
			{
				var rb = _rigidbodies[i];
				if ( rb == null || !rb.IsValid() ) continue;
				rb.Enabled = _rigidbodyEnabled[i];
				rb.MotionEnabled = true;
				rb.Gravity = true;
			}
		}
		else
		{
			// Problem items (ores + goblet): keep RBs off; EndDropThrow enables at the correct time.
			for ( int i = 0; i < _rigidbodies.Count; i++ )
			{
				var rb = _rigidbodies[i];
				if ( rb == null || !rb.IsValid() ) continue;
				rb.Enabled = false;
				rb.MotionEnabled = false;
				rb.Gravity = false;
			}
		}

		// Let pickup logic re-apply special-case colliders/physics (ore/goblet), and set velocity there.
		var anyPickup = false;
		foreach ( var pickup in descendantPickups )
		{
			if ( pickup == null || !pickup.IsValid() ) continue;
			pickup.EndDropThrow( _dropVelocity );
			anyPickup = true;
		}
		if ( rootPickup != null && rootPickup.IsValid() )
		{
			rootPickup.EndDropThrow( _dropVelocity );
			anyPickup = true;
		}

		// Safety: for normal items, if EndDropThrow didn't apply velocity to a rigidbody (prefab layout mismatch),
		// apply it here.
		if ( anyPickup && !isProblemDrop )
		{
			var applyRb = Components.Get<Rigidbody>()
				?? Components.GetAll<Rigidbody>( FindMode.InDescendants ).FirstOrDefault();
			if ( applyRb != null && applyRb.IsValid() )
				applyRb.Velocity = _dropVelocity;
		}

		// Fallback: if no pickup exists, still apply velocity to a rigidbody.
		if ( !anyPickup )
		{
			var applyRb = Components.Get<Rigidbody>()
				?? Components.GetAll<Rigidbody>( FindMode.InDescendants ).FirstOrDefault();
			if ( applyRb != null && applyRb.IsValid() )
				applyRb.Velocity = _dropVelocity;
		}
	}

	protected override void OnUpdate()
	{
		if ( Network.IsProxy )
			return;
		if ( _duration <= 0f )
			return;

		var age = Time.Now - _startTime;
		var t = (age / _duration).Clamp( 0f, 1f );
		var eased = t * t * (3f - 2f * t);

		var playerPos = _player != null && _player.IsValid ? _player.WorldPosition : _endPos;
		var playerRot = _player != null && _player.IsValid ? _player.WorldRotation : Rotation.Identity;
		var right = playerRot.Right;
		var forward = playerRot.Forward;

		var dist = Vector3.DistanceBetween( _startPos, _endPos );
		var sideDist = (35f + dist * 0.12f).Clamp( 25f, 85f );
		var upDist = (25f + dist * 0.06f).Clamp( 20f, 65f );
		var backDist = (25f + dist * 0.10f).Clamp( 20f, 90f );
		var wobble = MathF.Sin( age * 10f + _phase ) * 0.10f;

		var control = playerPos
			+ right * (sideDist * _sideSign * (1f + wobble))
			+ forward * (-backDist)
			+ Vector3.Up * upDist;

		var a = Vector3.Lerp( _startPos, control, eased );
		var b = Vector3.Lerp( control, _endPos, eased );
		WorldPosition = Vector3.Lerp( a, b, eased );

		// Slight grow-out as it leaves the back.
		var scaleFactor = 0.80f + 0.20f * eased;
		WorldScale = _startScale * scaleFactor;

		if ( t >= 1f )
		{
			WorldPosition = _endPos;
			WorldRotation = _endRot;
			WorldScale = _startScale;
			RestorePhysicsAndCollisions();

			if ( _registerPersistence && !string.IsNullOrWhiteSpace( _persistPrefabPath ) )
			{
				var persistId = WorldDropPersistence.RegisterDrop( GameObject, _persistItemId, _persistCount, _persistDurability, _persistPrefabPath, _dropVelocity, _persistDroppedBy, _persistDroppedAtUtcTicks );
				if ( persistId != Guid.Empty )
				{
					var rootPickup = Components.Get<VeggaPickupItem>();
					if ( rootPickup != null && rootPickup.IsValid() )
					{
						rootPickup.PersistId = persistId;
						rootPickup.DroppedAtUtcTicks = _persistDroppedAtUtcTicks;
					}
					foreach ( var pickup in Components.GetAll<VeggaPickupItem>( FindMode.InDescendants ) )
					{
						if ( pickup == null || !pickup.IsValid() ) continue;
						pickup.PersistId = persistId;
						pickup.DroppedAtUtcTicks = _persistDroppedAtUtcTicks;
					}
				}
			}

			Destroy();
		}
	}
}
