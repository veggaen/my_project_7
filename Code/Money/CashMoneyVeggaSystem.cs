using System;
using System.Linq;
using Sandbox;
using Sandbox.Security;

namespace Sandbox.Money;

/// <summary>
/// CashMoneyVeggaSystem - Money prop with visual tiers and anti-exploit protection.
///
/// Visual Tiers:
/// - $1-10: models/money/single_clean_bended.vmdl (bent single bill)
/// - $10-1,000: models/money/single_clean.vmdl (flat single bill)
/// - $1,000-100,000: models/money/batch_clean.vmdl (stack of bills)
/// - $100,000-10,000,000: models/money/box.vmdl (money box)
/// </summary>
public sealed class CashMoneyVeggaSystem : Component
{
	[Property, Sync] public int Amount { get; set; } = 100;
	
	[Sync] private string _ownerId { get; set; } // Player who dropped this
	[Sync] private Guid _uniqueId { get; set; } // Unique ID to prevent double-consumption
	[Sync] private bool _consumed { get; set; } = false;
	[Sync] private float _dropTime { get; set; }

	private const float ConsumeCooldown = 2.0f; // Can't be consumed for 2 seconds after drop
	private int _lastVisualAmount = int.MinValue;
	private int _lastPickupAmount = int.MinValue;

	protected override void OnStart()
	{
		_uniqueId = Guid.NewGuid();
		_dropTime = Time.Now;

		EnsurePickupIsWired();
		
		// Set visual model based on amount
		UpdateVisualModel();
		_lastVisualAmount = Amount;
		_lastPickupAmount = Amount;
		
		Log.Info( $"💵 CashMoneyVeggaSystem spawned: ${Amount} | ID: {_uniqueId}" );
	}

	/// <summary>
	/// Update the visual model based on amount
	/// </summary>
	void UpdateVisualModel()
	{
		var modelRenderer = Components.Get<ModelRenderer>();
		if ( modelRenderer == null ) return;

		string modelPath = Amount switch
		{
			<= 100 => "models/money/single_clean_bended.vmdl",
			<= 10_000 => "models/money/single_clean.vmdl",
			<= 1_000_000 => "models/money/batch_clean.vmdl",
			_ => "models/money/box.vmdl"
		};

		modelRenderer.Model = Model.Load( modelPath );
	}

	/// <summary>
	/// Called when this money touches something (using trigger instead of collision)
	/// </summary>
	protected override void OnUpdate()
	{
		if ( Network.IsProxy ) return;
		if ( _consumed ) return;

		// Keep pickup amount synced.
		if ( Amount != _lastPickupAmount )
		{
			EnsurePickupIsWired();
			_lastPickupAmount = Amount;
		}

		// Keep model in sync if Amount changed after spawn.
		if ( Amount != _lastVisualAmount )
		{
			UpdateVisualModel();
			_lastVisualAmount = Amount;
		}

		// Cooldown check
		if ( Time.Now - _dropTime < ConsumeCooldown )
		{
			return;
		}

		// Check for nearby money pots (simple proximity check for now)
		// TODO: Implement proper collision detection when available
	}

	void EnsurePickupIsWired()
	{
		// This lets you place a prefab in the world with only a ModelRenderer,
		// and we still make it pick-upable cash.
		var pickup = Components.Get<VeggaPickupItem>();
		if ( pickup == null )
		{
			pickup = Components.Create<VeggaPickupItem>();
		}

		pickup.ItemId = VeggaCurrency.CashItemId;
		pickup.Quantity = Math.Clamp( Amount, 1, int.MaxValue );
		if ( pickup.PickupDelay <= 0 )
			pickup.PickupDelay = 0.25f;
	}

	/// <summary>
	/// Consume this money (called by money pot)
	/// </summary>
	public bool TryConsume( string consumerId )
	{
		if ( _consumed )
		{
			Log.Warning( $"⚠️ Money already consumed! ID: {_uniqueId}" );
			return false;
		}

		if ( Time.Now - _dropTime < ConsumeCooldown )
		{
			Log.Warning( $"⚠️ Money on cooldown! {ConsumeCooldown - (Time.Now - _dropTime):F1}s remaining" );
			return false;
		}

		_consumed = true;
		
		// Log transaction
		TransactionLog.LogTransaction(
			consumerId,
			TransactionLog.TransactionType.MoneyGained,
			$"Money pot consumed ${Amount}",
			0,
			Amount,
			_ownerId
		);

		Log.Info( $"✅ Money consumed: ${Amount} | ID: {_uniqueId} | Consumer: {consumerId}" );
		
		// Destroy the money prop
		GameObject.Destroy();
		
		return true;
	}

	/// <summary>
	/// Set the owner of this money (who dropped it)
	/// </summary>
	public void SetOwner( string playerId )
	{
		_ownerId = playerId;
	}

	/// <summary>
	/// Get unique ID
	/// </summary>
	public Guid GetUniqueId() => _uniqueId;

	/// <summary>
	/// Check if already consumed
	/// </summary>
	public bool IsConsumed() => _consumed;

	/// <summary>
	/// Get the owner ID (who dropped this money)
	/// </summary>
	public string GetOwner() => _ownerId;
}

