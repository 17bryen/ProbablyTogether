using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(AppraisalItemsWindow), "SetAllowed")]
internal static class AppraisalBlockPatch
{
	private static bool Prepare()
	{
		return AccessTools.Method(typeof(AppraisalItemsWindow), "SetAllowed", (Type[])null, (Type[])null) != null;
	}

	private static bool Prefix(AppraisalItemType type, bool allowed)
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		Negotiation.RequestBlock(type, allowed);
		return false;
	}
}
