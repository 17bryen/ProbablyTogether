using System.Collections.Generic;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(NegociationUIManager), "SellItem")]
internal static class NegoSellOnePatch
{
	private static bool Prefix()
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		GameItem val = (PlayerStore.HasInstance ? PlayerStore.Instance.currentNegociatedItem : null);
		if (val != null)
		{
			Negotiation.RequestDeal("nego.sell", new List<GameItem> { val }, single: true, ignoreBudget: false);
		}
		return false;
	}
}
