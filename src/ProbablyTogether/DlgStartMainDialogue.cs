using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class DlgStartMainDialogue
{
	private static MethodBase Target()
	{
		return AccessTools.Method(typeof(StoreClient), "StartMainDialogue", Type.EmptyTypes, (Type[])null);
	}

	private static bool Prepare()
	{
		return Target() != null;
	}

	private static MethodBase TargetMethod()
	{
		return Target();
	}

	private static bool Prefix(StoreClient __instance, out DialogueSystem.Step __state)
	{
		__state = null;
		if (!Net.IsOnline)
		{
			return true;
		}
		if (Net.IsServer)
		{
			__state = DialogueSystem.Step.Begin(DialogueSystem.Counter, "main", delegate(MsgDlgStep m)
			{
				m.rf = __instance.identifier;
			});
			return true;
		}
		if (Net.Applying)
		{
			return true;
		}
		DialogueSystem.MainBlocked++;
		return false;
	}

	private static Exception Finalizer(Exception __exception, DialogueSystem.Step __state)
	{
		return DialoguePatchUtil.Close(__exception, __state);
	}
}
