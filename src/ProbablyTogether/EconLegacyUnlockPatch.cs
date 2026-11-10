using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class EconLegacyUnlockPatch
{
	private static readonly MethodBase target = EconomyHook.Find(() => AccessTools.Method(typeof(WildUIManager), "OnUnlockClicked", Type.EmptyTypes, (Type[])null), "WildUIManager.OnUnlockClicked");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(WildUIManager __instance)
	{
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		string text = Economy.LegacySelected(__instance);
		if (Net.IsServer)
		{
			if (text != null)
			{
				Economy.ServerBuy(text, 0);
			}
			return false;
		}
		if (text != null && Economy.CheckBuy(text, out var _) == null)
		{
			Economy.RequestBuy(text);
		}
		return false;
	}
}
