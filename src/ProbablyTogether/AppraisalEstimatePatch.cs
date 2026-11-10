using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(AppraisalServiceHelper), "OnEstimate")]
internal static class AppraisalEstimatePatch
{
	private static bool Prefix(GameItem gameItem, int value)
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		if (gameItem != null)
		{
			Negotiation.RequestEstimate(gameItem, value);
		}
		return false;
	}
}
