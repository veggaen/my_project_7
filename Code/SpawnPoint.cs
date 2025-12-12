using Sandbox;

namespace Sandbox;

/// <summary>
/// Spawn point for player respawning.
/// Place these in your map where you want players to spawn.
/// </summary>
public sealed class VeggaSpawnPoint : Component
{
	[Property] public bool Enabled { get; set; } = true;
	[Property] public string SpawnGroup { get; set; } = "default";
	[Property] public Color GizmoColor { get; set; } = Color.Green;

	protected override void DrawGizmos()
	{
		if ( !Enabled )
		{
			Gizmo.Draw.Color = Color.Red.WithAlpha( 0.5f );
		}
		else
		{
			Gizmo.Draw.Color = GizmoColor;
		}

		// Draw spawn point indicator
		Gizmo.Draw.LineSphere( Vector3.Zero, 32f );
		Gizmo.Draw.Arrow( Vector3.Zero, Vector3.Forward * 64f, 16f, 4f );
		
		// Draw text label
		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.Text( $"Spawn Point\n{SpawnGroup}", new Transform( Vector3.Up * 50f ) );
	}
}

