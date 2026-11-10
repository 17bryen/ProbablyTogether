using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ProductionButcherContentPatch
{
	private static readonly MethodBase target = ProductionHook.Check(() => AccessTools.Method(typeof(MachineAutoButcher), "ButcherContent", new Type[1] { typeof(GameItem) }, (Type[])null), "MachineAutoButcher.ButcherContent");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem machine, ref bool __result)
	{
		if (Production.Let(ProdOp.ButcherContent, machine))
		{
			return true;
		}
		__result = false;
		return false;
	}
}
