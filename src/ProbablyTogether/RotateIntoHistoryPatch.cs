using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(SaveFiles), "RotateIntoHistory")]
internal static class RotateIntoHistoryPatch
{
	private static bool Prefix(ref string __result)
	{
		if (!Snapshot.Capturing && !Snapshot.SuppressRotate)
		{
			return true;
		}
		__result = null;
		return false;
	}
}
