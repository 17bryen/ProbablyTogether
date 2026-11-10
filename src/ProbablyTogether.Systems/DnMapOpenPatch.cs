using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class DnMapOpenPatch
{
	private static readonly MethodBase target = DayNightGate.Find(() => AccessTools.Method(typeof(MapUIManager), "OpenUI", Type.EmptyTypes, (Type[])null), "MapUIManager.OpenUI");

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
		if (!DayNightGate.LocalCommit)
		{
			return true;
		}
		if (Outings.RequestGo())
		{
			return false;
		}
		EmporiumEntry instance = EmporiumEntry.Instance;
		GameItem val = ((instance != null) ? instance.selectedBackpack : null);
		long backpackId = ((val != null) ? Ids.Of(val) : 0);
		if ((Object)(object)instance != (Object)null)
		{
			instance.selectedBackpack = null;
		}
		DayNight.Send("dayNight.goOut", new MsgDnGoOut
		{
			backpackId = backpackId
		});
		return false;
	}
}
