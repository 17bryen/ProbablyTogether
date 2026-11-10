using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsQuickTransferPatch
{
	private static readonly List<MethodBase> targets = Targets();

	private static List<MethodBase> Targets()
	{
		List<MethodBase> list = new List<MethodBase>();
		MethodBase methodBase = ItemsHook.Check(() => AccessTools.Method(typeof(ItemQuickTransferHandler), "OnEventRelease", (Type[])null, (Type[])null), "ItemQuickTransferHandler.OnEventRelease");
		if (methodBase != null && methodBase.DeclaringType == typeof(ItemQuickTransferHandler))
		{
			list.Add(methodBase);
		}
		MethodBase methodBase2 = ItemsHook.Check(() => AccessTools.Method(typeof(ItemQuickTransferAltHandler), "OnEventRelease", (Type[])null, (Type[])null), "ItemQuickTransferAltHandler.OnEventRelease");
		if (methodBase2 != null && methodBase2.DeclaringType == typeof(ItemQuickTransferAltHandler))
		{
			list.Add(methodBase2);
		}
		MethodBase methodBase3 = ItemsHook.Check(() => AccessTools.Method(typeof(ItemQuickTransferAltHandler1), "OnEventRelease", (Type[])null, (Type[])null), "ItemQuickTransferAltHandler1.OnEventRelease");
		if (methodBase3 != null && methodBase3.DeclaringType == typeof(ItemQuickTransferAltHandler1))
		{
			list.Add(methodBase3);
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

	private static void Prefix(out bool __state)
	{
		__state = ItemsCapture.Enter("quick", out var _);
	}

	private static void Finalizer(bool __state)
	{
		ItemsCapture.Exit(__state);
	}
}
