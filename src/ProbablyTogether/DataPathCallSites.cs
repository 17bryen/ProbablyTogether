using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class DataPathCallSites
{
	private static readonly MethodInfo Replacement = AccessTools.Method(typeof(Paths), "GameDataPath", (Type[])null, (Type[])null);

	private static bool Prepare()
	{
		return Paths.Redirecting;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return Paths.FindCallers();
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
	{
		foreach (CodeInstruction item in code)
		{
			if (item.operand is MethodInfo { Name: "get_persistentDataPath" } methodInfo && methodInfo.DeclaringType == typeof(Application))
			{
				item.operand = Replacement;
				Paths.RewrittenSites++;
			}
			yield return item;
		}
	}
}
