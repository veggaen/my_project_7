using Sandbox;
using System.Collections.Generic;

namespace Sandbox.UI;

/// <summary>
/// Global, client-only flag + simple persistence for HUD layout edit mode.
/// Now uses a preset-based system for easier positioning.
/// </summary>
public static class VeggaHudLayoutState
{
	/// <summary>
	/// True when the HUD layout editor mode is active on this client.
	/// </summary>
	public static bool IsActive { get; private set; } = false;

	/// <summary>
	/// Preset keys for common HUD elements.
	/// </summary>
	public const string KeyXpBar = "xp_bar";
	public const string KeyPlayerHud = "player_hud";
	public const string KeyMinimap = "minimap";
	public const string KeyChat = "chat";
	public const string KeyInventory = "inventory";

	/// <summary>
	/// Available screen positions.
	/// </summary>
	public enum ScreenPosition
	{
		TopLeft,
		TopCenter,
		TopRight,
		MiddleLeft,
		MiddleCenter,
		MiddleRight,
		BottomLeft,
		BottomCenter,
		BottomRight,
		Custom // For custom X/Y offsets
	}

	/// <summary>
	/// Preset position configurations.
	/// </summary>
	public static readonly Dictionary<ScreenPosition, Vector2> PresetPositions = new()
	{
		{ ScreenPosition.TopLeft, new Vector2( 0.02f, 0.02f ) },
		{ ScreenPosition.TopCenter, new Vector2( 0.5f, 0.02f ) },
		{ ScreenPosition.TopRight, new Vector2( 0.98f, 0.02f ) },
		{ ScreenPosition.MiddleLeft, new Vector2( 0.02f, 0.5f ) },
		{ ScreenPosition.MiddleCenter, new Vector2( 0.5f, 0.5f ) },
		{ ScreenPosition.MiddleRight, new Vector2( 0.98f, 0.5f ) },
		{ ScreenPosition.BottomLeft, new Vector2( 0.02f, 0.98f ) },
		{ ScreenPosition.BottomCenter, new Vector2( 0.5f, 0.98f ) },
		{ ScreenPosition.BottomRight, new Vector2( 0.98f, 0.98f ) }
	};

	class LayoutEntry
	{
		public float X { get; set; }
		public float Y { get; set; }
		public int Preset { get; set; } = -1; // -1 = custom, 0-8 = ScreenPosition enum
		public float Scale { get; set; } = 1f;
	}

	class LayoutSaveData
	{
		public Dictionary<string, LayoutEntry> Elements { get; set; } = new();
	}

	static bool _loaded;
	static LayoutSaveData _data = new();
	const string LayoutFile = "hud_layout.json";

	/// <summary>
	/// Currently selected element for editing.
	/// </summary>
	public static string SelectedElement { get; set; } = null;

	/// <summary>
	/// Set layout mode for this client.
	/// </summary>
	public static void SetActive( bool active )
	{
		IsActive = active;
		if ( !active )
		{
			SelectedElement = null;
		}
		Log.Info( $"[VeggaHudLayoutState] Layout mode set to: {IsActive}" );
	}

	static void EnsureLoaded()
	{
		if ( _loaded )
			return;

		_loaded = true;

		if ( FileSystem.Data.FileExists( LayoutFile ) )
		{
			var loaded = FileSystem.Data.ReadJson<LayoutSaveData>( LayoutFile );
			if ( loaded != null )
			{
				_data = loaded;
			}
		}
	}

	/// <summary>
	/// Get a normalized position (0–1 in X/Y) for a HUD element, or return the given default.
	/// </summary>
	public static Vector2 GetPosition( string key, Vector2 @default )
	{
		EnsureLoaded();

		if ( _data.Elements != null && _data.Elements.TryGetValue( key, out var entry ) )
		{
			return new Vector2( entry.X, entry.Y );
		}

		return @default;
	}

	/// <summary>
	/// Get the preset position for an element.
	/// </summary>
	public static ScreenPosition GetPreset( string key )
	{
		EnsureLoaded();

		if ( _data.Elements != null && _data.Elements.TryGetValue( key, out var entry ) )
		{
			if ( entry.Preset >= 0 && entry.Preset <= 8 )
			{
				return (ScreenPosition)entry.Preset;
			}
		}

		return ScreenPosition.Custom;
	}

	/// <summary>
	/// Get the scale for an element.
	/// </summary>
	public static float GetScale( string key, float defaultScale = 1f )
	{
		EnsureLoaded();

		if ( _data.Elements != null && _data.Elements.TryGetValue( key, out var entry ) )
		{
			return entry.Scale > 0 ? entry.Scale : defaultScale;
		}

		return defaultScale;
	}

	/// <summary>
	/// Store a normalized position (0–1 in X/Y) for a HUD element and persist it.
	/// </summary>
	public static void SetPosition( string key, Vector2 pos )
	{
		EnsureLoaded();

		if ( _data.Elements == null )
		{
			_data.Elements = new Dictionary<string, LayoutEntry>();
		}

		if ( !_data.Elements.TryGetValue( key, out var entry ) )
		{
			entry = new LayoutEntry();
			_data.Elements[key] = entry;
		}

		entry.X = pos.x;
		entry.Y = pos.y;
		entry.Preset = -1; // Custom position

		FileSystem.Data.WriteJson( LayoutFile, _data );
	}

	/// <summary>
	/// Set element to a preset position.
	/// </summary>
	public static void SetPreset( string key, ScreenPosition preset )
	{
		EnsureLoaded();

		if ( _data.Elements == null )
		{
			_data.Elements = new Dictionary<string, LayoutEntry>();
		}

		if ( !_data.Elements.TryGetValue( key, out var entry ) )
		{
			entry = new LayoutEntry();
			_data.Elements[key] = entry;
		}

		if ( PresetPositions.TryGetValue( preset, out var pos ) )
		{
			entry.X = pos.x;
			entry.Y = pos.y;
		}
		entry.Preset = (int)preset;

		FileSystem.Data.WriteJson( LayoutFile, _data );
		Log.Info( $"[HUD Layout] Set {key} to preset: {preset}" );
	}

	/// <summary>
	/// Set the scale for an element.
	/// </summary>
	public static void SetScale( string key, float scale )
	{
		EnsureLoaded();

		if ( _data.Elements == null )
		{
			_data.Elements = new Dictionary<string, LayoutEntry>();
		}

		if ( !_data.Elements.TryGetValue( key, out var entry ) )
		{
			entry = new LayoutEntry();
			_data.Elements[key] = entry;
		}

		entry.Scale = scale;
		FileSystem.Data.WriteJson( LayoutFile, _data );
		Log.Info( $"[HUD Layout] Set {key} scale to: {scale}" );
	}

	/// <summary>
	/// Nudge an element by pixel offset.
	/// </summary>
	public static void NudgePosition( string key, Vector2 offset )
	{
		EnsureLoaded();

		var current = GetPosition( key, new Vector2( 0.5f, 0.5f ) );
		// Convert pixel offset to normalized (assuming 1920x1080 base)
		var normalized = new Vector2( offset.x / 1920f, offset.y / 1080f );
		SetPosition( key, current + normalized );
	}

	/// <summary>
	/// Get list of all configurable HUD element keys.
	/// </summary>
	public static List<string> GetAllElementKeys()
	{
		return new List<string>
		{
			KeyPlayerHud,
			KeyXpBar,
			KeyChat,
			KeyMinimap,
			KeyInventory
		};
	}

	/// <summary>
	/// Get friendly name for an element key.
	/// </summary>
	public static string GetElementName( string key )
	{
		return key switch
		{
			KeyPlayerHud => "Player Stats (HP/Armor)",
			KeyXpBar => "XP Bar",
			KeyChat => "Chat Box",
			KeyMinimap => "Minimap",
			KeyInventory => "Inventory",
			_ => key
		};
	}

	/// <summary>
	/// Reset all saved HUD layout back to defaults.
	/// </summary>
	public static void ResetAll()
	{
		_data = new LayoutSaveData();
		_loaded = true;

		if ( FileSystem.Data.FileExists( LayoutFile ) )
		{
			FileSystem.Data.DeleteFile( LayoutFile );
		}
	}
}


