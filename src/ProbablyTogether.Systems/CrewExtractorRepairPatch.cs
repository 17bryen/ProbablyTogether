using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CrewExtractorRepairPatch
{
	private static readonly MethodBase target = CrewHook.Find(() => AccessTools.Method(typeof(ModuleHelper), "RepairExtractor", new Type[2]
	{
		typeof(GameItem),
		typeof(GameItem)
	}, (Type[])null), "ModuleHelper.RepairExtractor");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Prefix(GameItem __0, out int __state)
	{
		__state = (Crew.Counting ? CrewHook.Int(__0, "CURRENT_USE_COUNT_INT") : int.MaxValue);
	}

	private static void Postfix(GameItem __0, int __state)
	{
		if (__state != int.MaxValue && Crew.Counting && CrewHook.Int(__0, "CURRENT_USE_COUNT_INT") > __state)
		{
			CrewHook.Guard("extractor repair", delegate
			{
				Crew.Repaired("an extractor");
			});
		}
	}
}
