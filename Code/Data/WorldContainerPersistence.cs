using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Lightweight persistence for world containers (furnaces, storage boxes).
/// Stores server-authoritative contents in FileSystem.Data.
/// </summary>
public static class WorldContainerPersistence
{
	private const string FilePath = "world/containers.json";
	private static SaveData _data;
	private static bool _loaded;

	class SaveData
	{
		public Dictionary<Guid, ContainerSave> Containers { get; set; } = new();
	}

	public class ContainerSave
	{
		public string Kind { get; set; }
		public int[] ItemIds { get; set; }
		public int[] Counts { get; set; }
		public int[] Durability { get; set; }
		public float FuelSecondsRemaining { get; set; }
		public bool IsOn { get; set; }
		public float JobProgress { get; set; }
		public int OreBufferGrams { get; set; }
		public int CastBufferGrams { get; set; }
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
			Log.Warning( $"[WorldContainerPersistence] Failed to read {FilePath}: {e.Message}" );
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
			Log.Warning( $"[WorldContainerPersistence] Failed to write {FilePath}: {e.Message}" );
		}
	}

	public static bool TryLoad( Guid id, out ContainerSave save )
	{
		EnsureLoaded();
		return _data.Containers.TryGetValue( id, out save );
	}

	public static void Store( Guid id, ContainerSave save )
	{
		if ( id == Guid.Empty || save == null ) return;
		EnsureLoaded();
		_data.Containers[id] = save;
		SaveNow();
	}
}
