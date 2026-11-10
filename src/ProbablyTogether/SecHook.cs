using System;
using System.Reflection;

namespace ProbablyTogether;

internal static class SecHook
{
	internal static MethodBase Check(Func<MethodBase> find, string what)
	{
		try
		{
			MethodBase methodBase = find();
			if (methodBase == null)
			{
				Log.Warn("security: hook target " + what + " not found in this game build (that hook is off)");
			}
			return methodBase;
		}
		catch (Exception ex)
		{
			Log.Warn("security: hook target " + what + ": " + ex.GetType().Name + " (that hook is off)");
			return null;
		}
	}
}
