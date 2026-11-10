using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

[HarmonyPatch(typeof(LabelerBlueUIManager), "OnClearButtonClick")]
internal static class LabelNameClearPatch
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
		if ((Object)(object)__instance.inputField != (Object)null)
		{
			__instance.inputField.text = "";
		}
		Negotiation.RequestName(val, null);
		return false;
	}
}
