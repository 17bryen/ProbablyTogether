using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecOfficerStartLeavePatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(GenericClientMono), "StartLeave", Type.EmptyTypes, (Type[])null), "GenericClientMono.StartLeave");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix(GenericClientMono __instance)
	{
		if (((Component)__instance).gameObject.activeSelf)
		{
			Security.OfficerWalkStarted(__instance, arriving: false);
		}
	}
}
