using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class SupQuantumConsole
{
	private static MethodBase Target()
	{
		return AccessTools.Method("QFSW.QC.QuantumConsole:Activate", new Type[1] { typeof(bool) }, (Type[])null);
	}

	private static bool Prepare()
	{
		return Target() != null;
	}

	private static MethodBase TargetMethod()
	{
		return Target();
	}

	private static bool Prefix()
	{
		return Suppress.DevToolAllowed("F8 console");
	}
}
