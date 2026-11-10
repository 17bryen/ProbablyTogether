using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class MachinesFridgePatch
{
	private static readonly MethodBase target = MachinesHook.Check(() => AccessTools.Method(typeof(MachineFridge), "CoolZoneContainers", new Type[3]
	{
		typeof(GameGridInventory),
		typeof(double),
		typeof(double)
	}, (Type[])null), "MachineFridge.CoolZoneContainers");

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
		return Machines.OnTickFunction();
	}
}
