using Concentus.Common.CPlusPlus;

namespace Concentus.Silk.Structs;

internal class SilkVADState
{
	internal readonly int[] AnaState = new int[2];

	internal readonly int[] AnaState1 = new int[2];

	internal readonly int[] AnaState2 = new int[2];

	internal readonly int[] XnrgSubfr = new int[4];

	internal readonly int[] NrgRatioSmth_Q8 = new int[4];

	internal short HPstate;

	internal readonly int[] NL = new int[4];

	internal readonly int[] inv_NL = new int[4];

	internal readonly int[] NoiseLevelBias = new int[4];

	internal int counter;

	internal void Reset()
	{
		Arrays.MemSet(AnaState, 0, 2);
		Arrays.MemSet(AnaState1, 0, 2);
		Arrays.MemSet(AnaState2, 0, 2);
		Arrays.MemSet(XnrgSubfr, 0, 4);
		Arrays.MemSet(NrgRatioSmth_Q8, 0, 4);
		HPstate = 0;
		Arrays.MemSet(NL, 0, 4);
		Arrays.MemSet(inv_NL, 0, 4);
		Arrays.MemSet(NoiseLevelBias, 0, 4);
		counter = 0;
	}
}
