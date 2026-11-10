using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class DnBeginDayPatch
{
	private static readonly MethodBase target = DayNightGate.Find(() => AccessTools.Method(typeof(PlayerStore), "BeginDay", Type.EmptyTypes, (Type[])null), "PlayerStore.BeginDay");

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

	private static void Postfix(PlayerStore __instance)
	{
		if (Net.IsServer && DayNight.LabNightEnding != null)
		{
			string labNightEnding = DayNight.LabNightEnding;
			DayNight.LabNightEnding = null;
			__instance.ExecuteGameOver(labNightEnding);
		}
	}
}
