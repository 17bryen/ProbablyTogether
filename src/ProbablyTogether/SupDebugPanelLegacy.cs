using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(CustomUIDebugPanel), "ToggleLegacyPanel")]
internal static class SupDebugPanelLegacy
{
	private static bool Prefix()
	{
		return Suppress.DevToolAllowed("F9 legacy debug panel");
	}
}
