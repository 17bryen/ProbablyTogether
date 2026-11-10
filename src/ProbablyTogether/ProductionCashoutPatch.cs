using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ProductionCashoutPatch
{
	private static readonly MethodBase target = ProductionHook.Check(() => AccessTools.Method(typeof(VendingMachineHelper), "TransferCashToPlayer", new Type[1] { typeof(GameItem) }, (Type[])null), "VendingMachineHelper.TransferCashToPlayer");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem vendingMachine)
	{
		return Production.Let(ProdOp.Cashout, vendingMachine);
	}
}
