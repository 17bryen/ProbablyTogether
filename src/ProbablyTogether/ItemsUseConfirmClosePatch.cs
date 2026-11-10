using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUseConfirmClosePatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(GenericConfirmationUIManager), "ClosePanel", Type.EmptyTypes, (Type[])null);

	private static bool Prepare()
	{
		return ItemsUseHook.Found(target, "GenericConfirmationUIManager.ClosePanel");
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix()
	{
		return ItemsUse.MayClosePanel();
	}
}
