using Concentus.Common.CPlusPlus;

namespace Concentus.Silk.Structs;

internal class StereoDecodeState
{
	internal readonly short[] pred_prev_Q13 = new short[2];

	internal readonly short[] sMid = new short[2];

	internal readonly short[] sSide = new short[2];

	internal void Reset()
	{
		Arrays.MemSet(pred_prev_Q13, (short)0, 2);
		Arrays.MemSet(sMid, (short)0, 2);
		Arrays.MemSet(sSide, (short)0, 2);
	}
}
