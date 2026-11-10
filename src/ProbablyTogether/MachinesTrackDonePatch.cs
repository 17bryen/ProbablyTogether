using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class MachinesTrackDonePatch
{
	private static readonly MethodBase target = MachinesHook.Check(() => AccessTools.Method(typeof(AudioManager), "OnTrackDone", Type.EmptyTypes, (Type[])null), "AudioManager.OnTrackDone");

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
		return Machines.OnTrackDone();
	}
}
