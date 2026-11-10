using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsCreatedCharPatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.Method(typeof(DirectoryMaster), "CharacterItem", new Type[1] { typeof(string) }, (Type[])null), "DirectoryMaster.CharacterItem");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix(GameCharacterItem __result)
	{
		if (Net.IsOnline)
		{
			Items.OnItemCreated((GameItem)(object)__result);
		}
	}
}
