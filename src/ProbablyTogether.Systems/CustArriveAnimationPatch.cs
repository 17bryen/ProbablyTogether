using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CustArriveAnimationPatch
{
	private static readonly MethodBase target = CustomersHook.Find(() => AccessTools.Method(typeof(StoreUIManager), "QueueClientArriveAnimation", Type.EmptyTypes, (Type[])null), "StoreUIManager.QueueClientArriveAnimation");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix()
	{
		if (Net.IsOnline)
		{
			Customers.LeavePending = false;
		}
	}
}
