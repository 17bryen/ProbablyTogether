using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(CustomUIDebugPanel), "Toggle")]
internal static class SupDebugPanel
{
	private static bool Prefix()
	{
		return Suppress.DevToolAllowed("F9 debug panel");
	}
}
