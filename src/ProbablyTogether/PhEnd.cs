using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(DialoguePhoneUIManager), "EndConversation")]
internal static class PhEnd
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
			__state = DialogueSystem.Step.Begin(DialogueSystem.Phone, "end");
			return true;
		}
		return Net.Applying;
	}

	private static Exception Finalizer(Exception __exception, DialogueSystem.Step __state)
	{
		return DialoguePatchUtil.ClosePhone(__exception, __state);
	}
}
