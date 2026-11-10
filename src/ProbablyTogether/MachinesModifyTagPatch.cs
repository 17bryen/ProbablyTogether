using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class MachinesModifyTagPatch
{
	private static readonly MethodBase target = MachinesHook.Check(() => AccessTools.Method(typeof(GameItem), "ModifyTag", new Type[3]
	{
		typeof(string),
		typeof(Action<TagState>),
		typeof(bool)
	}, (Type[])null), "GameItem.ModifyTag");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix(GameItem __instance, string tagName)
	{
		if (Net.IsClient)
		{
			Machines.OnTagWrite(__instance, tagName);
		}
	}
}
