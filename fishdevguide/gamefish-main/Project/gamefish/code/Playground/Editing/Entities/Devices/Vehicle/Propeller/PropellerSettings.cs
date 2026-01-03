namespace Playground;

public partial struct PropellerSettings
{
	[Title( "Accelerate" )]
	public string KeyForward { get; set; } = "KP_8";

	[Title( "Reverse" )]
	public string KeyReverse { get; set; } = "KP_2";

	[Title( "Speed" )]
	public float Speed { get; set; } = 40f;

	[Title( "Speed Limit" )]
	public float Limit { get; set; } = 40f;

	[Title( "Lift" )]
	public float TorqueLift { get; set; } = 0.5f;

	[Title( "Friction" )]
	public Friction Friction { get; set; } = new( 0.05f, 2f );

	public PropellerSettings() { }
}
