using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsGraphAcceptOncePatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(GraphUtils), "TryAcceptOnce", new Type[3]
	{
		typeof(GraphNodeStorage),
		typeof(GameItem),
		typeof(int)
	}, (Type[])null), "GraphUtils.TryAcceptOnce");

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
		__result = ItemsCapture.OnGraphAcceptOnce(currentNode, item, amount);
		return false;
	}
}
