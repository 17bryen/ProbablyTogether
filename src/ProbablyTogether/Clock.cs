using System.Diagnostics;

namespace ProbablyTogether;

internal static class Clock
{
	private static readonly Stopwatch sw = Stopwatch.StartNew();

	internal static long Ms => sw.ElapsedMilliseconds;
}
