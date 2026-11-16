using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(SaveFiles), "NextFreeSlotId")]
internal static class NextFreeSlotIdPatch
{
	private static bool Prefix(ref int __result)
	{
		if (!Net.IsServer || !ServerMode.NewRunPending)
		{
			return true;
		}
		__result = ServerMode.NewRunSlot();
		if (Paths.IndexLoad<int>("slotIdGenerator", 0) <= __result)
		{
			Paths.IndexSave<int>("slotIdGenerator", __result + 1);
		}
		return false;
	}
}
