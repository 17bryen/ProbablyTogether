using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecEvidenceDayPatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(SecData), "OnNewDay", Type.EmptyTypes, (Type[])null), "SecData.OnNewDay");

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
