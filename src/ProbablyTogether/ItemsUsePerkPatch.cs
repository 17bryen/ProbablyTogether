using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUsePerkPatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(MindReprogrammerHelper), "Apply", new Type[2]
	{
		typeof(GameItem),
		typeof(StartingPerk)
	}, (Type[])null);

	private static bool Prepare()
	{
		return ItemsUseHook.Found(target, "MindReprogrammerHelper.Apply");
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem item, StartingPerk definition)
	{
		return ItemsUse.InterceptPerk(item, definition);
	}
}
