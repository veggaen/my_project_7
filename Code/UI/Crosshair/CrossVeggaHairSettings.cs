using Sandbox;
using System;

namespace Sandbox;

/// <summary>
/// CS2-style crosshair settings - saved per player
/// </summary>
public class CrossVeggaHairSettings
{
	// ========== STYLE ==========
	public enum CrossVeggaHairStyle
	{
		Classic,      // Standard + shape
		ClassicDynamic, // Expands when moving
		Default,      // Simple cross
		DefaultDynamic, // Simple cross that expands
		Dot           // Just a dot
	}

	public CrossVeggaHairStyle Style { get; set; } = CrossVeggaHairStyle.Classic;

	// ========== SIZE & THICKNESS ==========
	public float Length { get; set; } = 5f;        // Length of each line
	public float Thickness { get; set; } = 1f;     // Thickness of lines
	public float Gap { get; set; } = 3f;           // Gap from center
	public float DotSize { get; set; } = 2f;       // Center dot size (0 = no dot)

	// ========== OUTLINE ==========
	public bool ShowOutline { get; set; } = true;
	public float OutlineThickness { get; set; } = 1f;

	// ========== COLOR ==========
	public Color Color { get; set; } = Color.Green;
	public Color OutlineColor { get; set; } = Color.Black;
	public float Opacity { get; set; } = 1f;

	// ========== DYNAMIC (Movement) ==========
	public bool IsDynamic { get; set; } = false;
	public float DynamicGap { get; set; } = 10f;   // Max gap when moving

	// ========== T-STYLE (Top line) ==========
	public bool ShowTopLine { get; set; } = true;

	// ========== PRESETS ==========
	public static CrossVeggaHairSettings CS2Default()
	{
		return new CrossVeggaHairSettings
		{
			Style = CrossVeggaHairStyle.Classic,
			Length = 5f,
			Thickness = 1f,
			Gap = 3f,
			DotSize = 0f,
			Color = Color.Green,
			ShowOutline = true,
			OutlineThickness = 1f,
			IsDynamic = false
		};
	}

	public static CrossVeggaHairSettings Dot()
	{
		return new CrossVeggaHairSettings
		{
			Style = CrossVeggaHairStyle.Dot,
			DotSize = 4f,
			Color = Color.White,
			ShowOutline = true,
			OutlineThickness = 1f
		};
	}

	public static CrossVeggaHairSettings Dynamic()
	{
		return new CrossVeggaHairSettings
		{
			Style = CrossVeggaHairStyle.ClassicDynamic,
			Length = 6f,
			Thickness = 2f,
			Gap = 2f,
			DotSize = 2f,
			Color = Color.Cyan,
			ShowOutline = true,
			IsDynamic = true,
			DynamicGap = 12f
		};
	}

	public static CrossVeggaHairSettings Minimal()
	{
		return new CrossVeggaHairSettings
		{
			Style = CrossVeggaHairStyle.Default,
			Length = 3f,
			Thickness = 1f,
			Gap = 1f,
			DotSize = 1f,
			Color = Color.White,
			ShowOutline = false
		};
	}

	/// <summary>
	/// Save to cookie (persistent across sessions)
	/// </summary>
	public void Save()
	{
		Cookie.Set( "crosshair_style", (int)Style );
		Cookie.Set( "crosshair_length", Length );
		Cookie.Set( "crosshair_thickness", Thickness );
		Cookie.Set( "crosshair_gap", Gap );
		Cookie.Set( "crosshair_dotsize", DotSize );
		Cookie.Set( "crosshair_color", Color.ToString() );
		Cookie.Set( "crosshair_outline", ShowOutline );
		Cookie.Set( "crosshair_outlinethickness", OutlineThickness );
		Cookie.Set( "crosshair_outlinecolor", OutlineColor.ToString() );
		Cookie.Set( "crosshair_opacity", Opacity );
		Cookie.Set( "crosshair_dynamic", IsDynamic );
		Cookie.Set( "crosshair_dynamicgap", DynamicGap );
		Cookie.Set( "crosshair_showtop", ShowTopLine );
	}

	/// <summary>
	/// Load from cookie
	/// </summary>
	public static CrossVeggaHairSettings Load()
	{
		var settings = new CrossVeggaHairSettings();

		settings.Style = (CrossVeggaHairStyle)Cookie.Get( "crosshair_style", (int)CrossVeggaHairStyle.Classic );
		settings.Length = Cookie.Get( "crosshair_length", 5f );
		settings.Thickness = Cookie.Get( "crosshair_thickness", 1f );
		settings.Gap = Cookie.Get( "crosshair_gap", 3f );
		settings.DotSize = Cookie.Get( "crosshair_dotsize", 0f );
		settings.ShowOutline = Cookie.Get( "crosshair_outline", true );
		settings.OutlineThickness = Cookie.Get( "crosshair_outlinethickness", 1f );
		settings.Opacity = Cookie.Get( "crosshair_opacity", 1f );
		settings.IsDynamic = Cookie.Get( "crosshair_dynamic", false );
		settings.DynamicGap = Cookie.Get( "crosshair_dynamicgap", 10f );
		settings.ShowTopLine = Cookie.Get( "crosshair_showtop", true );

		// Parse colors
		var colorStr = Cookie.Get( "crosshair_color", "Green" );
		settings.Color = ParseColor( colorStr, Color.Green );

		var outlineColorStr = Cookie.Get( "crosshair_outlinecolor", "Black" );
		settings.OutlineColor = ParseColor( outlineColorStr, Color.Black );

		return settings;
	}

	private static Color ParseColor( string str, Color fallback )
	{
		if ( Color.TryParse( str, out var color ) )
			return color;
		return fallback;
	}
}

