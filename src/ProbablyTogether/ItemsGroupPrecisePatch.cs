using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsGroupPrecisePatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(ItemMultiSelectHandler), "CommitPrecisePlacement", (Type[])null, (Type[])null), "ItemMultiSelectHandler.CommitPrecisePlacement");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(List<GameItemElement> ___selectedItems, GameInventory inventory, int deltaX, int deltaY)
	{
		if (!ItemsCapture.ClientActive)
		{
			return true;
		}
		List<GameItem> list = new List<GameItem>();
		if (___selectedItems != null)
		{
			foreach (GameItemElement ___selectedItem in ___selectedItems)
			{
				if (___selectedItem != null && !___selectedItem.IsDestroyed())
				{
					list.Add((GameItem)(object)___selectedItem);
				}
			}
		}
		if (list.Count > 0)
		{
			ItemsMove.GroupMove(list, inventory, deltaX, deltaY, "group");
		}
		return false;
	}
}
