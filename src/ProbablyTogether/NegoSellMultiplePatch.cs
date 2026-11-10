using System.Collections.Generic;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(NegociationUIManager), "SellMultiple")]
internal static class NegoSellMultiplePatch
{
	private static bool Prefix(List<GameItem> items, bool ignoreBudget)
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		Negotiation.RequestDeal("nego.sell", items, single: false, ignoreBudget);
		return false;
	}
}
