using System;

namespace Sandbox.Money;

public static class VeggaBankExchange
{
	// Configurable via admin chat commands.
	public static int CoinToCashRate { get; private set; } = 140;
	public static float FeePercent { get; private set; } = 5.0f;

	public static void SetRate( int newRate )
	{
		CoinToCashRate = Math.Clamp( newRate, 0, 1_000_000_000 );
	}

	public static void SetFeePercent( float newFeePercent )
	{
		FeePercent = Math.Clamp( newFeePercent, 0f, 100f );
	}

	public static void SetConfig( int rate, float feePercent )
	{
		SetRate( rate );
		SetFeePercent( feePercent );
	}

	public static bool TryCompute( int coins, out int grossCash, out int feeCash, out int netCash )
	{
		grossCash = 0;
		feeCash = 0;
		netCash = 0;

		if ( coins <= 0 )
			return false;

		// Use 64-bit math to avoid overflow.
		long gross = (long)coins * (long)CoinToCashRate;
		if ( gross <= 0 )
			return false;

		long fee = (long)MathF.Floor( (float)gross * (FeePercent / 100f) );
		if ( fee < 0 ) fee = 0;
		if ( fee > gross ) fee = gross;

		long net = gross - fee;
		if ( net <= 0 )
			return false;

		grossCash = (int)Math.Clamp( gross, 0, int.MaxValue );
		feeCash = (int)Math.Clamp( fee, 0, int.MaxValue );
		netCash = (int)Math.Clamp( net, 0, int.MaxValue );
		return netCash > 0;
	}
}
