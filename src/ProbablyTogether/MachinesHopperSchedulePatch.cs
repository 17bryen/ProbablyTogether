using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class MachinesHopperSchedulePatch
{
	private static readonly MethodBase target = MachinesHook.Check(() => AccessTools.Method(typeof(HopperHelper), "Schedule", new Type[1] { typeof(GameItem) }, (Type[])null), "HopperHelper.Schedule");

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
		return Machines.OnHopperSchedule();
	}
}
