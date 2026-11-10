using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class MachinesRadioPatch
{
	private static readonly MethodBase target = MachinesHook.Check(() => AccessTools.Method(typeof(MachineryRadioButton), "SetOn", new Type[2]
	{
		typeof(bool),
		typeof(bool)
	}, (Type[])null), "MachineryRadioButton.SetOn");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(MachineryRadioButton __instance, bool on, bool notify)
	{
		return Machines.OnRadio(__instance, on, notify);
	}
}
