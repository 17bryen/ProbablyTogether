using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(BargainUIManager), "OfferMarkup")]
internal static class BargainMarkupPatch
{
	private static bool Prefix(int percent)
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		Negotiation.RequestBargain((BargainType)1, percent);
		return false;
	}
}
