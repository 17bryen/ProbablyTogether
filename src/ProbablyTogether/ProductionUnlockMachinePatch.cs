using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ProductionUnlockMachinePatch
{
	private static readonly MethodBase target = ProductionHook.Check(() => AccessTools.Method(typeof(VendingMachineHelper), "UnlockVendingMachine", new Type[1] { typeof(int) }, (Type[])null), "VendingMachineHelper.UnlockVendingMachine");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(int charge)
	{
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (Net.IsRemote)
		{
			return Production.ClientBlockUnlock("UnlockVendingMachine");
		}
		if (!Production.ServerDay)
		{
			return true;
		}
		Production.ServerUnlock(ProdOp.UnlockMachine, charge);
		return false;
	}
}
