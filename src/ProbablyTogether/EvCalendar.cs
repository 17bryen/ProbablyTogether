using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(AdvCalendarUIManager), "HandleInnateEvent")]
internal static class EvCalendar
{
	private static bool Prefix()
	{
		if (!DialoguePatchUtil.ClientBlocks)
		{
			return true;
		}
		if (DialogueSystem.CanRequest)
		{
			Net.ToServer("events.calendar", new MsgCalendar
			{
				day = Game.Day
			});
		}
		return false;
	}
}
