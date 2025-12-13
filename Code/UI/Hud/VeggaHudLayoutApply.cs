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

	public static bool TryGetLastApplied( string key, out LastApplied info )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
		{
			info = default;
			return false;
		}
		return _lastApplied.TryGetValue( key, out info );
	}

	public static void Apply( Panel panel, string key, Vector2 fallbackDefaultPos, float fallbackDefaultScale = 1f )
	{
		if ( panel == null || string.IsNullOrWhiteSpace( key ) )
			return;

		var screen = Screen.Size;
		if ( screen.x <= 0 || screen.y <= 0 )
			return;

		// Apply within calibrated safe-area if present (per resolution).
		var (safeMinN, safeMaxN) = VeggaHudLayoutState.GetSafeAreaNormalized();
		float safeMinX = safeMinN.x * screen.x;
		float safeMinY = safeMinN.y * screen.y;
		float safeMaxX = safeMaxN.x * screen.x;
		float safeMaxY = safeMaxN.y * screen.y;
		float safeW = System.Math.Max( 1f, safeMaxX - safeMinX );
		float safeH = System.Math.Max( 1f, safeMaxY - safeMinY );

		var pos = VeggaHudLayoutState.GetPosition( key, fallbackDefaultPos );
		float scale = VeggaHudLayoutState.GetScale( key, fallbackDefaultScale );
		var anchor = VeggaHudLayoutState.GetAnchor( key );

		var size = panel.Box.Rect.Size;
		float w = size.x;
		float h = size.y;

		// If size isn't ready yet, just apply without extra clamping.
		if ( w <= 1f || h <= 1f )
		{
			_lastApplied[key] = new LastApplied
			{
				PositionN = pos,
				Scale = scale,
				Anchor = anchor,
				SizePxScaled = new Vector2( 0f, 0f ),
				ScreenPx = screen,
				SafeMinN = safeMinN,
				SafeMaxN = safeMaxN,
				HasValidSize = false
			};
			ApplyRaw( panel, pos, scale, anchor );
			return;
		}

		// Account for CSS scale transform.
		w *= scale;
		h *= scale;

		float pxX = pos.x * screen.x;
		float pxY = pos.y * screen.y;
		float localX = pxX - safeMinX;
		float localY = pxY - safeMinY;

		(float minX, float maxX) = ClampRangeX( anchor, w, safeW );
		(float minY, float maxY) = ClampRangeY( anchor, h, safeH );

		localX = localX.Clamp( minX, maxX );
		localY = localY.Clamp( minY, maxY );
		pxX = safeMinX + localX;
		pxY = safeMinY + localY;

		var clampedPos = new Vector2( pxX / screen.x, pxY / screen.y );
		_lastApplied[key] = new LastApplied
		{
			PositionN = clampedPos,
			Scale = scale,
			Anchor = anchor,
			SizePxScaled = new Vector2( w, h ),
			ScreenPx = screen,
			SafeMinN = safeMinN,
			SafeMaxN = safeMaxN,
			HasValidSize = true
		};
		// If we're editing layout and we had to clamp, keep the saved position aligned
		// so the overlay's anchor marker matches the actual on-screen location.
		if ( VeggaHudLayoutState.IsActive )
		{
			var dx = clampedPos.x - pos.x;
			var dy = clampedPos.y - pos.y;
			if ( (dx * dx + dy * dy) > 0.00000025f )
			{
				VeggaHudLayoutState.UpdatePositionClamped( key, clampedPos );
			}
		}
		ApplyRaw( panel, clampedPos, scale, anchor );
	}

	static void ApplyRaw( Panel panel, Vector2 pos, float scale, VeggaHudLayoutState.HudAnchor anchor )
	{
		var screen = Screen.Size;
		if ( screen.x <= 0 || screen.y <= 0 )
			return;

		float pxX = pos.x * screen.x;
		float pxY = pos.y * screen.y;

		panel.Style.Position = PositionMode.Absolute;
		panel.Style.Left = Length.Pixels( pxX );
		panel.Style.Top = Length.Pixels( pxY );
		panel.Style.Set( "transform", $"{TranslateForAnchor( anchor )} scale({scale})" );
	}

	static string TranslateForAnchor( VeggaHudLayoutState.HudAnchor anchor )
	{
		return anchor switch
		{
			VeggaHudLayoutState.HudAnchor.TopLeft => "translate(0%,0%)",
			VeggaHudLayoutState.HudAnchor.TopCenter => "translate(-50%,0%)",
			VeggaHudLayoutState.HudAnchor.TopRight => "translate(-100%,0%)",
			VeggaHudLayoutState.HudAnchor.MiddleLeft => "translate(0%,-50%)",
			VeggaHudLayoutState.HudAnchor.MiddleCenter => "translate(-50%,-50%)",
			VeggaHudLayoutState.HudAnchor.MiddleRight => "translate(-100%,-50%)",
			VeggaHudLayoutState.HudAnchor.BottomLeft => "translate(0%,-100%)",
			VeggaHudLayoutState.HudAnchor.BottomCenter => "translate(-50%,-100%)",
			VeggaHudLayoutState.HudAnchor.BottomRight => "translate(-100%,-100%)",
			_ => "translate(-50%,-50%)"
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
