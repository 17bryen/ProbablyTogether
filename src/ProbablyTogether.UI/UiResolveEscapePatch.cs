using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.UI;

[HarmonyPatch]
internal static class UiResolveEscapePatch
{
	private static MethodBase Target()
	{
		return AccessTools.Method(typeof(StoreUIManager), "ResolveEscape", Type.EmptyTypes, (Type[])null);
	}

	private static bool Prepare()
	{
		return UiPatchTargets.Found(Target(), "StoreUIManager.ResolveEscape()");
	}

	private static MethodBase TargetMethod()
	{
		return Target();
	}

	private static bool Prefix()
	{
		if (Net.IsClient)
		{
			return !Chat.OwnsKeyboard;
		}
		return true;
	}
}
