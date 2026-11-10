using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(DevTools), "IsFullEnabled", MethodType.Getter)]
internal static class SupDevToolsFull
{
	private static void Postfix(ref bool __result)
	{
		if (Net.IsClient)
		{
			__result = false;
		}
	}
}
