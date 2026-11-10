using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class DnEndDayClosePatch
{
	private static readonly MethodBase target = DayNightGate.Find(() => AccessTools.Method(typeof(EndOfDayUIManager), "CloseUI", new Type[1] { typeof(bool) }, (Type[])null), "EndOfDayUIManager.CloseUI");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix()
	{
		if (DayNightGate.LocalCommit && !DayNight.SuppressUnready)
		{
			DayNight.RequestReady(on: false);
		}
	}
}
