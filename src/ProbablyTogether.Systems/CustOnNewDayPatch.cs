using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CustOnNewDayPatch
{
	private static readonly MethodBase target = CustomersHook.Find(() => AccessTools.Method(typeof(StoreClientManager), "OnNewDay", Type.EmptyTypes, (Type[])null), "StoreClientManager.OnNewDay");

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
		return CustomersGate.Allow();
	}

	private static void Postfix()
	{
		if (Net.IsOnline && CustomersGate.Allow())
		{
			Customers.Visit = 0;
		}
	}
}
