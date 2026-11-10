using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUseQuietPanelsPatch
{
	private static List<MethodBase> found;

	private static List<MethodBase> Find()
	{
		if (found != null)
		{
			return found;
		}
		Type typeFromHandle = typeof(GameItem);
		Type typeFromHandle2 = typeof(string);
		Type typeFromHandle3 = typeof(int);
		Type[] emptyTypes = Type.EmptyTypes;
		MethodInfo[] array = new MethodInfo[43]
		{
			AccessTools.Method(typeof(PaperUIManager), "OpenUI", new Type[4] { typeFromHandle2, typeFromHandle2, typeFromHandle2, typeFromHandle3 }, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenContract", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenDeed", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenBook", new Type[1] { typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenIndex", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenSlipUI", new Type[1] { typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenGeneralSlipUI", new Type[1] { typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "ToggleJoeCard", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenCigaretteGuide", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenStampGuide", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenInjectorGuide", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenWantedPaper", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenSecCard", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenCigaretteColor", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenCodeBook", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenBrewerCard", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenMiningCard", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenChemCard", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenPharmaCard", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenCard", new Type[2] { typeFromHandle2, typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenLotteryInfo", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(PaperUIManager), "OpenAugIdGuide", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(MultiPageUIManager), "OpenUI", new Type[1] { typeFromHandle3 }, (Type[])null),
			AccessTools.Method(typeof(WaterStripUIManager), "Open", new Type[1] { typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(WaterScannerUIManager), "Open", new Type[2] { typeFromHandle, typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(ChemScannerUIManager), "Open", new Type[2] { typeFromHandle, typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(PlantAnalyserUIManager), "OpenUI", new Type[1] { typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(LabelerUIManager), "OpenUI", new Type[1] { typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(LabelerBlueUIManager), "OpenUI", new Type[1] { typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(LabelerBlueUIManager), "OpenUI", new Type[4]
			{
				typeFromHandle2,
				typeof(Action<string>),
				typeof(Action),
				typeFromHandle3
			}, (Type[])null),
			AccessTools.Method(typeof(RecordUIManager), "OpenUI", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(RepublicLogoUIManager), "OpenPanel", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(HandnoteUIManager), "OpenPanel", new Type[1] { typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(ExchangeUIManager), "OpenPanel", new Type[1] { typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(InspectionUIManager), "OnInspectItem", new Type[1] { typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(AppraisalItemsWindow), "Open", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(NewsUIManager), "ToggleUI", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(LogUIManager), "Toggle", new Type[1] { typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(LoanPanelUIManager), "OpenUI", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(ChemFinisherUIManager), "OpenUI", new Type[1] { typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(HopperHelper), "OpenWindow", new Type[1] { typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(MachineStorageCoordinator), "OpenWindow", new Type[1] { typeFromHandle }, (Type[])null),
			AccessTools.Method(typeof(MindReprogrammerHelper), "OpenWindow", new Type[1] { typeFromHandle }, (Type[])null)
		};
		found = array.Where((MethodInfo m) => m != null).Cast<MethodBase>().ToList();
		if (found.Count < array.Length)
		{
			Log.Warn("items-use: " + (array.Length - found.Count) + " quiet-run panel hook(s) not found in this game build (those panels open for everyone)");
		}
		return found;
	}

	private static bool Prepare()
	{
		return Find().Count > 0;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return Find();
	}

	private static bool Prefix()
	{
		return !ItemsUse.QuietUi;
	}
}
