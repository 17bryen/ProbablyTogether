using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CustOnNextClientArrivedPatch
{
	private static readonly MethodBase target = CustomersHook.Find(() => AccessTools.Method(typeof(StoreUIManager), "OnNextClientArrived", Type.EmptyTypes, (Type[])null), "StoreUIManager.OnNextClientArrived");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(out Customers.Scope __state)
	{
		__state = null;
		if (!Net.IsOnline)
		{
			return true;
		}
		if (Net.IsRemote)
		{
			return Net.Applying;
		}
		StoreClientInstance currentClientInstance = PlayerStore.Instance.currentClientInstance;
		if (Net.Applying || currentClientInstance == null)
		{
			return true;
		}
		__state = Customers.Open();
		return true;
	}

	private static void Postfix(Customers.Scope __state)
	{
		if (Net.IsOnline && CustomersGate.Allow())
		{
			StoreClientInstance currentClientInstance = PlayerStore.Instance.currentClientInstance;
			Customers.ArrivedFor = ((currentClientInstance != null) ? currentClientInstance.GetClientBlueprint() : null);
			if (__state != null)
			{
				__state.Close();
				Customers.AfterServerArrived(__state);
			}
		}
	}

	private static Exception Finalizer(Exception __exception, Customers.Scope __state)
	{
		__state?.Close();
		if (__exception != null)
		{
			Log.Error("customers: OnNextClientArrived threw: " + __exception);
		}
		return __exception;
	}
}
