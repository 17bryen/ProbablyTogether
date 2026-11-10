using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CustFutureQueuePatch
{
	private static readonly MethodBase target = CustomersHook.Find(() => AccessTools.Method(typeof(PlayerStore), "HandleFutureClientQueue", Type.EmptyTypes, (Type[])null), "PlayerStore.HandleFutureClientQueue");

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
