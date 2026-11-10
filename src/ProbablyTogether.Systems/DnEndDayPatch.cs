using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class DnEndDayPatch
{
	private static readonly MethodBase target = DayNightGate.Find(() => AccessTools.Method(typeof(EndOfDayUIManager), "OnEndDayButtonClick", Type.EmptyTypes, (Type[])null), "EndOfDayUIManager.OnEndDayButtonClick");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix()
	{
		if (!DayNightGate.LocalCommit)
		{
			return true;
		}
		DayNight.RequestReady(on: true);
		DayNight.RefreshTablet();
		DayNight.Notice("Ready. The night starts when everyone is ready.");
		return false;
	}
}
