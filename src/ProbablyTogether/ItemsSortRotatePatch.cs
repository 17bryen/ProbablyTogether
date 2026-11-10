using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class ItemsSortRotatePatch
{
	private static readonly MethodBase target = ItemsHook.Check(() => AccessTools.PropertyGetter(typeof(EmporiumOption), "SortAutoRotate"), "EmporiumOption.SortAutoRotate");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(ref bool __result)
	{
		bool? sortRotateOverride = ItemsMove.SortRotateOverride;
		if (sortRotateOverride.HasValue)
		{
			bool valueOrDefault = sortRotateOverride == true;
			__result = valueOrDefault;
			return false;
		}
		return true;
	}
}
