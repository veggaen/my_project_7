using Sandbox;
using System;

namespace Sandbox;

/// <summary>
/// Attach this to any world object to make it a pickupable item.
/// When a player walks near it (auto-loot) or presses E, it goes into their inventory.
/// </summary>
public sealed class VeggaPickupItem : Component, Component.ITriggerListener
{
	/// <summary>
	/// The item ID from VeggaItemRegistry.
	/// </summary>
	[Property, Title( "Item ID" )]
	public int ItemId { get; set; } = 100; // Default: 200g Gold Bar

	/// <summary>
	/// How many of this item the pickup contains.
	/// </summary>
	[Property]
	public int Quantity { get; set; } = 1;

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
	/// Target player for vacuum loot.
	/// </summary>
	private GameObject _vacuumTarget;

	/// <summary>
	/// Time remaining on pickup delay.
	/// </summary>
	private float _pickupTimer;

	/// <summary>
	/// Cached item definition.
	/// </summary>
	public VeggaItemDef ItemDef => VeggaItemRegistry.Get( ItemId );

	protected override void OnStart()
	{
		_pickupTimer = PickupDelay;

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

	protected override void OnUpdate()
	{
		// Countdown pickup delay
		if ( _pickupTimer > 0 )
		{
			_pickupTimer -= Time.Delta;
			return;
		}

		// Handle vacuum loot movement
		if ( IsBeingLooted && _vacuumTarget != null && _vacuumTarget.IsValid )
		{
			VacuumTowardTarget();
			return;
		}

		// Check for manual E press pickup
		if ( !Network.IsProxy )
		{
			CheckManualPickup();
		}
	}

	void CheckManualPickup()
	{
		var localPlayer = PlayerVeggaStats.Local;
		if ( localPlayer == null ) return;

		var playerPos = localPlayer.WorldPosition;
		var itemPos = WorldPosition;
		var dist = Vector3.DistanceBetween( playerPos, itemPos );

		// Too far
		if ( dist > InteractRange ) return;

		// Player pressed E
		if ( Input.Pressed( "use" ) )
		{
			TryPickup( localPlayer.GameObject );
		}
	}

	/// <summary>
	/// Called when player's auto-loot trigger enters our area.
	/// </summary>
	public void OnTriggerEnter( Collider other )
	{
		if ( _pickupTimer > 0 ) return;
		if ( IsBeingLooted ) return;

		// Check if this is a player's auto-loot collector
		var collector = other.GameObject.Components.Get<VeggaAutoLootCollector>();
		if ( collector == null ) return;

		// Check if auto-loot is enabled
		if ( !collector.AutoLootEnabled ) return;

		// Start vacuuming to player
		var player = collector.GetPlayerOwner();
		if ( player != null )
		{
			StartVacuum( player );
		}
	}

	public void OnTriggerExit( Collider other )
	{
		// Optional: cancel vacuum if player runs away?
	}

	void StartVacuum( GameObject player )
	{
		IsBeingLooted = true;
		_vacuumTarget = player;

		// Disable physics so we can control movement
		var rb = Components.Get<Rigidbody>();
		if ( rb != null )
		{
			rb.Enabled = false;
		}

		Log.Info( $"🧲 Vacuuming {ItemDef?.Name ?? "item"} to player" );
	}

	void VacuumTowardTarget()
	{
		if ( _vacuumTarget == null || !_vacuumTarget.IsValid )
		{
			IsBeingLooted = false;
			return;
		}

		// Get target position (lower back / hip area)
		var targetPos = _vacuumTarget.WorldPosition + Vector3.Up * 40f;
		var currentPos = WorldPosition;
		var direction = (targetPos - currentPos).Normal;
		var distance = Vector3.DistanceBetween( currentPos, targetPos );

		// Speed increases as we get closer
		float speed = MathF.Max( 500f, 1500f - distance * 3f );
		var newPos = currentPos + direction * speed * Time.Delta;

		WorldPosition = newPos;

		// Close enough? Pickup!
		if ( distance < 30f )
		{
			TryPickup( _vacuumTarget );
		}
	}

	void TryPickup( GameObject player )
	{
		if ( Network.IsProxy ) return;

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

		// Try to add to inventory
		if ( inventory.AddItem( ItemId, Quantity ) )
		{
			Log.Info( $"✅ Picked up {def.Name} x{Quantity}" );

			// Play pickup effect/sound here if desired
			// Sound.FromWorld( "pickup.item", WorldPosition );

			// Destroy the pickup
			GameObject.Destroy();
		}
		else
		{
			Log.Warning( "❌ Inventory full - cannot pickup!" );
			IsBeingLooted = false;
		}
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
