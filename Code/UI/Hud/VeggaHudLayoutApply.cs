using Sandbox;
using Sandbox.UI;

namespace Sandbox.UI;

/// <summary>
/// Single source of truth for applying HUD layout position/scale.
/// Uses VeggaHudLayoutState (saved normalized position), per-element anchors,
/// and panel.Box.Rect.Size to clamp consistently across any resolution.
/// </summary>
public static class VeggaHudLayoutApply
{
	const float MarginPx = 48f;
	static readonly Vector2 FallbackSizePx = new( 160f, 60f );

	static Vector2 GetCanvasPx( Panel panel )
	{
		// Prefer the top-most panel size (shared by all HUD children on the same ScreenPanel).
		// Using the immediate parent can diverge per element and causes the overlay/UI drift
		// that gets worse as elements move away from the top-left.
		if ( panel != null )
		{
			Panel root = panel;
			while ( root.Parent != null )
				root = root.Parent;

			var rootSize = root.Box.Rect.Size;
			if ( rootSize.x > 1f && rootSize.y > 1f )
				return rootSize;
		}

		var screen = Screen.Size;
		if ( screen.x > 1f && screen.y > 1f )
			return screen;

		return new Vector2( 1920f, 1080f );
	}

	static Panel GetRootPanel( Panel panel )
	{
		if ( panel == null )
			return null;

		Panel root = panel;
		while ( root.Parent != null )
			root = root.Parent;
		return root;
	}

	public struct LastApplied
	{
		public Vector2 PositionN;
		public float Scale;
		public VeggaHudLayoutState.HudAnchor Anchor;
		public Vector2 SizePxScaled;
		public Vector2 ScreenPx;
		public Vector2 SafeMinN;
		public Vector2 SafeMaxN;
		public bool HasValidSize;
	}

	static readonly System.Collections.Generic.Dictionary<string, LastApplied> _lastApplied = new();
	static readonly System.Collections.Generic.Dictionary<string, System.WeakReference<Panel>> _panels = new();

	public static bool TryGetLastApplied( string key, out LastApplied info )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
		{
			info = default;
			return false;
		}
		return _lastApplied.TryGetValue( key, out info );
	}

	/// <summary>
	/// Try to get the actual on-screen rect of the live panel for a key.
	/// This is useful for debugging visual mismatch vs the blue-box rect.
	/// </summary>
	public static bool TryGetRuntimePanelRectPx( string key, out Vector2 topLeftPx, out Vector2 sizePx )
	{
		topLeftPx = default;
		sizePx = default;
		if ( string.IsNullOrWhiteSpace( key ) )
			return false;

		if ( !_panels.TryGetValue( key, out var weak ) || weak == null )
			return false;

		if ( !weak.TryGetTarget( out var panel ) || panel == null )
			return false;

		var rect = panel.Box.Rect;
		topLeftPx = new Vector2( rect.Left, rect.Top );
		sizePx = new Vector2( rect.Width, rect.Height );
		return sizePx.x > 0.5f && sizePx.y > 0.5f;
	}

	public static void Apply( Panel panel, string key, Vector2 fallbackDefaultPos, float fallbackDefaultScale = 1f )
	{
		if ( panel == null || string.IsNullOrWhiteSpace( key ) )
			return;

		_panels[key] = new System.WeakReference<Panel>( panel );

		var canvas = GetCanvasPx( panel );
		if ( canvas.x <= 0 || canvas.y <= 0 )
			return;

		// Apply within calibrated safe-area if present (per resolution).
		var (safeMinN, safeMaxN) = VeggaHudLayoutState.GetSafeAreaNormalizedForClamping();
		float safeMinX = safeMinN.x * canvas.x;
		float safeMinY = safeMinN.y * canvas.y;
		float safeMaxX = safeMaxN.x * canvas.x;
		float safeMaxY = safeMaxN.y * canvas.y;
		float safeW = System.Math.Max( 1f, safeMaxX - safeMinX );
		float safeH = System.Math.Max( 1f, safeMaxY - safeMinY );

		var pos = VeggaHudLayoutState.GetPosition( key, fallbackDefaultPos );
		float scale = VeggaHudLayoutState.GetScale( key, fallbackDefaultScale );
		var anchor = VeggaHudLayoutState.GetAnchor( key );

		// Determine a stable size for anchoring.
		// If the panel hasn't measured yet, use cached size (if available) or a safe fallback.
		var measuredSizePx = panel.Box.Rect.Size;
		bool measuredValid = measuredSizePx.x > 1f && measuredSizePx.y > 1f;
		Vector2 baseSizePx = measuredValid ? measuredSizePx : FallbackSizePx;
		bool hadCachedValid = false;
		if ( !measuredValid && _lastApplied.TryGetValue( key, out var prev ) && prev.HasValidSize )
		{
			var prevScale = prev.Scale <= 0.0001f ? 1f : prev.Scale;
			baseSizePx = prev.SizePxScaled / prevScale;
			hadCachedValid = true;
		}

		// Account for CSS scale transform.
		var scaledSizePx = baseSizePx * scale;
		bool hasValidSize = measuredValid || hadCachedValid;

		// IMPORTANT: Keep positioning identical between editor preview (blue boxes) and normal gameplay.
		// We do not apply safe-area clamping here; if you can place it in the editor, it should stay there.
		_lastApplied[key] = new LastApplied
		{
			PositionN = pos,
			Scale = scale,
			Anchor = anchor,
			SizePxScaled = scaledSizePx,
			ScreenPx = canvas,
			SafeMinN = safeMinN,
			SafeMaxN = safeMaxN,
			HasValidSize = hasValidSize
		};
		ApplyRaw( panel, pos, scale, anchor, scaledSizePx );
	}

	static void ApplyRaw( Panel panel, Vector2 pos, float scale, VeggaHudLayoutState.HudAnchor anchor, Vector2? scaledSizePxOverride = null )
	{
		// IMPORTANT: Use percentage positioning and translate anchoring.
		// This matches how other UI elements (like the crosshair) are centered, and it behaves
		// correctly under ScreenPanel scaling (ConsistentHeight/etc) without drift.
		float leftPct = pos.x * 100f;
		float topPct = pos.y * 100f;

		(float txPct, float tyPct) = anchor switch
		{
			VeggaHudLayoutState.HudAnchor.TopLeft => (0f, 0f),
			VeggaHudLayoutState.HudAnchor.TopCenter => (-50f, 0f),
			VeggaHudLayoutState.HudAnchor.TopRight => (-100f, 0f),
			VeggaHudLayoutState.HudAnchor.MiddleLeft => (0f, -50f),
			VeggaHudLayoutState.HudAnchor.MiddleCenter => (-50f, -50f),
			VeggaHudLayoutState.HudAnchor.MiddleRight => (-100f, -50f),
			VeggaHudLayoutState.HudAnchor.BottomLeft => (0f, -100f),
			VeggaHudLayoutState.HudAnchor.BottomCenter => (-50f, -100f),
			VeggaHudLayoutState.HudAnchor.BottomRight => (-100f, -100f),
			_ => (-50f, -50f)
		};

		panel.Style.Position = PositionMode.Absolute;
		// Margins can shift an absolutely-positioned panel away from its computed top-left,
		// making the panel look misaligned compared to the layout editor rectangles.
		panel.Style.Set( "margin", "0" );
		// Make sure old stylesheet constraints (eg. right/bottom) don't fight our computed top-left.
		// If both left+right or top+bottom are set, s&box can stretch/reposition in ways that look
		// "offset" from the layout editor's intended position.
		panel.Style.Set( "right", "auto" );
		panel.Style.Set( "bottom", "auto" );
		panel.Style.Set( "left", $"{leftPct:0.###}%" );
		panel.Style.Set( "top", $"{topPct:0.###}%" );
		panel.Style.Set( "transform-origin", "0% 0%" );
		panel.Style.Set( "transform", $"translate({txPct:0.#}%, {tyPct:0.#}%) scale({scale})" );
	}

	static Vector2 AnchorOffsetPx( VeggaHudLayoutState.HudAnchor anchor, Vector2 scaledSizePx )
	{
		return anchor switch
		{
			VeggaHudLayoutState.HudAnchor.TopLeft => new Vector2( 0, 0 ),
			VeggaHudLayoutState.HudAnchor.TopCenter => new Vector2( -scaledSizePx.x * 0.5f, 0 ),
			VeggaHudLayoutState.HudAnchor.TopRight => new Vector2( -scaledSizePx.x, 0 ),
			VeggaHudLayoutState.HudAnchor.MiddleLeft => new Vector2( 0, -scaledSizePx.y * 0.5f ),
			VeggaHudLayoutState.HudAnchor.MiddleCenter => new Vector2( -scaledSizePx.x * 0.5f, -scaledSizePx.y * 0.5f ),
			VeggaHudLayoutState.HudAnchor.MiddleRight => new Vector2( -scaledSizePx.x, -scaledSizePx.y * 0.5f ),
			VeggaHudLayoutState.HudAnchor.BottomLeft => new Vector2( 0, -scaledSizePx.y ),
			VeggaHudLayoutState.HudAnchor.BottomCenter => new Vector2( -scaledSizePx.x * 0.5f, -scaledSizePx.y ),
			VeggaHudLayoutState.HudAnchor.BottomRight => new Vector2( -scaledSizePx.x, -scaledSizePx.y ),
			_ => new Vector2( -scaledSizePx.x * 0.5f, -scaledSizePx.y * 0.5f )
		};
	}

	static (float minX, float maxX) ClampRangeX( VeggaHudLayoutState.HudAnchor anchor, float w, float screenW )
	{
		switch ( anchor )
		{
			case VeggaHudLayoutState.HudAnchor.TopLeft:
			case VeggaHudLayoutState.HudAnchor.MiddleLeft:
			case VeggaHudLayoutState.HudAnchor.BottomLeft:
				return ( -MarginPx, (screenW - w) + MarginPx );

			case VeggaHudLayoutState.HudAnchor.TopCenter:
			case VeggaHudLayoutState.HudAnchor.MiddleCenter:
			case VeggaHudLayoutState.HudAnchor.BottomCenter:
				return ( (w * 0.5f) - MarginPx, (screenW - (w * 0.5f)) + MarginPx );

			case VeggaHudLayoutState.HudAnchor.TopRight:
			case VeggaHudLayoutState.HudAnchor.MiddleRight:
			case VeggaHudLayoutState.HudAnchor.BottomRight:
				return ( w - MarginPx, screenW + MarginPx );
		}

		return ( (w * 0.5f) - MarginPx, (screenW - (w * 0.5f)) + MarginPx );
	}

	static (float minY, float maxY) ClampRangeY( VeggaHudLayoutState.HudAnchor anchor, float h, float screenH )
	{
		switch ( anchor )
		{
			case VeggaHudLayoutState.HudAnchor.TopLeft:
			case VeggaHudLayoutState.HudAnchor.TopCenter:
			case VeggaHudLayoutState.HudAnchor.TopRight:
				return ( -MarginPx, (screenH - h) + MarginPx );

			case VeggaHudLayoutState.HudAnchor.MiddleLeft:
			case VeggaHudLayoutState.HudAnchor.MiddleCenter:
			case VeggaHudLayoutState.HudAnchor.MiddleRight:
				return ( (h * 0.5f) - MarginPx, (screenH - (h * 0.5f)) + MarginPx );

			case VeggaHudLayoutState.HudAnchor.BottomLeft:
			case VeggaHudLayoutState.HudAnchor.BottomCenter:
			case VeggaHudLayoutState.HudAnchor.BottomRight:
				return ( h - MarginPx, screenH + MarginPx );
		}

		return ( (h * 0.5f) - MarginPx, (screenH - (h * 0.5f)) + MarginPx );
	}
}
