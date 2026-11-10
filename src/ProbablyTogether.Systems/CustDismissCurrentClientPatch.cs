using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CustDismissCurrentClientPatch
{
	private static readonly MethodBase target = CustomersHook.Find(() => AccessTools.Method(typeof(PlayerStore), "DismissCurrentClient", new Type[3]
	{
		typeof(bool),
		typeof(bool),
		typeof(bool)
	}, (Type[])null), "PlayerStore.DismissCurrentClient");

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
		if (Net.IsRemote)
		{
			return Net.Applying;
		}
		StoreClientInstance currentClientInstance = __instance.currentClientInstance;
		if (currentClientInstance == null)
		{
			return true;
		}
		Customers.ApplyAuto();
		bool flag = !Net.Applying;
		__state = (flag ? Customers.Open() : new Customers.Scope
		{
			Done = true
		});
		__state.Had = true;
		__state.Top = flag;
		__state.Before = currentClientInstance;
		__state.V = Customers.Visit;
		__state.Id = currentClientInstance.GetClientBlueprint()?.identifier;
		__state.Gone = Customers.CaptureGone();
		return true;
	}

	private static void Postfix(PlayerStore __instance, bool callNextWhenPossible, bool right, bool interruptDialog, Customers.Scope __state)
	{
		if (__state != null && __state.Had)
		{
			if (__instance.currentClientInstance == __state.Before)
			{
				__state.Close();
				return;
			}
			__state.Close();
			Customers.AfterServerDismiss(__state, callNextWhenPossible, right, interruptDialog);
		}
	}

	private static Exception Finalizer(Exception __exception, Customers.Scope __state)
	{
		__state?.Close();
		if (__exception != null)
		{
			Log.Error("customers: DismissCurrentClient threw: " + __exception);
		}
		return __exception;
	}
}
