using Sandbox;
using System;

namespace Sandbox;

public static class GoldBarWorldDrop
{
	public const int GoldBarItemId = 100;
	public const string PrefabPath = "goldbar_200g.prefab";

	public static GameObject Spawn( Scene scene, Vector3 position, Rotation rotation, Guid droppedById )
	{
		if ( scene == null )
			throw new ArgumentNullException( nameof( scene ) );

		// Clone the prefab instance so __Prefab is preserved.
		try
		{
			var go = GameObject.Clone( PrefabPath, new Transform( position, rotation ), scene, startEnabled: false, name: "goldbar_200g" );
			if ( go != null && go.IsValid() )
			{
				// Ensure pickup metadata is correct.
				var pickup = go.Components.Get<VeggaPickupItem>()
					?? go.Components.GetAll<VeggaPickupItem>( FindMode.InDescendants ).FirstOrDefault();
				if ( pickup != null )
				{
					pickup.ItemId = GoldBarItemId;
					pickup.Quantity = 1;
					pickup.PickupDelay = 0.25f;
					pickup.DroppedById = droppedById;
					pickup.DroppedAtTime = Time.Now;
				}

				go.Enabled = true;
				return go;
			}
		}
		catch
		{
			// fall through to fallback
		}

		// Fallback: create a simple physics pickup.
		var fallback = new GameObject( true, "DroppedGoldBar" );
		fallback.WorldPosition = position;
		fallback.WorldRotation = rotation;

		var renderer = fallback.Components.Create<ModelRenderer>();
		try
		{
			renderer.Model = Model.Load( "goldbar/gold_bar.vmdl" );
		}
		catch { }

		var pickupFallback = fallback.Components.Create<VeggaPickupItem>();
		pickupFallback.ItemId = GoldBarItemId;
		pickupFallback.Quantity = 1;
		pickupFallback.PickupDelay = 0.25f;
		pickupFallback.DroppedById = droppedById;
		pickupFallback.DroppedAtTime = Time.Now;

		var rb = fallback.Components.Create<Rigidbody>();
		rb.Gravity = true;

		var collider = fallback.Components.Create<ModelCollider>();
		try { collider.Model = renderer.Model; } catch { }
		collider.IsTrigger = false;

		return fallback;
	}
}
