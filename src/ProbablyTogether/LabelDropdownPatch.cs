using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

[HarmonyPatch(typeof(LabelerElement), "OnDropdownValueChanged")]
internal static class LabelDropdownPatch
{
	private static bool Prepare()
	{
		return AccessTools.Field(typeof(LabelerElement), "thisFeature") != null;
	}

	private static bool Prefix(LabelerElement __instance)
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		ItemFeature val = Negotiation.ElementFeature(__instance);
		GameItem val2 = Negotiation.LabelerItem();
		if (val == null || val2 == null || (Object)(object)__instance.dropdown == (Object)null)
		{
			return false;
		}
		if (!ItemConditionList.conditionLookup.TryGetValue(val.category, out var value) || __instance.dropdown.value < 0 || __instance.dropdown.value >= value.Count)
		{
			return false;
		}
		Negotiation.RequestLabel(val2, val.category, value[__instance.dropdown.value].identifier, repeat: false);
		return false;
	}
}
