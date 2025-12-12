using Sandbox;
using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Manages player-to-player trading. Attach to the player prefab.
/// Handles trade requests, trade sessions, and money/item exchanges.
/// </summary>
public sealed class TradeManager : Component
{
	[Property] public PlayerVeggaStats Stats { get; set; }

	/// <summary>
	/// Maximum distance to initiate a trade (in units).
	/// </summary>
	[Property] public float MaxTradeDistance { get; set; } = 200f;

	/// <summary>
	/// If true, automatically accept incoming trade requests.
	/// </summary>
	[Property, Sync] public bool AutoAcceptTrades { get; set; } = true;

	/// <summary>
	/// Current trade session (null if not trading).
	/// </summary>
	public TradeSession ActiveTrade { get; private set; }

	/// <summary>
	/// Pending incoming trade request from another player.
	/// </summary>
	public TradeRequest PendingRequest { get; private set; }

	/// <summary>
	/// Local singleton for UI access.
	/// </summary>
	public static TradeManager Local { get; private set; }

	/// <summary>
	/// Event fired when a trade request is received.
	/// </summary>
	public Action<TradeRequest> OnTradeRequestReceived;

	/// <summary>
	/// Event fired when a trade session starts.
	/// </summary>
	public Action<TradeSession> OnTradeStarted;

	/// <summary>
	/// Event fired when a trade session ends.
	/// </summary>
	public Action OnTradeEnded;

	protected override void OnStart()
	{
		Stats ??= Components.Get<PlayerVeggaStats>();

		if ( !IsProxy && Connection.Local != null )
		{
			Local = this;
		}
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		// Press E while aiming at a player to request trade
		if ( Input.Pressed( "use" ) && ActiveTrade == null )
		{
			TryInitiateTrade();
		}
	}

	void TryInitiateTrade()
	{
		// Raycast from camera to find player
		var camera = Scene.Camera;
		if ( camera == null ) return;

		var ray = camera.ScreenNormalToRay( new Vector2( 0.5f, 0.5f ) );
		var tr = Scene.Trace.Ray( ray, MaxTradeDistance )
			.WithoutTags( "trigger" )
			.Run();

		if ( !tr.Hit ) return;

		// Find PlayerVeggaStats on hit object
		var targetStats = tr.GameObject?.Components.Get<PlayerVeggaStats>();
		if ( targetStats == null || targetStats == Stats ) return;

		// Don't trade with yourself
		if ( targetStats.Network?.Owner == Stats?.Network?.Owner ) return;

		Log.Info( $"[Trade] Requesting trade with {targetStats.Network?.Owner?.DisplayName}" );
		SendTradeRequest( targetStats.Network.Owner.Id );
	}

	/// <summary>
	/// Send a trade request to another player.
	/// </summary>
	[Rpc.Broadcast]
	public void SendTradeRequest( Guid targetOwnerId )
	{
		// Only process on the target's client
		if ( Connection.Local?.Id != targetOwnerId ) return;

		var localManager = Local;
		if ( localManager == null ) return;

		var requesterName = Stats?.Network?.Owner?.DisplayName ?? "Unknown";
		var requesterId = Stats?.Network?.Owner?.Id ?? Guid.Empty;

		Log.Info( $"[Trade] Received trade request from {requesterName}" );

		var request = new TradeRequest
		{
			RequesterName = requesterName,
			RequesterId = requesterId,
			ReceivedTime = Time.Now
		};

		localManager.PendingRequest = request;
		localManager.OnTradeRequestReceived?.Invoke( request );

		// Auto-accept if enabled
		if ( localManager.AutoAcceptTrades )
		{
			localManager.AcceptTradeRequest();
		}
	}

	/// <summary>
	/// Accept a pending trade request.
	/// </summary>
	public void AcceptTradeRequest()
	{
		if ( PendingRequest == null ) return;

		Log.Info( $"[Trade] Accepting trade from {PendingRequest.RequesterName}" );

		// Notify the requester that we accepted
		BroadcastTradeAccepted( PendingRequest.RequesterId, Connection.Local.Id );

		PendingRequest = null;
	}

	/// <summary>
	/// Decline a pending trade request.
	/// </summary>
	public void DeclineTradeRequest()
	{
		if ( PendingRequest == null ) return;

		Log.Info( $"[Trade] Declining trade from {PendingRequest.RequesterName}" );

		BroadcastTradeDeclined( PendingRequest.RequesterId );
		PendingRequest = null;
	}

	[Rpc.Broadcast]
	void BroadcastTradeAccepted( Guid requesterId, Guid accepterId )
	{
		// Start trade session on both clients
		if ( Connection.Local?.Id != requesterId && Connection.Local?.Id != accepterId ) return;

		var localManager = Local;
		if ( localManager == null ) return;

		// Find the other player's name
		string partnerName = "Unknown";
		Guid partnerId = Guid.Empty;

		if ( Connection.Local?.Id == requesterId )
		{
			partnerId = accepterId;
		}
		else
		{
			partnerId = requesterId;
		}

		// Find partner name from all players
		foreach ( var stats in Scene.GetAllComponents<PlayerVeggaStats>() )
		{
			if ( stats.Network?.Owner?.Id == partnerId )
			{
				partnerName = stats.Network.Owner.DisplayName;
				break;
			}
		}

		var session = new TradeSession
		{
			PartnerId = partnerId,
			PartnerName = partnerName,
			MyOffer = new TradeOffer(),
			TheirOffer = new TradeOffer()
		};

		localManager.ActiveTrade = session;
		localManager.OnTradeStarted?.Invoke( session );

		Log.Info( $"[Trade] Trade session started with {partnerName}" );
	}

	[Rpc.Broadcast]
	void BroadcastTradeDeclined( Guid requesterId )
	{
		if ( Connection.Local?.Id != requesterId ) return;

		Log.Info( "[Trade] Trade request was declined." );
		// Could show a notification here
	}

	/// <summary>
	/// Update the money amount in my offer.
	/// </summary>
	public void SetOfferMoney( int amount )
	{
		if ( ActiveTrade == null ) return;

		amount = Math.Max( 0, Math.Min( amount, Stats?.Money ?? 0 ) );
		ActiveTrade.MyOffer.Money = amount;
		ActiveTrade.MyConfirmed = false;

		// Sync to partner
		SyncOfferToPartner( ActiveTrade.PartnerId, amount );
	}

	[Rpc.Broadcast]
	void SyncOfferToPartner( Guid partnerId, int money )
	{
		if ( Connection.Local?.Id != partnerId ) return;

		var localManager = Local;
		if ( localManager?.ActiveTrade == null ) return;

		localManager.ActiveTrade.TheirOffer.Money = money;
		localManager.ActiveTrade.PartnerConfirmed = false;

		Log.Info( $"[Trade] Partner updated offer: ${money}" );
	}

	/// <summary>
	/// Confirm my side of the trade.
	/// </summary>
	public void ConfirmTrade()
	{
		if ( ActiveTrade == null ) return;

		ActiveTrade.MyConfirmed = true;

		// Notify partner
		SyncConfirmation( ActiveTrade.PartnerId, true );

		// Check if both confirmed
		CheckTradeCompletion();
	}

	/// <summary>
	/// Unconfirm (if I change my offer).
	/// </summary>
	public void UnconfirmTrade()
	{
		if ( ActiveTrade == null ) return;

		ActiveTrade.MyConfirmed = false;
		SyncConfirmation( ActiveTrade.PartnerId, false );
	}

	[Rpc.Broadcast]
	void SyncConfirmation( Guid partnerId, bool confirmed )
	{
		if ( Connection.Local?.Id != partnerId ) return;

		var localManager = Local;
		if ( localManager?.ActiveTrade == null ) return;

		localManager.ActiveTrade.PartnerConfirmed = confirmed;
		localManager.CheckTradeCompletion();
	}

	void CheckTradeCompletion()
	{
		if ( ActiveTrade == null ) return;

		if ( ActiveTrade.MyConfirmed && ActiveTrade.PartnerConfirmed )
		{
			// Execute the trade on the host
			if ( Networking.IsHost )
			{
				ExecuteTradeOnHost( Connection.Local.Id, ActiveTrade.PartnerId, ActiveTrade.MyOffer.Money, ActiveTrade.TheirOffer.Money );
			}
			else
			{
				// Request host to execute
				RequestTradeExecution( ActiveTrade.PartnerId, ActiveTrade.MyOffer.Money );
			}
		}
	}

	[Rpc.Broadcast]
	void RequestTradeExecution( Guid partnerId, int myOfferMoney )
	{
		if ( !Networking.IsHost ) return;

		// Host executes the trade
		// Find both players' stats and swap money
		PlayerVeggaStats playerA = null;
		PlayerVeggaStats playerB = null;
		int playerAOffer = myOfferMoney;
		int playerBOffer = 0;

		var senderId = Stats?.Network?.Owner?.Id ?? Guid.Empty;

		foreach ( var stats in Scene.GetAllComponents<PlayerVeggaStats>() )
		{
			if ( stats.Network?.Owner?.Id == senderId )
			{
				playerA = stats;
			}
			else if ( stats.Network?.Owner?.Id == partnerId )
			{
				playerB = stats;
				// Get partner's offer from their TradeManager
				var partnerTrade = stats.GameObject?.Components.Get<TradeManager>();
				if ( partnerTrade?.ActiveTrade != null )
				{
					playerBOffer = partnerTrade.ActiveTrade.MyOffer.Money;
				}
			}
		}

		if ( playerA != null && playerB != null )
		{
			ExecuteTradeOnHost( senderId, partnerId, playerAOffer, playerBOffer );
		}
	}

	void ExecuteTradeOnHost( Guid playerAId, Guid playerBId, int playerAOffer, int playerBOffer )
	{
		if ( !Networking.IsHost ) return;

		PlayerVeggaStats playerA = null;
		PlayerVeggaStats playerB = null;

		foreach ( var stats in Scene.GetAllComponents<PlayerVeggaStats>() )
		{
			if ( stats.Network?.Owner?.Id == playerAId )
				playerA = stats;
			else if ( stats.Network?.Owner?.Id == playerBId )
				playerB = stats;
		}

		if ( playerA == null || playerB == null )
		{
			Log.Warning( "[Trade] Could not find both players for trade execution." );
			return;
		}

		// Verify both players have enough money
		if ( playerA.Money < playerAOffer || playerB.Money < playerBOffer )
		{
			Log.Warning( "[Trade] One or both players don't have enough money." );
			BroadcastTradeFailed( playerAId, playerBId, "Insufficient funds." );
			return;
		}

		// Execute the swap
		playerA.AddMoney( -playerAOffer + playerBOffer );
		playerB.AddMoney( -playerBOffer + playerAOffer );

		Log.Info( $"[Trade] Trade completed! {playerA.Network?.Owner?.DisplayName} gave ${playerAOffer}, received ${playerBOffer}" );

		// Notify both players
		BroadcastTradeCompleted( playerAId, playerBId );
	}

	[Rpc.Broadcast]
	void BroadcastTradeCompleted( Guid playerAId, Guid playerBId )
	{
		if ( Connection.Local?.Id != playerAId && Connection.Local?.Id != playerBId ) return;

		var localManager = Local;
		if ( localManager == null ) return;

		Log.Info( "[Trade] Trade completed successfully!" );

		localManager.ActiveTrade = null;
		localManager.OnTradeEnded?.Invoke();
	}

	[Rpc.Broadcast]
	void BroadcastTradeFailed( Guid playerAId, Guid playerBId, string reason )
	{
		if ( Connection.Local?.Id != playerAId && Connection.Local?.Id != playerBId ) return;

		var localManager = Local;
		if ( localManager == null ) return;

		Log.Warning( $"[Trade] Trade failed: {reason}" );

		localManager.ActiveTrade = null;
		localManager.OnTradeEnded?.Invoke();
	}

	/// <summary>
	/// Cancel the current trade.
	/// </summary>
	public void CancelTrade()
	{
		if ( ActiveTrade == null ) return;

		var partnerId = ActiveTrade.PartnerId;
		ActiveTrade = null;
		OnTradeEnded?.Invoke();

		BroadcastTradeCancelled( partnerId );
	}

	[Rpc.Broadcast]
	void BroadcastTradeCancelled( Guid partnerId )
	{
		if ( Connection.Local?.Id != partnerId ) return;

		var localManager = Local;
		if ( localManager == null ) return;

		Log.Info( "[Trade] Trade was cancelled by partner." );

		localManager.ActiveTrade = null;
		localManager.OnTradeEnded?.Invoke();
	}
}

/// <summary>
/// Represents a pending trade request.
/// </summary>
public class TradeRequest
{
	public string RequesterName { get; set; }
	public Guid RequesterId { get; set; }
	public float ReceivedTime { get; set; }
}

/// <summary>
/// Represents an active trade session.
/// </summary>
public class TradeSession
{
	public Guid PartnerId { get; set; }
	public string PartnerName { get; set; }
	public TradeOffer MyOffer { get; set; }
	public TradeOffer TheirOffer { get; set; }
	public bool MyConfirmed { get; set; }
	public bool PartnerConfirmed { get; set; }
}

/// <summary>
/// Represents one side's offer in a trade.
/// </summary>
public class TradeOffer
{
	public int Money { get; set; }
	// Future: public List<Item> Items { get; set; }
}
