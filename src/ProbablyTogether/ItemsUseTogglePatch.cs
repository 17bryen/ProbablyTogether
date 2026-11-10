using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUseTogglePatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(GameItem), "ToggleSlotItem", Type.EmptyTypes, (Type[])null);

	private static bool Prepare()
	{
		return ItemsUseHook.Found(target, "GameItem.ToggleSlotItem()");
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem __instance)
	{
		return ItemsUse.Intercept(UseOp.Toggle, __instance, null);
	}
}
