using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(NegociationUIManager), "OnGlasseButtonClick")]
internal static class NegoGlassesPatch
{
	private static bool Prefix()
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		if (Negotiation.CurrentCustomer()?.glasseDialogue != null)
		{
			Negotiation.RequestTalk("glasses");
		}
		return false;
	}
}
