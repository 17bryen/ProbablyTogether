using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ProductionBlendContentsPatch
{
	private static readonly MethodBase target = ProductionHook.Check(() => AccessTools.Method(typeof(MachineFeedDispenser), "BlendContents", new Type[3]
	{
		typeof(GameItem),
		typeof(GameGridInventory),
		typeof(GameItem)
	}, (Type[])null), "MachineFeedDispenser.BlendContents");

	private static readonly FieldInfo blending = AccessTools.Field(typeof(MachineFeedDispenser), "blending");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem machine, GameItem except, ref bool __result, out object __state)
	{
		__state = null;
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (Net.IsRemote)
		{
			__result = false;
			return Production.ClientBlockAutoBlend();
		}
		if (!Production.ServerDay || machine == null || except != null)
		{
			return true;
		}
		try
		{
			if (blending != null && (bool)blending.GetValue(null))
			{
				return true;
			}
		}
		catch
		{
		}
		__state = Production.BeginServerAutoBlend(machine);
		return true;
	}

	private static Exception Finalizer(Exception __exception, bool __result, object __state)
	{
		if (__state != null)
		{
			Production.EndServerAutoBlend(__state, __result, __exception);
		}
		return __exception;
	}
}
