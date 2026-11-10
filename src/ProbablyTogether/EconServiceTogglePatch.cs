using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class EconServiceTogglePatch
{
	private static readonly MethodBase target = EconomyHook.Find(() => AccessTools.Method(typeof(EndOfDayUIManager), "OnToggled", new Type[2]
	{
		typeof(StoreService),
		typeof(bool)
	}, (Type[])null), "EndOfDayUIManager.OnToggled");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(StoreService storeService, bool on)
	{
		if (!Net.IsOnline || Net.Applying || storeService == null)
		{
			return true;
		}
		if (!Net.IsClient)
		{
			Economy.ServerToggle(storeService.id, on, 0);
			return false;
		}
		try
		{
			TutorialUIManager instance = TutorialUIManager.Instance;
			if (instance != null)
			{
				instance.DisableNightlyServiceTutorial();
			}
		}
		catch
		{
		}
		Economy.Click();
		Economy.RequestToggle(storeService.id, on);
		return false;
	}
}
