using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(DialoguePhoneUIManager), "OnCurrentTextDisplayed")]
internal static class PhTextDisplayed
{
	private static bool Prefix()
	{
		DialoguePatchUtil.EnterHome();
		if (!Net.IsOnline)
		{
			return true;
		}
		if (Net.IsClient)
		{
			PhoneChannel phone = DialogueSystem.Phone;
			if (phone.LineUnfinished)
			{
				DialogueSync.RequestShown("phone", phone.SkipCurrent);
			}
		}
		return false;
	}

	private static void Finalizer()
	{
		DialoguePatchUtil.LeaveHome();
	}
}
