using Sandbox;
using Sandbox.Money;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Host-side helper that spawns large cash drops over time, building a neat pallet of 10M boxes.
/// Allows other players to pick up already-placed pieces while the pallet is still building.
/// </summary>
public sealed class VeggaCashPalletSpawner : Component
{
	// Layout tuning for the 10M cash box pallet.
	// We now use ModelCollider-only for the box, so the old BoxCollider-derived Z spacing caused
	// large gaps and "floating" layers. These values are tuned to be tighter while still avoiding
	// overlap-driven expansion when physics enables.
	const float SpacingX = 33.0f;
	const float SpacingY = 19.0f;
	const float SpacingZ = 18.0f;
	const float BaseLift = 3.0f;

	Scene _scene;
	GameObject _player;
	PlayerVeggaStats _playerStats;
	Vector3 _cameraOrigin;
	Vector3 _baseEndPos;
	Rotation _rot;
	Vector3 _dropVelocity;
	Guid _droppedBy;
	Vector3 _upAxis;

	readonly List<int> _pending = new();
	int _pendingIndex;
	readonly Dictionary<int, float> _reservedSlotsUntil = new();
	readonly HashSet<int> _occupiedSlots = new();
	float _nextRescanTime;

	int _targetBoxCapacity;
	int _cols;
	int _rows;
	int _perLayer;

	float _nextSpawnTime;
	float _spawnInterval;
	int _smallJitterIndex;

	public void Begin(
		Scene scene,
		GameObject player,
		Vector3 cameraOrigin,
		Vector3 baseEndPos,
		Rotation rot,
		Vector3 dropVelocity,
		Guid droppedBy,
		IReadOnlyList<int> chunks )
	{
		_scene = scene;
		_player = player;
		_cameraOrigin = cameraOrigin;
		_baseEndPos = baseEndPos;
		// IMPORTANT: use the floor-aligned pallet rotation passed from the drop code.
		// Do not rebuild from camera/head here.
		_rot = rot;
		_dropVelocity = dropVelocity;
		_droppedBy = droppedBy;
		_upAxis = _rot.Up;

		_playerStats = _player != null && _player.IsValid ? _player.Components.Get<PlayerVeggaStats>() : null;

		_pending.Clear();
		_pending.AddRange( chunks );
		_pendingIndex = 0;

		_playerStats?.SetCashDropBuildProgress( active: true, progress01: 0f );

		int pendingBoxes = 0;
		for ( int i = 0; i < _pending.Count; i++ )
			if ( _pending[i] == VeggaCurrency.CashBoxAmount ) pendingBoxes++;

		int existingBoxes = CountExistingNearbyBoxes();
		_targetBoxCapacity = Math.Max( 1, existingBoxes + pendingBoxes );

		ComputeGrid( _targetBoxCapacity, out _cols, out _rows );
		_perLayer = Math.Max( 1, _cols * _rows );

		// Pacing: base primarily on total pieces, not just box count.
		// Previously this used _targetBoxCapacity, which could be 1 (one box) while we still had
		// lots of bundles/bills queued, causing extremely slow spawning.
		int totalPieces = Math.Max( 1, _pending.Count );
		float desiredTotalSeconds = (totalPieces * 0.045f).Clamp( 0.6f, 12f );
		// Keep boxes a bit more "cinematic", but don't force huge minimums.
		if ( pendingBoxes >= 2 )
			desiredTotalSeconds = MathF.Max( desiredTotalSeconds, (pendingBoxes * 0.12f).Clamp( 0.9f, 7f ) );
		_spawnInterval = desiredTotalSeconds / totalPieces;

		_nextSpawnTime = Time.Now + 0.05f;
		_smallJitterIndex = 0;
		_nextRescanTime = 0f;
		_occupiedSlots.Clear();
		_reservedSlotsUntil.Clear();
	}

	protected override void OnUpdate()
	{
		if ( Network.IsProxy )
			return;
		if ( _scene == null )
			_scene = Scene ?? Game.ActiveScene;
		if ( _scene == null )
			return;

		if ( _pendingIndex >= _pending.Count )
		{
			VeggaCashBuildLock.UnlockAllNear( _scene, _baseEndPos, radius: 520f );
			_playerStats?.SetCashDropBuildProgress( active: false, progress01: 1f );
			GameObject.Destroy();
			return;
		}

		// Spawn enough pieces to catch up if we're running behind, but cap per-frame so we don't hitch.
		int spawnedThisFrame = 0;
		const int MaxSpawnPerFrame = 4;
		while ( _pendingIndex < _pending.Count && Time.Now >= _nextSpawnTime && spawnedThisFrame < MaxSpawnPerFrame )
		{
			int amount = _pending[_pendingIndex++];
			_playerStats?.SetCashDropBuildProgress( active: true, progress01: _pending.Count > 0 ? (_pendingIndex / (float)_pending.Count) : 1f );

			Vector3 endPos;
			Vector3 velocity;
			if ( amount == VeggaCurrency.CashBoxAmount )
			{
				int slot = FindFirstEmptyBoxSlotAndReserve();
				endPos = GetSlotEndPos( slot );
				velocity = Vector3.Zero; // keep boxes stable; gravity will handle the settle
			}
			else
			{
				// Smaller piles: keep them close, slightly spaced.
				float jitterX = ((_smallJitterIndex % 3) - 1) * 7.0f;
				float jitterY = ((_smallJitterIndex / 3) - 1) * 7.0f;
				_smallJitterIndex++;
				endPos = _baseEndPos + _rot.Right * jitterX + _rot.Forward * jitterY + Vector3.Up * 3.0f;
				velocity = _dropVelocity;
			}

			var startPos = FindLowerBack( _player )?.WorldPosition
				?? (_player != null && _player.IsValid ? (_player.WorldPosition + _player.WorldRotation.Backward * 6f + Vector3.Up * 30f) : (endPos + Vector3.Up * 40f));

			var cashGo = CashWorldDrop.Spawn( _scene, startPos, _rot, amount, _droppedBy );
			if ( cashGo == null || !cashGo.IsValid() )
				return;

			// While building: freeze placed 10M boxes so new spawns can't shove the whole pallet around.
			if ( amount == VeggaCurrency.CashBoxAmount )
				cashGo.Components.Create<VeggaCashBuildLock>();

			// Force the stack item rotation. Prefab auth rotation or camera pitch should never affect pallet pieces.
			cashGo.WorldRotation = _rot;

			var anim = cashGo.Components.Create<VeggaDropThrowAnimator>();
			anim.Begin( _player, _cameraOrigin, endPos, _rot, velocity,
				registerPersistence: false,
				itemId: VeggaCurrency.CashItemId,
				count: amount,
				durability: 0,
				prefabPath: null,
				droppedBy: _droppedBy,
				droppedAtUtcTicks: DateTime.UtcNow.Ticks,
				startDelaySeconds: 0f );

			spawnedThisFrame++;
			_nextSpawnTime += MathF.Max( 0.01f, _spawnInterval );
		}
	}

	void CleanupSlotReservations()
	{
		if ( _reservedSlotsUntil.Count <= 0 )
			return;
		var now = Time.Now;
		var expired = _reservedSlotsUntil.Where( kv => now >= kv.Value ).Select( kv => kv.Key ).ToList();
		for ( int i = 0; i < expired.Count; i++ )
			_reservedSlotsUntil.Remove( expired[i] );
	}

	void RescanOccupiedSlotsIfNeeded()
	{
		if ( _scene == null )
			return;
		if ( Time.Now < _nextRescanTime )
			return;
		_nextRescanTime = Time.Now + 0.25f;

		_occupiedSlots.Clear();

		const float radius = 420f;
		float radius2 = radius * radius;
		foreach ( var p in _scene.GetAllComponents<VeggaPickupItem>() )
		{
			if ( p == null || !p.IsValid() ) continue;
			if ( p.ItemId != VeggaCurrency.CashItemId ) continue;
			if ( p.Quantity != VeggaCurrency.CashBoxAmount ) continue;
			// INCLUDE in-flight boxes too; otherwise we think their slots are empty and stack into a pillar.
			if ( p.IsBeingLooted ) continue;
			if ( Vector3.DistanceBetweenSquared( p.WorldPosition, _baseEndPos ) > radius2 ) continue;

			int idx = ApproxSlotIndex( p.WorldPosition );
			if ( idx >= 0 )
				_occupiedSlots.Add( idx );
		}

		// Also treat reserved slots as occupied.
		foreach ( var k in _reservedSlotsUntil.Keys )
			_occupiedSlots.Add( k );
	}

	int CountExistingNearbyBoxes()
	{
		if ( _scene == null )
			return 0;

		const float radius = 320f;
		float radius2 = radius * radius;
		int count = 0;
		foreach ( var p in _scene.GetAllComponents<VeggaPickupItem>() )
		{
			if ( p == null || !p.IsValid() ) continue;
			if ( p.ItemId != VeggaCurrency.CashItemId ) continue;
			if ( p.Quantity != VeggaCurrency.CashBoxAmount ) continue;
			if ( p.IsInDropThrow || p.IsBeingLooted ) continue;
			if ( Vector3.DistanceBetweenSquared( p.WorldPosition, _baseEndPos ) > radius2 ) continue;
			count++;
		}
		return count;
	}

	static void ComputeGrid( int boxes, out int cols, out int rows )
	{
		// Rubik/cube-style build: choose an NxN footprint where N ~= cbrt(boxes).
		// Examples:
		// - 1..8 boxes   => 2x2 per layer
		// - 9..27 boxes  => 3x3 per layer
		// - 28..64 boxes => 4x4 per layer
		boxes = Math.Max( 1, boxes );
		float n = MathF.Pow( boxes, 1f / 3f );
		int target = (int)MathF.Ceiling( n );
		cols = Math.Clamp( target, 2, 18 );
		rows = cols;
	}

	Vector3 GetSlotEndPos( int slotIndex )
	{
		int layer = slotIndex / _perLayer;
		int within = slotIndex % _perLayer;
		int row = within / _cols;
		int col = within % _cols;

		float centeredX = col - (_cols - 1) * 0.5f;
		float centeredY = row - (_rows - 1) * 0.5f;
		var offset = _rot.Right * (centeredX * SpacingX) + _rot.Forward * (centeredY * SpacingY) + _rot.Up * (layer * SpacingZ);
		return _baseEndPos + offset + _rot.Up * BaseLift;
	}

	int FindFirstEmptyBoxSlotAndReserve()
	{
		CleanupSlotReservations();
		RescanOccupiedSlotsIfNeeded();

		// Deterministic slot selection.
		int capacity = Math.Max( 1, _targetBoxCapacity );
		for ( int i = 0; i < capacity; i++ )
			if ( !_occupiedSlots.Contains( i ) && !_reservedSlotsUntil.ContainsKey( i ) )
			{
				// Reserve the slot long enough for the in-flight box to finish its throw.
				// Without this, IsInDropThrow boxes don't count as occupied yet, causing a vertical pile.
				_reservedSlotsUntil[i] = Time.Now + 6.0f;
				_occupiedSlots.Add( i );
				return i;
			}

		// If we're completely full, extend upward one more layer.
		_targetBoxCapacity += _perLayer;
		_reservedSlotsUntil[capacity] = Time.Now + 6.0f;
		_occupiedSlots.Add( capacity );
		return capacity;
	}

	int ApproxSlotIndex( Vector3 worldPos )
	{
		var local = worldPos - _baseEndPos;
		float x = Vector3.Dot( local, _rot.Right );
		float y = Vector3.Dot( local, _rot.Forward );
		float z = Vector3.Dot( local, _rot.Up );

		int col = (int)MathF.Round( x / SpacingX + (_cols - 1) * 0.5f );
		int row = (int)MathF.Round( y / SpacingY + (_rows - 1) * 0.5f );
		int layer = (int)MathF.Round( z / SpacingZ );

		if ( col < 0 || col >= _cols ) return -1;
		if ( row < 0 || row >= _rows ) return -1;
		if ( layer < 0 ) return -1;

		return layer * _perLayer + row * _cols + col;
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
}
