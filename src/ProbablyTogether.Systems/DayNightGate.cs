using System;
using System.Reflection;

namespace ProbablyTogether.Systems;

internal static class DayNightGate
{
	internal static bool LocalCommit
	{
		get
		{
			if (Net.IsClient)
			{
				return !Net.Applying;
			}
			return false;
		}
	}

	internal static MethodBase Find(Func<MethodBase> lookup, string name)
	{
		MethodBase methodBase = null;
		try
		{
			methodBase = lookup();
		}
		catch (Exception ex)
		{
			Log.Warn("dayNight: hook " + name + " lookup failed: " + ex.Message);
		}
		if (methodBase == null)
		{
			Log.Warn("dayNight: hook " + name + " not found in this game build; that part of day-night is not synced");
		}
		return methodBase;
	}
}
