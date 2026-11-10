using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecInspectionPatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(ContrabandHelper), "StartInspection", new Type[2]
	{
		typeof(int),
		typeof(bool)
	}, (Type[])null), "ContrabandHelper.StartInspection");

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
