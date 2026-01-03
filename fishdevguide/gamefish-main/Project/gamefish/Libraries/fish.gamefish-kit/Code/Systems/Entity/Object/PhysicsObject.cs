namespace GameFish;

[Icon( "sports_football" )]
public partial class PhysicsObject : DynamicEntity
{
	protected override bool? IsNetworkedOverride => true;
	protected override bool IsNetworkedAutomatically => true;

	protected override OwnerTransfer NetworkTransferModeDefault => OwnerTransfer.Takeover;

	public override Connection DefaultNetworkOwner => null;
}
