using Concentus.Common.CPlusPlus;

namespace Concentus.Silk.Structs;

internal class SilkEncoderControl
{
	internal readonly int[] Gains_Q16 = new int[4];

	internal readonly short[][] PredCoef_Q12 = Arrays.InitTwoDimensionalArray<short>(2, 16);

	internal readonly short[] LTPCoef_Q14 = new short[20];

	internal int LTP_scale_Q14;

	internal readonly int[] pitchL = new int[4];

	internal readonly short[] AR1_Q13 = new short[64];

	internal readonly short[] AR2_Q13 = new short[64];

	internal readonly int[] LF_shp_Q14 = new int[4];

	internal readonly int[] GainsPre_Q14 = new int[4];

	internal readonly int[] HarmBoost_Q14 = new int[4];

	internal readonly int[] Tilt_Q14 = new int[4];

	internal readonly int[] HarmShapeGain_Q14 = new int[4];

	internal int Lambda_Q10;

	internal int input_quality_Q14;

	internal int coding_quality_Q14;

	internal int sparseness_Q8;

	internal int predGain_Q16;

	internal int LTPredCodGain_Q7;

	internal readonly int[] ResNrg = new int[4];

	internal readonly int[] ResNrgQ = new int[4];

	internal readonly int[] GainsUnq_Q16 = new int[4];

	internal sbyte lastGainIndexPrev;

	internal void Reset()
	{
		Arrays.MemSet(Gains_Q16, 0, 4);
		Arrays.MemSet(PredCoef_Q12[0], (short)0, 16);
		Arrays.MemSet(PredCoef_Q12[1], (short)0, 16);
		Arrays.MemSet(LTPCoef_Q14, (short)0, 20);
		LTP_scale_Q14 = 0;
		Arrays.MemSet(pitchL, 0, 4);
		Arrays.MemSet(AR1_Q13, (short)0, 64);
		Arrays.MemSet(AR2_Q13, (short)0, 64);
		Arrays.MemSet(LF_shp_Q14, 0, 4);
		Arrays.MemSet(GainsPre_Q14, 0, 4);
		Arrays.MemSet(HarmBoost_Q14, 0, 4);
		Arrays.MemSet(Tilt_Q14, 0, 4);
		Arrays.MemSet(HarmShapeGain_Q14, 0, 4);
		Lambda_Q10 = 0;
		input_quality_Q14 = 0;
		coding_quality_Q14 = 0;
		sparseness_Q8 = 0;
		predGain_Q16 = 0;
		LTPredCodGain_Q7 = 0;
		Arrays.MemSet(ResNrg, 0, 4);
		Arrays.MemSet(ResNrgQ, 0, 4);
		Arrays.MemSet(GainsUnq_Q16, 0, 4);
		lastGainIndexPrev = 0;
	}
}
