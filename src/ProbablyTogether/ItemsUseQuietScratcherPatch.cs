using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUseQuietScratcherPatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(ScratcherUIManager), "OpenUI", new Type[1] { typeof(GameItem) }, (Type[])null);

	private static bool Prepare()
	{
		return ItemsUseHook.Found(target, "ScratcherUIManager.OpenUI");
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem gameItem)
	{
		if (!ItemsUse.QuietUi)
		{
			return true;
		}
		if (gameItem != null)
		{
			ScratcherHelper.TryRerollForBetterLuck(gameItem);
		}
		return false;
	}
}
