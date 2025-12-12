using Sandbox;

namespace My_Project_7;

/// <summary>
/// Placeholder API client. The real HTTP integration is disabled for now
/// to keep the game compiling cleanly. We can re-enable it later with a
/// version that matches the current s&box API surface.
/// </summary>
public static class VeggaApiClient
{
	public static string BaseUrl { get; set; } = "http://localhost:3000";
	public static bool Enabled { get; set; } = false;
}

public static class VeggaApiCommands
{
	[ConCmd( "vegga_api_test" )]
	public static void TestApi()
	{
		Log.Info( "[VeggaAPI] Web integration is currently disabled (placeholder client)." );
	}
}

