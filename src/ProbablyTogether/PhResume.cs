using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(DialoguePhoneUIManager), "ResumeIfWaitingForAdvance")]
internal static class PhResume
{
	private static bool Prefix()
	{
		if (!DialoguePatchUtil.ClientBlocks)
		{
			return true;
		}
		PhoneChannel phone = DialogueSystem.Phone;
		if (phone.WaitingManual && !phone.IsChoiceNow && !phone.LineUnfinished && DialogueSystem.MayPaceQuiet(phone))
		{
			DialogueSync.RequestAdvance("phone");
		}
		return false;
	}
}
