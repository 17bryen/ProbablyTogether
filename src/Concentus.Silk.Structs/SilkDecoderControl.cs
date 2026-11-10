using Concentus.Common.CPlusPlus;

namespace Concentus.Silk.Structs;

internal class SilkDecoderControl
{
	internal readonly int[] pitchL = new int[4];

	internal readonly int[] Gains_Q16 = new int[4];

	internal readonly short[][] PredCoef_Q12 = Arrays.InitTwoDimensionalArray<short>(2, 16);

	internal readonly short[] LTPCoef_Q14 = new short[20];

	internal int LTP_scale_Q14;

	internal void Reset()
	{
		Arrays.MemSet(pitchL, 0, 4);
		Arrays.MemSet(Gains_Q16, 0, 4);
		Arrays.MemSet(PredCoef_Q12[0], (short)0, 16);
		Arrays.MemSet(PredCoef_Q12[1], (short)0, 16);
		Arrays.MemSet(LTPCoef_Q14, (short)0, 20);
		LTP_scale_Q14 = 0;
	}
}
