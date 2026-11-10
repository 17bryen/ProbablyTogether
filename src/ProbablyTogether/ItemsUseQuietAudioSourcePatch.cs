using System;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether;

[HarmonyPatch(typeof(AudioSource), "Play", new Type[] { })]
internal static class ItemsUseQuietAudioSourcePatch
{
	private static bool Prepare()
	{
		string text = Application.unityVersion ?? "";
		if (text.StartsWith("2021.3.", StringComparison.Ordinal))
		{
			return true;
		}
		Log.Warn("items-use: Unity " + text + " is not the version the AudioSource.Play hook was checked on: that hook is off");
		return false;
	}

	private static bool Prefix()
	{
		if (!ItemsUse.QuietUi)
		{
			ItemsUse.SoundsPlayed++;
			return true;
		}
		ItemsUse.QuietSoundsBlocked++;
		return false;
	}
}
