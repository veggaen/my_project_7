using Sandbox;
using System;
using System.Linq;

/// <summary>
/// Simple job salary system:
/// - Runs on the host only.
/// - Every SalaryIntervalSeconds, gives the player money based on JobName.
/// - Uses PlayerVeggaStats.AddMoney so data is marked dirty for saving.
/// Attach this to the player prefab alongside PlayerVeggaStats.
/// </summary>
public sealed class PlayerSalary : Component
{
	[Property] public PlayerVeggaStats Stats { get; set; }

	/// <summary>
	/// How often to pay salary, in seconds. Default: 5 minutes.
	/// </summary>
	[Property] public float SalaryIntervalSeconds { get; set; } = 300f;

	RealTimeSince _timeSinceLastPay;

	protected override void OnStart()
	{
		Stats ??= Components.Get<PlayerVeggaStats>();

		if ( Stats == null )
		{
			Log.Warning( "PlayerSalary: No PlayerVeggaStats found on this GameObject." );
		}
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( Stats is null || !Stats.IsValid() )
			return;

		// Only pay real players that are owned by a connection.
		if ( Stats.Network?.Owner is null )
			return;

		if ( _timeSinceLastPay < SalaryIntervalSeconds )
			return;

		_timeSinceLastPay = 0;

		int amount = GetSalaryForJob( Stats.JobName );
		if ( amount <= 0 )
			return;

		Stats.AddMoney( amount );
		Log.Info( $"💰 Salary: paid ${amount} to {Stats.Network.Owner.DisplayName} ({Stats.JobName})" );

		// Client feedback
		var ownerId = Stats.Network.Owner.Id;
		var mgr = FindChatManagerForOwner( ownerId );
		mgr?.RpcSendSystemMessage( ownerId, $"Salary paid: ${amount}.", ChatMessageType.System );
		if ( VeggaSfxSettings.Enabled && VeggaSfxSettings.SalaryEnabled )
			mgr?.RpcPlayUiSound( ownerId, VeggaSfxSettings.CoinSound );
	}

	VeggaChatManager FindChatManagerForOwner( Guid ownerId )
	{
		var scene = Scene ?? Game.ActiveScene;
		if ( scene == null ) return null;
		return scene.GetAllComponents<VeggaChatManager>()
			.FirstOrDefault( m => m != null && m.IsValid() && m.Network?.Owner?.Id == ownerId );
	}

	int GetSalaryForJob( string job )
	{
		if ( string.IsNullOrWhiteSpace( job ) )
			return 0;

		job = job.ToLowerInvariant();

		// Very simple first pass – tune amounts per job later.
		return job switch
		{
			"citizen" => 50,
			"police" or "guard" => 150,
			"doctor" => 120,
			_ => 30 // fallback for any other job
		};
	}
}


