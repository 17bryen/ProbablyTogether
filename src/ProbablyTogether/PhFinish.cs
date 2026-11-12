using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(DialoguePhoneUIManager), "FinishConversationNow")]
internal static class PhFinish
{
	private static bool Prefix(out DialogueSystem.Step __state)
	{
		DialoguePatchUtil.EnterHome();
		__state = null;
		if (!Net.IsOnline)
		{
			return true;
		}
		if (Net.IsServer)
		{
			__state = DialogueSystem.Step.Begin(DialogueSystem.Phone, "finish");
			return true;
		}
		if (Net.Applying)
		{
			return true;
		}
		DialogueSync.RequestAdvance("phone");
		return false;
	}

	private static Exception Finalizer(Exception __exception, DialogueSystem.Step __state)
	{
		return DialoguePatchUtil.ClosePhone(__exception, __state);
	}
}
