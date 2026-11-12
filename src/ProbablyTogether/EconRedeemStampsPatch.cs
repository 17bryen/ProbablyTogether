using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class EconRedeemStampsPatch
{
	private static readonly MethodBase target = EconomyHook.Find(() => AccessTools.Method(typeof(CommissaryUIManager), "OnRedeemAllButtonClick", Type.EmptyTypes, (Type[])null), "CommissaryUIManager.OnRedeemAllButtonClick");

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
		if (Net.IsOnline && Systems.Outings.Private && !DayLog.Replaying)
		{
			// On a private trip the game would reach into the shop at home from here, on this copy only.
			Economy.RedeemOnTrip("stamps");
			return false;
		}
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (!Net.IsClient)
		{
			Economy.ServerCommissary("stamps", null, 0);
			return false;
		}
		Economy.RequestCommissary("stamps");
		return false;
	}
}
