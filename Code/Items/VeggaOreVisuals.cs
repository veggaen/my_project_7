using Sandbox;

namespace Sandbox;

public static class VeggaOreVisuals
{
	public const int MinVariant = 1;
	public const int MaxVariant = 3;

	public static bool IsOre( int itemId )
	{
		return itemId == VeggaItemIds.GoldOre
			|| itemId == VeggaItemIds.ClayOre
			|| itemId == VeggaItemIds.TinOre
			|| itemId == VeggaItemIds.CopperOre
			|| itemId == VeggaItemIds.IronOre
			|| itemId == VeggaItemIds.CoalOre;
	}

	public static int EnsureVariant( int itemId, int durabilityVariant, bool allowRandomize )
	{
		if ( !IsOre( itemId ) )
			return durabilityVariant;
		if ( durabilityVariant >= MinVariant && durabilityVariant <= MaxVariant )
			return durabilityVariant;
		if ( !allowRandomize )
			return 0;
		return Game.Random.Int( MinVariant, MaxVariant );
	}

	public static string GetVariantModelPath( int variant )
	{
		return variant switch
		{
			1 => "models/props/rock_scatter/rock_scatter_01.vmdl",
			// rock_scatter_02 has been reported missing/broken in some installs; avoid it.
			2 => "models/props/rock_scatter/rock_scatter_01.vmdl",
			3 => "models/props/rock_scatter/rock_scatter_03.vmdl",
			_ => "models/props/rock_scatter/rock_scatter_01.vmdl"
		};
	}

	public static Color GetTintForItem( int itemId )
	{
		// Subtle tints so the same rock mesh reads as different metal.
		return itemId switch
		{
			VeggaItemIds.TinOre => new Color( 0.70f, 0.70f, 0.75f, 1.0f ),
			VeggaItemIds.CopperOre => new Color( 0.90f, 0.55f, 0.25f, 1.0f ),
			VeggaItemIds.IronOre => new Color( 0.45f, 0.45f, 0.47f, 1.0f ),
			VeggaItemIds.CoalOre => new Color( 0.20f, 0.20f, 0.20f, 1.0f ),
			VeggaItemIds.GoldOre => new Color( 0.80f, 0.70f, 0.25f, 1.0f ),
			VeggaItemIds.ClayOre => new Color( 0.65f, 0.50f, 0.35f, 1.0f ),
			_ => Color.White
		};
	}

	public static void ApplyToWorldObject( GameObject go, int itemId, int durabilityVariant )
	{
		if ( go == null || !go.IsValid() )
			return;
		if ( !IsOre( itemId ) )
			return;

		int variant = EnsureVariant( itemId, durabilityVariant, allowRandomize: false );
		if ( variant <= 0 )
			return;

		var modelPath = GetVariantModelPath( variant );
		var tint = GetTintForItem( itemId );
		Model model = null;
		try { model = Model.Load( modelPath ); } catch { }

		static System.Collections.Generic.IEnumerable<T> SelfAndDescendants<T>( GameObject target ) where T : Component
		{
			var root = target.Components.Get<T>();
			if ( root != null && root.IsValid() )
				yield return root;
			foreach ( var child in target.Components.GetAll<T>( FindMode.InDescendants ) )
			{
				if ( child != null && child.IsValid() )
					yield return child;
			}
		}

		// Cover common prefab patterns: Prop + ModelRenderer (or either).
		foreach ( var prop in SelfAndDescendants<Prop>( go ) )
		{
			if ( prop == null || !prop.IsValid() ) continue;
			if ( model != null ) prop.Model = model;
			prop.Tint = tint;
		}

		foreach ( var renderer in SelfAndDescendants<ModelRenderer>( go ) )
		{
			if ( renderer == null || !renderer.IsValid() ) continue;
			if ( model != null ) renderer.Model = model;
			renderer.Tint = tint;
		}

		// Keep physics colliders aligned with the visual model so drops collide/fall correctly.
		foreach ( var collider in SelfAndDescendants<ModelCollider>( go ) )
		{
			if ( collider == null || !collider.IsValid() ) continue;
			if ( model != null ) collider.Model = model;
			collider.IsTrigger = false;
			collider.Static = false;
		}
	}
}
