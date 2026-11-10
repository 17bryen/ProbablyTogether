using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsCreatedPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(DirectoryMaster), "Item", new Type[2]
	{
		typeof(string),
		typeof(bool)
	}, (Type[])null), "DirectoryMaster.Item");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix(GameItem __result)
	{
		if (Net.IsOnline)
		{
			Items.OnItemCreated(__result);
		}
	}
}
