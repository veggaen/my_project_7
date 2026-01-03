using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace Sandbox.Money
{
	public static class CashWorldDrop
	{
		private const string DefaultPrefabPath = "moneyveggabundle.prefab";
		private const float DefaultPickupDelay = 0.25f;

		public static GameObject Spawn( Scene scene, Vector3 position, Rotation rotation, int amount, Guid droppedById, string ownerSteamId = null )
		{
			if ( scene == null )
				throw new ArgumentNullException( nameof( scene ) );

			amount = Math.Clamp( amount, 1, int.MaxValue );

			GameObject go = null;
			try
			{
				// Spawn the actual prefab so it stays in sync with Assets/*.prefab
				// and serializes as a prefab instance.
				var prefabPath = VeggaCurrency.GetCashPrefabPathForAmount( amount );
				if ( string.IsNullOrWhiteSpace( prefabPath ) )
					prefabPath = DefaultPrefabPath;
				go = GameObject.Clone( prefabPath, new Transform( position, rotation ), scene, startEnabled: false, name: "droppedcash" );
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
				renderer.Model = Model.Load( VeggaCurrency.GetCashModelPathForAmount( amount ) );

				var cashFallback = go.Components.Create<CashMoneyVeggaSystem>();
				cashFallback.Amount = amount;
				if ( !string.IsNullOrWhiteSpace( ownerSteamId ) )
					cashFallback.SetOwner( ownerSteamId );

				var pickupFallback = go.Components.Create<VeggaPickupItem>();
				pickupFallback.ItemId = VeggaCurrency.CashItemId;
				pickupFallback.Quantity = amount;
				pickupFallback.PickupDelay = DefaultPickupDelay;
				if ( droppedById != Guid.Empty )
					pickupFallback.DroppedById = droppedById;
				pickupFallback.DroppedAtTime = Time.Now;
				pickupFallback.PlayerBumpRadius = 28f;
				pickupFallback.PlayerBumpMinSpeed = 22f;
				pickupFallback.PlayerBumpStrength = 185f;
				pickupFallback.PlayerBumpUpStrength = 40f;


				// Prefer a box collider so we never end up with a "ghost" drop due to missing physics meshes.
				var colliderFallback = go.Components.Create<BoxCollider>();
				colliderFallback.IsTrigger = false;
				colliderFallback.Static = false;
				colliderFallback.Scale = VeggaCurrency.GetCashBoxColliderScaleForAmount( amount );
				colliderFallback.Friction = 1.4f;
				colliderFallback.RollingResistance = 1.1f;

				var rbFallback = go.Components.Create<Rigidbody>();
				rbFallback.MotionEnabled = true;
				rbFallback.Gravity = true;
				VeggaCurrency.ApplyCashRigidbodyTuning( rbFallback, amount );

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

			BoxCollider EnsureCashBoxColliderEnabled()
			{
				var box = go.Components.Get<BoxCollider>()
					?? go.Components.GetAll<BoxCollider>( FindMode.InDescendants ).FirstOrDefault();
				if ( box == null )
					box = go.Components.Create<BoxCollider>();
				if ( box == null || !box.IsValid() )
					return null;

				box.Enabled = true;
				box.IsTrigger = false;
				box.Static = false;
				// Respect prefab-authored size. If it's missing/invalid, fall back to a safe default.
				if ( box.Scale.x <= 0f || box.Scale.y <= 0f || box.Scale.z <= 0f )
					box.Scale = VeggaCurrency.GetCashBoxColliderScaleForAmount( amount );
				box.Friction ??= 1.4f;
				box.RollingResistance ??= 1.1f;

				EnforceSingleBoxCollider( box );
				foreach ( var mc in SelfAndDescendants<ModelCollider>() )
				{
					if ( mc == null || !mc.IsValid() ) continue;
					mc.Enabled = false;
				}
				return box;
			}

			void EnforceSingleRigidbody( Rigidbody keep )
			{
				foreach ( var other in SelfAndDescendants<Rigidbody>() )
				{
					if ( other == null || !other.IsValid() ) continue;
					if ( keep != null && other == keep ) continue;
					other.MotionEnabled = false;
					other.Enabled = false;
				}
			}

			void EnforceSingleBoxCollider( BoxCollider keep )
			{
				foreach ( var other in SelfAndDescendants<BoxCollider>() )
				{
					if ( other == null || !other.IsValid() ) continue;
					if ( keep != null && other == keep ) continue;
					other.Enabled = false;
				}
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
			else
			{
				cash = go.Components.Create<CashMoneyVeggaSystem>();
				cash.Amount = amount;
				if ( !string.IsNullOrWhiteSpace( ownerSteamId ) )
					cash.SetOwner( ownerSteamId );
			}


			var pickup = go.Components.Get<VeggaPickupItem>()
				?? go.Components.GetAll<VeggaPickupItem>( FindMode.InDescendants ).FirstOrDefault();
			if ( pickup == null )
				pickup = go.Components.Create<VeggaPickupItem>();
			pickup.ItemId = VeggaCurrency.CashItemId;
			pickup.Quantity = amount;
			if ( pickup.PickupDelay <= 0 )
				pickup.PickupDelay = DefaultPickupDelay;
			if ( droppedById != Guid.Empty )
				pickup.DroppedById = droppedById;
			pickup.DroppedAtTime = Time.Now;
			// Make cash gently pushable by walking into it.
			// Large cash uses a huge box collider, so we need a larger bump radius
			// (distance is measured from player position to item origin).
			bool isMassiveCash = amount >= VeggaCurrency.CashBoxAmount;
			pickup.PlayerBumpRadius = isMassiveCash ? 72f : 34f;
			pickup.PlayerBumpMinSpeed = 22f;
			pickup.PlayerBumpStrength = 185f;
			pickup.PlayerBumpUpStrength = 40f;

			// Enforce stable physics settings even if the prefab gets edited later.
			var prop = go.Components.Get<Prop>()
				?? go.Components.GetAll<Prop>( FindMode.InDescendants ).FirstOrDefault();
			if ( prop != null && prop.IsValid() )
				prop.IsStatic = false;

			var rb = go.Components.Get<Rigidbody>()
				?? go.Components.GetAll<Rigidbody>( FindMode.InDescendants ).FirstOrDefault();
			if ( rb == null )
				rb = go.Components.Create<Rigidbody>();
			if ( rb != null && rb.IsValid() )
			{
				rb.Enabled = true;
				rb.MotionEnabled = true;
				rb.Gravity = true;
				VeggaCurrency.ApplyCashRigidbodyTuning( rb, amount );
			}
			EnforceSingleRigidbody( rb );

			// Collider policy: always ensure a box collider exists for cash.
			EnsureCashBoxColliderEnabled();

			// Enable after configuration to avoid one-frame defaults.
			go.Enabled = true;
			go.WorldRotation = rotation;
			EnsureCashBoxColliderEnabled();
			EnforceSingleRigidbody( rb );

			// Some prefab/component lifecycles can apply defaults on enable.
			// Re-apply after enabling so the drop amount is always authoritative.
			if ( rb != null && rb.IsValid() )
			{
				rb.Enabled = true;
				rb.MotionEnabled = true;
				rb.Gravity = true;
				VeggaCurrency.ApplyCashRigidbodyTuning( rb, amount );
			}
			foreach ( var polish in SelfAndDescendants<VeggaDropImpactPolish>() )
				polish?.RefreshBaseline();
			EnforceSingleRigidbody( rb );
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
