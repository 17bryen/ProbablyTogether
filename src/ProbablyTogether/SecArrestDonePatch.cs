using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecArrestDonePatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(StoreUIManager), "OnArrestDialoguePlayed", Type.EmptyTypes, (Type[])null), "StoreUIManager.OnArrestDialoguePlayed");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(out IDisposable __state)
	{
		__state = null;
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (Net.IsRemote)
		{
			return false;
		}
		__state = Security.BeginFollow("sec.arrestDone", 0, 0L);
		return true;
	}

	private static Exception Finalizer(Exception __exception, IDisposable __state)
	{
		__state?.Dispose();
		return __exception;
	}
}
