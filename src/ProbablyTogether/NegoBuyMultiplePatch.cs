using System.Collections.Generic;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(NegociationUIManager), "BuyMultipleItem")]
internal static class NegoBuyMultiplePatch
{
	private static bool Prefix(List<GameItem> items)
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		Negotiation.RequestDeal("nego.buy", items, single: false, ignoreBudget: false);
		return false;
	}
}
