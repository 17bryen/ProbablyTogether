using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUnloadPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(GameItem), "TryUnloadAll", new Type[1] { typeof(bool) }, (Type[])null), "GameItem.TryUnloadAll");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem __instance, ref GameItem __result)
	{
		ItemsCapture.Ctx current = ItemsCapture.Current;
		if (!ItemsCapture.ClientActive || current == null || current.kind != "menu")
		{
			return true;
		}
		ItemsMove.UnloadAll(__instance, "menu");
		__result = __instance;
		return false;
	}
}
