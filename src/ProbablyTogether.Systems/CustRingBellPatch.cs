using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CustRingBellPatch
{
	private static readonly MethodBase target = CustomersHook.Find(() => AccessTools.Method(typeof(StoreBell), "RingBell", Type.EmptyTypes, (Type[])null), "StoreBell.RingBell");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Prefix()
	{
		Customers.InBell = true;
	}

	private static Exception Finalizer(Exception __exception)
	{
		Customers.InBell = false;
		return __exception;
	}
}
