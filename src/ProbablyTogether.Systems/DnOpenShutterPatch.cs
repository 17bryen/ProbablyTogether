using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class DnOpenShutterPatch
{
	private static readonly MethodBase target = DayNightGate.Find(() => AccessTools.Method(typeof(StoreShutterButton), "OpenShutter", Type.EmptyTypes, (Type[])null), "StoreShutterButton.OpenShutter");

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
		DayNight.Send("dayNight.open", new MsgDnOpen
		{
			paper = EmporiumOption.NewspaperToTrash
		});
		return false;
	}
}
