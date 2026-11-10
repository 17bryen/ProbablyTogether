using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(PlayerStore), "PreLoadStore")]
internal static class PreLoadStorePatch
{
	private static Exception Finalizer(Exception __exception)
	{
		if (__exception != null && Net.IsServer)
		{
			ServerMode.LoadFailed(__exception);
		}
		return __exception;
	}
}
