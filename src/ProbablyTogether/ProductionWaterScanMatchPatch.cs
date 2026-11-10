using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ProductionWaterScanMatchPatch
{
	private static readonly MethodBase target = ProductionHook.Check(() => AccessTools.Method(typeof(WaterScannerUIManager), "MatchWaterLabel", (Type[])null, (Type[])null), "WaterScannerUIManager.MatchWaterLabel");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem waterContainer)
	{
		return Production.Let(ProdOp.WaterMatch, waterContainer);
	}
}
