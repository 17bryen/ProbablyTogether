using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CustTryPlayerDismissPatch
{
	private static readonly MethodBase target = CustomersHook.Find(() => AccessTools.Method(typeof(PlayerStore), "TryPlayerDismissCurrentClient", new Type[1] { typeof(bool) }, (Type[])null), "PlayerStore.TryPlayerDismissCurrentClient");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(PlayerStore __instance, bool callNextWhenPossible, out Customers.Scope __state)
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
			Customers.RequestDismiss(callNextWhenPossible);
			return false;
		}
		if (Net.Applying || __instance.currentClientInstance == null)
		{
			return true;
		}
		__state = Customers.Open();
		__state.Before = __instance.currentClientInstance;
		__state.V = Customers.Visit;
		__state.Id = __instance.currentClientInstance.GetClientBlueprint()?.identifier;
		__state.By = Locks.Holder("counter");
		Customers.PendingGone = null;
		return true;
	}

	private static void Postfix(bool callNextWhenPossible, Customers.Scope __state)
	{
		if (__state != null)
		{
			__state.Close();
			Customers.AfterServerPlayerDismiss(__state, callNextWhenPossible);
		}
	}

	private static Exception Finalizer(Exception __exception, Customers.Scope __state)
	{
		__state?.Close();
		if (__exception != null)
		{
			Log.Error("customers: TryPlayerDismissCurrentClient threw: " + __exception);
		}
		return __exception;
	}
}
