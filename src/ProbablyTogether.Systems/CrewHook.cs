using System;
using System.Reflection;

namespace ProbablyTogether.Systems;

internal static class CrewHook
{
	internal static MethodBase Find(Func<MethodBase> lookup, string name)
	{
		MethodBase methodBase = null;
		try
		{
			methodBase = lookup();
		}
		catch (Exception ex)
		{
			Log.Warn("crew: hook " + name + " lookup failed: " + ex.Message);
		}
		if (methodBase == null)
		{
			Log.Warn("crew: hook " + name + " not found in this game build; the crew report leaves that out");
		}
		return methodBase;
	}

	internal static void Guard(string what, Action a)
	{
		try
		{
			a();
		}
		catch (Exception ex)
		{
			if (Log.Allow("crew|hook|" + what, 10000L, out var skipped))
			{
				Log.Warn("crew: " + what + " observer: " + ex.Message + Log.More(skipped));
			}
		}
	}

	internal static int Int(GameItem it, string tag)
	{
		try
		{
			return (it != null) ? it.GetTagReadonly(tag).GetInt() : 0;
		}
		catch
		{
			return 0;
		}
	}
}
