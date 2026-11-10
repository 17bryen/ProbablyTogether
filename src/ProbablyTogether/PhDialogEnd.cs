using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(PhoneUIManager), "OnPhoneDialogEnd")]
internal static class PhDialogEnd
{
	private static void Postfix()
	{
		if (Net.IsServer)
		{
			DialogueSystem.ServerCallEnded();
		}
	}
}
