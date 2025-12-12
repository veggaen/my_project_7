using Sandbox;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// AAA-quality death and respawn system with ragdoll physics.
/// Handles death detection, ragdoll creation, death camera, and respawning.
/// </summary>
public sealed class PlayerVeggaDeath : Component
{
	[Property] public float RespawnDelay { get; set; } = 5f;
	[Property] public float DeathCameraDistance { get; set; } = 200f;
	[Property] public bool DropItemsOnDeath { get; set; } = false;

	// Component references
	PlayerVeggaStats Stats { get; set; }
	PlayerVeggaMovement Movement { get; set; }
	SkinnedModelRenderer ModelRenderer { get; set; }
	ModelPhysics RagdollPhysics { get; set; }
	CameraComponent Camera { get; set; }

	// Death state
	[Sync] public bool IsDead { get; private set; }
	[Sync] public float TimeSinceDeath { get; private set; }
	
	// Death info
	Vector3 _deathPosition;
	Vector3 _deathForce;
	Vector3 _deathImpactPoint;
	GameObject _ragdollObject;

	// Events
	public Action OnDeath;
	public Action OnRespawn;

	protected override void OnAwake()
	{
		// Get component references on this GameObject first
		Stats = Components.Get<PlayerVeggaStats>();
		Movement = Components.Get<PlayerVeggaMovement>();
		ModelRenderer = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();
		Camera = Components.GetInDescendantsOrSelf<CameraComponent>();
	}

	protected override void OnStart()
	{
		// If Stats wasn't found on this GameObject, try to locate the correct
		// PlayerVeggaStats in the scene owned by the same connection.
		if ( Stats == null )
		{
			var owner = Network.Owner;
			if ( owner != null )
			{
				Stats = Scene
					.GetAllComponents<PlayerVeggaStats>()
					.FirstOrDefault( s => s.Network.Owner == owner );
			}

			if ( Stats == null )
			{
				Log.Warning( "PlayerVeggaDeath: No PlayerVeggaStats found!" );
				return;
			}
		}

		// Subscribe to stats changes
		Stats.OnHealthChanged += CheckDeath;
	}

	protected override void OnUpdate()
	{
		if ( IsDead )
		{
			TimeSinceDeath += Time.Delta;
			UpdateDeathCamera();
		}
	}

	void CheckDeath()
	{
		if ( Stats.Health <= 0 && !IsDead )
		{
			Die( Vector3.Zero, Vector3.Zero );
		}
	}

	/// <summary>
	/// Kill the player with optional force/impact point for ragdoll physics
	/// </summary>
	[Rpc.Broadcast]
	public void Die( Vector3 force, Vector3 impactPoint )
	{
		if ( IsDead ) return;
		if ( Network.IsProxy ) return;

		IsDead = true;
		TimeSinceDeath = 0f;
		_deathPosition = WorldPosition;
		_deathForce = force;
		_deathImpactPoint = impactPoint;

		Log.Info( $"💀 {GameObject.Name} died!" );

		// Trigger death effects
		OnDeath?.Invoke();

		// Disable movement
		if ( Movement != null )
			Movement.Enabled = false;

		// Create ragdoll
		CreateRagdoll();

		// Hide player model
		if ( ModelRenderer != null )
			ModelRenderer.Enabled = false;

		// Disable collisions
		var collider = Components.Get<Collider>();
		if ( collider != null )
			collider.Enabled = false;

		// Drop items (optional)
		if ( DropItemsOnDeath )
		{
			// TODO: Implement inventory drop
		}

		// Start respawn timer
		_ = RespawnAfterDelay();
	}

	/// <summary>
	/// Create ragdoll with physics
	/// </summary>
	void CreateRagdoll()
	{
		if ( !ModelRenderer.IsValid() ) return;

		// Create ragdoll GameObject
		_ragdollObject = new GameObject( true, $"{GameObject.Name}_Ragdoll" );
		_ragdollObject.WorldPosition = WorldPosition;
		_ragdollObject.WorldRotation = WorldRotation;

		// Add model renderer
		var ragdollRenderer = _ragdollObject.Components.Create<SkinnedModelRenderer>();
		ragdollRenderer.Model = ModelRenderer.Model;
		ragdollRenderer.CopyFrom( ModelRenderer );

		// Add physics
		var ragdollPhysics = _ragdollObject.Components.Create<ModelPhysics>();
		ragdollPhysics.Renderer = ragdollRenderer;
		ragdollPhysics.Model = ModelRenderer.Model;

		// Apply death force to ragdoll
		if ( _deathForce.LengthSquared > 0 )
		{
			_ = ApplyRagdollForce( ragdollPhysics );
		}

		// Destroy ragdoll after delay
		_ = DestroyRagdollAfterDelay( _ragdollObject, 10f );

		Log.Info( "✅ Ragdoll created!" );
	}

	async Task ApplyRagdollForce( ModelPhysics physics )
	{
		// Wait one frame for physics to initialize
		await Task.Frame();

		if ( !physics.IsValid() ) return;

		// Apply force to all physics bodies in the ragdoll
		var bodies = physics.GameObject.Components.GetAll<Rigidbody>( FindMode.InDescendants );
		foreach ( var body in bodies )
		{
			if ( !body.IsValid() ) continue;

			if ( _deathImpactPoint != Vector3.Zero )
			{
				body.ApplyImpulseAt( _deathImpactPoint, _deathForce );
			}
			else
			{
				body.ApplyImpulse( _deathForce );
			}
		}

		Log.Info( $"💥 Applied ragdoll force: {_deathForce} to {bodies.Count()} bodies" );
	}

	async Task DestroyRagdollAfterDelay( GameObject ragdoll, float delay )
	{
		await Task.DelaySeconds( delay );

		if ( ragdoll.IsValid() )
		{
			ragdoll.Destroy();
		}
	}

	/// <summary>
	/// Update death camera to look at ragdoll or killer
	/// </summary>
	void UpdateDeathCamera()
	{
		if ( !Camera.IsValid() ) return;
		if ( !_ragdollObject.IsValid() ) return;

		// Orbit camera around ragdoll
		var targetPos = _ragdollObject.WorldPosition + Vector3.Up * 50f;
		var cameraOffset = Vector3.Backward * DeathCameraDistance + Vector3.Up * 100f;
		var cameraPos = targetPos + cameraOffset;

		// Smooth camera movement
		Camera.WorldPosition = Vector3.Lerp( Camera.WorldPosition, cameraPos, Time.Delta * 2f );
		Camera.WorldRotation = Rotation.LookAt( targetPos - Camera.WorldPosition );
	}

	/// <summary>
	/// Respawn after delay
	/// </summary>
	async Task RespawnAfterDelay()
	{
		await Task.DelaySeconds( RespawnDelay );

		if ( IsDead )
		{
			Respawn();
		}
	}

	/// <summary>
	/// Respawn the player at a spawn point
	/// </summary>
	[Rpc.Broadcast]
	public void Respawn()
	{
		if ( !IsDead ) return;
		if ( Network.IsProxy ) return;

		IsDead = false;
		TimeSinceDeath = 0f;

		Log.Info( $"✨ {GameObject.Name} respawned!" );

		// Restore health
		if ( Stats != null )
		{
			Stats.SetHealth( Stats.MaxHealth );
			Stats.SetArmor( Stats.InitArmor );
		}

		// Re-enable movement
		if ( Movement != null )
			Movement.Enabled = true;

		// Show player model
		if ( ModelRenderer != null )
			ModelRenderer.Enabled = true;

		// Re-enable collisions
		var collider = Components.Get<Collider>();
		if ( collider != null )
			collider.Enabled = true;

		// Teleport to spawn point
		TeleportToSpawnPoint();

		// Trigger respawn event
		OnRespawn?.Invoke();

		// Destroy ragdoll if still exists
		if ( _ragdollObject.IsValid() )
		{
			_ragdollObject.Destroy();
			_ragdollObject = null;
		}
	}

	/// <summary>
	/// Find and teleport to a spawn point
	/// </summary>
	void TeleportToSpawnPoint()
	{
		var spawnPoints = Scene.GetAllComponents<VeggaSpawnPoint>()
			.Where( x => x.Enabled )
			.ToList();

		if ( spawnPoints.Count == 0 )
		{
			Log.Warning( "No spawn points found! Player will respawn at origin." );
			WorldPosition = Vector3.Zero;
			return;
		}

		// Pick random spawn point
		var spawnPoint = Random.Shared.FromList( spawnPoints );
		WorldPosition = spawnPoint.WorldPosition;
		WorldRotation = spawnPoint.WorldRotation;

		Log.Info( $"📍 Respawned at: {WorldPosition}" );
	}

	/// <summary>
	/// Force instant respawn (for testing)
	/// </summary>
	[Button( "Force Respawn" ), Group( "Testing" )]
	public void ForceRespawn()
	{
		if ( IsDead )
		{
			Respawn();
		}
	}

	/// <summary>
	/// Test death (for testing)
	/// </summary>
	[Button( "Test Death" ), Group( "Testing" )]
	public void TestDeath()
	{
		var force = Vector3.Forward * 500f + Vector3.Up * 300f;
		Die( force, WorldPosition );
	}
}

