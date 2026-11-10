using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CustGenerateClientPatch
{
	private static readonly MethodBase target = CustomersHook.Find(() => AccessTools.Method(typeof(StoreClientManager), "GenerateClient", Type.EmptyTypes, (Type[])null), "StoreClientManager.GenerateClient");

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
