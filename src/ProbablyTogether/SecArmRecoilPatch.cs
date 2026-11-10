using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecArmRecoilPatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(Arm), "TriggerRecoil", Type.EmptyTypes, (Type[])null), "Arm.TriggerRecoil");

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
