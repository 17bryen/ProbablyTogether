using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUseQuietNoticePatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(StoreUIManager), "Notify", new Type[2]
	{
		typeof(string),
		typeof(string)
	}, (Type[])null);

	private static bool Prepare()
	{
		return ItemsUseHook.Found(target, "StoreUIManager.Notify");
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix()
	{
		if (!ItemsUse.QuietUi)
		{
			ItemsUse.NoticesShown++;
			return true;
		}
		ItemsUse.QuietNoticesBlocked++;
		return false;
	}
}
