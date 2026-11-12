using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(DialoguePhoneUIManager), "SelectChoice")]
internal static class PhSelectChoice
{
	private static bool Prefix(DialoguePhoneUIManager __instance, int index, out DialogueSystem.Step __state)
	{
		DialoguePatchUtil.EnterHome();
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
				__state = DialogueSystem.Step.Begin(DialogueSystem.Phone, "choose", delegate(MsgDlgStep m)
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
		DialogueSync.RequestChoose("phone", index);
		return false;
	}

	private static Exception Finalizer(Exception __exception, DialogueSystem.Step __state)
	{
		return DialoguePatchUtil.ClosePhone(__exception, __state);
	}
}
