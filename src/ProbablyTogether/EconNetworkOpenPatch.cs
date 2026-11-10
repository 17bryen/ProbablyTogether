using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class EconNetworkOpenPatch
{
	private static readonly MethodBase target = EconomyHook.Find(() => AccessTools.Method(typeof(WildNetworkUI), "Open", new Type[1] { typeof(bool) }, (Type[])null), "WildNetworkUI.Open");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(out bool __state)
	{
		__state = false;
		if (!Net.IsOnline)
		{
			return true;
		}
		if (Net.IsServer)
		{
			if (Net.Applying)
			{
				Economy.BrowseState();
			}
			else if (Net.IsDedicated)
			{
				Economy.ServerBrowse(0);
			}
			Economy.ServerWindowOpened("network");
			if (Net.IsDedicated)
			{
				return false;
			}
			if (Net.Applying)
			{
				if (!Economy.UiForMe())
				{
					Economy.SkippedWindow("network");
					return false;
				}
				return true;
			}
			__state = true;
			return true;
		}
		if (Net.Applying && !Economy.UiForMe())
		{
			Economy.BrowseState();
			Economy.SkippedWindow("network");
			return false;
		}
		if (Net.Applying && WildNetworkUI.IsOpen)
		{
			Economy.BrowseState();
		}
		__state = !Net.Applying;
		return true;
	}

	private static void Postfix(bool __state)
	{
		if (__state && Net.IsClient && WildNetworkUI.IsOpen)
		{
			Economy.SendBrowse();
		}
	}
}
