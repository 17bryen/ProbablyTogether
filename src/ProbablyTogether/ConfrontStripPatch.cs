using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(WaterStripUIManager), "OnConfront")]
internal static class ConfrontStripPatch
{
	private static bool Prepare()
	{
		return AccessTools.Method(typeof(WaterStripUIManager), "OnConfront", Type.EmptyTypes, (Type[])null) != null;
	}

	private static bool Prefix(WaterStripUIManager __instance)
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		GameItem currentWaterContainer = __instance.currentWaterContainer;
		if (currentWaterContainer != null && __instance.CanConfront(currentWaterContainer))
		{
			Negotiation.RequestConfront(currentWaterContainer, "strip");
		}
		return false;
	}
}
