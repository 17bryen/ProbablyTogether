using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(DialogUIManager), "SelectChoice")]
internal static class DlgSelectChoice
{
	private static bool Prefix(DialogUIManager __instance, int index, out DialogueSystem.Step __state)
	{
		__state = null;
		if (!Net.IsOnline)
		{
			return true;
		}
		if (Net.IsServer)
		{
			Dialogue cur = __instance.currentDialog;
			if (cur != null && index >= 0 && index < cur.choices.Count && cur.IsChoiceEnabledAt(index))
			{
				__state = DialogueSystem.Step.Begin(DialogueSystem.Counter, "choose", delegate(MsgDlgStep m)
				{
					m.index = index;
					m.count = cur.choices.Count;
					m.fp = DialogueRefs.Fp(cur);
					m.cfp = DialogueRefs.Fp(cur.choices[index].Item2);
				});
			}
			return true;
		}
		if (Net.Applying)
		{
			return true;
		}
		DialogueSync.RequestChoose("counter", index);
		return false;
	}

	private static Exception Finalizer(Exception __exception, DialogueSystem.Step __state)
	{
		return DialoguePatchUtil.Close(__exception, __state);
	}
}
