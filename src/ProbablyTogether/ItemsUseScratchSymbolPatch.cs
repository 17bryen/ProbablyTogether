using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUseScratchSymbolPatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(ScratcherHelper), "MarkSymbolScratched", new Type[2]
	{
		typeof(GameItem),
		typeof(int)
	}, (Type[])null);

	private static bool Prepare()
	{
		return ItemsUseHook.Found(target, "ScratcherHelper.MarkSymbolScratched");
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem gameItem, int symbolIndex)
	{
		return ItemsUse.InterceptScratch(gameItem, symbolIndex);
	}
}
