using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecOfficerOnLeftPatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(GenericClientMono), "OnLeft", Type.EmptyTypes, (Type[])null), "GenericClientMono.OnLeft");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Prefix(GenericClientMono __instance)
	{
		Security.CurrentOfficer = __instance;
		if (((Component)__instance).gameObject.activeSelf)
		{
			Security.OfficerWalkEnded(__instance, arrived: false);
		}
	}

	private static Exception Finalizer(Exception __exception)
	{
		Security.CurrentOfficer = null;
		return __exception;
	}
}
