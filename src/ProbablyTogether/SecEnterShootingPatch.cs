using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecEnterShootingPatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(StoreUIManager), "EnterShootingMode", Type.EmptyTypes, (Type[])null), "StoreUIManager.EnterShootingMode");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix()
	{
		if (!Net.IsClient || Net.Applying)
		{
			return true;
		}
		return Security.AimAllowed();
	}

	private static void Postfix()
	{
		if (Net.IsClient && !Net.Applying)
		{
			Security.SendAim(on: true);
		}
	}
}
