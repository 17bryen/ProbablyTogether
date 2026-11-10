using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(PlayerStore), "SaveGame")]
internal static class SaveGamePatch
{
	private static bool Prefix(PlayerStore __instance)
	{
		if (Net.IsRemote || __instance.saveSlotId == 990077)
		{
			return false;
		}
		if (Net.IsServer && GamePatches.SaveIsBlocked)
		{
			Log.Info("server: a save of slot " + __instance.saveSlotId + " was skipped (saves are blocked: the run has ended)");
			return false;
		}
		return true;
	}

	private static void Postfix(bool __runOriginal)
	{
		if (__runOriginal && Net.IsServer && !Snapshot.Capturing)
		{
			ServerMode.OnSlotSaved(Snapshot.SuppressRotate);
		}
	}
}
