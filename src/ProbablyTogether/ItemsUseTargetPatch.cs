using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUseTargetPatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(GameItem), "Target", new Type[1] { typeof(GameItem) }, (Type[])null);

	private static bool Prepare()
	{
		return ItemsUseHook.Found(target, "GameItem.Target(GameItem)");
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem __instance, GameItem targetItem)
	{
		return ItemsUse.Intercept(UseOp.Target, __instance, targetItem);
	}
}
