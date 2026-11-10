using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CrewCageRepairPatch
{
	private static readonly MethodBase target = CrewHook.Find(() => AccessTools.Method(typeof(AnimalCage), "Repair", new Type[2]
	{
		typeof(GameItem),
		typeof(GameItem)
	}, (Type[])null), "AnimalCage.Repair");

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
		__state = (Crew.Counting ? CrewHook.Int(__0, "CAGE_HEALTH_TAG") : int.MaxValue);
	}

	private static void Postfix(GameItem __0, int __state)
	{
		if (__state != int.MaxValue && Crew.Counting && CrewHook.Int(__0, "CAGE_HEALTH_TAG") > __state)
		{
			CrewHook.Guard("cage repair", delegate
			{
				Crew.Repaired("an animal cage");
			});
		}
	}
}
