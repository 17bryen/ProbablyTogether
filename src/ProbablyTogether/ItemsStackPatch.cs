using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsStackPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(SlotMarker), "StackItemUnchecked", (Type[])null, (Type[])null), "SlotMarker.StackItemUnchecked");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Prefix(GameItem item, GameItem targetItem, int amount, out ItemsMoveRecorder.StackState __state)
	{
		__state = null;
		if (Net.IsServer)
		{
			__state = ItemsMoveRecorder.BeforeStack(item, targetItem, amount);
		}
		else if (Net.IsClient && !Net.Applying)
		{
			ItemsGuard.Saw("SlotMarker.StackItemUnchecked", item, (GraphNodeStorage)(object)((item != null) ? item.parentInventory : null));
		}
	}

	private static void Postfix(GameItem item, GameItem targetItem, ItemsMoveRecorder.StackState __state)
	{
		if (__state != null)
		{
			ItemsMoveRecorder.AfterStack(item, targetItem, __state);
		}
	}

	private static void Finalizer(ItemsMoveRecorder.StackState __state)
	{
		if (__state != null)
		{
			ItemsMoveRecorder.Leave(__state.mode);
		}
	}
}
