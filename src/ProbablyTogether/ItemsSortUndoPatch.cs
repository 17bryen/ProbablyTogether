using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsSortUndoPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(InventorySortHelper), "Undo", (Type[])null, (Type[])null), "InventorySortHelper.Undo");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameGridInventory inventory, ref bool __result)
	{
		if (!ItemsCapture.ClientActive)
		{
			return true;
		}
		__result = ItemsMove.SortUndo(inventory, "menu") != 0;
		return false;
	}
}
