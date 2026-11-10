using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class EconCommissaryClosePatch
{
	private static readonly MethodBase target = EconomyHook.Find(() => AccessTools.Method(typeof(CommissaryUIManager), "CloseUI", Type.EmptyTypes, (Type[])null), "CommissaryUIManager.CloseUI");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix()
	{
		if (Net.IsClient)
		{
			Economy.ClientWindowClosed();
		}
	}
}
