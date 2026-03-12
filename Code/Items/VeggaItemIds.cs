namespace Sandbox;

/// <summary>
/// Stable item id constants.
///
/// Keep these values stable once shipped, so saves/trades/drops remain valid.
/// Reserve ranges by category to avoid collisions.
/// </summary>
public static class VeggaItemIds
{
	// Currency
	public const int Cash = 1;
	public const int GoldCoin = 2;

	// Materials / metals
	public const int GoldBar200g = 100; // Kept stable id (now treated as 1 "Gold Bar")
	public const int GoldOre = 102;

	public const int ClayOre = 110;
	public const int TinOre = 111;
	public const int CopperOre = 112;
	public const int IronOre = 113;
	public const int CoalOre = 114;

	public const int TinBar = 130;
	public const int CopperBar = 131;
	public const int BronzeBar = 132;
	public const int IronBar = 133;
	public const int SteelBar = 134;

	// Wood / fuel
	public const int LogFull = 200;
	public const int LogChopped = 201;
	public const int NotedLogFull = 210;
	public const int NotedLogChopped = 211;

	// Moulds
	public const int MouldBar = 300;
	public const int MouldCoin = 301;
	public const int MouldGoblet = 302;

	// Crafted
	public const int GoldGoblet = 400;

	// Weapons / tools
	public const int Pistol9mm = 500;
	public const int Rifle556 = 501;
	public const int DualPistols9mm = 502;
	public const int Knife = 520;
	public const int BuildHammer = 540;

	// Ammo
	public const int Ammo9mm = 600;
	public const int Ammo556 = 601;
}
