using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(TutorialUIManager), "TutorialStart")]
internal static class SupTutorialStart
{
	private static bool Prefix(TutorialUIManager __instance)
	{
		if (!Net.IsOnline)
		{
			return true;
		}
		try
		{
			__instance.CloseAllTutorial();
		}
		catch
		{
		}
		return false;
	}
}
