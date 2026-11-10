using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUseQuietSoundsPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		Type[] array = new Type[1] { typeof(string) };
		return new MethodInfo[4]
		{
			AccessTools.Method(typeof(AudioManager), "Play", array, (Type[])null),
			AccessTools.Method(typeof(AudioManager), "Play2", array, (Type[])null),
			AccessTools.Method(typeof(AudioManager), "Play3", array, (Type[])null),
			AccessTools.Method(typeof(AudioManager), "TryPlayOne", new Type[1] { typeof(string[]) }, (Type[])null)
		}.Where((MethodInfo m) => m != null && m.ReturnType == typeof(bool)).Cast<MethodBase>().ToList();
	}

	private static bool Prepare()
	{
		return TargetMethods().Any();
	}

	private static bool Prefix(ref bool __result)
	{
		if (!ItemsUse.QuietUi)
		{
			ItemsUse.SoundsPlayed++;
			return true;
		}
		ItemsUse.QuietSoundsBlocked++;
		__result = false;
		return false;
	}
}
