using System;
using System.Linq;
using Sandbox;
using Sandbox.Security;

namespace Sandbox.Money;

/// <summary>
/// CashMoneyVeggaSystem - Money prop with visual tiers and anti-exploit protection.
///
/// Visual Tiers:
/// - $1-100: models/money/single_clean_bended.vmdl (bent single bill)
/// - $100-10,000: models/money/single_clean.vmdl (flat single bill)
/// - $10,000-1,000,000: models/money/batch_clean.vmdl (stack of bills)
/// - $100,000-10,000,000: models/money/box.vmdl (money box)
/// </summary>
public sealed class CashMoneyVeggaSystem : Component
{
	[Property] public int Amount { get; set; } = 100;
	
	[Sync] private string _ownerId { get; set; } // Player who dropped this
	[Sync] private Guid _uniqueId { get; set; } // Unique ID to prevent double-consumption
	[Sync] private bool _consumed { get; set; } = false;
	[Sync] private float _dropTime { get; set; }

	private const float ConsumeCooldown = 2.0f; // Can't be consumed for 2 seconds after drop

	protected override void OnStart()
	{
		_uniqueId = Guid.NewGuid();
		_dropTime = Time.Now;
		
		// Set visual model based on amount
		UpdateVisualModel();
		
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
			<= 10 => "models/money/single_clean_bended.vmdl",
			<= 1000 => "models/money/single_clean.vmdl",
			<= 100000 => "models/money/batch_clean.vmdl",
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

		// Cooldown check
		if ( Time.Now - _dropTime < ConsumeCooldown )
		{
			return;
		}

		// Check for nearby money pots (simple proximity check for now)
		// TODO: Implement proper collision detection when available
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

