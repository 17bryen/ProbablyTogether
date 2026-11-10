using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsUseRenameTabPatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(StorageExpansionHelper), "SetTabName", new Type[3]
	{
		typeof(GameItem),
		typeof(int),
		typeof(string)
	}, (Type[])null);

	private static bool Prepare()
	{
		return ItemsUseHook.Found(target, "StorageExpansionHelper.SetTabName");
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(GameItem bay, int index, string name)
	{
		return ItemsUse.InterceptRename(bay, index, name);
	}
}
