using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUseConfirmSinglePatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(GenericConfirmationUIManager), "OpenPanelSingleButton", new Type[3]
	{
		typeof(string),
		typeof(string),
		typeof(Action)
	}, (Type[])null);

	private static bool Prepare()
	{
		return ItemsUseHook.Found(target, "GenericConfirmationUIManager.OpenPanelSingleButton");
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(ref Action onConfirm)
	{
		Action onCancel = null;
		return ItemsUse.OnConfirmPanel(ref onConfirm, ref onCancel, single: true);
	}
}
