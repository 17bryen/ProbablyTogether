using System.Collections.Generic;
using System.Reflection;

namespace ProbablyTogether.UI;

internal static class UiPatchTargets
{
	private static readonly HashSet<string> warned = new HashSet<string>();

	internal static bool Found(MethodBase m, string what)
	{
		if (m != null)
		{
			return true;
		}
		if (warned.Add(what))
		{
			Log.Warn("ui: patch target " + what + " not found in this game version; that patch is skipped");
		}
		return false;
	}
}
