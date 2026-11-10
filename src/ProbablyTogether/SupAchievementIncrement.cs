using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(AchievementTracker), "IncrementCountAndUnlock")]
internal static class SupAchievementIncrement
{
	private static bool Prefix(string statName)
	{
		if (!Net.IsServer)
		{
			return true;
		}
		Suppress.BlockedAchievements++;
		Log.Info("server: Steam stat " + statName + " not written (dedicated server)");
		return false;
	}
}
