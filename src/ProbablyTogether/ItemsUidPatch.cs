using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUidPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(PlayerStore), "GetUniqueID", Type.EmptyTypes, (Type[])null), "PlayerStore.GetUniqueID");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(PlayerStore __instance, ref int __result)
	{
		if (Net.IsOnline)
		{
			return Items.OnGetUniqueId(__instance, ref __result);
		}
		return true;
	}

	private static void Postfix(int __result)
	{
		if (Net.IsServer)
		{
			Items.OnGotUniqueId(__result);
		}
	}
}
