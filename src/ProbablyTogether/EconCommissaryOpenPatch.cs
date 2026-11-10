using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class EconCommissaryOpenPatch
{
	private static readonly MethodBase target = EconomyHook.Find(() => AccessTools.Method(typeof(CommissaryUIManager), "OpenUI", Type.EmptyTypes, (Type[])null), "CommissaryUIManager.OpenUI");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix()
	{
		if (!Net.IsOnline)
		{
			return true;
		}
		if (Net.IsDedicated)
		{
			Economy.ServerWindowOpened("commissary");
			return false;
		}
		if (Net.Applying)
		{
			if (Economy.UiForMe())
			{
				Economy.NoteReplayWindow();
				return true;
			}
			Economy.SkippedWindow("commissary");
			return false;
		}
		return Economy.ClientWindowOpening();
	}
}
