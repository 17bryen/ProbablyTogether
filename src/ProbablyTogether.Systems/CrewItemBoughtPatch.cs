using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CrewItemBoughtPatch
{
	private static readonly MethodBase target = CrewHook.Find(() => AccessTools.Method(typeof(RunStats), "OnItemBought", new Type[3]
	{
		typeof(GameItem),
		typeof(int),
		typeof(StoreClient)
	}, (Type[])null), "RunStats.OnItemBought");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix(GameItem __0, int __1, StoreClient __2)
	{
		if (__0 != null && __1 > 0)
		{
			CrewHook.Guard("standing bought", delegate
			{
				Standing.Bought(__0, __1);
			});
		}
		if (Crew.Counting && __0 != null && __1 > 0)
		{
			CrewHook.Guard("bought", delegate
			{
				Crew.Bought(__0, __1, __2);
			});
		}
	}
}
