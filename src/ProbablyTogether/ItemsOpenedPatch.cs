using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsOpenedPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(ItemMouseDoubleClickHandler), "OpenContentAction", (Type[])null, (Type[])null), "ItemMouseDoubleClickHandler.OpenContentAction");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Prefix(GameItem newItem, out bool __state)
	{
		__state = ItemsCapture.ClientActive && newItem != null && newItem.IsTag("TRACK_OPENED_TAG") && !newItem.IsTag("OPENED_TAG");
	}

	private static void Postfix(GameItem newItem, bool __state)
	{
		if (__state && newItem != null && newItem.IsTag("OPENED_TAG"))
		{
			ItemsMove.MarkOpened(newItem, "open");
		}
	}
}
