using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CustSetupWaitingClientPatch
{
	private static readonly MethodBase target = CustomersHook.Find(() => AccessTools.Method(typeof(PlayerStore), "SetupWaitingClient", new Type[1] { typeof(string) }, (Type[])null), "PlayerStore.SetupWaitingClient");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(PlayerStore __instance, out StoreClientInstance __state)
	{
		__state = __instance.currentClientInstance;
		return CustomersGate.Allow();
	}

	private static void Postfix(PlayerStore __instance, StoreClientInstance __state)
	{
		if (Net.IsOnline && CustomersGate.Allow() && __instance.currentClientInstance != null && __instance.currentClientInstance != __state)
		{
			Customers.BumpVisit();
			Customers.PassCounterIfNeeded();
		}
	}
}
