using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

[HarmonyPatch(typeof(LabelerBlueUIManager), "OnEditEnd")]
internal static class LabelNamePatch
{
	private static bool Prepare()
	{
		return AccessTools.Field(typeof(LabelerBlueUIManager), "curItem") != null;
	}

	private static bool Prefix(LabelerBlueUIManager __instance)
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		GameItem val = Negotiation.BlueLabelerItem();
		if (val == null)
		{
			return true;
		}
		try
		{
			StoreUIManager instance = StoreUIManager.Instance;
			if (instance != null)
			{
				instance.StopTyping();
			}
		}
		catch
		{
		}
		Negotiation.RequestName(val, ((Object)(object)__instance.inputField != (Object)null) ? __instance.inputField.text : "");
		return false;
	}
}
