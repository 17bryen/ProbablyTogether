using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

/// Keeps a hosted shop's files in their own folder without patching the save library.
/// Slot and ResolveLoadFile return full paths while another folder is in use; names written to the index stay bare.
[HarmonyPatch(typeof(SaveFiles), "Slot")]
internal static class SaveSlotPathPatch
{
	private static void Postfix(ref string __result)
	{
		__result = Paths.File(__result);
	}
}

[HarmonyPatch(typeof(SaveFiles), "ResolveLoadFile")]
internal static class SaveLoadFilePathPatch
{
	private static void Postfix(ref string __result)
	{
		__result = Paths.File(__result);
	}
}

[HarmonyPatch]
internal static class IndexCallSites
{
	private static readonly MethodInfo file = AccessTools.Method(typeof(Paths), "File", (Type[])null, (Type[])null);

	private static readonly MethodInfo load = AccessTools.Method(typeof(Paths), "IndexLoad", (Type[])null, (Type[])null);

	private static readonly MethodInfo save = AccessTools.Method(typeof(Paths), "IndexSave", (Type[])null, (Type[])null);

	private static bool Prepare()
	{
		return true;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return Paths.FindIndexUsers();
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
	{
		foreach (CodeInstruction item in code)
		{
			if (item.operand is MethodInfo { IsGenericMethod: true } methodInfo && methodInfo.DeclaringType == typeof(SaveFiles) && (methodInfo.Name == "IndexLoad" || methodInfo.Name == "IndexSave"))
			{
				item.operand = ((methodInfo.Name == "IndexLoad") ? load : save).MakeGenericMethod(methodInfo.GetGenericArguments());
				Paths.IndexSites++;
				yield return item;
				continue;
			}
			yield return item;
			if (item.operand is string text && text == Paths.IndexName)
			{
				Paths.IndexSites++;
				yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, (object)file);
			}
		}
	}
}
