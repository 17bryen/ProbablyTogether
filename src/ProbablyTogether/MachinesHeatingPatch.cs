using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class MachinesHeatingPatch
{
	private static readonly MethodBase target = MachinesHook.Check(() => AccessTools.Method(typeof(MachineHeatingPlate), "HandleHeating", new Type[3]
	{
		typeof(GameSlotInventory),
		typeof(int),
		typeof(GameItem)
	}, (Type[])null), "MachineHeatingPlate.HandleHeating");

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
