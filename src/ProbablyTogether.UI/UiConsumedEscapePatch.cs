using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.UI;

[HarmonyPatch]
internal static class UiConsumedEscapePatch
{
	private static MethodBase Target()
	{
		return AccessTools.Method(typeof(GameOptionUIManager), "ConsumedEscapeThisFrame", Type.EmptyTypes, (Type[])null);
	}

	private static bool Prepare()
	{
		return UiPatchTargets.Found(Target(), "GameOptionUIManager.ConsumedEscapeThisFrame()");
	}

	private static MethodBase TargetMethod()
	{
		return Target();
	}

	private static void Postfix(ref bool __result)
	{
		if (!__result && Net.IsClient && Chat.OwnsKeyboard)
		{
			__result = true;
		}
	}
}
