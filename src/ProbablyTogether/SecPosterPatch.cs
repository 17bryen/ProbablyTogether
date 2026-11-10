using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecPosterPatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(StorePoster), "OnClick", Type.EmptyTypes, (Type[])null), "StorePoster.OnClick");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(StorePoster __instance)
	{
		if (!Net.IsClient || Net.Applying)
		{
			return true;
		}
		Security.RequestPoster(!__instance.isPulled);
		return false;
	}
}
