using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CustOnLeftPatch
{
	private static readonly MethodBase target = CustomersHook.Find(() => AccessTools.Method(typeof(StoreClientMono), "OnLeft", Type.EmptyTypes, (Type[])null), "StoreClientMono.OnLeft");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Prefix(StoreClientMono __instance)
	{
		if (Net.IsOnline && (!(Customers.ignoreOnLeftField != null) || !(bool)Customers.ignoreOnLeftField.GetValue(__instance)))
		{
			Customers.LeavePending = false;
		}
	}
}
