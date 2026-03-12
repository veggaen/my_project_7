using Sandbox;

namespace Sandbox;

public static class AnimationParameterWriter
{
	public static void Set( SkinnedModelRenderer renderer, string name, int value )
	{
		if ( renderer == null || !renderer.IsValid() )
			return;

		try { renderer.Set( name, value ); } catch { }
	}

	public static void Set( SkinnedModelRenderer renderer, string name, float value )
	{
		if ( renderer == null || !renderer.IsValid() )
			return;

		try { renderer.Set( name, value ); } catch { }
	}

	public static void Set( SkinnedModelRenderer renderer, string name, bool value )
	{
		if ( renderer == null || !renderer.IsValid() )
			return;

		try { renderer.Set( name, value ); } catch { }
	}
}