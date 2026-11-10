using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsTrashKeyPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(StoreUIManager), "TryTrashHoveredItem", (Type[])null, (Type[])null), "StoreUIManager.TryTrashHoveredItem");

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
		__state = ItemsCapture.Enter("trash", out var _);
	}

	private static void Finalizer(bool __state)
	{
		ItemsCapture.Exit(__state);
	}
}
