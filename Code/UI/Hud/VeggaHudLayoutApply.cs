using Sandbox;
using Sandbox.UI;
using System.Globalization;

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

	static Vector2 GetFallbackSizePxForKey( string key )
	{
		// These are only used before a panel has measured at least once.
		// If the fallback is too small, clamping can allow the real (larger) panel
		// to disappear off-screen on right/bottom presets.
		return key switch
		{
			// Chat has explicit width/height in CSS.
			VeggaHudLayoutState.KeyChat => new Vector2( 720f, 360f ),
			// Inventory: grid (520w) + details (240w) ≈ 760w, height varies.
			VeggaHudLayoutState.KeyInventory => new Vector2( 780f, 460f ),
			// Hotbar: 9 slots + padding/gap.
			VeggaHudLayoutState.KeyHotbar => new Vector2( 520f, 80f ),
			// Skills: min-width 480; height varies with list.
			VeggaHudLayoutState.KeySkillsPanel => new Vector2( 560f, 420f ),
			// Player modular HUD is relatively small.
			VeggaHudLayoutState.KeyPlayerHud => new Vector2( 340f, 220f ),
			// XP bar is a slim horizontal widget.
			VeggaHudLayoutState.KeyXpBar => new Vector2( 520f, 90f ),
			// Cash drop progress bar is a slim horizontal widget.
			VeggaHudLayoutState.KeyCashDropProgress => new Vector2( 360f, 70f ),
			// Ammo counter is a compact horizontal widget.
			VeggaHudLayoutState.KeyAmmo => new Vector2( 240f, 70f ),
			// Minimap is typically square-ish.
			VeggaHudLayoutState.KeyMinimap => new Vector2( 260f, 260f ),
			// World container menus.
			VeggaHudLayoutState.KeyFurnaceMenu => new Vector2( 560f, 520f ),
			VeggaHudLayoutState.KeyStorageMenu => new Vector2( 560f, 520f ),
			// Scoreboard: wide panel.
			VeggaHudLayoutState.KeyScoreboard => new Vector2( 1400f, 800f ),
			_ => FallbackSizePx
		};
	}

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
	static readonly System.Collections.Generic.HashSet<string> _loggedParentMismatch = new();
	static readonly System.Collections.Generic.Dictionary<string, Vector2> _dragBaseSizeOverridePx = new();

	public static void BeginDrag( string key )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
			return;

		Vector2 baseSizePx;
		if ( _lastApplied.TryGetValue( key, out var prev ) && (prev.HasValidSize || (prev.SizePxScaled.x > 1f && prev.SizePxScaled.y > 1f)) )
		{
			var invScale = prev.Scale <= 0.0001f ? 1f : prev.Scale;
			baseSizePx = prev.SizePxScaled / invScale;
		}
		else
		{
			baseSizePx = GetFallbackSizePxForKey( key );
		}

		_dragBaseSizeOverridePx[key] = baseSizePx;
	}

	public static void EndDrag( string key )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
			return;
		_dragBaseSizeOverridePx.Remove( key );
	}

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
		// While dragging in the layout editor, freeze the size to avoid jitter for variable-height panels.
		var measuredSizePx = panel.Box.Rect.Size;
		bool measuredValid = measuredSizePx.x > 1f && measuredSizePx.y > 1f;
		Vector2 baseSizePx = measuredValid ? measuredSizePx : GetFallbackSizePxForKey( key );
		bool hadCachedValid = false;
		if ( _dragBaseSizeOverridePx.TryGetValue( key, out var dragOverride ) && dragOverride.x > 1f && dragOverride.y > 1f )
		{
			baseSizePx = dragOverride;
			hadCachedValid = true;
			measuredValid = false;
		}
		else if ( !measuredValid && _lastApplied.TryGetValue( key, out var prev ) && prev.HasValidSize )
		{
			var prevScale = prev.Scale <= 0.0001f ? 1f : prev.Scale;
			baseSizePx = prev.SizePxScaled / prevScale;
			hadCachedValid = true;
		}

		// Account for CSS scale transform.
		var scaledSizePx = baseSizePx * scale;
		bool hasValidSize = measuredValid || hadCachedValid;

		// Clamp the anchor position so the element can't fully leave the screen.
		// This prevents Right/Bottom presets from pushing menus out of view.
		var clampedPos = ClampToBoundsNormalized( pos, anchor, scaledSizePx, canvas );
		if ( VeggaHudLayoutState.IsActive && (System.Math.Abs( clampedPos.x - pos.x ) > 0.0001f || System.Math.Abs( clampedPos.y - pos.y ) > 0.0001f) )
		{
			// Persist the clamped value while in layout mode so the editor boxes and gameplay stay in sync.
			VeggaHudLayoutState.UpdatePositionClamped( key, clampedPos );
		}

		// IMPORTANT: Keep positioning identical between editor preview (blue boxes) and normal gameplay.
		// We do not apply safe-area clamping here; if you can place it in the editor, it should stay there.
		_lastApplied[key] = new LastApplied
		{
			PositionN = clampedPos,
			Scale = scale,
			Anchor = anchor,
			SizePxScaled = scaledSizePx,
			ScreenPx = canvas,
			SafeMinN = safeMinN,
			SafeMaxN = safeMaxN,
			HasValidSize = hasValidSize
		};
		ApplyRaw( panel, clampedPos, scale, anchor, scaledSizePx, canvas );
	}

	static Vector2 ClampToBoundsNormalized( Vector2 posN, VeggaHudLayoutState.HudAnchor anchor, Vector2 scaledSizePx, Vector2 canvasPx )
	{
		float w = System.Math.Max( 0f, scaledSizePx.x );
		float h = System.Math.Max( 0f, scaledSizePx.y );
		float screenW = System.Math.Max( 1f, canvasPx.x );
		float screenH = System.Math.Max( 1f, canvasPx.y );

		// Robust clamping: clamp the panel's *top-left* rather than clamping the anchor point.
		// Clamping anchor directly can behave badly for right/bottom anchors and/or large panels
		// (eg. when w > screenW, the anchor clamp ranges can invert and shove the panel away).
		var anchorPx = new Vector2( posN.x * screenW, posN.y * screenH );
		var offsetPx = AnchorOffsetPx( anchor, new Vector2( w, h ) );
		var topLeftPx = anchorPx + offsetPx;

		// Allow a small amount of off-screen to keep HUD from feeling "pinned".
		// Works even when the panel is larger than the screen.
		// Desired constraints:
		// left >= -Margin
		// left + w <= screenW + Margin  =>  left <= (screenW - w) + Margin
		// Same for top.
		float leftA = -MarginPx;
		float leftB = (screenW - w) + MarginPx;
		float topA = -MarginPx;
		float topB = (screenH - h) + MarginPx;

		float minLeft = System.Math.Min( leftA, leftB );
		float maxLeft = System.Math.Max( leftA, leftB );
		float minTop = System.Math.Min( topA, topB );
		float maxTop = System.Math.Max( topA, topB );

		topLeftPx.x = topLeftPx.x.Clamp( minLeft, maxLeft );
		topLeftPx.y = topLeftPx.y.Clamp( minTop, maxTop );

		anchorPx = topLeftPx - offsetPx;
		return new Vector2( anchorPx.x / screenW, anchorPx.y / screenH );
	}

	static void ApplyRaw( Panel panel, Vector2 posN, float scale, VeggaHudLayoutState.HudAnchor anchor, Vector2 scaledSizePx, Vector2 canvasPx )
	{
		if ( panel == null )
			return;

		// The layout positions are saved in full-screen normalized space.
		// BUT: CSS percentages are relative to the panel's *parent*.
		// If a HUD panel isn't parented directly to the full-screen root, using root-normalized %
		// will drift and can push right/bottom presets off-screen while BoxHud (root-space) looks correct.
		// So we convert the anchor point from root-space -> parent-space, then apply % + translate anchoring.
		var root = GetRootPanel( panel );
		var rootRect = root?.Box.Rect ?? default;
		var parentRect = panel.Parent?.Box.Rect ?? rootRect;

		float rootW = System.Math.Max( 1f, canvasPx.x );
		float rootH = System.Math.Max( 1f, canvasPx.y );
		float parentW = System.Math.Max( 1f, parentRect.Width );
		float parentH = System.Math.Max( 1f, parentRect.Height );

		var anchorPxInRoot = new Vector2( posN.x * rootW, posN.y * rootH );
		var anchorPxInScreen = new Vector2( rootRect.Left + anchorPxInRoot.x, rootRect.Top + anchorPxInRoot.y );


		// Convert desired anchor point -> desired TOP-LEFT in screen space.
		//
		// Why: some panels were visually acting like translate(-50%/-100%) anchoring was not applied,
		// making right/bottom presets appear off-screen while the layout editor (which uses anchor math)
		// looked correct. By baking the anchor offset into left/top directly, presets don't depend on
		// CSS translate being honored.
		var invScale = scale <= 0.0001f ? 1f : scale;
		var baseSizePx = scaledSizePx / invScale;
		var topLeftInScreen = anchorPxInScreen + AnchorOffsetPx( anchor, baseSizePx );

		float leftPct = ((topLeftInScreen.x - parentRect.Left) / parentW) * 100f;
		float topPct = ((topLeftInScreen.y - parentRect.Top) / parentH) * 100f;

		if ( !_loggedParentMismatch.Contains( panel.GetHashCode().ToString() ) )
		{
			// One-time log per panel instance (helps diagnose why only some presets fail).
			bool parentMismatch = System.Math.Abs( parentW - rootW ) > 1f || System.Math.Abs( parentH - rootH ) > 1f;
			if ( parentMismatch )
			{
				_loggedParentMismatch.Add( panel.GetHashCode().ToString() );
				Log.Info( $"[HudLayout] Parent mismatch for {panel.GetType().Name}: parent={parentW:0}x{parentH:0} root={rootW:0}x{rootH:0} parentRect={parentRect} rootRect={rootRect}" );
			}
		}

		panel.Style.Position = PositionMode.Absolute;
		// Margins can shift an absolutely-positioned panel away from its computed top-left,
		// making the panel look misaligned compared to the layout editor rectangles.
		panel.Style.Set( "margin", "0" );
		// Make sure old stylesheet constraints (eg. right/bottom) don't fight our computed top-left.
		// If both left+right or top+bottom are set, s&box can stretch/reposition in ways that look
		// "offset" from the layout editor's intended position.
		panel.Style.Set( "right", "auto" );
		panel.Style.Set( "bottom", "auto" );
		panel.Style.Set( "left", leftPct.ToString( "0.###", CultureInfo.InvariantCulture ) + "%" );
		panel.Style.Set( "top", topPct.ToString( "0.###", CultureInfo.InvariantCulture ) + "%" );
		panel.Style.Set( "transform-origin", "0% 0%" );
		var scaleText = scale.ToString( "0.###", CultureInfo.InvariantCulture );
		panel.Style.Set( "transform", $"scale({scaleText})" );
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
