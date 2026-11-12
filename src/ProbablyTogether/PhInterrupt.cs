using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(DialoguePhoneUIManager), "InterruptDialog")]
internal static class PhInterrupt
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
			__state = DialogueSystem.Step.Begin(DialogueSystem.Phone, "interrupt");
			return true;
		}
		return Net.Applying;
	}

	private static Exception Finalizer(Exception __exception, DialogueSystem.Step __state)
	{
		return DialoguePatchUtil.ClosePhone(__exception, __state);
	}
}
