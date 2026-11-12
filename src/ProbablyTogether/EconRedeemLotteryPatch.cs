using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class EconRedeemLotteryPatch
{
	private static readonly MethodBase target = EconomyHook.Find(() => AccessTools.Method(typeof(CommissaryUIManager), "OnRedeemAllLottery", Type.EmptyTypes, (Type[])null), "CommissaryUIManager.OnRedeemAllLottery");

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
			// On a private trip the game would redeem from the shop at home, on this copy only.
			Economy.RedeemOnTrip("lottery");
			return false;
		}
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (!Net.IsClient)
		{
			Economy.ServerCommissary("lottery", null, 0);
			return false;
		}
		Economy.RequestCommissary("lottery");
		return false;
	}
}
