using System;

namespace Sandbox;

/// <summary>
/// Gives a world object a stable, saved container id.
/// Attach this to furnace/storage prefabs.
/// </summary>
public sealed class VeggaWorldContainerId : Component
{
	[Property, Sync]
	public Guid ContainerId { get; set; } = Guid.Empty;

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
			return;

		if ( ContainerId == Guid.Empty )
		{
			// Prefer a stable id that survives restarts for placed scene objects.
			try
			{
				ContainerId = GameObject.Id;
			}
			catch
			{
				// Some contexts may not expose a stable GameObject id.
				ContainerId = Guid.Empty;
			}

			if ( ContainerId == Guid.Empty )
				ContainerId = Guid.NewGuid();
		}
	}
}
