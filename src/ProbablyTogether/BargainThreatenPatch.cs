using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(BargainUIManager), "ThreatenAug")]
internal static class BargainThreatenPatch
{
	private static bool Prefix()
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		Negotiation.RequestBargain((BargainType)5, 20);
		return false;
	}
}
