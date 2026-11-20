using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether;

/// While a replayed message runs, the game's sound effects are skipped; music and ambience play on.
[HarmonyPatch]
internal static class ReplayQuietEffectsPatch
{
	internal static int Skipped;

	private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
	{
		foreach (string name in new string[3] { "Play", "Play2", "Play3" })
		{
			System.Reflection.MethodInfo m = AccessTools.Method(typeof(AudioManager), name, new Type[1] { typeof(string) }, (Type[])null);
			if (m != null)
			{
				yield return m;
			}
		}
	}

	private static bool Prefix(ref bool __result)
	{
		if (!DayLog.Replaying)
		{
			return true;
		}
		Skipped++;
		__result = false;
		return false;
	}
}

[HarmonyPatch]
internal static class ReplayQuietOneShotPatch
{
	private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
	{
		System.Reflection.MethodInfo one = AccessTools.Method(typeof(AudioSource), "PlayOneShot", new Type[1] { typeof(AudioClip) }, (Type[])null);
		if (one != null)
		{
			yield return one;
		}
		System.Reflection.MethodInfo two = AccessTools.Method(typeof(AudioSource), "PlayOneShot", new Type[2]
		{
			typeof(AudioClip),
			typeof(float)
		}, (Type[])null);
		if (two != null)
		{
			yield return two;
		}
	}

	private static bool Prefix()
	{
		if (!DayLog.Replaying)
		{
			return true;
		}
		ReplayQuietEffectsPatch.Skipped++;
		return false;
	}
}

/// During a replay, finish the shot at once; the log's own steps remove the body.
[HarmonyPatch(typeof(StoreUIManager), "WoundAnimationRoutine")]
internal static class ShotReplayInstantPatch
{
	internal static int Instant;

	private static bool Prefix(StoreUIManager __instance, ref IEnumerator __result)
	{
		if (!DayLog.Replaying || __instance == null)
		{
			return true;
		}
		try
		{
			Instant++;
			if ((UnityEngine.Object)(object)__instance.shootingWoundRenderer != (UnityEngine.Object)null)
			{
				__instance.shootingWoundRenderer.gameObject.SetActive(false);
			}
			if ((UnityEngine.Object)(object)__instance.shootingBodyRenderer != (UnityEngine.Object)null)
			{
				__instance.shootingBodyRenderer.gameObject.SetActive(false);
			}
			if ((UnityEngine.Object)(object)__instance.clientGameObject != (UnityEngine.Object)null)
			{
				__instance.clientGameObject.gameObject.SetActive(false);
			}
			if ((UnityEngine.Object)(object)__instance.body != (UnityEngine.Object)null)
			{
				__instance.body.SetActive(true);
			}
			if (__instance.isShootingMode)
			{
				__instance.ExitShootingMode();
			}
		// On a player's copy the server's "shootoutStart" step starts the shootout.
		}
		catch (Exception ex)
		{
			Log.Warn("security: ending a replayed shot at once threw: " + ex.Message);
		}
		__result = Nothing();
		return false;
	}

	private static IEnumerator Nothing()
	{
		yield break;
	}
}
