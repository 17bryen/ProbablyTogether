using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(LabelerUIManager), "Repeat")]
internal static class LabelRepeatPatch
{
	private static readonly FieldInfo last = AccessTools.Field(typeof(LabelerUIManager), "lastLabels");

	private static bool Prepare()
	{
		if (last != null)
		{
			return AccessTools.Field(typeof(LabelerUIManager), "curItem") != null;
		}
		return false;
	}

	private static bool Prefix()
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		GameItem val = Negotiation.LabelerItem();
		if (val?.itemFeatures == null || !(last.GetValue(null) is Dictionary<string, string> dictionary))
		{
			return false;
		}
		HashSet<string> hashSet = new HashSet<string>();
		foreach (ItemFeature itemFeature in val.itemFeatures)
		{
			if (itemFeature != null && itemFeature.category != null && hashSet.Add(itemFeature.category) && dictionary.TryGetValue(itemFeature.category, out var value))
			{
				Negotiation.RequestLabel(val, itemFeature.category, value, repeat: true);
			}
		}
		return false;
	}
}
