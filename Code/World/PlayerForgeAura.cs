using Sandbox;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Attach this to a furnace/forge GameObject (e.g. Furnace_vegga_prop)
/// to provide a smelting speed aura:
/// - Players inside Radius gain a forge buff.
/// - Buff stacks via highest multiplier when overlapping multiple forges.
/// All authority is on the host; clients receive synced values from
/// PlayerVeggaStats.
/// </summary>
public sealed class PlayerForgeAura : Component
{
	/// <summary>
	/// Radius in world units. Roughly 300–400 units ≈ 3–5 meters.
	/// </summary>
	[Property]
	public float Radius { get; set; } = 350f;

	/// <summary>
	/// Base smelt speed multiplier provided by this furnace.
	/// Hand crafting uses 1x, standing in this aura provides this
	/// base multiplier before skill scaling (e.g. 100x at level 1).
	/// </summary>
	[Property]
	public float BaseForgeMultiplier { get; set; } = 100f;

	// Track which players are currently inside this aura so we only
	// register/unregister when they cross the boundary, not every frame.
	private readonly HashSet<PlayerVeggaStats> _inside = new();

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		var scene = Game.ActiveScene;
		if ( scene == null )
			return;

		Vector3 center = WorldPosition;
		float rSq = Radius * Radius;

		// Build a fresh set of who is inside this frame
		var newlyInside = new HashSet<PlayerVeggaStats>();

		foreach ( var stats in scene.GetAllComponents<PlayerVeggaStats>() )
		{
			if ( stats == null || !stats.IsValid() )
				continue;

			// Only care about real, owned players
			if ( stats.Network?.Owner == null )
				continue;

			float distSq = (stats.WorldPosition - center).LengthSquared;
			if ( distSq <= rSq )
			{
				newlyInside.Add( stats );

				if ( !_inside.Contains( stats ) )
				{
					// Player just entered the aura
					stats.RegisterForgeAura( this );
				}
			}
		}

		// Players that were inside but are no longer within the radius
		foreach ( var prev in _inside )
		{
			if ( !newlyInside.Contains( prev ) )
			{
				prev.UnregisterForgeAura( this );
			}
		}

		_inside.Clear();
		_inside.UnionWith( newlyInside );
	}

	protected override void OnDisabled()
	{
		if ( !Networking.IsHost )
			return;

		// When this aura is disabled/destroyed, make sure we remove it
		// from all players that were inside.
		foreach ( var stats in _inside )
		{
			if ( stats != null && stats.IsValid() )
			{
				stats.UnregisterForgeAura( this );
			}
		}

		_inside.Clear();
	}
}
