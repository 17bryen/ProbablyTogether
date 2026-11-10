using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class DlgRunMorningDialogue
{
	private static MethodBase Target()
	{
		return AccessTools.Method(typeof(StoreUIManager), "RunMorningDialogue", Type.EmptyTypes, (Type[])null);
	}

	private static bool Prepare()
	{
		return Target() != null;
	}

	private static MethodBase TargetMethod()
	{
		return Target();
	}

	private static bool Prefix()
	{
		if (!Net.IsClient)
		{
			return true;
		}
		DialogueSystem.MorningSkipped++;
		return false;
	}
}
