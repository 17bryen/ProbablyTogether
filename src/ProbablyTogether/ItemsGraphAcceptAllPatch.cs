using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsGraphAcceptAllPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(GraphUtils), "TryAcceptAll", new Type[3]
	{
		typeof(GraphNodeStorage),
		typeof(GameItem),
		typeof(int)
	}, (Type[])null), "GraphUtils.TryAcceptAll");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GraphNodeStorage currentNode, GameItem item, int amount, ref int __result)
	{
		if (!ItemsCapture.ClientActive || ItemsCapture.Current == null)
		{
			return true;
		}
		__result = ItemsCapture.OnGraphAcceptAll(currentNode, item, amount);
		return false;
	}
}
