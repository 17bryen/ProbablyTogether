using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class DnGameOverPatch
{
	private static readonly MethodBase target = DayNightGate.Find(() => AccessTools.Method(typeof(PlayerStore), "ExecuteGameOver", new Type[1] { typeof(string) }, (Type[])null), "PlayerStore.ExecuteGameOver");

	[ThreadStatic]
	private static bool skipped;

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(string ending)
	{
		skipped = false;
		if (!Net.IsRemote)
		{
			return true;
		}
		if (!Net.Applying)
		{
			skipped = true;
			DayNightRun.NoteDropped(ending);
			Log.Info("dayNight: a local ending (" + ending + ") was dropped: the server decides endings");
			return false;
		}
		return true;
	}

	private static void Postfix(string ending)
	{
		if (skipped)
		{
			skipped = false;
		}
		else if (Net.IsServer)
		{
			DayNightRun.OnGameOverServer(ending);
		}
		else if (Net.IsClient)
		{
			DayNightRun.NoteShown(ending);
		}
	}
}
