using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(BargainUIManager), "DemandBuyingDiscount")]
internal static class BargainBuyingDiscountPatch
{
	private static bool Prefix(int percent)
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		Negotiation.RequestBargain((BargainType)3, percent);
		return false;
	}
}
