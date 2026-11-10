using System;
using HarmonyLib;
using UnityEngine.UI;

namespace ProbablyTogether;

[HarmonyPatch(typeof(WaterScannerUIManager), "ConfrontWater")]
internal static class ConfrontScannerPatch
{
	private static bool Prepare()
	{
		return AccessTools.Method(typeof(WaterScannerUIManager), "ConfrontWater", new Type[2]
		{
			typeof(GameItem),
			typeof(Button)
		}, (Type[])null) != null;
	}

	private static bool Prefix(GameItem waterContainer)
	{
		if (!Negotiation.Intercept)
		{
			return true;
		}
		if (waterContainer != null)
		{
			Negotiation.RequestConfront(waterContainer, "scanner");
		}
		return false;
	}
}
