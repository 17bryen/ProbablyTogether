using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecCheckCrimePatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(PlayerStore), "CheckCrime", Type.EmptyTypes, (Type[])null), "PlayerStore.CheckCrime");

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
