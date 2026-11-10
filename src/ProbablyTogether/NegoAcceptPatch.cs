using System.Runtime.CompilerServices;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(NegociationUIManager), "OnOfferAcceptClick")]
internal static class NegoAcceptPatch
{
	private static bool Prefix()
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		StoreClient val = Negotiation.CurrentCustomer();
		if (val == null)
		{
			return false;
		}
		StoreClient.ClientIntent clientIntent = val.clientIntent;
		if ((int)clientIntent != 6 && (int)clientIntent != 10)
		{
			if ((int)clientIntent == 15)
			{
				if (!NegociationUIManager.IsTradeLocked() && !val.isBusinessComplete)
				{
					Negotiation.RequestAccept("nego.accept", "GUNSMITH", GunOrderData.ResolveOrderGun());
				}
				return false;
			}
			return Negotiation.MayHaggle("nego.accept");
		}
		if (!NegociationUIManager.IsTradeLocked() && !val.isBusinessComplete)
		{
			Negotiation.RequestAccept("nego.accept", val.clientIntent.ToString());
		}
		return false;
	}
}
