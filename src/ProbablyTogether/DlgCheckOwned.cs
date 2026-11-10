using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(DialogUIManager), "CheckOwned")]
internal static class DlgCheckOwned
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
			__state = DialogueSystem.Step.Begin(DialogueSystem.Counter, "checkOwned");
			return true;
		}
		return Net.Applying;
	}

	private static Exception Finalizer(Exception __exception, DialogueSystem.Step __state)
	{
		return DialoguePatchUtil.Close(__exception, __state);
	}
}
