using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUseScratchStartPatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(ScratchInput), "OnAnyScratch", Type.EmptyTypes, (Type[])null);

	private static bool Prepare()
	{
		return ItemsUseHook.Found(target, "ScratchInput.OnAnyScratch");
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix()
	{
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		ScratcherUIManager instance = ScratcherUIManager.Instance;
		GameItem val = (((Object)(object)instance != (Object)null) ? instance.currentActiveScratcherItem : null);
		if (val == null || !val.IsTag("SCRATCHER_NEW_TAG"))
		{
			return true;
		}
		return ItemsUse.InterceptScratch(val, 0);
	}
}
