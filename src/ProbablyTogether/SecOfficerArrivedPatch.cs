using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SecOfficerArrivedPatch
{
	private static readonly MethodBase target = SecHook.Check(() => AccessTools.Method(typeof(StoreUIManager), "OnGenericArrived", new Type[1] { typeof(string) }, (Type[])null), "StoreUIManager.OnGenericArrived");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(string id, out IDisposable __state)
	{
		__state = null;
		if (!Net.IsOnline || Net.Applying || id != "SEC")
		{
			return true;
		}
		if (Net.IsRemote)
		{
			return false;
		}
		__state = Security.BeginFollow("sec.arrived", Security.OfficerIndex(Security.CurrentOfficer), 0L);
		return true;
	}

	private static Exception Finalizer(Exception __exception, IDisposable __state)
	{
		__state?.Dispose();
		return __exception;
	}
}
