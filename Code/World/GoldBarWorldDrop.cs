using Sandbox;
using System;
using System.Linq;

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
			if ( go == null || !go.IsValid() )
				return null;

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
		catch
		{
			// Prefab-first policy: never fall back to a non-prefab drop.
			return null;
		}
	}
}
