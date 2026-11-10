using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecNightVandalismPatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(PlayerStore), "HandleVandalism", Type.EmptyTypes, (Type[])null), "PlayerStore.HandleVandalism");

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
