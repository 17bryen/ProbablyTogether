using System;
using System.Reflection;

namespace ProbablyTogether;

internal static class EconomyHook
{
	internal static MethodBase Find(Func<MethodBase> f, string name)
	{
		MethodBase methodBase = null;
		try
		{
			methodBase = f();
		}
		catch (Exception ex)
		{
			Log.Warn("economy: hook " + name + " lookup failed: " + ex.Message);
		}
		if (methodBase == null)
		{
			Log.Warn("economy: hook " + name + " not found in this game build; that part is not synced");
		}
		return methodBase;
	}
}
