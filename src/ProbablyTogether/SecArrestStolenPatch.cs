using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecArrestStolenPatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(PlayerStore), "ArrestCurrentClientForStolenGoods", Type.EmptyTypes, (Type[])null), "PlayerStore.ArrestCurrentClientForStolenGoods");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(ref bool __result, out IDisposable __state)
	{
		__state = null;
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (Net.IsRemote)
		{
			__result = Security.LocalStolenArrestResult();
			return false;
		}
		__state = Security.BeginFollow("sec.arrestStolen", 0, Security.FindUnownedCrate());
		return true;
	}

	private static Exception Finalizer(Exception __exception, IDisposable __state)
	{
		__state?.Dispose();
		return __exception;
	}
}
