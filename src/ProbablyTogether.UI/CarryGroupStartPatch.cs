using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.UI;

[HarmonyPatch]
internal static class CarryGroupStartPatch
{
	private static readonly MethodBase target = CarryHook.Check(() => AccessTools.Method(typeof(ItemMultiSelectHandler), "StartGroupDrag", new Type[0], (Type[])null), "ItemMultiSelectHandler.StartGroupDrag");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix(ItemMultiSelectHandler __instance)
	{
		if (!Net.HasLocalPlayer || (Object)(object)__instance == (Object)null)
		{
			return;
		}
		try
		{
			if (__instance.IsGroupDragging)
			{
				Carry.OnLocalDragStart(Carry.GroupLead(__instance, out var _), group: true);
			}
		}
		catch (Exception e)
		{
			UiErrors.Report("carry group start", e);
		}
	}
}
