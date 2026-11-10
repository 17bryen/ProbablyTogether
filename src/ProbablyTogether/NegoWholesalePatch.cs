using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(NegociationUIManager), "OnWholesaleClick")]
internal static class NegoWholesalePatch
{
	private static bool Prefix()
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		if (PlayerStore.HasInstance && PlayerStore.Instance.currentClientInstance != null)
		{
			Negotiation.RequestAccept("nego.wholesale", "WHOLESALE");
		}
		return false;
	}
}
