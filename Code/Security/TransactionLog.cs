using System;
using System.Collections.Generic;
using Sandbox;

namespace Sandbox.Security;

/// <summary>
/// Tracks all player transactions for anti-exploit validation.
/// Logs money, items, stats changes with timestamps.
/// </summary>
public sealed class TransactionLog : Component
{
	public enum TransactionType
	{
		MoneyGained,
		MoneyLost,
		ItemAdded,
		ItemRemoved,
		HealthChanged,
		ArmorChanged,
		Trade,
		AdminCommand,
		Death,
		Spawn
	}

	public class Transaction
	{
		public DateTime Timestamp { get; set; }
		public TransactionType Type { get; set; }
		public string Description { get; set; }
		public float ValueBefore { get; set; }
		public float ValueAfter { get; set; }
		public string SourcePlayerId { get; set; }
		public string TargetPlayerId { get; set; }
		public bool Validated { get; set; } = true;
	}

	// History per player (SteamID -> List of transactions)
	private static Dictionary<string, List<Transaction>> _playerHistory = new();
	
	// Suspicious activity tracking
	private static Dictionary<string, int> _suspicionScore = new();

	// Settings
	private const int MaxHistoryPerPlayer = 1000;
	private const int SuspicionThreshold = 10;
	private const float NetworthChangeThreshold = 2.0f; // 200% increase = suspicious

	/// <summary>
	/// Log a transaction for a player
	/// </summary>
	public static void LogTransaction( string playerId, TransactionType type, string description, float valueBefore, float valueAfter, string sourceId = null )
	{
		if ( !_playerHistory.ContainsKey( playerId ) )
		{
			_playerHistory[playerId] = new List<Transaction>();
		}

		var transaction = new Transaction
		{
			Timestamp = DateTime.UtcNow,
			Type = type,
			Description = description,
			ValueBefore = valueBefore,
			ValueAfter = valueAfter,
			SourcePlayerId = sourceId ?? playerId,
			TargetPlayerId = playerId
		};

		_playerHistory[playerId].Add( transaction );

		// Trim old history
		if ( _playerHistory[playerId].Count > MaxHistoryPerPlayer )
		{
			_playerHistory[playerId].RemoveAt( 0 );
		}

		// Validate transaction
		ValidateTransaction( playerId, transaction );

		Log.Info( $"📝 Transaction: {playerId} | {type} | {description} | {valueBefore} → {valueAfter}" );
	}

	/// <summary>
	/// Validate transaction for suspicious activity
	/// </summary>
	private static void ValidateTransaction( string playerId, Transaction transaction )
	{
		// Check for rapid networth increase
		if ( transaction.Type == TransactionType.MoneyGained || transaction.Type == TransactionType.ItemAdded )
		{
			float change = transaction.ValueAfter - transaction.ValueBefore;
			float percentChange = transaction.ValueBefore > 0 ? change / transaction.ValueBefore : 0;

			if ( percentChange > NetworthChangeThreshold )
			{
				// Suspicious! Networth doubled or more
				AddSuspicion( playerId, $"Rapid networth increase: {percentChange * 100:F0}%" );
				transaction.Validated = false;
			}
		}

		// Check for rapid item duplication
		var recentTransactions = GetRecentTransactions( playerId, 5.0f ); // Last 5 seconds
		int itemAdds = 0;
		foreach ( var t in recentTransactions )
		{
			if ( t.Type == TransactionType.ItemAdded )
				itemAdds++;
		}

		if ( itemAdds > 5 )
		{
			AddSuspicion( playerId, $"Rapid item addition: {itemAdds} items in 5 seconds" );
			transaction.Validated = false;
		}
	}

	/// <summary>
	/// Add suspicion score to player
	/// </summary>
	private static void AddSuspicion( string playerId, string reason )
	{
		if ( !_suspicionScore.ContainsKey( playerId ) )
		{
			_suspicionScore[playerId] = 0;
		}

		_suspicionScore[playerId]++;

		Log.Warning( $"⚠️ SUSPICIOUS ACTIVITY: {playerId} | {reason} | Score: {_suspicionScore[playerId]}" );

		if ( _suspicionScore[playerId] >= SuspicionThreshold )
		{
			NotifyAdmins( playerId, reason );
		}
	}

	/// <summary>
	/// Notify admins of suspicious activity
	/// </summary>
	private static void NotifyAdmins( string playerId, string reason )
	{
		Log.Error( $"🚨 ALERT: Player {playerId} flagged for suspicious activity! Reason: {reason}" );
		// TODO: Send chat message to all admins
		// TODO: Auto-freeze player for admin review
	}

	/// <summary>
	/// Get recent transactions for a player
	/// </summary>
	public static List<Transaction> GetRecentTransactions( string playerId, float seconds )
	{
		if ( !_playerHistory.ContainsKey( playerId ) )
			return new List<Transaction>();

		var cutoff = DateTime.UtcNow.AddSeconds( -seconds );
		var recent = new List<Transaction>();

		foreach ( var t in _playerHistory[playerId] )
		{
			if ( t.Timestamp >= cutoff )
				recent.Add( t );
		}

		return recent;
	}
}

