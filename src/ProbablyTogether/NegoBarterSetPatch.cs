using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(NegociationUIManager), "CycleBarterOfferSet")]
internal static class NegoBarterSetPatch
{
	private static bool Prepare()
	{
		return AccessTools.Method(typeof(NegociationUIManager), "CycleBarterOfferSet", (Type[])null, (Type[])null) != null;
	}

	private static bool Prefix(int offset)
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		Negotiation.RequestBarterSet(offset);
		return false;
	}
}
