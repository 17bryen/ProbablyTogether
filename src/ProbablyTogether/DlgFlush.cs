using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(DialogUIManager), "FlushScheduledEndActions")]
internal static class DlgFlush
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
			List<Dialogue> scheduled = DialogueSystem.Counter.Scheduled;
			if (scheduled != null && scheduled.Count > 0)
			{
				__state = DialogueSystem.Step.Begin(DialogueSystem.Counter, "flush");
			}
			return true;
		}
		return Net.Applying;
	}

	private static Exception Finalizer(Exception __exception, DialogueSystem.Step __state)
	{
		return DialoguePatchUtil.Close(__exception, __state);
	}
}
