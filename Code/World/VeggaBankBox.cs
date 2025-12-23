using System;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Bank interaction point. Players must be near a bank to exchange coins for cash.
/// </summary>
public sealed class VeggaBankBox : Component
{
	[Property] public float InteractRange { get; set; } = 160f;

	protected override void OnUpdate()
	{
		// Client-side: allow pressing E to get usage info.
		if ( Connection.Local != null )
			TryClientInteractHint();
	}

	void TryClientInteractHint()
	{
		if ( Sandbox.UI.VeggaChat.IsChatInputOpenGlobal )
			return;

		var local = PlayerVeggaStats.Local;
		if ( local == null || !local.IsValid() )
			return;

		var scene = Scene ?? Game.ActiveScene;
		var cam = scene?.Camera;
		if ( cam == null )
			return;

		var ray = cam.ScreenNormalToRay( new Vector2( 0.5f, 0.5f ) );
		var tr = scene.Trace.Ray( ray, InteractRange + 40f )
			.WithoutTags( "trigger", "particles" )
			.Run();

		if ( !tr.Hit )
			return;

		var hitObj = tr.GameObject;
		if ( hitObj == null )
			return;

		bool isThis = false;
		for ( var obj = hitObj; obj != null; obj = obj.Parent )
		{
			if ( obj == GameObject ) { isThis = true; break; }
		}
		if ( !isThis ) return;

		float dist = Vector3.DistanceBetween( local.WorldPosition, WorldPosition );
		if ( dist > InteractRange )
			return;

		if ( Input.Pressed( "use" ) )
		{
			var rate = Sandbox.Money.VeggaBankExchange.CoinToCashRate;
			var fee = Sandbox.Money.VeggaBankExchange.FeePercent;
			VeggaChatManager.Local?.AddLocalMessage( $"Bank: /bank <coins> (rate={rate} cash/coin, fee={fee:0.#}%). Also: /note <log|chopped> <count> and /unnote <log|chopped> <count>.", ChatMessageType.System );
		}
	}

	public static bool IsPlayerNearAnyBank( PlayerVeggaStats stats, float extraRange = 0f )
	{
		if ( stats == null || !stats.IsValid() )
			return false;

		var scene = stats.Scene ?? Game.ActiveScene;
		if ( scene == null )
			return false;

		foreach ( var bank in scene.GetAllComponents<VeggaBankBox>() )
		{
			if ( bank == null || !bank.IsValid() )
				continue;

			float r = MathF.Max( 0f, bank.InteractRange + extraRange );
			float dist = Vector3.DistanceBetween( stats.WorldPosition, bank.WorldPosition );
			if ( dist <= r )
				return true;
		}

		return false;
	}
}
