using System;
using System.Reflection;
using ProbablyTogether.UI;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsDragEndPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(ItemMouseDragHandler), "EndDrag", (Type[])null, (Type[])null), "ItemMouseDragHandler.EndDrag");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Prefix(GameItemElement ___currentItem, GameItem ___lastItem, out bool __state)
	{
		__state = ItemsCapture.Enter("drag", out var c);
		if (__state)
		{
			c.dragItem = (GameItem)(object)___currentItem;
			c.dragOnto = ___lastItem;
			c.insertPending = ___lastItem != null && ___currentItem != null && GeneralHelper.MayPlayerInsertInto(___lastItem) && ___lastItem.MayHaveValidInventorySlot((GameItem)(object)___currentItem);
		}
	}

	private static void Finalizer(bool __state)
	{
		ItemsCapture.Exit(__state);
		Carry.OnLocalDragEnd("drop");
	}
}
