using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUseBehaviourTargetPatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(ItemBehaviourManager), "Target", new Type[2]
	{
		typeof(GameItem),
		typeof(GameItem)
	}, (Type[])null);

	private static bool Prepare()
	{
		return ItemsUseHook.Found(target, "ItemBehaviourManager.Target(GameItem, GameItem)");
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix()
	{
		if (Net.IsRemote)
		{
			return Net.Applying;
		}
		return true;
	}
}
