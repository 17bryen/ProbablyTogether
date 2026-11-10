using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.UI;

[HarmonyPatch]
internal static class CarryDragStartPatch
{
	private static readonly MethodBase target = CarryHook.Check(() => AccessTools.Method(typeof(ItemMouseDragHandler), "StartDrag", new Type[0], (Type[])null), "ItemMouseDragHandler.StartDrag");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix(ItemMouseDragHandler __instance)
	{
		if (!Net.HasLocalPlayer || (Object)(object)__instance == (Object)null)
		{
			return;
		}
		try
		{
			if (__instance.IsDraggingItem)
			{
				Carry.OnLocalDragStart(__instance.GetCurrentItem(), group: false);
			}
		}
		catch (Exception e)
		{
			UiErrors.Report("carry start", e);
		}
	}
}
