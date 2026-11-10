using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUseConfirmPanelPatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(GenericConfirmationUIManager), "OpenPanel", new Type[5]
	{
		typeof(string),
		typeof(string),
		typeof(string),
		typeof(Action),
		typeof(Action)
	}, (Type[])null);

	private static bool Prepare()
	{
		return ItemsUseHook.Found(target, "GenericConfirmationUIManager.OpenPanel");
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(ref Action onConfirm, ref Action onCancel)
	{
		return ItemsUse.OnConfirmPanel(ref onConfirm, ref onCancel, single: false);
	}
}
