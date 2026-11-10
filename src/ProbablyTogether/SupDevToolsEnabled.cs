using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(DevTools), "IsEnabled", MethodType.Getter)]
internal static class SupDevToolsEnabled
{
	private static void Postfix(ref bool __result)
	{
		if (Net.IsClient)
		{
			__result = false;
		}
	}
}
