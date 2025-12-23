using System;
using System.Collections.Generic;
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


				// Paper-thin model colliders can behave badly (spins/launches). Use a simple box instead.
				var colliderFallback = go.Components.Create<BoxCollider>();
				colliderFallback.Scale = new Vector3( 8f, 4f, 1f );
				colliderFallback.IsTrigger = false;

				var rbFallback = go.Components.Create<Rigidbody>();
				rbFallback.MotionEnabled = true;
				rbFallback.Gravity = true;
				// Match the "heavy" feel used by gold bars (prevents floaty bills if we hit this fallback path).
				rbFallback.MassOverride = 25;
				rbFallback.LinearDamping = 0.25f;
				rbFallback.AngularDamping = 1f;

				return go;
			}

			IEnumerable<T> SelfAndDescendants<T>() where T : Component
			{
				var root = go.Components.Get<T>();
				if ( root != null )
					yield return root;
				foreach ( var child in go.Components.GetAll<T>( FindMode.InDescendants ) )
					yield return child;
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


			foreach ( var pickup in SelfAndDescendants<VeggaPickupItem>() )
			{
				if ( pickup == null ) continue;
				pickup.ItemId = VeggaCurrency.CashItemId;
				pickup.Quantity = amount;
				if ( pickup.PickupDelay <= 0 )
					pickup.PickupDelay = 0.25f;
				if ( droppedById != Guid.Empty )
					pickup.DroppedById = droppedById;
				pickup.DroppedAtTime = Time.Now;
			}

			// Enforce stable physics settings even if the prefab gets edited later.
			var rb = go.Components.Get<Rigidbody>()
				?? go.Components.GetAll<Rigidbody>( FindMode.InDescendants ).FirstOrDefault();
			if ( rb != null )
			{
				rb.MotionEnabled = true;
				rb.Gravity = true;
				rb.MassOverride = 25;
				rb.LinearDamping = 0.25f;
				rb.AngularDamping = 1f;
			}

			// Replace thin model collider with a simple box to avoid “flying away” on collision.
			var existingBox = go.Components.Get<BoxCollider>()
				?? go.Components.GetAll<BoxCollider>( FindMode.InDescendants ).FirstOrDefault();
			if ( existingBox == null )
			{
				var modelCollider = go.Components.Get<ModelCollider>()
					?? go.Components.GetAll<ModelCollider>( FindMode.InDescendants ).FirstOrDefault();
				if ( modelCollider != null )
					modelCollider.Enabled = false;

				var box = go.Components.Create<BoxCollider>();
				box.Scale = new Vector3( 8f, 4f, 1f );
				box.IsTrigger = false;
			}

			// Enable after configuration to avoid one-frame defaults.
			go.Enabled = true;

			// Some prefab/component lifecycles can apply defaults on enable.
			// Re-apply after enabling so the drop amount is always authoritative.
			cash = go.Components.Get<CashMoneyVeggaSystem>()
				?? go.Components.GetAll<CashMoneyVeggaSystem>( FindMode.InDescendants ).FirstOrDefault();
			if ( cash != null )
			{
				cash.Amount = amount;
				if ( !string.IsNullOrWhiteSpace( ownerSteamId ) )
					cash.SetOwner( ownerSteamId );
			}

			foreach ( var pickup2 in SelfAndDescendants<VeggaPickupItem>() )
			{
				if ( pickup2 == null ) continue;
				pickup2.ItemId = VeggaCurrency.CashItemId;
				pickup2.Quantity = amount;
				if ( pickup2.PickupDelay <= 0 )
					pickup2.PickupDelay = 0.25f;
				if ( droppedById != Guid.Empty )
					pickup2.DroppedById = droppedById;
				pickup2.DroppedAtTime = Time.Now;
			}
			return go;
		}

		public static GameObject Spawn( Scene scene, Vector3 position, Rotation rotation, int amount, string ownerSteamId = null )
			=> Spawn( scene, position, rotation, amount, Guid.Empty, ownerSteamId );
	}
}
