using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsAcceptUncheckedPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(SlotMarker), "AcceptUnchecked", (Type[])null, (Type[])null), "SlotMarker.AcceptUnchecked");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(SlotMarker __instance, ref int __result, out ItemsMoveRecorder.AcceptState __state)
	{
		__state = null;
		if (!Net.IsOnline)
		{
			return true;
		}
		if (Net.IsHost && !Net.Applying && ItemsCapture.Current != null)
		{
			__result = ItemsCapture.OnAcceptUnchecked(__instance);
			return false;
		}
		if (Net.IsServer)
		{
			__state = ItemsMoveRecorder.BeforeReposition(__instance);
			return true;
		}
		if (Net.Applying)
		{
			return true;
		}
		if (ItemsCapture.Current != null)
		{
			__result = ItemsCapture.OnAcceptUnchecked(__instance);
			return false;
		}
		ItemsGuard.Saw("SlotMarker.AcceptUnchecked", __instance.item, (GraphNodeStorage)(object)__instance.inventory);
		return true;
	}

	private static void Postfix(SlotMarker __instance, int __result, ItemsMoveRecorder.AcceptState __state)
	{
		if (__state != null)
		{
			ItemsMoveRecorder.AfterReposition(__instance, __result, __state);
		}
	}

	private static void Finalizer(ItemsMoveRecorder.AcceptState __state)
	{
		if (__state != null)
		{
			ItemsMoveRecorder.Leave(__state.mode);
		}
	}
}
