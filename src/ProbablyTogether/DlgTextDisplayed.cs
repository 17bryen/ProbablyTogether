using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(DialogUIManager), "OnCurrentTextDisplayed")]
internal static class DlgTextDisplayed
{
	private static bool Prefix()
	{
		if (!Net.IsOnline)
		{
			return true;
		}
		if (Net.IsClient)
		{
			CounterChannel counter = DialogueSystem.Counter;
			if (counter.LineUnfinished)
			{
				DialogueSync.RequestShown("counter", counter.SkipCurrent);
			}
		}
		return false;
	}
}
