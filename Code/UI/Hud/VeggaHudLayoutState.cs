using Sandbox;
using System.Collections.Generic;

namespace Sandbox.UI;

/// <summary>
/// Global, client-only flag + simple persistence for HUD layout edit mode.
/// Now uses a preset-based system for easier positioning.
/// </summary>
public static class VeggaHudLayoutState
{
	// Allows placing UI a tiny bit off-screen (requested) so users can tuck panels.
	// Kept small to avoid losing elements entirely.
	const float AllowedOffscreenPixels = 48f;
	// If a calibrated safe-area is too small, it was almost certainly mis-clicked.
	// In that case we ignore it for clamping so the HUD can still reach screen edges.
	const float MinSafeAreaSizeNormalized = 0.80f;

	/// <summary>
	/// How a HUD element should be anchored relative to its saved position.
	/// Saved X/Y is always the anchor point.
	/// </summary>
	public enum HudAnchor
	{
		TopLeft,
		TopCenter,
		TopRight,
		MiddleLeft,
		MiddleCenter,
		MiddleRight,
		BottomLeft,
		BottomCenter,
		BottomRight
	}

	// Source of truth: which anchor each module uses.
	static readonly Dictionary<string, HudAnchor> AnchorByKey = new()
	{
		// Chat should sit on the bottom edge and grow upward.
		{ KeyChat, HudAnchor.BottomLeft },
		// XP bar sits on the bottom edge and grows upward.
		{ KeyXpBar, HudAnchor.BottomCenter },
		// Hotbar sits on the bottom edge.
		{ KeyHotbar, HudAnchor.BottomCenter },
		// Player HUD sits on the bottom-left.
		{ KeyPlayerHud, HudAnchor.BottomLeft },
		// Inventory should behave OSRS-like: anchored bottom-right.
		{ KeyInventory, HudAnchor.BottomRight },
		// Minimap sits in the top-right.
		{ KeyMinimap, HudAnchor.TopRight },
		// Skills panel is a list that grows downward.
		{ KeySkillsPanel, HudAnchor.TopLeft },
	};

	// Runtime registry for modular HUD "plugins".
	// This lets modules register themselves so the layout editor can scale beyond hard-coded keys.
	static readonly Dictionary<string, string> _registeredNames = new();
	static readonly HashSet<string> _registeredKeys = new();

	/// <summary>
	/// True when the HUD layout editor mode is active on this client.
	/// </summary>
	public static bool IsActive { get; private set; } = false;

	/// <summary>
	/// Preset keys for common HUD elements.
	/// </summary>
	public const string KeyXpBar = "xp_bar";
	public const string KeyPlayerHud = "player_hud";
	public const string KeyHotbar = "hotbar";
	public const string KeyMinimap = "minimap";
	public const string KeyChat = "chat";
	public const string KeyInventory = "inventory";
	public const string KeySkillsPanel = "skills";

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
		// Presets target true edges/center; clamping in VeggaHudLayoutApply prevents panels from going off-screen.
		{ ScreenPosition.TopLeft, new Vector2( 0.0f, 0.0f ) },
		{ ScreenPosition.TopCenter, new Vector2( 0.5f, 0.0f ) },
		{ ScreenPosition.TopRight, new Vector2( 1.0f, 0.0f ) },
		{ ScreenPosition.MiddleLeft, new Vector2( 0.0f, 0.5f ) },
		{ ScreenPosition.MiddleCenter, new Vector2( 0.5f, 0.5f ) },
		{ ScreenPosition.MiddleRight, new Vector2( 1.0f, 0.5f ) },
		{ ScreenPosition.BottomLeft, new Vector2( 0.0f, 1.0f ) },
		{ ScreenPosition.BottomCenter, new Vector2( 0.5f, 1.0f ) },
		{ ScreenPosition.BottomRight, new Vector2( 1.0f, 1.0f ) }
	};

	/// <summary>
	/// Default positions per element key. Used when there is no saved layout yet.
	/// </summary>
	public static readonly Dictionary<string, Vector2> DefaultPositions = new()
	{
		// Kept away from dead-center to avoid markers/panels stacking.
		{ KeyPlayerHud, new Vector2( 0.04f, 0.96f ) },
		{ KeyXpBar, new Vector2( 0.50f, 0.96f ) },
		// Hotbar sits above the XP bar by default.
		{ KeyHotbar, new Vector2( 0.50f, 0.90f ) },
		// Chat shares the bottom-left anchor with the Player HUD, so default it higher to avoid overlap.
		{ KeyChat, new Vector2( 0.04f, 0.78f ) },
		{ KeyMinimap, new Vector2( 0.96f, 0.04f ) },
		{ KeyInventory, new Vector2( 0.96f, 0.96f ) },
		{ KeySkillsPanel, new Vector2( 0.70f, 0.10f ) },
	};

	/// <summary>
	/// Safe to call multiple times.
	/// </summary>
	public static void RegisterElement( string key, string displayName, Vector2? defaultPosition = null )
	{
		if ( string.IsNullOrWhiteSpace( key ) ) return;
		key = key.Trim();

		_registeredKeys.Add( key );

		if ( !string.IsNullOrWhiteSpace( displayName ) )
		{
			_registeredNames[key] = displayName.Trim();
		}

		if ( defaultPosition.HasValue )
		{
			DefaultPositions[key] = defaultPosition.Value;
		}
	}

	class LayoutEntry
	{
		public float X { get; set; }
		public float Y { get; set; }
		public int Preset { get; set; } = -1; // -1 = custom, 0-8 = ScreenPosition enum
		public float Scale { get; set; } = 1f;
		public int Anchor { get; set; } = -1; // -1 = use default-by-key
	}

	class LayoutSaveData
	{
		public Dictionary<string, LayoutEntry> Elements { get; set; } = new();
		public Dictionary<string, Dictionary<string, LayoutEntry>> ElementsByResolution { get; set; } = new();
		public Dictionary<string, SafeAreaEntry> SafeAreas { get; set; } = new();
	}

	class SafeAreaEntry
	{
		// Normalized 0..1 coordinates (relative to Screen.Size)
		public float MinX { get; set; } = 0f;
		public float MinY { get; set; } = 0f;
		public float MaxX { get; set; } = 1f;
		public float MaxY { get; set; } = 1f;
	}

	static bool _loaded;
	static LayoutSaveData _data = new();
	const string LayoutFile = "hud_layout.json";
	static bool _dirty;
	static float _nextAutosaveTime;

	const int MaxUndoSteps = 10;
	static readonly Stack<LayoutSaveData> _undoStack = new();
	static bool _suspendUndo;

	public static bool CanUndo => _undoStack.Count > 0;

	static LayoutSaveData CloneData( LayoutSaveData src )
	{
		if ( src == null ) return new LayoutSaveData();

		var clone = new LayoutSaveData
		{
			Elements = new Dictionary<string, LayoutEntry>(),
			ElementsByResolution = new Dictionary<string, Dictionary<string, LayoutEntry>>(),
			SafeAreas = new Dictionary<string, SafeAreaEntry>()
		};

		if ( src.Elements != null )
		{
			foreach ( var kv in src.Elements )
			{
				var e = kv.Value;
				if ( e == null ) continue;
				clone.Elements[kv.Key] = new LayoutEntry
				{
					X = e.X,
					Y = e.Y,
					Preset = e.Preset,
					Scale = e.Scale,
					Anchor = e.Anchor
				};
			}
		}

		if ( src.ElementsByResolution != null )
		{
			foreach ( var resKv in src.ElementsByResolution )
			{
				var resDict = new Dictionary<string, LayoutEntry>();
				if ( resKv.Value != null )
				{
					foreach ( var kv in resKv.Value )
					{
						var e = kv.Value;
						if ( e == null ) continue;
						resDict[kv.Key] = new LayoutEntry
						{
							X = e.X,
							Y = e.Y,
							Preset = e.Preset,
							Scale = e.Scale,
							Anchor = e.Anchor
						};
					}
				}
				clone.ElementsByResolution[resKv.Key] = resDict;
			}
		}

		if ( src.SafeAreas != null )
		{
			foreach ( var kv in src.SafeAreas )
			{
				var s = kv.Value;
				if ( s == null ) continue;
				clone.SafeAreas[kv.Key] = new SafeAreaEntry
				{
					MinX = s.MinX,
					MinY = s.MinY,
					MaxX = s.MaxX,
					MaxY = s.MaxY
				};
			}
		}

		return clone;
	}

	static void PushUndoState()
	{
		if ( _suspendUndo ) return;
		EnsureLoaded();
		_undoStack.Push( CloneData( _data ) );
		while ( _undoStack.Count > MaxUndoSteps )
		{
			// Drop oldest by rebuilding stack.
			var keep = _undoStack.ToArray();
			System.Array.Reverse( keep );
			_undoStack.Clear();
			int start = System.Math.Max( 0, keep.Length - MaxUndoSteps );
			for ( int i = keep.Length - 1; i >= start; i-- )
				_undoStack.Push( keep[i] );
			break;
		}
	}

	public static void Undo()
	{
		EnsureLoaded();
		if ( _undoStack.Count <= 0 )
			return;

		_suspendUndo = true;
		try
		{
			_data = _undoStack.Pop() ?? new LayoutSaveData();
			_loaded = true;
			_dirty = false;
			_nextAutosaveTime = 0f;
			FileSystem.Data.WriteJson( LayoutFile, _data );
		}
		finally
		{
			_suspendUndo = false;
		}
		Log.Info( "[HUD Layout] Undo" );
	}

	static string CurrentResolutionKey()
	{
		var s = Screen.Size;
		int w = (int)s.x;
		int h = (int)s.y;
		if ( w <= 0 ) w = 1920;
		if ( h <= 0 ) h = 1080;
		return $"{w}x{h}";
	}

	static Dictionary<string, LayoutEntry> GetElementsForCurrentScreen( bool create )
	{
		EnsureLoaded();
		string res = CurrentResolutionKey();

		if ( _data.ElementsByResolution == null )
		{
			if ( !create ) return null;
			_data.ElementsByResolution = new Dictionary<string, Dictionary<string, LayoutEntry>>();
		}

		if ( _data.ElementsByResolution.TryGetValue( res, out var dict ) && dict != null )
			return dict;

		if ( !create )
			return null;

		dict = new Dictionary<string, LayoutEntry>();
		_data.ElementsByResolution[res] = dict;
		return dict;
	}

	static bool TryGetEntry( string key, out LayoutEntry entry )
	{
		entry = null;
		if ( string.IsNullOrWhiteSpace( key ) ) return false;
		EnsureLoaded();

		var perScreen = GetElementsForCurrentScreen( create: false );
		if ( perScreen != null && perScreen.TryGetValue( key, out entry ) && entry != null )
			return true;

		if ( _data.Elements != null && _data.Elements.TryGetValue( key, out entry ) && entry != null )
			return true;

		entry = null;
		return false;
	}

	static LayoutEntry GetOrCreateEntryForWrite( string key )
	{
		EnsureLoaded();
		if ( string.IsNullOrWhiteSpace( key ) )
			return null;

		// While editing, save per-screen so each resolution/monitor can have its own layout.
		var dict = IsActive ? GetElementsForCurrentScreen( create: true ) : null;
		if ( dict != null )
		{
			if ( !dict.TryGetValue( key, out var entry ) || entry == null )
			{
				entry = new LayoutEntry();
				dict[key] = entry;
			}
			return entry;
		}

		if ( _data.Elements == null )
			_data.Elements = new Dictionary<string, LayoutEntry>();

		if ( !_data.Elements.TryGetValue( key, out var globalEntry ) || globalEntry == null )
		{
			globalEntry = new LayoutEntry();
			_data.Elements[key] = globalEntry;
		}
		return globalEntry;
	}

	public static (Vector2 min, Vector2 max) GetSafeAreaNormalized()
	{
		EnsureLoaded();
		string key = CurrentResolutionKey();

		if ( _data.SafeAreas != null && _data.SafeAreas.TryGetValue( key, out var entry ) && entry != null )
		{
			var min = new Vector2( entry.MinX, entry.MinY );
			var max = new Vector2( entry.MaxX, entry.MaxY );
			min.x = min.x.Clamp( 0f, 1f );
			min.y = min.y.Clamp( 0f, 1f );
			max.x = max.x.Clamp( 0f, 1f );
			max.y = max.y.Clamp( 0f, 1f );
			// Ensure min <= max
			var fixedMin = new Vector2( System.Math.Min( min.x, max.x ), System.Math.Min( min.y, max.y ) );
			var fixedMax = new Vector2( System.Math.Max( min.x, max.x ), System.Math.Max( min.y, max.y ) );
			return (fixedMin, fixedMax);
		}

		return (new Vector2( 0f, 0f ), new Vector2( 1f, 1f ));
	}

	static bool IsSafeAreaValid( Vector2 min, Vector2 max )
	{
		var size = max - min;
		return size.x >= MinSafeAreaSizeNormalized && size.y >= MinSafeAreaSizeNormalized;
	}

	/// <summary>
	/// True if there is a safe-area entry for this resolution (even if it's invalid).
	/// </summary>

	public static bool HasSafeAreaForCurrentScreen()
	{
		EnsureLoaded();
		string key = CurrentResolutionKey();
		return _data.SafeAreas != null && _data.SafeAreas.ContainsKey( key );
	}

	/// <summary>
	/// True if the current screen safe-area is present and looks sane.
	/// </summary>
	public static bool IsSafeAreaValidForCurrentScreen()
	{
		if ( !HasSafeAreaForCurrentScreen() )
			return false;

		var (min, max) = GetSafeAreaNormalized();
		return IsSafeAreaValid( min, max );
	}

	/// <summary>
	/// Safe-area used for clamping HUD elements. Falls back to full screen if calibration looks invalid.
	/// </summary>
	public static (Vector2 min, Vector2 max) GetSafeAreaNormalizedForClamping()
	{
		// Safe-area calibration UI was removed. Older saved safe-areas can cause an apparent
		// "invisible border" where elements stop short of the screen edge (eg. top-right).
		// Until calibration is reintroduced, clamp against the full screen.
		return (new Vector2( 0f, 0f ), new Vector2( 1f, 1f ));
	}

	public static void SetSafeAreaNormalized( Vector2 topLeft, Vector2 bottomRight )
	{
		EnsureLoaded();
		string key = CurrentResolutionKey();

		if ( _data.SafeAreas == null )
			_data.SafeAreas = new Dictionary<string, SafeAreaEntry>();

		topLeft.x = topLeft.x.Clamp( 0f, 1f );
		topLeft.y = topLeft.y.Clamp( 0f, 1f );
		bottomRight.x = bottomRight.x.Clamp( 0f, 1f );
		bottomRight.y = bottomRight.y.Clamp( 0f, 1f );

		var min = new Vector2( System.Math.Min( topLeft.x, bottomRight.x ), System.Math.Min( topLeft.y, bottomRight.y ) );
		var max = new Vector2( System.Math.Max( topLeft.x, bottomRight.x ), System.Math.Max( topLeft.y, bottomRight.y ) );

		// Keep a minimum size so it can't collapse to nothing.
		min.x = min.x.Clamp( 0f, 0.98f );
		min.y = min.y.Clamp( 0f, 0.98f );
		max.x = max.x.Clamp( 0.02f, 1f );
		max.y = max.y.Clamp( 0.02f, 1f );

		_data.SafeAreas[key] = new SafeAreaEntry
		{
			MinX = min.x,
			MinY = min.y,
			MaxX = max.x,
			MaxY = max.y
		};

		FileSystem.Data.WriteJson( LayoutFile, _data );
		Log.Info( $"[HUD SafeArea] Set for {key}: min={min} max={max}" );
	}

	public static void ClearSafeAreaForCurrentScreen()
	{
		EnsureLoaded();
		string key = CurrentResolutionKey();
		if ( _data.SafeAreas == null )
			return;

		if ( _data.SafeAreas.Remove( key ) )
		{
			FileSystem.Data.WriteJson( LayoutFile, _data );
			Log.Info( $"[HUD SafeArea] Cleared for {key}" );
		}
	}

	/// <summary>
	/// Currently selected element for editing.
	/// </summary>
	public static string SelectedElement { get; set; } = null;

	/// <summary>
	/// When enabled, we render the "blue box" HUD surfaces.
	/// This is intended to become the new visual HUD shell.
	/// </summary>
	public static bool BoxHudEnabled { get; set; } = false;

	/// <summary>
	/// When enabled (and layout mode is active), elements can be dragged.
	/// </summary>
	public static bool DragModeEnabled { get; set; } = true;

	/// <summary>
	/// Set layout mode for this client.
	/// </summary>
	public static void SetActive( bool active )
	{
		IsActive = active;
		if ( !active )
		{
			// Flush any debounced writes so "Save & Exit" is reliable.
			if ( _dirty )
			{
				FileSystem.Data.WriteJson( LayoutFile, _data );
				_dirty = false;
				_nextAutosaveTime = 0f;
			}
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
		if ( TryGetEntry( key, out var entry ) )
			return new Vector2( entry.X, entry.Y );

		return @default;
	}

	/// <summary>
	/// Get a normalized position (0–1 in X/Y) for a HUD element using the built-in defaults.
	/// </summary>
	public static Vector2 GetPosition( string key )
	{
		return GetPosition( key, GetDefaultPosition( key ) );
	}

	/// <summary>
	/// Get the default normalized position for an element key.
	/// </summary>
	public static Vector2 GetDefaultPosition( string key )
	{
		if ( !string.IsNullOrWhiteSpace( key ) && DefaultPositions.TryGetValue( key, out var pos ) )
			return pos;

		return PresetPositions[ScreenPosition.MiddleCenter];
	}

	public static HudAnchor GetAnchor( string key )
	{
		EnsureLoaded();
		if ( TryGetEntry( key, out var entry ) )
			if ( entry.Anchor >= 0 && entry.Anchor <= (int)HudAnchor.BottomRight )
				return (HudAnchor)entry.Anchor;

		if ( !string.IsNullOrWhiteSpace( key ) && AnchorByKey.TryGetValue( key, out var a ) )
			return a;

		return HudAnchor.MiddleCenter;
	}

	static HudAnchor AnchorFromPreset( ScreenPosition preset )
	{
		return preset switch
		{
			ScreenPosition.TopLeft => HudAnchor.TopLeft,
			ScreenPosition.TopCenter => HudAnchor.TopCenter,
			ScreenPosition.TopRight => HudAnchor.TopRight,
			ScreenPosition.MiddleLeft => HudAnchor.MiddleLeft,
			ScreenPosition.MiddleCenter => HudAnchor.MiddleCenter,
			ScreenPosition.MiddleRight => HudAnchor.MiddleRight,
			ScreenPosition.BottomLeft => HudAnchor.BottomLeft,
			ScreenPosition.BottomCenter => HudAnchor.BottomCenter,
			ScreenPosition.BottomRight => HudAnchor.BottomRight,
			_ => GetAnchor( null )
		};
	}

	/// <summary>
	/// Convert a preset (which is defined as 0..1 within the usable area) into a screen-normalized position.
	/// If a safe-area is calibrated, presets target the safe-area (not the full screen).
	/// </summary>
	public static Vector2 GetPresetPositionForCurrentScreen( ScreenPosition preset )
	{
		if ( !PresetPositions.TryGetValue( preset, out var presetN ) )
			presetN = PresetPositions[ScreenPosition.MiddleCenter];

		var (safeMinN, safeMaxN) = GetSafeAreaNormalized();
		var safeSize = safeMaxN - safeMinN;
		if ( safeSize.x <= 0f ) safeSize.x = 1f;
		if ( safeSize.y <= 0f ) safeSize.y = 1f;

		var screenN = new Vector2(
			safeMinN.x + (presetN.x * safeSize.x),
			safeMinN.y + (presetN.y * safeSize.y)
		);

		return ClampToAllowedRange( screenN );
	}

	/// <summary>
	/// Get the preset position for an element.
	/// </summary>
	public static ScreenPosition GetPreset( string key )
	{
		EnsureLoaded();
		if ( TryGetEntry( key, out var entry ) )
			if ( entry.Preset >= 0 && entry.Preset <= 8 )
				return (ScreenPosition)entry.Preset;

		return ScreenPosition.Custom;
	}

	/// <summary>
	/// Get the scale for an element.
	/// </summary>
	public static float GetScale( string key, float defaultScale = 1f )
	{
		EnsureLoaded();
		if ( TryGetEntry( key, out var entry ) )
			return entry.Scale > 0 ? entry.Scale : defaultScale;

		return defaultScale;
	}

	/// <summary>
	/// Store a normalized position (0–1 in X/Y) for a HUD element and persist it.
	/// </summary>
	public static void SetPosition( string key, Vector2 pos )
	{
		SetPositionInternal( key, pos, pushUndo: true );
	}

	internal static void SetPositionNoUndo( string key, Vector2 pos )
	{
		SetPositionInternal( key, pos, pushUndo: false );
	}

	static void SetPositionInternal( string key, Vector2 pos, bool pushUndo )
	{
		EnsureLoaded();
		pos = ClampToAllowedRange( pos );
		if ( pushUndo ) PushUndoState();

		var entry = GetOrCreateEntryForWrite( key );
		if ( entry == null ) return;

		if ( entry.Anchor < 0 )
		{
			entry.Anchor = (int)(AnchorByKey.TryGetValue( key, out var a ) ? a : HudAnchor.MiddleCenter);
		}

		entry.X = pos.x;
		entry.Y = pos.y;
		entry.Preset = -1; // Custom position

		FileSystem.Data.WriteJson( LayoutFile, _data );
	}

	/// <summary>
	/// Update a position without changing preset/anchor. Used by the layout applier
	/// to keep guide markers aligned when clamping to screen bounds.
	/// Debounced to avoid spamming disk writes.
	/// </summary>
	internal static void UpdatePositionClamped( string key, Vector2 pos )
	{
		EnsureLoaded();
		pos = ClampToAllowedRange( pos );

		var entry = GetOrCreateEntryForWrite( key );
		if ( entry == null ) return;

		if ( entry.Anchor < 0 )
			entry.Anchor = (int)(AnchorByKey.TryGetValue( key, out var a ) ? a : HudAnchor.MiddleCenter);

		entry.X = pos.x;
		entry.Y = pos.y;
		_dirty = true;

		// Debounce writes (layout mode can call this every frame when clamped).
		float now = Time.Now;
		if ( now >= _nextAutosaveTime )
		{
			_nextAutosaveTime = now + 0.25f;
			FileSystem.Data.WriteJson( LayoutFile, _data );
			_dirty = false;
		}
	}

	static Vector2 ClampToAllowedRange( Vector2 pos )
	{
		var screen = Screen.Size;
		float sx = screen.x > 0 ? screen.x : 1920f;
		float sy = screen.y > 0 ? screen.y : 1080f;

		float marginX = AllowedOffscreenPixels / sx;
		float marginY = AllowedOffscreenPixels / sy;

		pos.x = pos.x.Clamp( -marginX, 1f + marginX );
		pos.y = pos.y.Clamp( -marginY, 1f + marginY );
		return pos;
	}

	/// <summary>
	/// Set element to a preset position.
	/// </summary>
	public static void SetPreset( string key, ScreenPosition preset )
	{
		EnsureLoaded();
		PushUndoState();

		var entry = GetOrCreateEntryForWrite( key );
		if ( entry == null ) return;

		// When selecting a preset, make the element's anchor match the preset.
		// This makes presets behave intuitively for any element (eg. inventory top-left).
		entry.Anchor = (int)AnchorFromPreset( preset );

		// Presets are defined in full-screen normalized space.
		// Calibrated safe-area still applies via VeggaHudLayoutApply clamping.
		if ( !PresetPositions.TryGetValue( preset, out var pos ) )
			pos = PresetPositions[ScreenPosition.MiddleCenter];
		pos = ClampToAllowedRange( pos );
		entry.X = pos.x;
		entry.Y = pos.y;
		entry.Preset = (int)preset;

		FileSystem.Data.WriteJson( LayoutFile, _data );
		Log.Info( $"[HUD Layout] Set {key} to preset: {preset}" );
	}

	/// <summary>
	/// Wipe all saved HUD layout data (positions, per-resolution overrides, and safe-areas).
	/// This is useful if a bad calibration or old file format causes weird offsets.
	/// </summary>
	public static void WipeAllSavedLayoutData()
	{
		EnsureLoaded();
		PushUndoState();
		_data = new LayoutSaveData();
		_loaded = true;
		_dirty = false;
		_nextAutosaveTime = 0f;
		FileSystem.Data.WriteJson( LayoutFile, _data );
		Log.Info( "[HUD Layout] Wiped hud_layout.json" );
	}

	/// <summary>
	/// Reset all HUD elements to true center immediately.
	/// This matches the old layout-editor behavior where Reset All stacked everything in the middle.
	/// </summary>
	public static void ResetAllToCenter()
	{
		EnsureLoaded();
		PushUndoState();

		_suspendUndo = true;
		try
		{
			// Start from a clean slate so old per-resolution overrides or safe-area entries
			// can't keep pushing elements around.
			_data = new LayoutSaveData();
			_loaded = true;
			_dirty = false;
			_nextAutosaveTime = 0f;

			foreach ( var key in GetAllElementKeys() )
			{
				if ( string.IsNullOrWhiteSpace( key ) )
					continue;

				var entry = GetOrCreateEntryForWrite( key );
				if ( entry == null )
					continue;

				entry.X = 0.5f;
				entry.Y = 0.5f;
				entry.Preset = (int)ScreenPosition.MiddleCenter;
				entry.Anchor = (int)HudAnchor.MiddleCenter;
				entry.Scale = 1f;
			}

			FileSystem.Data.WriteJson( LayoutFile, _data );
		}
		finally
		{
			_suspendUndo = false;
		}

		Log.Info( "[HUD Layout] Reset all -> centered" );
	}

	/// <summary>
	/// Set the scale for an element.
	/// </summary>
	public static void SetScale( string key, float scale )
	{
		EnsureLoaded();
		PushUndoState();

		var entry = GetOrCreateEntryForWrite( key );
		if ( entry == null ) return;

		if ( entry.Anchor < 0 )
			entry.Anchor = (int)(AnchorByKey.TryGetValue( key, out var a ) ? a : HudAnchor.MiddleCenter);

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
		var current = GetPosition( key, GetDefaultPosition( key ) );
		// Convert pixel offset to normalized using current screen resolution.
		// (Fallback to 1920x1080 if unavailable.)
		var screen = Screen.Size;
		float sx = screen.x > 0 ? screen.x : 1920f;
		float sy = screen.y > 0 ? screen.y : 1080f;
		var normalized = new Vector2( offset.x / sx, offset.y / sy );

		var next = current + normalized;
		SetPosition( key, next );
	}

	/// <summary>
	/// Get list of all configurable HUD element keys.
	/// </summary>
	public static List<string> GetAllElementKeys()
	{
		EnsureLoaded();

		var keys = new HashSet<string>
		{
			KeyPlayerHud,
			KeyXpBar,
			KeyHotbar,
			KeyChat,
			KeyMinimap,
			KeyInventory,
			KeySkillsPanel
		};

		foreach ( var k in _registeredKeys )
			keys.Add( k );

		if ( _data?.Elements != null )
		{
			foreach ( var k in _data.Elements.Keys )
				keys.Add( k );
		}

		var per = GetElementsForCurrentScreen( create: false );
		if ( per != null )
		{
			foreach ( var k in per.Keys )
				keys.Add( k );
		}

		var list = keys.ToList();
		list.Sort( ( a, b ) => string.Compare( GetElementName( a ), GetElementName( b ), System.StringComparison.OrdinalIgnoreCase ) );
		return list;
	}

	/// <summary>
	/// Get friendly name for an element key.
	/// </summary>
	public static string GetElementName( string key )
	{
		if ( !string.IsNullOrWhiteSpace( key ) && _registeredNames.TryGetValue( key, out var registeredName ) )
			return registeredName;

		return key switch
		{
			KeyPlayerHud => "Player Stats (HP/Armor)",
			KeyXpBar => "XP Bar",
			KeyHotbar => "Hotbar",
			KeyChat => "Chat Box",
			KeyMinimap => "Minimap",
			KeyInventory => "Inventory",
			KeySkillsPanel => "Skills",
			_ => key
		};
	}

	/// <summary>
	/// Reset all saved HUD layout back to defaults.
	/// </summary>
	public static void ResetElement( string key )
	{
		EnsureLoaded();
		if ( string.IsNullOrWhiteSpace( key ) )
			return;
		PushUndoState();

		key = key.Trim();

		// Remove the per-resolution override for the current screen, if any.
		var per = GetElementsForCurrentScreen( create: false );
		per?.Remove( key );

		// Reset the global/default entry.
		if ( _data.Elements == null )
			_data.Elements = new Dictionary<string, LayoutEntry>();

		var pos = GetDefaultPosition( key );
		var anchor = AnchorByKey.TryGetValue( key, out var a ) ? a : HudAnchor.MiddleCenter;

		_data.Elements[key] = new LayoutEntry
		{
			X = pos.x,
			Y = pos.y,
			Preset = -1,
			Scale = 1f,
			Anchor = (int)anchor
		};

		FileSystem.Data.WriteJson( LayoutFile, _data );
		Log.Info( $"[HUD Layout] Reset '{key}' to defaults" );
	}

	[ConCmd( "vegga_hud_reset_chat" )]
	public static void ResetChatLayoutCommand()
	{
		ResetElement( KeyChat );
	}

	static Vector2 PixelOffsetToNormalized( Vector2 px )
	{
		var screen = Screen.Size;
		float sx = screen.x > 0 ? screen.x : 1920f;
		float sy = screen.y > 0 ? screen.y : 1080f;
		return new Vector2( px.x / sx, px.y / sy );
	}

	static Vector2 InwardOffsetPxForAnchor( HudAnchor anchor, float px )
	{
		return anchor switch
		{
			HudAnchor.TopLeft => new Vector2( px, px ),
			HudAnchor.TopCenter => new Vector2( 0f, px ),
			HudAnchor.TopRight => new Vector2( -px, px ),
			HudAnchor.MiddleLeft => new Vector2( px, 0f ),
			HudAnchor.MiddleCenter => new Vector2( 0f, 0f ),
			HudAnchor.MiddleRight => new Vector2( -px, 0f ),
			HudAnchor.BottomLeft => new Vector2( px, -px ),
			HudAnchor.BottomCenter => new Vector2( 0f, -px ),
			HudAnchor.BottomRight => new Vector2( -px, -px ),
			_ => new Vector2( 0f, 0f )
		};
	}

	/// <summary>
	/// Soft reset: applies preferred default placements for key HUD panels
	/// with a small 4px inward offset. Does not wipe unrelated elements.
	/// </summary>
	public static void ResetSoft()
	{
		EnsureLoaded();
		PushUndoState();

		_suspendUndo = true;
		try
		{
			var keys = new[]
			{
				KeyChat,
				KeyInventory,
				KeyXpBar,
				KeySkillsPanel,
				KeyPlayerHud
			};

			// Remove per-resolution overrides for the current screen for these keys.
			var per = GetElementsForCurrentScreen( create: false );
			if ( per != null )
			{
				foreach ( var k in keys )
					per.Remove( k );
			}

			if ( _data.Elements == null )
				_data.Elements = new Dictionary<string, LayoutEntry>();

			foreach ( var k in keys )
			{
				var basePos = GetDefaultPosition( k );
				var anchor = AnchorByKey.TryGetValue( k, out var a ) ? a : HudAnchor.MiddleCenter;
				var offsetPx = InwardOffsetPxForAnchor( anchor, 4f );
				var pos = ClampToAllowedRange( basePos + PixelOffsetToNormalized( offsetPx ) );

				_data.Elements[k] = new LayoutEntry
				{
					X = pos.x,
					Y = pos.y,
					Preset = -1,
					Scale = 1f,
					Anchor = (int)anchor
				};
			}

			FileSystem.Data.WriteJson( LayoutFile, _data );
		}
		finally
		{
			_suspendUndo = false;
		}

		Log.Info( "[HUD Layout] Reset soft" );
	}

	public static void ResetAll()
	{
		EnsureLoaded();
		PushUndoState();

		// Full reset: layouts, per-screen overrides, and any saved safe-area calibration.
		_data = new LayoutSaveData();
		_loaded = true;

		// Repopulate with defaults so panels don't all fall back to (0.5, 0.5).
		var keys = new HashSet<string>
		{
			KeyPlayerHud,
			KeyXpBar,
			KeyChat,
			KeyMinimap,
			KeyInventory,
			KeySkillsPanel
		};

		foreach ( var k in _registeredKeys )
			keys.Add( k );

		foreach ( var k in DefaultPositions.Keys )
			keys.Add( k );

		foreach ( var k in keys )
		{
			var pos = GetDefaultPosition( k );
			var anchor = AnchorByKey.TryGetValue( k, out var a ) ? a : HudAnchor.MiddleCenter;
			_data.Elements[k] = new LayoutEntry
			{
				X = pos.x,
				Y = pos.y,
				Preset = -1,
				Scale = 1f,
				Anchor = (int)anchor
			};
		}

		// Nothing else to clear (new LayoutSaveData starts empty).

		FileSystem.Data.WriteJson( LayoutFile, _data );
	}
}


