using Sandbox;
using System;
using System.Linq;
using System.Threading.Tasks;
using Sandbox.Money;

namespace Sandbox;

/// <summary>
/// Attach this to any world object to make it a pickupable item.
/// When a player walks near it (auto-loot) or presses E, it goes into their inventory.
/// </summary>
public sealed class VeggaPickupItem : Component, Component.ITriggerListener
{
	const bool DebugNet = true;
	const float DefaultOwnDropAutoLootDelaySeconds = 300f;
	const float MinVacuumSeconds = 0.12f;

	/// <summary>
	/// If this pickup was spawned by a player dropping an inventory item, this is their Connection.Id.
	/// Used to prevent auto-loot vacuuming your own drops for a short time.
	/// </summary>
	[Property, Sync]
	public Guid DroppedById { get; set; } = Guid.Empty;

	/// <summary>
	/// Host time (Time.Now) when this pickup was dropped.
	/// </summary>
	[Property, Sync]
	public float DroppedAtTime { get; set; } = 0f;

	/// <summary>
	/// Persistent ID for world-drop saving across restarts.
	/// Host assigns this when spawning a dropped item.
	/// </summary>
	[Property, Sync]
	public Guid PersistId { get; set; } = Guid.Empty;

	/// <summary>
	/// UTC timestamp (ticks) used for TTL on persisted drops.
	/// </summary>
	[Property, Sync]
	public long DroppedAtUtcTicks { get; set; } = 0;

	/// <summary>
	/// How long the dropper's auto-loot should ignore this item.
	/// Manual pickup (E) is still allowed.
	/// </summary>
	[Property]
	public float OwnDropAutoLootDelay { get; set; } = DefaultOwnDropAutoLootDelaySeconds;
	/// <summary>
	/// The item ID from VeggaItemRegistry.
	/// </summary>
	[Property, Title( "Item ID" ), Sync]
	public int ItemId { get; set; } = 100; // Default: 200g Gold Bar

	/// <summary>
	/// How many of this item the pickup contains.
	/// </summary>
	[Property, Sync]
	public int Quantity { get; set; } = 1;

	/// <summary>
	/// Optional per-item durability/custom value.
	/// For ores, this stores a visual variant (1..3) so the rock model stays stable.
	/// </summary>
	[Property, Sync]
	public int Durability { get; set; } = 0;

	/// <summary>
	/// Pickup range for "Press E" interaction.
	/// </summary>
	[Property]
	public float InteractRange { get; set; } = 100f;

	/// <summary>
	/// Auto-loot vacuum range (items fly toward player).
	/// </summary>
	[Property]
	public float AutoLootRange { get; set; } = 150f;

	/// <summary>
	/// If true, show floating item name above the item.
	/// </summary>
	[Property]
	public bool ShowFloatingName { get; set; } = true;

	/// <summary>
	/// Time before the item can be picked up (for drops).
	/// </summary>
	[Property]
	public float PickupDelay { get; set; } = 0f;

	/// <summary>
	/// Is this pickup currently being vacuumed to a player?
	/// </summary>
	public bool IsBeingLooted { get; private set; } = false;

	/// <summary>
	/// True while a freshly dropped item is being animated from the player's LowerBack out to the drop point.
	/// While true, we disable pickup interaction and pause any physics-enforcement.
	/// </summary>
	internal bool IsInDropThrow { get; private set; } = false;

	/// <summary>
	/// Target player for vacuum loot.
	/// </summary>
	private GameObject _vacuumTarget;
	private GameObject _vacuumPickupPlayer;
	private Guid _vacuumPickupRequesterId;
	private bool _vacuumGrantViaRpc;
	private Vector3 _vacuumStartPos;
	private float _vacuumStartTime;
	private Vector3 _vacuumSwirlAxis;
	private float _vacuumSwirlPhase;
	private Vector3 _vacuumStartScale;
	private float _vacuumStartDistance;
	private float _vacuumDuration;
	private int _vacuumSideSign;
	private bool _vacuumCollidersCached;
	private bool _vacuumModelColliderEnabled;
	private bool _vacuumSphereColliderEnabled;
	private bool _vacuumBoxColliderEnabled;

	/// <summary>
	/// Time remaining on pickup delay.
	/// </summary>
	private float _pickupTimer;

	/// <summary>
	/// Host-side: prevents double-pickup.
	/// </summary>
	[Sync] private bool _consumed { get; set; } = false;

	/// <summary>
	/// Cached item definition.
	/// </summary>
	public VeggaItemDef ItemDef => VeggaItemRegistry.Get( ItemId );

	int _lastVisualItemId;
	int _lastVisualDurability;

	protected override void OnStart()
	{
		try
		{
			_pickupTimer = PickupDelay;

			UpdateOreVisualsIfNeeded( allowRandomizeOnHost: true );
			EnsureDropPhysicsReady();

			var def = ItemDef;
			if ( def != null )
			{
				Log.Info( $"📦 Pickup spawned: {def.Name} x{Quantity}" );
			}
			else
			{
				Log.Warning( $"⚠️ VeggaPickupItem has invalid ItemId: {ItemId}" );
			}
		}
		catch ( Exception ex )
		{
			Log.Error( ex, $"VeggaPickupItem.OnStart failed (ItemId={ItemId} Qty={Quantity} GO={GameObject?.Name})" );
		}
	}

	void EnsureDropPhysicsReady()
	{
		// Only the host simulates physics for networked pickups.
		if ( Network.IsProxy )
			return;

		// Only force this for items known to have flaky physics/collision when dropped.
		bool isOre = VeggaOreVisuals.IsOre( ItemId );
		bool isMouldGoblet = ItemId == VeggaItemIds.MouldGoblet;
		bool needsPhysicsFix = isOre || isMouldGoblet;
		if ( !needsPhysicsFix )
			return;

		var modelCollider = Components.Get<ModelCollider>();
		var renderer = Components.Get<ModelRenderer>();
		if ( modelCollider != null && modelCollider.IsValid() )
		{
			if ( renderer != null && renderer.IsValid() && renderer.Model != null )
				modelCollider.Model = renderer.Model;
			// Ores and goblet mould use simple collider fallbacks for stability.
			modelCollider.Enabled = !(isOre || isMouldGoblet);
			modelCollider.IsTrigger = false;
			modelCollider.Static = false;
		}

		if ( isMouldGoblet )
		{
			// A sphere collider makes the mould roll forever. Use a box like we do for cash drops.
			var sphere = Components.Get<SphereCollider>();
			if ( sphere != null && sphere.IsValid() )
				sphere.Enabled = false;

			var box = Components.Get<BoxCollider>();
			if ( box == null )
				box = Components.Create<BoxCollider>();
			if ( box != null && box.IsValid() )
			{
				box.Enabled = true;
				box.IsTrigger = false;
				// Roughly "brick"-ish so it settles instead of rolling.
				// Make it thick enough that it won't slip through terrain seams.
				box.Scale = new Vector3( 12f, 12f, 10f );
			}
		}
		else
		{
			// Fallback collider: ensure a simple collider so the rigidbody can simulate.
			var sphere = Components.Get<SphereCollider>();
			if ( sphere == null )
				sphere = Components.Create<SphereCollider>();
			if ( sphere != null && sphere.IsValid() )
			{
				sphere.Enabled = true;
				sphere.IsTrigger = false;
				// Too-small spheres can slip through terrain seams. Keep this modest but stable.
				sphere.Radius = 10f;
			}
		}

		var rb = Components.Get<Rigidbody>();
		if ( rb == null )
			rb = Components.Create<Rigidbody>();
		if ( rb != null && rb.IsValid() )
		{
			rb.Enabled = true;
			rb.MotionEnabled = true;
			rb.Gravity = true;
			// Match the "heavy" feel used elsewhere so drops settle.
			rb.MassOverride = 25;
			if ( isMouldGoblet )
			{
				rb.LinearDamping = 0.35f;
				rb.AngularDamping = 4f;
			}
			else if ( isOre )
			{
				rb.LinearDamping = 0.25f;
				rb.AngularDamping = 1f;
			}
		}
	}

	protected override void OnUpdate()
	{
		if ( _consumed ) return;

		UpdateOreVisualsIfNeeded( allowRandomizeOnHost: true );
		if ( IsInDropThrow )
			return;
		if ( !Network.IsProxy && !IsBeingLooted )
			EnsureDropPhysicsReady();

		// Clients: only handle input (request pickup). Never simulate vacuum/physics.
		if ( Network.IsProxy )
		{
			CheckManualPickup();
			return;
		}

		// Host (or local owner sim): countdown pickup delay
		if ( _pickupTimer > 0 )
		{
			_pickupTimer -= Time.Delta;
			return;
		}

		// Host: handle vacuum loot movement.
		// If the target became invalid, restore physics so the item doesn't hang mid-air.
		if ( IsBeingLooted )
		{
			if ( _vacuumTarget == null || !_vacuumTarget.IsValid )
			{
				StopVacuumAndRestorePhysics();
			}
			else
			{
				VacuumTowardTarget();
				return;
			}
		}

		// Host (singleplayer): allow manual pickup too
		CheckManualPickup();
	}

	void CheckManualPickup()
	{
		var localPlayer = PlayerVeggaStats.Local;
		if ( localPlayer == null ) return;
		if ( _consumed ) return;
		if ( IsInDropThrow ) return;
		if ( IsBeingLooted ) return;

		var playerPos = localPlayer.WorldPosition;
		var itemPos = WorldPosition;
		var dist = Vector3.DistanceBetween( playerPos, itemPos );

		// Too far
		if ( dist > InteractRange ) return;

		// Only the item we're actually looking at should react to the Use key.
		// Otherwise every nearby pickup sees the same keypress and we loot a pile at once.
		var scene = Scene ?? Game.ActiveScene;
		var lookedAt = VeggaPickupLook.GetLookedAtPickup( scene );
		if ( lookedAt != this )
			return;

		// Player pressed E
		if ( Input.Pressed( "use" ) )
		{
			if ( DebugNet ) Log.Info( $"[Pickup] Use pressed; host={Networking.IsHost} proxy={Network.IsProxy} local={Connection.Local?.Id} dist={dist:0.0}" );
			if ( Networking.IsHost && !Network.IsProxy )
			{
				// Host/local: animate pickup too (no instant poof).
				var sceneLocal = Scene ?? Game.ActiveScene;
				var cameraSide = VeggaPickupLook.GetCameraShoulderSide( sceneLocal, localPlayer.GameObject );
				StartVacuumToPlayer( localPlayer.GameObject, Guid.Empty, grantViaRpc: false, cameraSide );
			}
			else
			{
				var id = Connection.Local?.Id ?? Guid.Empty;
				var sceneLocal = Scene ?? Game.ActiveScene;
				var cameraSide = VeggaPickupLook.GetCameraShoulderSide( sceneLocal, localPlayer.GameObject );
				if ( DebugNet ) Log.Info( $"[Pickup] Sending pickup request id={id}" );
				RpcRequestPickup( id, cameraSide );
			}
		}
	}

	[Rpc.Broadcast]
	void RpcRequestPickup( Guid requesterId, int cameraSide )
	{
		if ( DebugNet ) Log.Info( $"[Pickup] RpcRequestPickup requesterId={requesterId} side={cameraSide} host={Networking.IsHost} consumed={_consumed} timer={_pickupTimer:0.00}" );
		if ( !Networking.IsHost ) return;
		if ( requesterId == Guid.Empty ) { if ( DebugNet ) Log.Warning( "[Pickup] Reject: empty requesterId" ); return; }
		if ( _consumed ) { if ( DebugNet ) Log.Warning( "[Pickup] Reject: already consumed" ); return; }
		if ( _pickupTimer > 0 ) { if ( DebugNet ) Log.Warning( "[Pickup] Reject: pickup delay active" ); return; }

		// Validate requester is near the item.
		var scene = Scene ?? Game.ActiveScene;
		if ( scene == null ) return;

		PlayerVeggaStats requester = null;
		foreach ( var stats in scene.GetAllComponents<PlayerVeggaStats>() )
		{
			if ( stats.IsValid() && stats.Network?.Owner?.Id == requesterId )
			{
				requester = stats;
				break;
			}
		}

		if ( requester == null ) { if ( DebugNet ) Log.Warning( "[Pickup] Reject: requester stats not found" ); return; }

		float dist = Vector3.DistanceBetween( requester.WorldPosition, WorldPosition );
		if ( dist > InteractRange ) { if ( DebugNet ) Log.Warning( $"[Pickup] Reject: too far dist={dist:0.0} range={InteractRange:0.0}" ); return; }

		var inventory = requester.GameObject?.Components.Get<VeggaInventory>();
		if ( inventory == null ) { if ( DebugNet ) Log.Warning( "[Pickup] Reject: requester has no inventory" ); return; }
		if ( !inventory.CanFitItem( ItemId, Quantity ) ) { if ( DebugNet ) Log.Warning( "[Pickup] Reject: inventory full" ); return; }

		// Mark consumed before starting vacuum to avoid double-pickup.
		_consumed = true;
		if ( DebugNet ) Log.Info( $"[Pickup] Begin vacuum pickup itemId={ItemId} qty={Quantity} requesterId={requesterId}" );
		var side = cameraSide > 0 ? 1 : (cameraSide < 0 ? -1 : 0);
		StartVacuumToPlayer( requester.GameObject, requesterId, grantViaRpc: true, side );
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

	void UpdateOreVisualsIfNeeded( bool allowRandomizeOnHost )
	{
		if ( !VeggaOreVisuals.IsOre( ItemId ) )
			return;

		if ( Networking.IsHost && allowRandomizeOnHost )
		{
			int ensured = VeggaOreVisuals.EnsureVariant( ItemId, Durability, allowRandomize: true );
			if ( ensured != Durability )
				Durability = ensured;
		}

		if ( ItemId == _lastVisualItemId && Durability == _lastVisualDurability )
			return;
		_lastVisualItemId = ItemId;
		_lastVisualDurability = Durability;

		VeggaOreVisuals.ApplyToWorldObject( GameObject, ItemId, Durability );
	}

	/// <summary>
	/// Called when player's auto-loot trigger enters our area.
	/// </summary>
	public void OnTriggerEnter( Collider other )
	{
		// Only the host should drive vacuum movement for networked pickups.
		if ( Network.IsProxy ) return;
		if ( _consumed ) return;
		if ( IsInDropThrow ) return;
		if ( _pickupTimer > 0 ) return;
		if ( IsBeingLooted ) return;

		// Check if this is a player's auto-loot collector
		var collector = other?.GameObject?.Components.GetInDescendantsOrSelf<VeggaAutoLootCollector>();
		if ( collector == null ) return;

		// Check if auto-loot is enabled
		if ( !collector.AutoLootEnabled ) return;

		// Prevent vacuuming your own drops for a short time.
		var stats = collector.PlayerStats;
		var ownerId = stats?.Network?.Owner?.Id ?? Guid.Empty;
		if ( ownerId != Guid.Empty && DroppedById != Guid.Empty && ownerId == DroppedById )
		{
			float delay = OwnDropAutoLootDelay;
			if ( delay <= 0 ) delay = DefaultOwnDropAutoLootDelaySeconds;
			double ageSeconds;
			if ( DroppedAtUtcTicks > 0 )
			{
				try
				{
					var droppedAtUtc = new DateTime( DroppedAtUtcTicks, DateTimeKind.Utc );
					ageSeconds = (DateTime.UtcNow - droppedAtUtc).TotalSeconds;
				}
				catch
				{
					ageSeconds = Time.Now - DroppedAtTime;
				}
			}
			else
			{
				ageSeconds = Time.Now - DroppedAtTime;
			}

			if ( ageSeconds < delay )
				return;
		}

		// Start vacuuming to player
		var player = collector.GetPlayerOwner();
		if ( player != null )
		{
			if ( DebugNet ) Log.Info( $"[Pickup] AutoLoot vacuum start -> {player.Name}" );
			StartVacuum( player );
		}
	}

	public void OnTriggerExit( Collider other )
	{
		// Optional: cancel vacuum if player runs away?
	}

	void StartVacuum( GameObject player )
	{
		StartVacuumToPlayer( player, Guid.Empty, grantViaRpc: false, 0 );
	}

	void StartVacuumToPlayer( GameObject player, Guid requesterId, bool grantViaRpc, int cameraSide )
	{
		IsBeingLooted = true;
		IsInDropThrow = false;
		_vacuumPickupPlayer = player;
		_vacuumPickupRequesterId = requesterId;
		_vacuumGrantViaRpc = grantViaRpc;

		// Move toward the player's LowerBack object if it exists.
		_vacuumTarget = FindLowerBack( player ) ?? player;
		_vacuumStartPos = WorldPosition;
		_vacuumStartTime = Time.Now;
		_vacuumStartScale = Transform.Scale;
		_vacuumStartDistance = MathF.Max( 1f, Vector3.DistanceBetween( _vacuumStartPos, _vacuumTarget.WorldPosition ) );
		var seed = (PersistId != Guid.Empty ? PersistId.GetHashCode() : HashCode.Combine( ItemId, Quantity, WorldPosition.GetHashCode() ));
		_vacuumSwirlPhase = seed * 0.001f;
		// Choose swing side based on camera (if provided), otherwise fall back to item vs player side,
		// and finally a deterministic per-item seed.
		if ( cameraSide != 0 )
		{
			_vacuumSideSign = cameraSide > 0 ? 1 : -1;
		}
		else
		{
			var playerRight = player != null && player.IsValid ? player.WorldRotation.Right : Vector3.Right;
			var rel = Vector3.Dot( (_vacuumStartPos - (player?.WorldPosition ?? _vacuumStartPos)), playerRight );
			if ( rel > 1f ) _vacuumSideSign = 1;
			else if ( rel < -1f ) _vacuumSideSign = -1;
			else _vacuumSideSign = ((seed & 1) == 0) ? 1 : -1;
		}
		// Slow enough to see, scales slightly with distance. (About 2x slower than before.)
		_vacuumDuration = (0.44f + (_vacuumStartDistance / 300f)).Clamp( MinVacuumSeconds, 2.25f );
		var initialDir = (_vacuumTarget.WorldPosition - WorldPosition).Normal;
		_vacuumSwirlAxis = Vector3.Cross( initialDir, Vector3.Up );
		if ( _vacuumSwirlAxis.Length.AlmostEqual( 0f, 0.001f ) )
			_vacuumSwirlAxis = Vector3.Right;
		else
			_vacuumSwirlAxis = _vacuumSwirlAxis.Normal;

		// Disable physics so we can control movement
		var rb = Components.Get<Rigidbody>();
		if ( rb != null )
		{
			rb.Enabled = false;
		}

		// Disable collisions while animating so the player doesn't bump into the moving item.
		// Cache states so we can restore if the vacuum cancels.
		if ( !_vacuumCollidersCached )
		{
			var mc = Components.Get<ModelCollider>();
			var sc = Components.Get<SphereCollider>();
			var bc = Components.Get<BoxCollider>();
			_vacuumModelColliderEnabled = mc != null && mc.IsValid() && mc.Enabled;
			_vacuumSphereColliderEnabled = sc != null && sc.IsValid() && sc.Enabled;
			_vacuumBoxColliderEnabled = bc != null && bc.IsValid() && bc.Enabled;
			_vacuumCollidersCached = true;
		}
		{
			var mc = Components.Get<ModelCollider>();
			if ( mc != null && mc.IsValid() ) mc.Enabled = false;
			var sc = Components.Get<SphereCollider>();
			if ( sc != null && sc.IsValid() ) sc.Enabled = false;
			var bc = Components.Get<BoxCollider>();
			if ( bc != null && bc.IsValid() ) bc.Enabled = false;
		}

		Log.Info( $"🧲 Vacuuming {ItemDef?.Name ?? "item"} to player" );
	}

	void StopVacuumAndRestorePhysics()
	{
		IsBeingLooted = false;
		// IsInDropThrow is managed by the drop-throw animator.
		_vacuumTarget = null;
		_vacuumPickupPlayer = null;
		_vacuumPickupRequesterId = Guid.Empty;
		_vacuumGrantViaRpc = false;
		_vacuumStartPos = default;
		_vacuumStartTime = 0f;
		_vacuumSwirlAxis = default;
		_vacuumSwirlPhase = 0f;
		_vacuumStartDistance = 0f;
		_vacuumDuration = 0f;
		_vacuumSideSign = 0;
		if ( _vacuumCollidersCached )
		{
			var mc = Components.Get<ModelCollider>();
			if ( mc != null && mc.IsValid() ) mc.Enabled = _vacuumModelColliderEnabled;
			var sc = Components.Get<SphereCollider>();
			if ( sc != null && sc.IsValid() ) sc.Enabled = _vacuumSphereColliderEnabled;
			var bc = Components.Get<BoxCollider>();
			if ( bc != null && bc.IsValid() ) bc.Enabled = _vacuumBoxColliderEnabled;
		}
		_vacuumCollidersCached = false;
		_vacuumModelColliderEnabled = false;
		_vacuumSphereColliderEnabled = false;
		_vacuumBoxColliderEnabled = false;
		if ( _vacuumStartScale != default )
			Transform.Scale = _vacuumStartScale;
		_vacuumStartScale = default;

		var rb = Components.Get<Rigidbody>();
		if ( rb != null )
		{
			rb.Enabled = true;
			rb.MotionEnabled = true;
			rb.Gravity = true;
		}
	}

	internal void BeginDropThrow()
	{
		IsInDropThrow = true;
		IsBeingLooted = false;
		// Disable any physics while we animate the transform.
		// Some prefabs put the Rigidbody on the root while the pickup component is on a child,
		// so disable self + descendants.
		var rootRb = Components.Get<Rigidbody>();
		if ( rootRb != null && rootRb.IsValid() )
			rootRb.Enabled = false;
		foreach ( var rb in Components.GetAll<Rigidbody>( FindMode.InDescendants ) )
		{
			if ( rb == null || !rb.IsValid() ) continue;
			rb.Enabled = false;
		}
	}

	internal void EndDropThrow( Vector3 velocity )
	{
		IsInDropThrow = false;
		bool isProblemDrop = VeggaOreVisuals.IsOre( ItemId ) || ItemId == VeggaItemIds.MouldGoblet;
		try
		{
			// Re-enforce known-problematic physics/collider setups (ore/goblet) now that the item is placed.
			if ( !Network.IsProxy )
				EnsureDropPhysicsReady();

			// Snap ore + goblet mould to ground to avoid starting interpenetrating terrain or falling through seams.
			if ( !Network.IsProxy && isProblemDrop )
			{
				var scene = Scene ?? Game.ActiveScene;
				if ( scene != null )
				{
					var start = WorldPosition + Vector3.Up * 32f;
					var end = WorldPosition + Vector3.Down * 800f;
					var tr = scene.Trace.Ray( start, end )
						.WithoutTags( "player", "trigger" )
						.Run();
					if ( tr.Hit )
						WorldPosition = tr.HitPosition + tr.Normal * 8f;
				}
			}
		}
		catch ( Exception ex )
		{
			Log.Error( ex, $"VeggaPickupItem.EndDropThrow setup failed (ItemId={ItemId} Qty={Quantity} GO={GameObject?.Name})" );
		}

		if ( isProblemDrop )
		{
			// Enabling physics on the same frame as collider creation/changes can be flaky.
			// Do it next frame so the engine registers the final collider state first.
			FinalizeDropAfterFrame( velocity );
			return;
		}

		// Normal items: enable immediately.
		Rigidbody apply = null;
		var rootRb = Components.Get<Rigidbody>();
		if ( rootRb != null && rootRb.IsValid() )
		{
			rootRb.Enabled = true;
			rootRb.MotionEnabled = true;
			rootRb.Gravity = true;
			apply = rootRb;
		}
		foreach ( var rb in Components.GetAll<Rigidbody>( FindMode.InDescendants ) )
		{
			if ( rb == null || !rb.IsValid() ) continue;
			rb.Enabled = true;
			rb.MotionEnabled = true;
			rb.Gravity = true;
			apply ??= rb;
		}
		if ( apply != null )
			apply.Velocity = velocity;
	}

	async void FinalizeDropAfterFrame( Vector3 velocity )
	{
		await Task.Frame();
		if ( !IsValid ) return;
		if ( Network.IsProxy ) return;

		Rigidbody apply = null;
		var rootRb = Components.Get<Rigidbody>();
		if ( rootRb != null && rootRb.IsValid() )
		{
			rootRb.Enabled = true;
			rootRb.MotionEnabled = true;
			rootRb.Gravity = true;
			apply = rootRb;
		}
		foreach ( var rb in Components.GetAll<Rigidbody>( FindMode.InDescendants ) )
		{
			if ( rb == null || !rb.IsValid() ) continue;
			rb.Enabled = true;
			rb.MotionEnabled = true;
			rb.Gravity = true;
			apply ??= rb;
		}
		if ( apply != null )
			apply.Velocity = velocity;
	}

	void VacuumTowardTarget()
	{
		if ( _vacuumTarget == null || !_vacuumTarget.IsValid )
		{
			StopVacuumAndRestorePhysics();
			return;
		}

		// Get target position (LowerBack if available, otherwise player root)
		var targetPos = _vacuumTarget.WorldPosition;
		var age = Time.Now - _vacuumStartTime;
		var duration = MathF.Max( MinVacuumSeconds, _vacuumDuration );
		var t = (age / duration).Clamp( 0f, 1f );
		// SmoothStep easing.
		var eased = t * t * (3f - 2f * t);

		// Build a control point that swings around the player left/right.
		var player = _vacuumPickupPlayer;
		var playerPos = player != null && player.IsValid ? player.WorldPosition : targetPos;
		var playerRot = player != null && player.IsValid ? player.WorldRotation : Rotation.Identity;
		var right = playerRot.Right;
		var forward = playerRot.Forward;

		// If the item starts in front of the player, push the arc outward more.
		var startToPlayer = (playerPos - _vacuumStartPos);
		var frontDot = Vector3.Dot( startToPlayer.Normal, forward );
		var frontBoost = (frontDot > 0.2f) ? 1.25f : 1.0f;

		var sideDist = (40f + _vacuumStartDistance * 0.15f).Clamp( 30f, 90f ) * frontBoost;
		var fwdDist = (20f + _vacuumStartDistance * 0.05f).Clamp( 10f, 55f );
		var upDist = (35f + _vacuumStartDistance * 0.04f).Clamp( 25f, 70f );

		// One consistent side per item, but add a subtle wobble so it feels alive.
		var wobble = MathF.Sin( age * 10f + _vacuumSwirlPhase ) * 0.12f;
		var side = _vacuumSideSign == 0 ? 1 : _vacuumSideSign;
		var control = playerPos
			+ right * (sideDist * side * (1f + wobble))
			+ forward * fwdDist
			+ Vector3.Up * upDist;

		// Quadratic Bezier curve from start -> control -> target.
		var a = Vector3.Lerp( _vacuumStartPos, control, eased );
		var b = Vector3.Lerp( control, targetPos, eased );
		var curvedPos = Vector3.Lerp( a, b, eased );

		WorldPosition = curvedPos;

		// Shrink as it approaches the target so it feels like it "slots" into the player.
		if ( _vacuumStartScale != default )
		{
			var scaleFactor = 1f - (eased * 0.75f);
			Transform.Scale = _vacuumStartScale * scaleFactor;
		}

		// Close enough? Pickup!
		if ( t >= 1f && age >= MinVacuumSeconds )
		{
			if ( _vacuumGrantViaRpc && _vacuumPickupPlayer != null && _vacuumPickupPlayer.IsValid )
			{
				CompletePickupRpc( _vacuumPickupPlayer, _vacuumPickupRequesterId );
			}
			else if ( _vacuumPickupPlayer != null && _vacuumPickupPlayer.IsValid )
			{
				TryPickupOnHost( _vacuumPickupPlayer );
			}
			else
			{
				StopVacuumAndRestorePhysics();
			}
		}
	}

	void CompletePickupRpc( GameObject player, Guid requesterId )
	{
		if ( !Networking.IsHost ) return;
		if ( requesterId == Guid.Empty ) { StopVacuumAndRestorePhysics(); return; }
		var inventory = player.Components.Get<VeggaInventory>();
		if ( inventory == null ) { StopVacuumAndRestorePhysics(); return; }

		inventory.RpcGiveItemToOwnerWithDurability( requesterId, ItemId, Quantity, Durability );
		if ( PersistId != Guid.Empty )
		{
			WorldDropPersistence.UnregisterDrop( PersistId );
			PersistId = Guid.Empty;
		}
		TryPlayPickupSfxFor( requesterId );
		GameObject.Destroy();
	}

	void TryPickupOnHost( GameObject player )
	{
		if ( Network.IsProxy ) return;
		if ( _consumed ) return;
		if ( _pickupTimer > 0 ) return;

		var inventory = player.Components.Get<VeggaInventory>();
		if ( inventory == null )
		{
			Log.Warning( "❌ Player has no VeggaInventory component!" );
			return;
		}

		var def = ItemDef;
		if ( def == null )
		{
			Log.Warning( $"❌ Cannot pickup: ItemId {ItemId} not found in registry" );
			return;
		}

		if ( !inventory.CanFitItem( ItemId, Quantity ) )
		{
			Log.Warning( "❌ Inventory full - cannot pickup!" );
			StopVacuumAndRestorePhysics();
			return;
		}

		// Host/local owner can mutate directly.
		if ( inventory.AddItem( ItemId, Quantity ) )
		{
			if ( DebugNet ) Log.Info( $"[Pickup] Host/local pickup success itemId={ItemId} qty={Quantity}" );

			var stats = player.Components.Get<PlayerVeggaStats>();
			var ownerId = stats?.Network?.Owner?.Id ?? Guid.Empty;
			if ( ownerId != Guid.Empty )
				TryPlayPickupSfxFor( ownerId );

			_consumed = true;
			if ( PersistId != Guid.Empty )
			{
				WorldDropPersistence.UnregisterDrop( PersistId );
				PersistId = Guid.Empty;
			}
			GameObject.Destroy();
		}
		else
		{
			if ( DebugNet ) Log.Warning( "[Pickup] Host/local pickup failed to add" );
			Log.Warning( "❌ Inventory add failed - cannot pickup!" );
			StopVacuumAndRestorePhysics();
		}
	}

	void TryPlayPickupSfxFor( Guid ownerId )
	{
		if ( !VeggaSfxSettings.Enabled || !VeggaSfxSettings.PickupEnabled )
			return;

		// Only play the coin sound for currency pickups (cash, etc).
		var def = ItemDef;
		bool isCurrency = ItemId == VeggaCurrency.CashItemId || def?.Category == ItemCategory.Currency;
		if ( !isCurrency )
			return;

		var scene = Scene ?? Game.ActiveScene;
		if ( scene == null ) return;
		var mgr = scene.GetAllComponents<VeggaChatManager>()
			.FirstOrDefault( m => m != null && m.IsValid() && m.Network?.Owner?.Id == ownerId );
		mgr?.RpcPlayUiSound( ownerId, VeggaSfxSettings.CoinSound );
	}

	/// <summary>
	/// Get the display text for interaction hint.
	/// </summary>
	public string GetInteractText()
	{
		var def = ItemDef;
		if ( def == null ) return "Press E to pick up";

		if ( Quantity > 1 )
			return $"Press E to pick up {def.Name} x{Quantity}";
		else
			return $"Press E to pick up {def.Name}";
	}

	/// <summary>
	/// Get rarity color for UI.
	/// </summary>
	public string GetRarityColor()
	{
		var def = ItemDef;
		if ( def == null ) return "#ffffff";

		return def.Rarity switch
		{
			ItemRarity.Common => "#ffffff",
			ItemRarity.Uncommon => "#1eff00",
			ItemRarity.Rare => "#0070dd",
			ItemRarity.Epic => "#a335ee",
			ItemRarity.Legendary => "#ff8000",
			_ => "#ffffff"
		};
	}
}
