using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Server-side persistence for dropped world pickups.
/// Persists drops in FileSystem.Data and respawns them on restart.
/// Drops expire after a fixed TTL.
/// </summary>
public static class WorldDropPersistence
{
	private const string FilePath = "world/drops.json";
	private const float DefaultTtlSeconds = 30f * 60f;

	private static SaveData _data;
	private static bool _loaded;
	private static bool _spawnedThisSession;
	private static RealTimeSince _sincePrune;

	class SaveData
	{
		public Dictionary<Guid, DropSave> Drops { get; set; } = new();
	}

	public class DropSave
	{
		public Guid PersistId { get; set; }
		public Guid DroppedById { get; set; }
		public int ItemId { get; set; }
		public int Count { get; set; }
		public int Durability { get; set; }
		public Vector3 Position { get; set; }
		public Rotation Rotation { get; set; }
		public Vector3 Velocity { get; set; }
		public string PrefabPath { get; set; }
		public long DroppedAtUtcTicks { get; set; }
	}

	static void EnsureLoaded()
	{
		if ( _loaded ) return;
		_loaded = true;

		try
		{
			if ( FileSystem.Data.FileExists( FilePath ) )
			{
				_data = FileSystem.Data.ReadJson<SaveData>( FilePath ) ?? new SaveData();
				return;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[WorldDropPersistence] Failed to read {FilePath}: {e.Message}" );
		}

		_data = new SaveData();
	}

	static void SaveNow()
	{
		try
		{
			FileSystem.Data.CreateDirectory( "world" );
			FileSystem.Data.WriteJson( FilePath, _data );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[WorldDropPersistence] Failed to write {FilePath}: {e.Message}" );
		}
	}

	static bool IsExpired( DropSave save, DateTime utcNow, float ttlSeconds )
	{
		if ( save == null ) return true;
		if ( save.DroppedAtUtcTicks <= 0 ) return false;
		var droppedAt = new DateTime( save.DroppedAtUtcTicks, DateTimeKind.Utc );
		return (utcNow - droppedAt).TotalSeconds > ttlSeconds;
	}

	public static Guid RegisterDrop( GameObject go, int itemId, int count, int durability, string prefabPath, Vector3 velocity, Guid? persistIdOverride = null )
		=> RegisterDrop( go, itemId, count, durability, prefabPath, velocity, Guid.Empty, 0, persistIdOverride );

	public static Guid RegisterDrop( GameObject go, int itemId, int count, int durability, string prefabPath, Vector3 velocity, Guid droppedById, long droppedAtUtcTicks, Guid? persistIdOverride = null )
	{
		if ( !Networking.IsHost ) return Guid.Empty;
		if ( go == null || !go.IsValid() ) return Guid.Empty;
		if ( itemId <= 0 || count <= 0 ) return Guid.Empty;

		EnsureLoaded();

		var persistId = persistIdOverride ?? Guid.NewGuid();
		long utcTicks = droppedAtUtcTicks > 0 ? droppedAtUtcTicks : DateTime.UtcNow.Ticks;

		var save = new DropSave
		{
			PersistId = persistId,
			DroppedById = droppedById,
			ItemId = itemId,
			Count = count,
			Durability = durability,
			Position = go.WorldPosition,
			Rotation = go.WorldRotation,
			Velocity = velocity,
			PrefabPath = prefabPath,
			DroppedAtUtcTicks = utcTicks
		};

		_data.Drops[persistId] = save;
		SaveNow();
		return persistId;
	}

	public static void UnregisterDrop( Guid persistId )
	{
		if ( !Networking.IsHost ) return;
		if ( persistId == Guid.Empty ) return;
		EnsureLoaded();
		if ( _data.Drops.Remove( persistId ) )
			SaveNow();
	}

	public static void SpawnSavedDropsOnce( Scene scene, float ttlSeconds = DefaultTtlSeconds )
	{
		if ( !Networking.IsHost ) return;
		if ( _spawnedThisSession ) return;
		_spawnedThisSession = true;

		EnsureLoaded();
		if ( scene == null ) scene = Game.ActiveScene;
		if ( scene == null ) return;

		var utcNow = DateTime.UtcNow;
		var toRemove = new List<Guid>();

		foreach ( var kvp in _data.Drops.ToArray() )
		{
			var id = kvp.Key;
			var save = kvp.Value;
			if ( save == null ) { toRemove.Add( id ); continue; }
			if ( IsExpired( save, utcNow, ttlSeconds ) ) { toRemove.Add( id ); continue; }

			var def = VeggaItemRegistry.Get( save.ItemId );
			if ( def == null ) { toRemove.Add( id ); continue; }

			GameObject go = null;
			try
			{
				// Prefer prefab if provided; fall back to def.PrefabPath.
				var prefab = string.IsNullOrWhiteSpace( save.PrefabPath ) ? def.PrefabPath : save.PrefabPath;
				if ( !string.IsNullOrWhiteSpace( prefab ) )
				{
					go = GameObject.Clone( prefab, new Transform( save.Position, save.Rotation ), scene, startEnabled: true, name: $"DroppedItem_{save.ItemId}" );
				}
			}
			catch
			{
				go = null;
			}

			if ( go == null )
			{
				// If we can’t respawn, drop the save entry so it doesn’t block forever.
				toRemove.Add( id );
				continue;
			}

			// Ensure pickup has the persist id so we can remove it on pickup.
			var rootPickup = go.Components.Get<VeggaPickupItem>();
			if ( rootPickup != null && rootPickup.IsValid() )
			{
				rootPickup.ItemId = save.ItemId;
				rootPickup.Quantity = save.Count;
				rootPickup.Durability = save.Durability;
				rootPickup.PersistId = save.PersistId;
				rootPickup.DroppedById = save.DroppedById;
				rootPickup.DroppedAtUtcTicks = save.DroppedAtUtcTicks;
			}
			foreach ( var pickup in go.Components.GetAll<VeggaPickupItem>( FindMode.InDescendants ) )
			{
				if ( pickup == null || !pickup.IsValid() )
					continue;
				pickup.ItemId = save.ItemId;
				pickup.Quantity = save.Count;
				pickup.Durability = save.Durability;
				pickup.PersistId = save.PersistId;
				pickup.DroppedById = save.DroppedById;
				pickup.DroppedAtUtcTicks = save.DroppedAtUtcTicks;
			}

			VeggaOreVisuals.ApplyToWorldObject( go, save.ItemId, save.Durability );

			// Ensure physics is enabled on respawned drops (fixes hovering/floating after restart).
			var prop = go.Components.Get<Prop>()
				?? go.Components.GetAll<Prop>( FindMode.InDescendants ).FirstOrDefault();
			if ( prop != null && prop.IsValid() )
				prop.IsStatic = false;

			var modelCollider = go.Components.Get<ModelCollider>()
				?? go.Components.GetAll<ModelCollider>( FindMode.InDescendants ).FirstOrDefault();
			if ( modelCollider != null && modelCollider.IsValid() )
			{
				var renderer = go.Components.Get<ModelRenderer>()
					?? go.Components.GetAll<ModelRenderer>( FindMode.InDescendants ).FirstOrDefault();
				if ( renderer != null && renderer.IsValid() && renderer.Model != null )
					modelCollider.Model = renderer.Model;

				modelCollider.Enabled = true;
				modelCollider.IsTrigger = false;
				modelCollider.Static = false;
			}

			// Some item models might not provide a usable physics mesh; ensure a simple collider exists.
			bool isGobletMould = save.ItemId == VeggaItemIds.MouldGoblet;
			bool needsFallbackCollider = VeggaOreVisuals.IsOre( save.ItemId ) || isGobletMould;
			if ( needsFallbackCollider )
			{
				if ( isGobletMould )
				{
					var sphere = go.Components.Get<SphereCollider>()
						?? go.Components.GetAll<SphereCollider>( FindMode.InDescendants ).FirstOrDefault();
					if ( sphere != null && sphere.IsValid() )
						sphere.Enabled = false;

					if ( modelCollider != null && modelCollider.IsValid() )
						modelCollider.Enabled = false;

					var box = go.Components.Get<BoxCollider>()
						?? go.Components.GetAll<BoxCollider>( FindMode.InDescendants ).FirstOrDefault();
					if ( box == null )
						box = go.Components.Create<BoxCollider>();
					if ( box != null && box.IsValid() )
					{
						box.Enabled = true;
						box.IsTrigger = false;
						box.Scale = new Vector3( 8f, 8f, 4f );
					}
				}
				else
				{
					var sphere = go.Components.Get<SphereCollider>()
						?? go.Components.GetAll<SphereCollider>( FindMode.InDescendants ).FirstOrDefault();
					if ( sphere == null )
						sphere = go.Components.Create<SphereCollider>();
					if ( sphere != null && sphere.IsValid() )
					{
						sphere.Enabled = true;
						sphere.IsTrigger = false;
						if ( sphere.Radius <= 0 || sphere.Radius > 8f ) sphere.Radius = 6f;
					}
				}
			}

			var rb = go.Components.Get<Rigidbody>()
				?? go.Components.GetAll<Rigidbody>( FindMode.InDescendants ).FirstOrDefault();
			if ( rb == null )
			{
				rb = go.Components.Create<Rigidbody>();
			}
			if ( rb != null && rb.IsValid() )
			{
				rb.Enabled = true;
				rb.MotionEnabled = true;
				rb.Gravity = true;
				if ( save.ItemId == VeggaItemIds.MouldGoblet )
				{
					rb.MassOverride = 25;
					rb.LinearDamping = 0.35f;
					rb.AngularDamping = 4f;
				}
				rb.Velocity = save.Velocity;
			}
		}

		if ( toRemove.Count > 0 )
		{
			foreach ( var id in toRemove )
				_data.Drops.Remove( id );
			SaveNow();
		}
	}

	public static void PruneExpiredWorldDrops( Scene scene, float ttlSeconds = DefaultTtlSeconds )
	{
		if ( !Networking.IsHost ) return;
		if ( _sincePrune < 15f ) return;
		_sincePrune = 0;

		EnsureLoaded();
		var utcNow = DateTime.UtcNow;
		var expiredIds = _data.Drops
			.Where( kvp => IsExpired( kvp.Value, utcNow, ttlSeconds ) )
			.Select( kvp => kvp.Key )
			.ToList();

		if ( expiredIds.Count == 0 ) return;

		if ( scene == null ) scene = Game.ActiveScene;
		if ( scene != null )
		{
			foreach ( var pickup in scene.GetAllComponents<VeggaPickupItem>() )
			{
				if ( pickup == null || !pickup.IsValid() ) continue;
				if ( pickup.PersistId == Guid.Empty ) continue;
				if ( !expiredIds.Contains( pickup.PersistId ) ) continue;
				pickup.GameObject?.Destroy();
			}
		}

		foreach ( var id in expiredIds )
			_data.Drops.Remove( id );
		SaveNow();
	}
}
