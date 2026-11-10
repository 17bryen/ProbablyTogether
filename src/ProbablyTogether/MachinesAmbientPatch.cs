using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class MachinesAmbientPatch
{
	private static readonly MethodBase target = MachinesHook.Check(() => AccessTools.Method(typeof(ChemicalHelper), "HandleAmbientTempDrop", new Type[1] { typeof(GameItem) }, (Type[])null), "ChemicalHelper.HandleAmbientTempDrop");

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
