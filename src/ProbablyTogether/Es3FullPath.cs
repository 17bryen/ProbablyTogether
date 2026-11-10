using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(ES3Settings), "FullPath", MethodType.Getter)]
internal static class Es3FullPath
{
	private static bool Prepare()
	{
		return Paths.Redirecting;
	}

	private static void Postfix(ref string __result)
	{
		__result = Paths.Rewrite(__result);
	}
}
