using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsSortPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(InventorySortHelper), "Sort", (Type[])null, (Type[])null), "InventorySortHelper.Sort");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameGridInventory inventory, bool bigFirst, bool fromEnd, ref bool __result)
	{
		if (!ItemsCapture.ClientActive)
		{
			return true;
		}
		__result = ItemsMove.Sort(inventory, bigFirst, fromEnd, EmporiumOption.SortAutoRotate, "menu") != 0;
		return false;
	}
}
