using Sandbox;
using System;
using System.Collections.Generic;

namespace Sandbox.UI;

/// <summary>
/// Tracks open UI layers in last-opened order so ESC closes one at a time.
/// </summary>
public static class VeggaUiCloseStack
{
	public const string KeyPauseMenu = "pause_menu";
	public const string KeyChatInput = "chat_input";

	sealed class Entry
	{
		public string Key;
		public Action Close;
	}

	static readonly List<Entry> _stack = new();

	public static void SetOpen( string key, bool isOpen, Action closeAction )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
			return;

		key = key.Trim();

		if ( !isOpen )
		{
			Remove( key );
			return;
		}

		// Move to top if already present.
		for ( int i = 0; i < _stack.Count; i++ )
		{
			if ( string.Equals( _stack[i].Key, key, StringComparison.Ordinal ) )
			{
				_stack.RemoveAt( i );
				break;
			}
		}

		_stack.Add( new Entry { Key = key, Close = closeAction } );
	}

	public static void Remove( string key )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
			return;

		key = key.Trim();
		for ( int i = _stack.Count - 1; i >= 0; i-- )
		{
			if ( string.Equals( _stack[i].Key, key, StringComparison.Ordinal ) )
				_stack.RemoveAt( i );
		}
	}

	public static bool TryCloseTop()
	{
		if ( _stack.Count <= 0 )
			return false;

		var top = _stack[_stack.Count - 1];
		_stack.RemoveAt( _stack.Count - 1 );

		try
		{
			top?.Close?.Invoke();
		}
		catch ( Exception e )
		{
			Log.Warning( $"[UI] CloseStack close failed for '{top?.Key}': {e}" );
		}

		return true;
	}

	/// <summary>
	/// ESC behavior:
	/// 1) If layout mode is active: ESC exits layout.
	/// 2) Else if any UI is open: ESC closes exactly one (last opened).
	/// 3) Else: ESC toggles pause menu.
	/// </summary>
	public static void HandleEscapePress()
	{
		if ( !Input.EscapePressed )
			return;

		Input.EscapePressed = false;

		if ( VeggaHudLayoutState.IsActive )
		{
			VeggaHudLayoutState.SetActive( false );
			return;
		}

		if ( TryCloseTop() )
			return;

		VeggaPauseMenu.ToggleFromEscape();
	}
}
