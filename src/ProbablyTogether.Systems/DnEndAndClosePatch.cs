using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class DnEndAndClosePatch
{
	private static readonly MethodBase target = DayNightGate.Find(() => AccessTools.Method(typeof(StoreShutterButton), "EndAndClose", Type.EmptyTypes, (Type[])null), "StoreShutterButton.EndAndClose");

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
		if (!DayNightGate.LocalCommit)
		{
			return true;
		}
		try
		{
			StoreUIManager instance = StoreUIManager.Instance;
			if (instance != null)
			{
				GameObject closeShutterConfirmationPanel = instance.closeShutterConfirmationPanel;
				if (closeShutterConfirmationPanel != null)
				{
					closeShutterConfirmationPanel.SetActive(false);
				}
			}
		}
		catch
		{
		}
		DayNight.Send("dayNight.close", new MsgDnEmpty());
		return false;
	}
}
