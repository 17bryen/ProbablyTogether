using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class MachinesEnableTagPatch
{
	private static readonly MethodBase target = MachinesHook.Check(() => AccessTools.Method(typeof(GameItem), "EnableTag", new Type[2]
	{
		typeof(string),
		typeof(bool)
	}, (Type[])null), "GameItem.EnableTag");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix(GameItem __instance, string tagName, bool __result)
	{
		if (__result && Net.IsClient)
		{
			Machines.OnTagWrite(__instance, tagName);
		}
	}
}
