using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(NegociationUIManager), "OpenUI")]
internal static class NegoAutoOpenPatch
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
			if (!Net.Applying)
			{
				return true;
			}
			string hostWhy;
			bool forHost = Negotiation.TabletForMe(out hostWhy);
			Negotiation.LastTabletGate = (forHost ? "opened (host): " : "skipped (host): ") + hostWhy;
			Log.Info("nego: tablet " + Negotiation.LastTabletGate);
			return forHost;
		}
		if (!Net.IsClient || !Net.Applying)
		{
			return true;
		}
		string why;
		bool num = Negotiation.TabletForMe(out why);
		if (!num)
		{
			Negotiation.TabletSkipped++;
		}
		Negotiation.LastTabletGate = (num ? "opened: " : "skipped: ") + why;
		return num;
	}
}
