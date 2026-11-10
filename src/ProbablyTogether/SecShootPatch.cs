using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecShootPatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(StoreUIManager), "Shoot", Type.EmptyTypes, (Type[])null), "StoreUIManager.Shoot");

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
		Security.RequestShoot();
		return false;
	}
}
