using System;
using System.Linq;
using Sandbox;

namespace Sandbox.Money
{
	public static class CashWorldDrop
	{
		private const string PrefabPath = "droppedcash.prefab";

		public static GameObject Spawn( Scene scene, Vector3 position, Rotation rotation, int amount, Guid droppedById, string ownerSteamId = null )
		{
			if ( scene == null )
				throw new ArgumentNullException( nameof( scene ) );

			amount = Math.Clamp( amount, 1, int.MaxValue );

			GameObject go = null;
			try
			{
				// Spawn the actual prefab so it stays in sync with Assets/droppedcash.prefab
				// and serializes as a prefab instance.
				go = GameObject.Clone( PrefabPath, new Transform( position, rotation ), scene, startEnabled: false, name: "droppedcash" );
			}
			catch
			{
				go = null;
			}

			if ( go == null || !go.IsValid() )
			{
				// Fallback: construct a compatible object if the prefab fails to clone.
				go = new GameObject( true, "droppedcash" );
				go.WorldPosition = position;
				go.WorldRotation = rotation;

				var renderer = go.Components.Create<ModelRenderer>();
				renderer.Model = Model.Load( "models/money/single_clean.vmdl" );

				var cashFallback = go.Components.Create<CashMoneyVeggaSystem>();
				cashFallback.Amount = amount;
				if ( !string.IsNullOrWhiteSpace( ownerSteamId ) )
					cashFallback.SetOwner( ownerSteamId );

				var pickupFallback = go.Components.Create<VeggaPickupItem>();
				pickupFallback.ItemId = VeggaCurrency.CashItemId;
				pickupFallback.Quantity = amount;
				pickupFallback.PickupDelay = 0.25f;
				if ( droppedById != Guid.Empty )
					pickupFallback.DroppedById = droppedById;
				pickupFallback.DroppedAtTime = Time.Now;

				var colliderFallback = go.Components.Create<ModelCollider>();
				colliderFallback.Model = renderer.Model;
				colliderFallback.IsTrigger = false;

				var rbFallback = go.Components.Create<Rigidbody>();
				rbFallback.MotionEnabled = true;
				rbFallback.Gravity = true;

				return go;
			}

			// Apply runtime overrides to the prefab instance.
			var cash = go.Components.Get<CashMoneyVeggaSystem>()
				?? go.Components.GetAll<CashMoneyVeggaSystem>( FindMode.InDescendants ).FirstOrDefault();
			if ( cash != null )
			{
				cash.Amount = amount;
				if ( !string.IsNullOrWhiteSpace( ownerSteamId ) )
					cash.SetOwner( ownerSteamId );
			}

			var pickup = go.Components.Get<VeggaPickupItem>()
				?? go.Components.GetAll<VeggaPickupItem>( FindMode.InDescendants ).FirstOrDefault();
			if ( pickup != null )
			{
				pickup.ItemId = VeggaCurrency.CashItemId;
				pickup.Quantity = amount;
				if ( pickup.PickupDelay <= 0 )
					pickup.PickupDelay = 0.25f;
				if ( droppedById != Guid.Empty )
					pickup.DroppedById = droppedById;
				pickup.DroppedAtTime = Time.Now;
			}

			// Enable after configuration to avoid one-frame defaults.
			go.Enabled = true;
			return go;
		}

		public static GameObject Spawn( Scene scene, Vector3 position, Rotation rotation, int amount, string ownerSteamId = null )
			=> Spawn( scene, position, rotation, amount, Guid.Empty, ownerSteamId );
	}
}
