namespace GameFish;

public static class Physics
{
	/// <summary>
	/// Searches for a <see cref="Rigidbody"/> and failing that any <see cref="Collider"/> to find any <see cref="PhysicsBody"/>.
	/// </summary>
	/// <returns> The body(or <c>null</c>). </returns>
	public static PhysicsBody FindBody( GameObject obj )
	{
		if ( !obj.IsValid() )
			return null;

		if ( obj.Components.TryGet<Rigidbody>( out var rb, FindMode.EnabledInSelf | FindMode.InAncestors ) )
			return rb.PhysicsBody;

#pragma warning disable CS0618 // Type or member is obsolete

		// This is the only exposed way, dunno why they made it obsolete.
		if ( obj.Components.TryGet<Collider>( out var c, FindMode.EnabledInSelf | FindMode.InAncestors ) )
			if ( c.KeyframeBody.IsValid() )
				return c.KeyframeBody;

#pragma warning restore CS0618 // Type or member is obsolete

		return null;
	}
}
