using System;
using System.Reflection;

namespace ProbablyTogether.UI;

internal static class CarryHook
{
	internal static MethodBase Check(Func<MethodBase> find, string what)
	{
		MethodBase methodBase = null;
		try
		{
			methodBase = find();
		}
		catch (Exception ex)
		{
			Log.Warn("carry: game method " + what + " lookup failed (" + ex.GetType().Name + "); carried items start from the cursor poll only");
			return null;
		}
		if (methodBase == null)
		{
			Log.Warn("carry: game method " + what + " not found; carried items start from the cursor poll only");
		}
		return methodBase;
	}
}
