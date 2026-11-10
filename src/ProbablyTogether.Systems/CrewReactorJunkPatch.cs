using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CrewReactorJunkPatch
{
	private static readonly MethodBase target = CrewHook.Find(() => AccessTools.Method(typeof(ReactorHelper), "RepairWithJunk", new Type[2]
	{
		typeof(GameItem),
		typeof(GameItem)
	}, (Type[])null), "ReactorHelper.RepairWithJunk");

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
		__state = ((Crew.Counting && __0 != null) ? ReactorHelper.GetIntegrity(__0) : int.MaxValue);
	}

	private static void Postfix(GameItem __0, int __state)
	{
		if (__state != int.MaxValue && Crew.Counting && __0 != null && ReactorHelper.GetIntegrity(__0) > __state)
		{
			CrewHook.Guard("reactor repair", delegate
			{
				Crew.Repaired("the reactor");
			});
		}
	}
}
