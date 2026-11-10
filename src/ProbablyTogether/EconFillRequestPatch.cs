using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class EconFillRequestPatch
{
	private static readonly MethodBase target = EconomyHook.Find(() => AccessTools.Method(typeof(StoreRequest), "FillRequest", Type.EmptyTypes, (Type[])null), "StoreRequest.FillRequest");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(StoreRequest __instance)
	{
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (Net.IsServer)
		{
			Economy.ServerFill((Game.Store?.storeRequests)?.IndexOf(__instance) ?? (-1), __instance.id, 0);
			return false;
		}
		Economy.RequestFill(__instance);
		return false;
	}
}
