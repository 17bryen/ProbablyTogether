using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsDestroyPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(GameItemElement), "Destroy", (Type[])null, (Type[])null), "GameItemElement.Destroy");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Prefix(GameItemElement __instance, out int __state)
	{
		__state = 0;
		if (Net.IsServer)
		{
			__state = ItemsMoveRecorder.BeforeDestroy(__instance);
		}
		else if (Net.IsClient && !Net.Applying && !__instance.IsDestroyed())
		{
			ItemsGuard.Saw("Destroy", (GameItem)(object)__instance, (GraphNodeStorage)(object)((GameItem)__instance).parentInventory);
		}
	}

	private static void Finalizer(int __state)
	{
		ItemsMoveRecorder.Leave(__state);
	}
}
