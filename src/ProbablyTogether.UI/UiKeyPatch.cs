using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.UI;

[HarmonyPatch]
internal static class UiKeyPatch
{
	private static MethodBase Target()
	{
		return AccessTools.Method(typeof(KeyBindHelper), "GetKey", new Type[1] { typeof(string) }, (Type[])null);
	}

	private static bool Prepare()
	{
		return UiPatchTargets.Found(Target(), "KeyBindHelper.GetKey(string)");
	}

	private static MethodBase TargetMethod()
	{
		return Target();
	}

	private static bool Prefix(ref bool __result)
	{
		if (!Net.HasLocalPlayer || !Chat.OwnsKeyboard)
		{
			return true;
		}
		__result = false;
		return false;
	}
}
