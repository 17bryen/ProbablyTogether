using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(BargainUIManager), "Blackmail")]
internal static class BargainBlackmailPatch
{
	private static bool Prefix()
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		Negotiation.RequestBargain((BargainType)4, 20);
		return false;
	}
}
