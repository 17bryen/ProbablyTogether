using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CustGetNextClientPatch
{
	private static readonly MethodBase target = CustomersHook.Find(() => AccessTools.Method(typeof(PlayerStore), "GetNextClient", Type.EmptyTypes, (Type[])null), "PlayerStore.GetNextClient");

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
		if (!Net.IsOnline)
		{
			return;
		}
		StoreClientInstance currentClientInstance = __instance.currentClientInstance;
		if (currentClientInstance != null && currentClientInstance != __state)
		{
			Customers.BumpVisit();
			StoreClient clientBlueprint = currentClientInstance.GetClientBlueprint();
			if (clientBlueprint != null && clientBlueprint.isAug && (int)clientBlueprint.augIdleTell == 0 && (Net.IsServer || Net.Applying))
			{
				clientBlueprint.augIdleTell = (StoreClient.AugIdleTell)(RNG.Roll(25) ? 2 : ((!RNG.Roll(5)) ? 1 : 3));
			}
		}
	}
}
