using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class MachinesSliderPatch
{
	private static readonly MethodBase target = MachinesHook.Check(() => AccessTools.Method(typeof(MachinerySlider), "SetValue", new Type[2]
	{
		typeof(float),
		typeof(bool)
	}, (Type[])null), "MachinerySlider.SetValue");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(MachinerySlider __instance, float newValue, bool notify)
	{
		return Machines.OnSlider(__instance, newValue, notify);
	}
}
