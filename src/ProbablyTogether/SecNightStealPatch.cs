using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecNightStealPatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(PlayerStore), "StealItem", new Type[1] { typeof(GameItem) }, (Type[])null), "PlayerStore.StealItem");

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
		if (Net.IsRemote)
		{
			return Net.Applying;
		}
		return true;
	}
}
