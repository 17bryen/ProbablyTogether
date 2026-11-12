using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(PhoneUIManager), "StartPhoneDialog")]
internal static class PhStartCall
{
	private static bool Prefix(PhoneUIManager __instance, long currentNumber, out DialogueSystem.Step __state)
	{
		DialoguePatchUtil.EnterHome();
		__state = null;
		if (!Net.IsOnline)
		{
			return true;
		}
		if (Net.IsServer)
		{
			if (StorePhoneClient.IsNumberExist(currentNumber))
			{
				__state = DialogueSystem.Step.Begin(DialogueSystem.Phone, "call", delegate(MsgDlgStep m)
				{
					m.number = currentNumber;
					m.quick = __instance.isQuickDialCall;
				});
			}
			return true;
		}
		if (Net.Applying)
		{
			return true;
		}
		// The host's dial goes through the server like any player's call, so the call belongs to the host.
		DialogueSync.RequestCall(currentNumber, __instance.isQuickDialCall);
		return false;
	}

	private static Exception Finalizer(Exception __exception, DialogueSystem.Step __state)
	{
		return DialoguePatchUtil.ClosePhone(__exception, __state);
	}
}
