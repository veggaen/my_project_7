using Sandbox;

namespace Sandbox;

/// <summary>
/// Debug helpers for verifying UI icon files are actually mounted in the packaged game.
/// </summary>
public static class VeggaIconDebugCommands
{
	[ConCmd( "vegga_icon_check" )]
	public static void CheckIcon( string path = "ui/items/icon_gold_coin.png" )
	{
		path ??= string.Empty;
		path = path.Trim();
		if ( path.Length == 0 )
			path = "ui/items/icon_gold_coin.png";

		var normalized = path.TrimStart( '/' );
		bool exists;
		try
		{
			exists = FileSystem.Mounted.FileExists( normalized );
		}
		catch ( System.Exception e )
		{
			Log.Error( $"[IconCheck] FileSystem.Mounted failed: {e.Message}" );
			return;
		}

		Log.Info( $"[IconCheck] '{normalized}' exists={exists}" );

		// Helpful follow-up: check the folder too.
		try
		{
			var folder = System.IO.Path.GetDirectoryName( normalized )?.Replace( "\\", "/" ) ?? "";
			if ( folder.Length > 0 )
			{
				var folderExists = FileSystem.Mounted.DirectoryExists( folder );
				Log.Info( $"[IconCheck] folder '{folder}' exists={folderExists}" );
			}
		}
		catch ( System.Exception e )
		{
			Log.Warning( $"[IconCheck] folder check failed: {e.Message}" );
		}
	}
}
