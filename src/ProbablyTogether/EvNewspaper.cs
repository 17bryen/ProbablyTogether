using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(NewsUIManager), "PopulateUI")]
internal static class EvNewspaper
{
	private static void Prefix(out bool __state)
	{
		__state = Net.IsOnline;
		if (__state)
		{
			DialogueEvents.inNewspaper++;
		}
	}

	private static Exception Finalizer(Exception __exception, bool __state)
	{
		if (__state)
		{
			DialogueEvents.inNewspaper--;
		}
		return __exception;
	}
}
