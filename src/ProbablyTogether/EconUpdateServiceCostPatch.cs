using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class EconUpdateServiceCostPatch
{
	private static readonly MethodBase target = EconomyHook.Find(() => AccessTools.Method(typeof(PlayerStore), "UpdateServiceCost", Type.EmptyTypes, (Type[])null), "PlayerStore.UpdateServiceCost");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix()
	{
		if (!Economy.Guard("PlayerStore.UpdateServiceCost"))
		{
			return false;
		}
		if (Net.IsOnline && (Net.IsServer || Net.Applying))
		{
			Economy.HealServices();
		}
		return true;
	}

	private static void Postfix(PlayerStore __instance)
	{
		if (Net.IsOnline && (Net.IsServer || Net.Applying))
		{
			Economy.NormalizeServices(__instance);
		}
	}
}
