using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.UI;

[HarmonyPatch]
internal static class UiKeyDownPatch
{
	private static MethodBase Target()
	{
		return AccessTools.Method(typeof(KeyBindHelper), "GetKeyDown", new Type[1] { typeof(string) }, (Type[])null);
	}

	private static bool Prepare()
	{
		return UiPatchTargets.Found(Target(), "KeyBindHelper.GetKeyDown(string)");
	}

	private static MethodBase TargetMethod()
	{
		return Target();
	}

	private static bool Prefix(ref bool __result)
	{
		if (!Net.IsClient || !Chat.OwnsKeyboard)
		{
			return true;
		}
		__result = false;
		return false;
	}
}
