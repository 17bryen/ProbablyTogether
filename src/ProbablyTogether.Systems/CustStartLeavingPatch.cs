using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CustStartLeavingPatch
{
	private static readonly MethodBase target = CustomersHook.Find(() => AccessTools.Method(typeof(StoreUIManager), "OnClientStartLeaving", new Type[1] { typeof(bool) }, (Type[])null), "StoreUIManager.OnClientStartLeaving");

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
			Customers.LeavePending = true;
		}
	}
}
