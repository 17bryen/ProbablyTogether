using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class EconCommissaryBuyPatch
{
	private static readonly MethodBase target = EconomyHook.Find(() => AccessTools.Method(typeof(CommissaryData), "BuyItem", new Type[1] { typeof(string) }, (Type[])null), "CommissaryData.BuyItem");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(string id)
	{
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (!Net.IsClient)
		{
			Economy.ServerCommissary("buy", id, 0);
			return false;
		}
		Economy.RequestCommissary("buy", id);
		return false;
	}
}
