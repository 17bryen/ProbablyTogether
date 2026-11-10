using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsInvAcceptPatch
{
	private static readonly List<MethodBase> targets = Targets();

	private static List<MethodBase> Targets()
	{
		List<MethodBase> list = new List<MethodBase>();
		MethodBase methodBase = ItemsHook.Check(() => AccessTools.Method(typeof(GameGridInventory), "UncheckedAccept", new Type[1] { typeof(GameItem) }, (Type[])null), "GameGridInventory.UncheckedAccept");
		if (methodBase != null && methodBase.DeclaringType == typeof(GameGridInventory))
		{
			list.Add(methodBase);
		}
		MethodBase methodBase2 = ItemsHook.Check(() => AccessTools.Method(typeof(GameSlotInventory), "UncheckedAccept", new Type[1] { typeof(GameItem) }, (Type[])null), "GameSlotInventory.UncheckedAccept");
		if (methodBase2 != null && methodBase2.DeclaringType == typeof(GameSlotInventory))
		{
			list.Add(methodBase2);
		}
		MethodBase methodBase3 = ItemsHook.Check(() => AccessTools.Method(typeof(GameGridScrollableInventory), "UncheckedAccept", new Type[1] { typeof(GameItem) }, (Type[])null), "GameGridScrollableInventory.UncheckedAccept");
		if (methodBase3 != null && methodBase3.DeclaringType == typeof(GameGridScrollableInventory))
		{
			list.Add(methodBase3);
		}
		MethodBase methodBase4 = ItemsHook.Check(() => AccessTools.Method(typeof(GameCharacterRaidInventory), "UncheckedAccept", new Type[1] { typeof(GameItem) }, (Type[])null), "GameCharacterRaidInventory.UncheckedAccept");
		if (methodBase4 != null && methodBase4.DeclaringType == typeof(GameCharacterRaidInventory))
		{
			list.Add(methodBase4);
		}
		return list;
	}

	private static bool Prepare()
	{
		return targets.Count > 0;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return targets;
	}

	private static void Prefix(GameInventory __instance, GameItem item, out ItemsMoveRecorder.InvState __state)
	{
		__state = null;
		if (Net.IsServer)
		{
			__state = ItemsMoveRecorder.BeforeInvAccept(__instance, item);
		}
		else if (Net.IsClient && !Net.Applying)
		{
			ItemsGuard.Saw("UncheckedAccept", item, (GraphNodeStorage)(object)__instance);
		}
	}

	private static void Postfix(GameInventory __instance, GameItem item, bool __result, ItemsMoveRecorder.InvState __state)
	{
		if (__state != null)
		{
			ItemsMoveRecorder.AfterInvAccept(__instance, item, __result, __state);
		}
	}

	private static void Finalizer(ItemsMoveRecorder.InvState __state)
	{
		if (__state != null)
		{
			ItemsMoveRecorder.Leave(__state.mode);
		}
	}
}
