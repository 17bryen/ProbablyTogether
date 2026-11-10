using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class MachinesRangePatch
{
	private static readonly MethodBase target = MachinesHook.Check(() => AccessTools.Method(typeof(MachineryRangeSlider), "SetValues", new Type[3]
	{
		typeof(float),
		typeof(float),
		typeof(bool)
	}, (Type[])null), "MachineryRangeSlider.SetValues");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(MachineryRangeSlider __instance, float newLow, float newHigh, bool notify)
	{
		return Machines.OnRange(__instance, newLow, newHigh, notify);
	}
}
