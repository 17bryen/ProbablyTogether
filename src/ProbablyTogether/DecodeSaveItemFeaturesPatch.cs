using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(PlayerStore), "DecodeSaveItem", new Type[]
{
	typeof(string),
	typeof(GameInventory)
})]
internal static class DecodeSaveItemFeaturesPatch
{
	private static bool Prepare()
	{
		return AccessTools.Method(typeof(PlayerStore), "DecodeSaveItem", new Type[2]
		{
			typeof(string),
			typeof(GameInventory)
		}, (Type[])null) != null;
	}

	private static void Prefix(PlayerStore __instance)
	{
		if (LoadGameFeaturesPatch.InLoad && LoadGameFeaturesPatch.Captured == null)
		{
			List<ItemFeature> savedItemFeatureList = __instance.savedItemFeatureList;
			LoadGameFeaturesPatch.Captured = ((savedItemFeatureList != null) ? new List<ItemFeature>(savedItemFeatureList) : new List<ItemFeature>());
		}
	}
}
