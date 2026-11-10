using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(NegociationData), MethodType.Constructor, new Type[] { typeof(string), typeof(int), typeof(string), typeof(string) })]
internal static class NegociationDataGuidPatch
{
	private static bool Prepare()
	{
		return AccessTools.Constructor(typeof(NegociationData), new Type[4]
		{
			typeof(string),
			typeof(int),
			typeof(string),
			typeof(string)
		}, false) != null;
	}

	private static void Postfix(NegociationData __instance)
	{
		if (Net.IsOnline && Net.Applying && __instance != null)
		{
			__instance.identifier = Negotiation.ReplayGuid();
		}
	}
}
