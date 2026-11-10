using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CrewItemSoldPatch
{
	private static readonly MethodBase target = CrewHook.Find(() => AccessTools.Method(typeof(RunStats), "OnItemSold", new Type[4]
	{
		typeof(GameItem),
		typeof(int),
		typeof(int),
		typeof(StoreClient)
	}, (Type[])null), "RunStats.OnItemSold");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix(GameItem __0, int __1, StoreClient __3)
	{
		if (Crew.Counting && __0 != null && __1 > 0 && (__3 == null || !(__3.identifier == "businessPermitOfficial")))
		{
			CrewHook.Guard("sold", delegate
			{
				Crew.Sold(__0, __1, __3);
			});
		}
	}
}
