using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ProductionTagPatch
{
	private static readonly MethodBase target = ProductionHook.Check(() => AccessTools.Method(typeof(GameItem), "ModifyTag", new Type[3]
	{
		typeof(string),
		typeof(Action<TagState>),
		typeof(bool)
	}, (Type[])null), "GameItem.ModifyTag");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Prefix(GameItem __instance, string tagName, out ProdTag __state)
	{
		__state = (Net.IsClient ? Production.BeforeTag(__instance, tagName) : null);
	}

	private static void Postfix(GameItem __instance, string tagName, ProdTag __state)
	{
		if (__state != null)
		{
			Production.AfterTag(__instance, tagName, __state);
		}
	}
}
