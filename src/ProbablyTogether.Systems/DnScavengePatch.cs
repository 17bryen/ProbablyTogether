using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class DnScavengePatch
{
	private static readonly MethodBase target = DayNightGate.Find(() => AccessTools.Method(typeof(ScavHelper), "ScavengeDumpingGrounds", Type.EmptyTypes, (Type[])null), "ScavHelper.ScavengeDumpingGrounds");

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
		if (!DayNightGate.LocalCommit)
		{
			return true;
		}
		if (ScavHelper.CanScavenge())
		{
			DayNight.Send("dayNight.scavenge", new MsgDnEmpty());
		}
		return false;
	}
}
