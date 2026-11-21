using HarmonyLib;

namespace ProbablyTogether;

/// While a deal is applied, the bought item follows the asking player's setting into shop storage, never a trip's bag.
[HarmonyPatch(typeof(PlayerStore), "MoveDealItemToInventory")]
internal static class NegoFilingPatch
{
	private static bool Prefix(GameItem item)
	{
		int filingNow = Negotiation.FilingNow;
		if (filingNow < 0 || !Net.IsOnline || item == null)
		{
			return true;
		}
		Negotiation.Filed++;
		if (filingNow == 1)
		{
			GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, item);
		}
		return false;
	}
}
