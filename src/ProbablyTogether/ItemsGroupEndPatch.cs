using System;
using System.Reflection;
using ProbablyTogether.UI;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsGroupEndPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(ItemMultiSelectHandler), "EndGroupDrag", (Type[])null, (Type[])null), "ItemMultiSelectHandler.EndGroupDrag");

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
		__state = ItemsCapture.Enter("group", out var _);
	}

	private static void Finalizer(bool __state)
	{
		ItemsCapture.Exit(__state);
		Carry.OnLocalDragEnd("group drop");
	}
}
