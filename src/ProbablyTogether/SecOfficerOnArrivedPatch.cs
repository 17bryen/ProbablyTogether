using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecOfficerOnArrivedPatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(GenericClientMono), "OnArrived", Type.EmptyTypes, (Type[])null), "GenericClientMono.OnArrived");

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
		Security.OfficerWalkEnded(__instance, arrived: true);
	}

	private static Exception Finalizer(Exception __exception)
	{
		Security.CurrentOfficer = null;
		return __exception;
	}
}
