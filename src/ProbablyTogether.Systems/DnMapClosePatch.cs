using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class DnMapClosePatch
{
	private static readonly MethodBase target = DayNightGate.Find(() => AccessTools.Method(typeof(MapUIManager), "CloseUI", new Type[1] { typeof(bool) }, (Type[])null), "MapUIManager.CloseUI");

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
		DayNight.Send("dayNight.closeMap", new MsgDnEmpty());
		return false;
	}
}
