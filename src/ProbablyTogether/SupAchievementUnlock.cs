using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(AchievementTracker), "Unlock")]
internal static class SupAchievementUnlock
{
	private static bool Prefix(string apiName)
	{
		if (!Net.IsServer)
		{
			return true;
		}
		Suppress.BlockedAchievements++;
		Log.Info("server: Steam achievement " + apiName + " not written (dedicated server)");
		return false;
	}
}
