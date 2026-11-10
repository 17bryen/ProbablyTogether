using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.UI;

[HarmonyPatch]
internal static class EscapeMenuOpenPatch
{
	private static MethodBase Target()
	{
		return AccessTools.Method(typeof(EscapeUIManager), "OpenEscapeMenu", Type.EmptyTypes, (Type[])null);
	}

	private static bool Prepare()
	{
		return UiPatchTargets.Found(Target(), "EscapeUIManager.OpenEscapeMenu()");
	}

	private static MethodBase TargetMethod()
	{
		return Target();
	}

	private static void Postfix(EscapeUIManager __instance)
	{
		try
		{
			EscapeMenu.OnOpened(__instance);
		}
		catch (Exception e)
		{
			UiErrors.Report("escape menu", e);
		}
	}
}
