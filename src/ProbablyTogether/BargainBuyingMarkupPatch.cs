using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(BargainUIManager), "OfferBuyingMarkup")]
internal static class BargainBuyingMarkupPatch
{
	private static bool Prefix(int percent)
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		Negotiation.RequestBargain((BargainType)2, percent);
		return false;
	}
}
