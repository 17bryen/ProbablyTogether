using System.Threading.Tasks;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(EmporiumAnalytics), "InitAnalytics")]
internal static class SupAnalytics
{
	private static bool Prefix(ref Task __result)
	{
		if (!Net.IsServer)
		{
			return true;
		}
		__result = Task.CompletedTask;
		return false;
	}
}
