using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsContextMenuPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(ItemContextHandler), "TriggerButton", (Type[])null, (Type[])null), "ItemContextHandler.TriggerButton");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Prefix(out bool __state)
	{
		__state = ItemsCapture.Enter("menu", out var _);
	}

	private static void Finalizer(bool __state)
	{
		ItemsCapture.Exit(__state);
	}
}
