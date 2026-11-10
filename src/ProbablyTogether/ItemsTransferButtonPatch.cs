using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsTransferButtonPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(StoreTransferButton), "OnClick", (Type[])null, (Type[])null), "StoreTransferButton.OnClick");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Prefix(out bool __state)
	{
		__state = ItemsCapture.Enter("button", out var _);
		if (__state)
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			ItemsMove.TransferButton((Object)(object)instance != (Object)null && instance.frontInvWindow != null && instance.frontInvWindow.IsVisible(), "button");
		}
	}

	private static void Finalizer(bool __state)
	{
		ItemsCapture.Exit(__state);
	}
}
