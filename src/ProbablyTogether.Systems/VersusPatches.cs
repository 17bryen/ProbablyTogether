using HarmonyLib;

namespace ProbablyTogether.Systems;

/// Versus: the end-of-day button asks the host for bedtime; the night runs when the host calls it (Versus.NightTick).
[HarmonyPatch(typeof(EndOfDayUIManager), "OnEndDayButtonClick")]
internal static class VersusEndDayPatch
{
	private static bool Prefix()
	{
		if (!Net.Versus || Versus.NightAllowed || Versus.Ended)
		{
			return true;
		}
		Versus.PressBed(on: true);
		return false;
	}
}

/// Versus: closing the end-of-day screen takes a bedtime request back.
[HarmonyPatch(typeof(EndOfDayUIManager), "CloseUI")]
internal static class VersusEndDayClosePatch
{
	private static void Postfix()
	{
		if (Net.Versus && Versus.LocalReady && !Versus.NightAllowed)
		{
			Versus.PressBed(on: false);
		}
	}
}

/// Versus: every save the game writes goes to the host's copy.
[HarmonyPatch(typeof(PlayerStore), "WriteSlotFile")]
internal static class VersusSavedPatch
{
	private static void Postfix(PlayerStore __instance)
	{
		if (Net.Versus && !Snapshot.Capturing && __instance != null)
		{
			Versus.OnSlotWritten(__instance.saveSlotId);
		}
	}
}
