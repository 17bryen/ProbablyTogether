using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsPinPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(ItemPinHelper), "TogglePinned", (Type[])null, (Type[])null), "ItemPinHelper.TogglePinned");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem item, ref bool __result)
	{
		if (!ItemsCapture.ClientActive || item == null)
		{
			return true;
		}
		bool flag = !ItemPinHelper.IsPinned(item);
		ItemsMove.Pin(item, flag, ItemsCapture.Current?.kind ?? "pin");
		__result = flag;
		return false;
	}
}
