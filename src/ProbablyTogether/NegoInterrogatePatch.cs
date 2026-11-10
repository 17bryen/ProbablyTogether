using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(NegociationUIManager), "OnInterogationButtonClick")]
internal static class NegoInterrogatePatch
{
	private static bool Prefix()
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		if (Negotiation.CurrentCustomer()?.interogationDialogue != null)
		{
			Negotiation.RequestTalk("interrogation");
		}
		return false;
	}
}
