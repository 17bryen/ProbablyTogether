using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.UI;

[HarmonyPatch]
internal static class UiLeavePatch
{
	private static MethodBase Target()
	{
		return AccessTools.Method(typeof(EscapeUIManager), "OnMainMenu", Type.EmptyTypes, (Type[])null);
	}

	private static bool Prepare()
	{
		return UiPatchTargets.Found(Target(), "EscapeUIManager.OnMainMenu()");
	}

	private static MethodBase TargetMethod()
	{
		return Target();
	}

	private static bool Prefix(EscapeUIManager __instance)
	{
		if (!Net.IsClient)
		{
			return true;
		}
		try
		{
			__instance.CloseEscapeMenu();
			ConfirmPanelUIManager val = (((Object)(object)StoreUIManager.Instance != (Object)null) ? StoreUIManager.Instance.confirmPanelUIManager : null);
			if ((Object)(object)val == (Object)null)
			{
				Leave();
				return false;
			}
			val.OpenPanel(Lang.Trf("Leave {0}? The others keep playing, and you can join again from the main menu.", Kit.Name(Net.ServerName)), (Action)Leave, (Action)null, false);
		}
		catch (Exception ex)
		{
			Log.Warn("ui: leave confirmation failed (" + ex.Message + "); leaving");
			Leave();
		}
		return false;
	}

	internal static void Leave()
	{
		Log.Info("ui: leaving the server from the escape menu");
		Net.Disconnect();
	}
}
