using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(DialogUIManager), "StartDialogue")]
internal static class DlgStartDialogue
{
	private static bool Prefix(Dialogue dialogue, bool forcedStart, ref bool __result, out DialogueSystem.Step __state)
	{
		__state = null;
		if (!Net.IsOnline || dialogue == null)
		{
			return true;
		}
		if (Net.IsServer)
		{
			if (!CounterChannel.WouldRefuse(dialogue, forcedStart))
			{
				__state = DialogueSystem.Step.Begin(DialogueSystem.Counter, "start", delegate(MsgDlgStep m)
				{
					DialogueSystem.DescribeStart(DialogueSystem.Counter, m, dialogue);
					m.forced = forcedStart;
				});
			}
			return true;
		}
		if (Net.Applying)
		{
			return true;
		}
		if (AimReaction.IsLocalOnly(dialogue))
		{
			AimReaction.LocalDropped++;
			__result = false;
			return false;
		}
		DialogueRefs.AddOffer(DialogueSystem.Counter, dialogue);
		__result = false;
		return false;
	}

	private static void Postfix(Dialogue dialogue, bool __result)
	{
		if (__result && Net.IsOnline)
		{
			DialogueSystem.Counter.PushRoot(dialogue);
		}
	}

	private static Exception Finalizer(Exception __exception, DialogueSystem.Step __state)
	{
		return DialoguePatchUtil.Close(__exception, __state);
	}
}
