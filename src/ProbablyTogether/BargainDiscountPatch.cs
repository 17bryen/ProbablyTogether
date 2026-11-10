using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(BargainUIManager), "OfferDiscount")]
internal static class BargainDiscountPatch
{
	private static bool Prefix(int percent)
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		Negotiation.RequestBargain((BargainType)0, percent);
		return false;
	}
}
