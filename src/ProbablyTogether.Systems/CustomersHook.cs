using System;
using System.Reflection;

namespace ProbablyTogether.Systems;

internal static class CustomersHook
{
	internal static MethodBase Find(Func<MethodBase> lookup, string name)
	{
		try
		{
			MethodBase methodBase = lookup();
			if (methodBase == null)
			{
				Log.Error("customers: game method " + name + " not found; that hook is off");
			}
			return methodBase;
		}
		catch (Exception ex)
		{
			Log.Error("customers: game method " + name + ": " + ex.Message + "; that hook is off");
			return null;
		}
	}
}
