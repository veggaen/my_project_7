using Sandbox;

namespace Sandbox;

/// <summary>
/// Attach this to the player (as a child with a trigger collider) to enable auto-looting.
/// Items with VeggaPickupItem that enter this trigger will vacuum to the player.
/// </summary>
public sealed class VeggaAutoLootCollector : Component
{
	/// <summary>
	/// Whether auto-loot is enabled. Can be toggled in settings.
	/// </summary>
	[Property, Sync]
	public bool AutoLootEnabled { get; set; } = true;

	/// <summary>
	/// The collider range for auto-loot detection.
	/// </summary>
	[Property]
	public float CollectionRadius { get; set; } = 150f;

	/// <summary>
	/// Reference to the player stats component (to find the player).
	/// </summary>
	[Property]
	public PlayerVeggaStats PlayerStats { get; set; }

	protected override void OnStart()
	{
		// Auto-find player stats on parent
		if ( PlayerStats == null )
		{
			PlayerStats = GameObject.Parent?.Components.Get<PlayerVeggaStats>();
			if ( PlayerStats == null )
			{
				PlayerStats = Components.GetInAncestorsOrSelf<PlayerVeggaStats>();
			}
		}

		// Ensure we have a trigger collider
		var collider = Components.Get<SphereCollider>( true );
		if ( collider == null )
		{
			collider = Components.Create<SphereCollider>();
			collider.Radius = CollectionRadius;
			collider.IsTrigger = true;
		}
		else
		{
			collider.Radius = CollectionRadius;
			collider.IsTrigger = true;
		}

		Log.Info( $"✅ VeggaAutoLootCollector initialized (radius: {CollectionRadius}, enabled: {AutoLootEnabled})" );
	}

	/// <summary>
	/// Get the owning player's GameObject.
	/// </summary>
	public GameObject GetPlayerOwner()
	{
		if ( PlayerStats != null && PlayerStats.IsValid )
			return PlayerStats.GameObject;

		// Fallback: try parent
		return GameObject.Parent;
	}

	/// <summary>
	/// Toggle auto-loot on/off.
	/// </summary>
	public void ToggleAutoLoot()
	{
		AutoLootEnabled = !AutoLootEnabled;
		Log.Info( $"🧲 Auto-loot: {(AutoLootEnabled ? "ENABLED" : "DISABLED")}" );
	}
}
