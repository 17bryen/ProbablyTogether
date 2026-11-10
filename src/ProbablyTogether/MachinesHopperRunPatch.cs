using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class MachinesHopperRunPatch
{
	private static readonly MethodBase target = MachinesHook.Check(() => AccessTools.Method(typeof(HopperHelper), "RunAndRecord", new Type[1] { typeof(GameItem) }, (Type[])null), "HopperHelper.RunAndRecord");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem intake, ref int __result)
	{
		return Machines.OnHopperRun(intake, ref __result);
	}
}
