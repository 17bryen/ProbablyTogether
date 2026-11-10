using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(PlayerStore), "LoadGame")]
internal static class LoadGamePatch
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
