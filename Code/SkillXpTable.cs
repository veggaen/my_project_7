using System;
using System.Collections.Generic;
using System.Text;
public static class SkillXpTable
{
	public const int MaxLevel = 99;

	// XpForLevel[1] = 0, XpForLevel[99] = 13034431
	public static readonly int[] XpForLevel = BuildXpTable();

	private static int[] BuildXpTable()
	{
		var xp = new int[MaxLevel + 1];
		xp[1] = 0;

		double total = 0;

		for ( int level = 2; level <= MaxLevel; level++ )
		{
			int i = level - 1;
			total += Math.Floor( i + 300 * Math.Pow( 2, i / 7.0 ) );
			xp[level] = (int)Math.Floor( total / 4 );
		}

		return xp;
	}

	public static int GetXpForLevel( int level )
	{
		if ( level <= 1 ) return 0;
		if ( level > MaxLevel ) level = MaxLevel;
		return XpForLevel[level];
	}

	public static int GetLevelForXp( int xp )
	{
		if ( xp <= 0 ) return 1;

		int current = 1;
		for ( int level = 2; level <= MaxLevel; level++ )
		{
			if ( xp < XpForLevel[level] )
				break;
			current = level;
		}
		return current;
	}
}
