using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(DialoguePhoneUIManager), "StartDialogue")]
internal static class PhStartDialogue
{
	private static bool Prefix(Dialogue dialogue, out DialogueSystem.Step __state)
	{
		DialoguePatchUtil.EnterHome();
		__state = null;
		if (!Net.IsOnline || dialogue == null)
		{
			return true;
		}
		if (Net.IsServer)
		{
			__state = DialogueSystem.Step.Begin(DialogueSystem.Phone, "start", delegate(MsgDlgStep m)
			{
				DialogueSystem.DescribeStart(DialogueSystem.Phone, m, dialogue);
			});
			return true;
		}
		if (Net.Applying)
		{
			return true;
		}
		DialogueRefs.AddOffer(DialogueSystem.Phone, dialogue);
		return false;
	}

	private static void Postfix(DialoguePhoneUIManager __instance, Dialogue dialogue)
	{
		if (Net.IsOnline && dialogue != null && __instance.currentDialog == dialogue)
		{
			DialogueSystem.Phone.PushRoot(dialogue);
		}
	}

	private static Exception Finalizer(Exception __exception, DialogueSystem.Step __state)
	{
		return DialoguePatchUtil.ClosePhone(__exception, __state);
	}
}
