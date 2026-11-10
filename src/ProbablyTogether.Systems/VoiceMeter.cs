using System;

namespace ProbablyTogether.Systems;

internal static class VoiceMeter
{
	internal static double Rms(float[] a, int start, int count)
	{
		if (a == null || count <= 0)
		{
			return 0.0;
		}
		double num = 0.0;
		for (int i = start; i < start + count; i++)
		{
			num += (double)a[i] * (double)a[i];
		}
		return Math.Sqrt(num / (double)count);
	}

	internal static float Dominant(float[] a, int start, int count, int rate, int lo, int hi, int step)
	{
		if (a == null || count < 64)
		{
			return 0f;
		}
		double num = -1.0;
		int num2 = 0;
		for (int i = lo; i <= hi; i += step)
		{
			double d = Math.PI * 2.0 * (double)i / (double)rate;
			double num3 = 2.0 * Math.Cos(d);
			double num4 = 0.0;
			double num5 = 0.0;
			for (int j = start; j < start + count; j++)
			{
				double num6 = (double)a[j] + num3 * num4 - num5;
				num5 = num4;
				num4 = num6;
			}
			double num7 = num4 * num4 + num5 * num5 - num3 * num4 * num5;
			if (num7 > num)
			{
				num = num7;
				num2 = i;
			}
		}
		return num2;
	}
}
