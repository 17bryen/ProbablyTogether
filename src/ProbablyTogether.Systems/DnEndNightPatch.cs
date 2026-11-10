using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class DnEndNightPatch
{
	private static readonly MethodBase target = DayNightGate.Find(() => AccessTools.Method(typeof(PlayerStore), "EndNight", Type.EmptyTypes, (Type[])null), "PlayerStore.EndNight");

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
		return !DayNightGate.LocalCommit;
	}
}
