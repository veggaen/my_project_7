using System;
using Sandbox;

namespace Sandbox.Money;

public static class CashWorldDrop
{
	public static GameObject Spawn( Scene scene, Vector3 position, Rotation rotation, int amount, string ownerSteamId = null )
	{
		if ( scene == null ) throw new ArgumentNullException( nameof( scene ) );
		amount = Math.Clamp( amount, 1, int.MaxValue );

		var go = new GameObject( true, "DroppedCash" );
		go.WorldPosition = position;
		go.WorldRotation = rotation;

		var renderer = go.Components.Create<ModelRenderer>();

		var cash = go.Components.Create<CashMoneyVeggaSystem>();
		cash.Amount = amount;
		if ( !string.IsNullOrWhiteSpace( ownerSteamId ) )
			cash.SetOwner( ownerSteamId );

		// Ensure model is set now that Amount is assigned.
		// (CashMoneyVeggaSystem also updates on update ticks.)
		renderer.Model = Model.Load( "models/money/single_clean.vmdl" );

		var pickup = go.Components.Create<VeggaPickupItem>();
		pickup.ItemId = VeggaCurrency.CashItemId;
		pickup.Quantity = amount;
		pickup.PickupDelay = 0.25f;

		var rb = go.Components.Create<Rigidbody>();
		rb.Gravity = true;

		var collider = go.Components.Create<SphereCollider>();
		collider.Radius = 12f;
		collider.IsTrigger = false;

		return go;
	}
}
