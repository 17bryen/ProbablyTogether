using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(Dialogue), "ExecuteEndAction")]
internal static class DlgExecuteEndAction
{
	private static bool Prefix()
	{
		return !DialoguePatchUtil.ClientBlocks;
	}
}
