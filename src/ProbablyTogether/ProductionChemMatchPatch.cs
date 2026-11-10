using System;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine.UI;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ProductionChemMatchPatch
{
	private static readonly MethodBase target = ProductionHook.Check(() => AccessTools.Method(typeof(ChemScannerUIManager), "MatchChemLabel", new Type[4]
	{
		typeof(GameItem),
		typeof(Button),
		typeof(TextMeshProUGUI),
		typeof(TextMeshProUGUI)
	}, (Type[])null), "ChemScannerUIManager.MatchChemLabel");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem chemContainer)
	{
		return Production.Let(ProdOp.ChemMatch, chemContainer);
	}
}
