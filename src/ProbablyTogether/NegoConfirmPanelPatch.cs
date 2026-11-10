using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(NegociationUIManager), "OpenConfirmPanel")]
internal static class NegoConfirmPanelPatch
{
	private static bool Prefix()
	{
		if (Systems.Outings.Private)
		{
			return true;
		}
		if (Net.IsDedicated)
		{
			return false;
		}
		if (Net.IsHost)
		{
			return Net.Actor < 0 || Net.Actor == Net.LocalPlayer || Negotiation.confirmFromServer;
		}
		if (Net.IsClient && Net.Applying && !Negotiation.confirmFromServer)
		{
			return false;
		}
		return true;
	}
}
