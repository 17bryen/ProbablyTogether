using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class CrewStolenReportPatch
{
	private static readonly MethodBase target = CrewHook.Find(() => AccessTools.Method(typeof(PlayerStore), "CreateStolenReportReward", new Type[2]
	{
		typeof(StoreClient),
		typeof(bool)
	}, (Type[])null), "PlayerStore.CreateStolenReportReward");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix()
	{
		if (!Crew.Counting)
		{
			return;
		}
		CrewHook.Guard("caught", delegate
		{
			int num = 0;
			EmporiumEntry instance = EmporiumEntry.Instance;
			if ((Object)(object)instance != (Object)null)
			{
				foreach (GameItem item in instance.GetAllNonOwnedItem())
				{
					if (item != null && (StolenHelper.IsStolenItem(item) || ContrabandHelper.IsContraband(item)))
					{
						num++;
					}
				}
			}
			Crew.Caught(num);
		});
	}
}
