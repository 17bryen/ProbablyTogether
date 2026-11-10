using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(PhoneUIManager), "StopCall")]
internal static class PhStopCall
{
	private static bool Prefix(out DialogueSystem.Step __state)
	{
		__state = null;
		if (!Net.IsOnline)
		{
			return true;
		}
		if (Net.IsServer)
		{
			__state = DialogueSystem.Step.Begin(DialogueSystem.Phone, "hangup");
			return true;
		}
		if (Net.Applying)
		{
			return true;
		}
		if (!PhoneChannel.Talking)
		{
			return true;
		}
		if (DialogueSystem.IsPacer(DialogueSystem.Phone))
		{
			DialogueSync.RequestHangup();
		}
		return false;
	}

	private static void Postfix()
	{
		if (Net.IsServer)
		{
			DialogueSystem.ServerCallEnded();
		}
	}

	private static Exception Finalizer(Exception __exception, DialogueSystem.Step __state)
	{
		return DialoguePatchUtil.Close(__exception, __state);
	}
}
