using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CustDismissClientStackPatch
{
	private static readonly MethodBase target = CustomersHook.Find(() => AccessTools.Method(typeof(StoreClientManager), "DismissClientStack", Type.EmptyTypes, (Type[])null), "StoreClientManager.DismissClientStack");

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
}
