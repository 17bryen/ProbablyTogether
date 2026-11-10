using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class MachinesButtonPatch
{
	private static readonly MethodBase target = MachinesHook.Check(() => AccessTools.Method(typeof(MachineryButton), "OnSelect", Type.EmptyTypes, (Type[])null), "MachineryButton.OnSelect");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(MachineryButton __instance)
	{
		return Machines.OnButton(__instance);
	}
}
