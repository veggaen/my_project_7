using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Security;

namespace Sandbox.Money;

/// <summary>
/// CashMoneyVeggaPot - Money storage with anti-exploit proximity detection.
/// Prevents double-consumption when multiple pots are close together.
/// </summary>
public sealed class CashMoneyVeggaPot : Component
{
	[Property] public float ProximityCheckRadius { get; set; } = 200f; // Check for other pots within 200 units
	[Property] public string OwnerId { get; set; } // Player who owns this pot

	[Sync] private int _storedMoney { get; set; } = 0;
	
	// Track consumed money IDs to prevent double-consumption
	private static HashSet<Guid> _globalConsumedIds = new();
	
	// Track money being processed (locked)
	private static HashSet<Guid> _processingIds = new();

	protected override void OnStart()
	{
		Log.Info( $"💰 CashMoneyVeggaPot created | Owner: {OwnerId}" );
	}

	/// <summary>
	/// Try to consume money that touched this pot
	/// </summary>
	public void TryConsumeMoney( CashMoneyVeggaSystem money )
	{
		if ( Network.IsProxy ) return;

		var moneyId = money.GetUniqueId();

		// Check if already consumed globally
		if ( _globalConsumedIds.Contains( moneyId ) )
		{
			Log.Warning( $"⚠️ Money already consumed globally! ID: {moneyId}" );
			return;
		}

		// Check if being processed by another pot
		if ( _processingIds.Contains( moneyId ) )
		{
			Log.Warning( $"⚠️ Money being processed by another pot! ID: {moneyId}" );
			return;
		}

		// Lock this money for processing
		_processingIds.Add( moneyId );

		// Check for nearby pots (anti-exploit)
		var nearbyPots = CheckNearbyPots();
		if ( nearbyPots.Count > 0 )
		{
			Log.Warning( $"⚠️ PROXIMITY ALERT: {nearbyPots.Count} other pots nearby! Entering validation mode..." );
			
			// Validation mode: Check transaction logs
			if ( !ValidateMoneyDrop( money ) )
			{
				Log.Error( $"🚨 VALIDATION FAILED: Money drop suspicious! ID: {moneyId}" );
				_processingIds.Remove( moneyId );
				return;
			}
		}

		// Consume the money
		int amount = money.Amount;
		if ( money.TryConsume( OwnerId ) )
		{
			_storedMoney += amount;
			_globalConsumedIds.Add( moneyId );
			
			Log.Info( $"✅ Money pot consumed ${amount} | Total: ${_storedMoney} | ID: {moneyId}" );
			
			// Log transaction
			TransactionLog.LogTransaction(
				OwnerId,
				TransactionLog.TransactionType.MoneyGained,
				$"Money pot absorbed ${amount}",
				_storedMoney - amount,
				_storedMoney
			);
		}

		// Unlock
		_processingIds.Remove( moneyId );
	}

	/// <summary>
	/// Check for nearby money pots (anti-exploit)
	/// </summary>
	List<CashMoneyVeggaPot> CheckNearbyPots()
	{
		var nearbyPots = new List<CashMoneyVeggaPot>();
		var allPots = Scene.GetAllComponents<CashMoneyVeggaPot>();

		foreach ( var pot in allPots )
		{
			if ( pot == this ) continue;
			if ( !pot.IsValid() ) continue;

			float distance = Vector3.DistanceBetween( WorldPosition, pot.WorldPosition );
			if ( distance <= ProximityCheckRadius )
			{
				nearbyPots.Add( pot );
				Log.Warning( $"⚠️ Nearby pot detected: {distance:F1} units away" );
			}
		}

		return nearbyPots;
	}

	/// <summary>
	/// Validate money drop by checking transaction logs
	/// </summary>
	bool ValidateMoneyDrop( CashMoneyVeggaSystem money )
	{
		// Get recent transactions for the money owner
		var recentTransactions = TransactionLog.GetRecentTransactions( money.GetOwner(), 10f );

		// Check if there's a legitimate money drop in the logs
		bool foundDrop = false;
		foreach ( var transaction in recentTransactions )
		{
			if ( transaction.Type == TransactionLog.TransactionType.MoneyLost &&
			     Math.Abs( transaction.ValueBefore - transaction.ValueAfter - money.Amount ) < 0.01f )
			{
				foundDrop = true;
				break;
			}
		}

		if ( !foundDrop )
		{
			Log.Error( $"🚨 No matching money drop found in transaction logs!" );
			return false;
		}

		return true;
	}

	/// <summary>
	/// Eject money from the pot
	/// </summary>
	[Button( "Eject Money" ), Group( "Testing" )]
	public void EjectMoney( int amount )
	{
		if ( Network.IsProxy ) return;

		if ( amount > _storedMoney )
		{
			Log.Warning( $"⚠️ Cannot eject ${amount}, only ${_storedMoney} available!" );
			return;
		}

		_storedMoney -= amount;

		// Spawn money prop
		// TODO: Implement money prop spawning

		Log.Info( $"💸 Ejected ${amount} | Remaining: ${_storedMoney}" );
	}

	/// <summary>
	/// Get stored money amount
	/// </summary>
	public int GetStoredMoney() => _storedMoney;
}

