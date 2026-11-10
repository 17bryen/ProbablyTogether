using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CustTryAddClientPatch
{
	private static readonly MethodBase target = CustomersHook.Find(() => AccessTools.Method(typeof(StoreClientManager), "TryAddClient", new Type[2]
	{
		typeof(StoreClient),
		typeof(bool)
	}, (Type[])null), "StoreClientManager.TryAddClient");

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
