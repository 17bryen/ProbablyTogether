using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecReportPatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(PlayerStore), "OnCustomerReported", Type.EmptyTypes, (Type[])null), "PlayerStore.OnCustomerReported");

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
		if (!Net.IsClient || Net.Applying)
		{
			return true;
		}
		Security.RequestReport();
		return false;
	}
}
