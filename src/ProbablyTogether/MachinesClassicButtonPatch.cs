using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class MachinesClassicButtonPatch
{
	private static readonly MethodBase target = MachinesHook.Check(() => AccessTools.Method(typeof(ButtonElement), "OnSelect", Type.EmptyTypes, (Type[])null), "ButtonElement.OnSelect");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(ButtonElement __instance)
	{
		return Machines.OnButton(__instance);
	}
}
