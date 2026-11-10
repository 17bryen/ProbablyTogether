using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

[HarmonyPatch(typeof(PhoneUIManager), "AutoCall")]
internal static class PhAutoCall
{
	private static bool Prefix(PhoneUIManager __instance, long number)
	{
		if (!DialoguePatchUtil.ClientBlocks || !PhoneUIManager.WillAnswerCall(number))
		{
			return true;
		}
		__instance.StopCall();
		if (DialogueSync.RequestCall(number, quick: true))
		{
			DialogueSystem.QuickDials++;
			try
			{
				if ((Object)(object)__instance.statusDisplay != (Object)null)
				{
					((TMP_Text)__instance.statusDisplay).text = LocHelper.GetLocalizedUI("ui_phone_in_call", Array.Empty<object>());
				}
			}
			catch
			{
			}
		}
		try
		{
			if ((Object)(object)__instance.phoneClientPickup != (Object)null)
			{
				__instance.phoneClientPickup.Play();
			}
		}
		catch
		{
		}
		return false;
	}
}
