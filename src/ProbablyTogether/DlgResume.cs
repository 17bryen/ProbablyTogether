using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(DialogUIManager), "ResumeIfWaitingForAdvance")]
internal static class DlgResume
{
	private static bool Prefix()
	{
		if (!DialoguePatchUtil.ClientBlocks)
		{
			return true;
		}
		CounterChannel counter = DialogueSystem.Counter;
		if (counter.WaitingManual && !counter.IsChoiceNow && !counter.LineUnfinished && DialogueSystem.MayPaceQuiet(counter))
		{
			DialogueSync.RequestAdvance("counter");
		}
		return false;
	}
}
