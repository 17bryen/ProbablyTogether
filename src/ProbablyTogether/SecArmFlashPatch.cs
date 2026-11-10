using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecArmFlashPatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(Arm), "TriggerMuzzleFlash", Type.EmptyTypes, (Type[])null), "Arm.TriggerMuzzleFlash");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(Arm __instance)
	{
		if (Net.IsOnline)
		{
			return ((Component)__instance).gameObject.activeInHierarchy;
		}
		return true;
	}
}
