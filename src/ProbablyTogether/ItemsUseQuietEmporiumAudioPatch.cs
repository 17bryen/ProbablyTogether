using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUseQuietEmporiumAudioPatch
{
	private static List<MethodBase> found;

	private static List<MethodBase> Find()
	{
		if (found != null)
		{
			return found;
		}
		Type[] emptyTypes = Type.EmptyTypes;
		MethodInfo[] array = new MethodInfo[6]
		{
			AccessTools.Method(typeof(EmporiumAudioPlayer), "Play", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(EmporiumAudioPlayer), "PlayMatched", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(EmporiumAudioPlayer), "PlayUnMatched", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(EmporiumAudioPlayer), "PlayBeatVictory", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(EmporiumAudioPlayer), "PlayWalking", emptyTypes, (Type[])null),
			AccessTools.Method(typeof(EmporiumAudioPlayer), "PlayRegister", emptyTypes, (Type[])null)
		};
		found = array.Where((MethodInfo m) => m != null && m.ReturnType == typeof(void)).Cast<MethodBase>().ToList();
		if (found.Count < array.Length)
		{
			Log.Warn("items-use: " + (array.Length - found.Count) + " EmporiumAudioPlayer quiet hook(s) not found in this game build");
		}
		return found;
	}

	private static bool Prepare()
	{
		return Find().Count > 0;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return Find();
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
