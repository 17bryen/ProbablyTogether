using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether;

/// Silences sounds from replayed actions (ItemsUse.QuietUi) by redirecting the game's AudioSource.Play calls to Play below.
[HarmonyPatch]
internal static class QuietAudioCallSites
{
	private static readonly MethodInfo replacement = AccessTools.Method(typeof(QuietAudioCallSites), "Play", (Type[])null, (Type[])null);

	private static readonly MethodInfo oneShot = AccessTools.Method(typeof(QuietAudioCallSites), "PlayOneShot", new Type[2]
	{
		typeof(AudioSource),
		typeof(AudioClip)
	}, (Type[])null);

	private static readonly MethodInfo oneShotScaled = AccessTools.Method(typeof(QuietAudioCallSites), "PlayOneShotScaled", (Type[])null, (Type[])null);

	internal static int ReplaySkipped;

	private static bool Quiet()
	{
		if (DayLog.Replaying)
		{
			ReplaySkipped++;
			return true;
		}
		if (ItemsUse.QuietUi)
		{
			ItemsUse.QuietSoundsBlocked++;
			return true;
		}
		return false;
	}

	internal static int Sites;

	private static bool Prepare()
	{
		return true;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return Paths.FindSoundPlayers();
	}

	public static void Play(AudioSource source)
	{
		if (Quiet())
		{
			return;
		}
		ItemsUse.SoundsPlayed++;
		source.Play();
	}

	/// The game's AudioSource.PlayOneShot calls are redirected here.
	public static void PlayOneShot(AudioSource source, AudioClip clip)
	{
		if (!Quiet())
		{
			source.PlayOneShot(clip);
		}
	}

	public static void PlayOneShotScaled(AudioSource source, AudioClip clip, float volumeScale)
	{
		if (!Quiet())
		{
			source.PlayOneShot(clip, volumeScale);
		}
	}

	internal static bool IsSoundCall(MethodInfo m)
	{
		if (m == null || m.DeclaringType != typeof(AudioSource))
		{
			return false;
		}
		int num = m.GetParameters().Length;
		return (m.Name == "Play" && num == 0) || (m.Name == "PlayOneShot" && (num == 1 || num == 2));
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
	{
		foreach (CodeInstruction item in code)
		{
			if (item.operand is MethodInfo methodInfo && IsSoundCall(methodInfo))
			{
				MethodInfo methodInfo2 = ((methodInfo.Name == "Play") ? replacement : ((methodInfo.GetParameters().Length == 1) ? oneShot : oneShotScaled));
				if (methodInfo2 != null)
				{
					item.opcode = System.Reflection.Emit.OpCodes.Call;
					item.operand = methodInfo2;
					Sites++;
				}
			}
			yield return item;
		}
	}
}
