using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecOfficerStartArrivePatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(GenericClientMono), "StartArrive", Type.EmptyTypes, (Type[])null), "GenericClientMono.StartArrive");

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
		if (Net.IsClient && !Net.Applying && Security.ClientOfficersHere)
		{
			return false;
		}
		return true;
	}

	private static void Postfix(GenericClientMono __instance)
	{
		Security.OfficerWalkStarted(__instance, arriving: true);
	}
}
