using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class DnReturnPatch
{
	private static readonly MethodBase target = DayNightGate.Find(() => AccessTools.Method(typeof(MapUIManager), "ReturnNow", Type.EmptyTypes, (Type[])null), "MapUIManager.ReturnNow");

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
		DayNight.Send("dayNight.return", new MsgDnEmpty());
		return false;
	}
}
