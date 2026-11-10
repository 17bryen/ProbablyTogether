using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CustTryCallNextClientPatch
{
	private static readonly MethodBase target = CustomersHook.Find(() => AccessTools.Method(typeof(PlayerStore), "TryCallNextClient", Type.EmptyTypes, (Type[])null), "PlayerStore.TryCallNextClient");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(PlayerStore __instance, out Customers.Scope __state)
	{
		__state = null;
		if (!Net.IsOnline)
		{
			return true;
		}
		if (Net.IsClient)
		{
			if (Net.Applying)
			{
				return true;
			}
			if (Customers.InBell)
			{
				Customers.RequestCall();
				return false;
			}
			if (!Net.IsHost)
			{
				return false;
			}
		}
		if (Net.Applying)
		{
			return true;
		}
		__state = Customers.Open();
		__state.Before = __instance.currentClientInstance;
		return true;
	}

	private static void Postfix(Customers.Scope __state)
	{
		if (__state != null)
		{
			__state.Close();
			Customers.AfterServerCall(__state);
		}
	}

	private static Exception Finalizer(Exception __exception, Customers.Scope __state)
	{
		__state?.Close();
		if (__exception != null)
		{
			Log.Error("customers: TryCallNextClient threw: " + __exception);
		}
		return __exception;
	}
}
