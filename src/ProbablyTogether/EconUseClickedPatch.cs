using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class EconUseClickedPatch
{
	private static readonly MethodBase target = EconomyHook.Find(() => AccessTools.Method(typeof(WildNetworkUI), "OnUseClicked", Type.EmptyTypes, (Type[])null), "WildNetworkUI.OnUseClicked");

	private static bool Prepare()
	{
		return target != null;
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
		string text = Economy.SelectedUpgrade();
		if (!Net.IsClient)
		{
			if (text != null)
			{
				Economy.ServerBuy(text, 0);
			}
			return false;
		}
		if (text == null || Economy.CheckBuy(text, out var _) != null)
		{
			return false;
		}
		Economy.RequestBuy(text);
		return false;
	}
}
