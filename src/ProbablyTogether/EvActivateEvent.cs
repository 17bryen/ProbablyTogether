using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(StoreEventManager), "ActivateEvent")]
internal static class EvActivateEvent
{
	private static bool Prefix()
	{
		if (Net.IsOnline)
		{
			return DialogueEvents.inNewspaper <= 0;
		}
		return true;
	}
}
