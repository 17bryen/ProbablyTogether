using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecExitShootingPatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(StoreUIManager), "ExitShootingMode", Type.EmptyTypes, (Type[])null), "StoreUIManager.ExitShootingMode");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(StoreUIManager __instance)
	{
		if (!Net.IsOnline)
		{
			return true;
		}
		if (!__instance.isShootingMode)
		{
			return false;
		}
		if (Net.IsClient && !Net.Applying)
		{
			if (!__instance.isShot && Game.Store?.currentClientInstance != null)
			{
				Security.RequestHolster();
			}
			Security.SendAim(on: false);
		}
		return true;
	}
}
