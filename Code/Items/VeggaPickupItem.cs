using Sandbox;
using System;
using System.Linq;
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
	/// How long the dropper's auto-loot should ignore this item.
	/// Manual pickup (E) is still allowed.
	/// </summary>
	[Property]
	public float OwnDropAutoLootDelay { get; set; } = DefaultOwnDropAutoLootDelaySeconds;
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
	/// Host-side: prevents double-pickup.
	/// </summary>
	[Sync] private bool _consumed { get; set; } = false;

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
		if ( _consumed ) return;

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

		// Host: handle vacuum loot movement
		if ( IsBeingLooted && _vacuumTarget != null && _vacuumTarget.IsValid )
		{
			VacuumTowardTarget();
			return;
		}

		// Host (singleplayer): allow manual pickup too
		CheckManualPickup();
	}

	void CheckManualPickup()
	{
		var localPlayer = PlayerVeggaStats.Local;
		if ( localPlayer == null ) return;
		if ( _consumed ) return;

		var playerPos = localPlayer.WorldPosition;
		var itemPos = WorldPosition;
		var dist = Vector3.DistanceBetween( playerPos, itemPos );

		// Too far
		if ( dist > InteractRange ) return;

		// Player pressed E
		if ( Input.Pressed( "use" ) )
		{
			if ( DebugNet ) Log.Info( $"[Pickup] Use pressed; host={Networking.IsHost} proxy={Network.IsProxy} local={Connection.Local?.Id} dist={dist:0.0}" );
			if ( Networking.IsHost && !Network.IsProxy )
			{
				TryPickupOnHost( localPlayer.GameObject );
			}
			else
			{
				var id = Connection.Local?.Id ?? Guid.Empty;
				if ( DebugNet ) Log.Info( $"[Pickup] Sending pickup request id={id}" );
				RpcRequestPickup( id );
			}
		}
	}

	[Rpc.Broadcast]
	void RpcRequestPickup( Guid requesterId )
	{
		if ( DebugNet ) Log.Info( $"[Pickup] RpcRequestPickup requesterId={requesterId} host={Networking.IsHost} consumed={_consumed} timer={_pickupTimer:0.00}" );
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

		// Mark consumed before granting to avoid double-pickup.
		_consumed = true;
		if ( DebugNet ) Log.Info( $"[Pickup] Granting itemId={ItemId} qty={Quantity} to requesterId={requesterId}" );
		inventory.RpcGiveItemToOwner( requesterId, ItemId, Quantity );
		TryPlayPickupSfxFor( requesterId );
		GameObject.Destroy();
	}

	/// <summary>
	/// Called when player's auto-loot trigger enters our area.
	/// </summary>
	public void OnTriggerEnter( Collider other )
	{
		// Only the host should drive vacuum movement for networked pickups.
		if ( Network.IsProxy ) return;
		if ( _consumed ) return;
		if ( _pickupTimer > 0 ) return;
		if ( IsBeingLooted ) return;

		// Check if this is a player's auto-loot collector
		var collector = other.GameObject.Components.Get<VeggaAutoLootCollector>();
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
			if ( Time.Now - DroppedAtTime < delay )
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
		float speed = Math.Max( 500f, 1500f - distance * 3f );
		var newPos = currentPos + direction * speed * Time.Delta;

		WorldPosition = newPos;

		// Close enough? Pickup!
		if ( distance < 30f )
		{
			TryPickupOnHost( _vacuumTarget );
		}
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
			IsBeingLooted = false;
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
			GameObject.Destroy();
		}
		else
		{
			if ( DebugNet ) Log.Warning( "[Pickup] Host/local pickup failed to add" );
			Log.Warning( "❌ Inventory add failed - cannot pickup!" );
			IsBeingLooted = false;
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
