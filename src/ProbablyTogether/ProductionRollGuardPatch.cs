using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ProductionRollGuardPatch
{
	private static readonly Dictionary<MethodBase, string> sites = Collect();

	private static Dictionary<MethodBase, string> Collect()
	{
		Dictionary<MethodBase, string> d = new Dictionary<MethodBase, string>();
		Add(ProductionHook.Check(() => AccessTools.Method(typeof(HusbandryHelper), "TryChangeSprite", new Type[1] { typeof(GameItem) }, (Type[])null), "HusbandryHelper.TryChangeSprite"), "HusbandryHelper.TryChangeSprite");
		Add(ProductionHook.Check(() => AccessTools.Method(typeof(HusbandryHelper), "GiveBirth", new Type[2]
		{
			typeof(GameItem),
			typeof(GameInventory)
		}, (Type[])null), "HusbandryHelper.GiveBirth"), "HusbandryHelper.GiveBirth");
		Add(ProductionHook.Check(() => AccessTools.Method(typeof(HusbandryHelper), "CheckMate", new Type[2]
		{
			typeof(GameItem),
			typeof(GameInventory)
		}, (Type[])null), "HusbandryHelper.CheckMate"), "HusbandryHelper.CheckMate");
		Add(ProductionHook.Check(() => AccessTools.Method(typeof(HusbandryHelper), "TryMoveToRandomSpotInGrid", new Type[1] { typeof(GameItem) }, (Type[])null), "HusbandryHelper.TryMoveToRandomSpotInGrid"), "HusbandryHelper.TryMoveToRandomSpotInGrid");
		Add(ProductionHook.Check(() => AccessTools.Method(typeof(HusbandryHelper), "FindOtherInventoryWithFood", new Type[2]
		{
			typeof(GameInventory[]),
			typeof(GameInventory)
		}, (Type[])null), "HusbandryHelper.FindOtherInventoryWithFood"), "HusbandryHelper.FindOtherInventoryWithFood");
		Add(ProductionHook.Check(() => AccessTools.Method(typeof(VendingMachineHelper), "CheckEligibleItem", new Type[2]
		{
			typeof(GameItem),
			typeof(List<GameItem>)
		}, (Type[])null), "VendingMachineHelper.CheckEligibleItem"), "VendingMachineHelper.CheckEligibleItem");
		Add(ProductionHook.Check(() => AccessTools.Method(typeof(VendingMachineHelper), "CheckSoldItem", new Type[2]
		{
			typeof(GameItem),
			typeof(List<GameItem>)
		}, (Type[])null), "VendingMachineHelper.CheckSoldItem"), "VendingMachineHelper.CheckSoldItem");
		Add(ProductionHook.Check(() => AccessTools.Method(typeof(WaterHelper), "RollNearLowerEdge", new Type[2]
		{
			typeof(int),
			typeof(int)
		}, (Type[])null), "WaterHelper.RollNearLowerEdge"), "WaterHelper.RollNearLowerEdge");
		return d;
		void Add(MethodBase m, string label)
		{
			if (m != null)
			{
				d[m] = label;
			}
		}
	}

	private static bool Prepare()
	{
		return sites.Count > 0;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return sites.Keys;
	}

	private static void Prefix(MethodBase __originalMethod)
	{
		Production.NoteRoll((__originalMethod != null && sites.TryGetValue(__originalMethod, out var value)) ? value : "a roll site");
	}
}
