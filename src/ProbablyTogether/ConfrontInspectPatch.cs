using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(InspectionUIManager), "OnConfront")]
internal static class ConfrontInspectPatch
{
	private static bool Prepare()
	{
		return AccessTools.Method(typeof(InspectionUIManager), "OnConfront", Type.EmptyTypes, (Type[])null) != null;
	}

	private static bool Prefix(InspectionUIManager __instance)
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		GameItem currentInspectingItem = __instance.currentInspectingItem;
		if (InspectionUIManager.IsConfrontLocked() || PlayerStore.Instance.currentClientInstance == null || currentInspectingItem == null || !__instance.IsAtLeastOneIssueSelected())
		{
			return false;
		}
		string text = Negotiation.InspectReason(__instance, currentInspectingItem);
		if (text == null || !Negotiation.InspectActs(currentInspectingItem))
		{
			return false;
		}
		if (Negotiation.RequestConfront(currentInspectingItem, "inspect", text))
		{
			try
			{
				__instance.CloseUI();
			}
			catch
			{
			}
		}
		return false;
	}
}
