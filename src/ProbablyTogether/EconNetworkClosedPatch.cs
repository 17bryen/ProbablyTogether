using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class EconNetworkClosedPatch
{
	private static readonly MethodBase target = EconomyHook.Find(() => AccessTools.Method(typeof(WildNetworkUI), "OnWindowClosed", Type.EmptyTypes, (Type[])null), "WildNetworkUI.OnWindowClosed");

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
		Economy.OnWindowClosedClient();
		return false;
	}
}
