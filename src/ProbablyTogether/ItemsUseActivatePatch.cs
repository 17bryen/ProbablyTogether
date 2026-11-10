using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUseActivatePatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(GameItem), "ActivateSlotItem", Type.EmptyTypes, (Type[])null);

	private static bool Prepare()
	{
		return ItemsUseHook.Found(target, "GameItem.ActivateSlotItem()");
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem __instance)
	{
		return ItemsUse.Intercept(UseOp.Activate, __instance, null);
	}
}
