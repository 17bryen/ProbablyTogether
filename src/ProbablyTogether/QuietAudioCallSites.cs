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
		if (ItemsUse.QuietUi)
		{
			ItemsUse.QuietSoundsBlocked++;
			return;
		}
		ItemsUse.SoundsPlayed++;
		source.Play();
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
	{
		foreach (CodeInstruction item in code)
		{
			if (item.operand is MethodInfo { Name: "Play" } methodInfo && methodInfo.DeclaringType == typeof(AudioSource) && methodInfo.GetParameters().Length == 0)
			{
				item.opcode = System.Reflection.Emit.OpCodes.Call;
				item.operand = replacement;
				Sites++;
			}
			yield return item;
		}
	}
}
