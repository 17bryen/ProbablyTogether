using System.Collections.Generic;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(NegociationUIManager), "BuyItem")]
internal static class NegoBuyOnePatch
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
			Negotiation.RequestDeal("nego.buy", new List<GameItem> { val }, single: true, ignoreBudget: false);
		}
		return false;
	}
}
