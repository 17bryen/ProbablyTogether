using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ProductionWaterStripMatchPatch
{
	private static readonly MethodBase target = ProductionHook.Check(() => AccessTools.Method(typeof(WaterStripUIManager), "OnMatch", Type.EmptyTypes, (Type[])null), "WaterStripUIManager.OnMatch");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(WaterStripUIManager __instance)
	{
		if (!Net.IsClient || Net.Applying)
		{
			return true;
		}
		GameItem currentWaterContainer = __instance.currentWaterContainer;
		if (currentWaterContainer != null && __instance.CanMatch(currentWaterContainer))
		{
			Production.Let(ProdOp.WaterMatch, currentWaterContainer);
		}
		return false;
	}
}
