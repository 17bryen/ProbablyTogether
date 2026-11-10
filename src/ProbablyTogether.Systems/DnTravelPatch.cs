using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class DnTravelPatch
{
	private static readonly MethodBase target = DayNightGate.Find(() => AccessTools.Method(typeof(MapUIManager), "Travel", new Type[1] { typeof(AfterhourLocation) }, (Type[])null), "MapUIManager.Travel");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(AfterhourLocation location)
	{
		if (!DayNightGate.LocalCommit)
		{
			return true;
		}
		if (location != null)
		{
			DayNight.Send("dayNight.travel", new MsgDnTravel
			{
				locationId = location.Id
			});
		}
		return false;
	}
}
