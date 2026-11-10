using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ProductionUnlockFountainPatch
{
	private static readonly MethodBase target = ProductionHook.Check(() => AccessTools.Method(typeof(VendingFountainHelper), "UnlockVendingFountain", Type.EmptyTypes, (Type[])null), "VendingFountainHelper.UnlockVendingFountain");

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
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (Net.IsRemote)
		{
			return Production.ClientBlockUnlock("UnlockVendingFountain");
		}
		if (!Production.ServerDay)
		{
			return true;
		}
		Production.ServerUnlock(ProdOp.UnlockFountain, 0);
		return false;
	}
}
