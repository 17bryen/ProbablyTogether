using System;
using System.Reflection;

namespace ProbablyTogether;

internal static class ItemsHook
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
			Log.Warn("items: game method " + what + " lookup failed (" + ex.GetType().Name + "); that path is not synced");
			return null;
		}
		if (methodBase == null)
		{
			Log.Warn("items: game method " + what + " not found; that path is not synced");
		}
		return methodBase;
	}
}
