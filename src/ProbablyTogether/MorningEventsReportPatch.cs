using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class MorningEventsReportPatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(StartOfDayUIManager), "ShowMorningReport", Type.EmptyTypes, (Type[])null);

	private static bool Prepare()
	{
		if (target == null)
		{
			Log.Warn("dialogue: StartOfDayUIManager.ShowMorningReport() not found; the morning report counts as a plain server call");
		}
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Prefix(out bool __state)
	{
		__state = Net.IsServer;
		if (__state)
		{
			MorningEvents.inMorningReport++;
		}
	}

	private static Exception Finalizer(Exception __exception, bool __state)
	{
		if (__state)
		{
			MorningEvents.inMorningReport--;
		}
		return __exception;
	}
}
