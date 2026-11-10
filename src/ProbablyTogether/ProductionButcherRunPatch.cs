using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ProductionButcherRunPatch
{
	private static readonly MethodBase target = ProductionHook.Check(() => AccessTools.Method(typeof(MachineAutoButcher), "Run", new Type[2]
	{
		typeof(GameItem),
		typeof(bool)
	}, (Type[])null), "MachineAutoButcher.Run");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem machine, bool manual)
	{
		if (!manual)
		{
			return Production.LetNightRun();
		}
		return Production.Let(ProdOp.ButcherRun, machine);
	}
}
