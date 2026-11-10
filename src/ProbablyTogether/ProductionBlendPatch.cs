using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ProductionBlendPatch
{
	private static readonly MethodBase target = ProductionHook.Check(() => AccessTools.Method(typeof(MachineFeedDispenser), "Blend", new Type[2]
	{
		typeof(GameItem),
		typeof(GameGridInventory)
	}, (Type[])null), "MachineFeedDispenser.Blend");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem machine)
	{
		return Production.Let(ProdOp.Blend, machine);
	}
}
