using System;
using System.Reflection;

namespace ProbablyTogether;

internal static class ProductionHook
{
	internal static MethodBase Check(Func<MethodBase> lookup, string name)
	{
		try
		{
			MethodBase methodBase = lookup();
			if (methodBase == null)
			{
				Log.Warn("production: " + name + " not found in this game build; that hook is off");
			}
			return methodBase;
		}
		catch (Exception ex)
		{
			Log.Warn("production: looking up " + name + " failed (" + ex.Message + "); that hook is off");
			return null;
		}
	}
}
