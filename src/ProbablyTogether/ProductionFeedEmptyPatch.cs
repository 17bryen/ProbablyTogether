using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ProductionFeedEmptyPatch
{
	private static readonly MethodBase target = ProductionHook.Check(() => AccessTools.Method(typeof(MachineFeedDispenser), "EmptyStorage", new Type[1] { typeof(GameItem) }, (Type[])null), "MachineFeedDispenser.EmptyStorage");

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
		return Production.Let(ProdOp.FeedEmpty, machine);
	}
}
