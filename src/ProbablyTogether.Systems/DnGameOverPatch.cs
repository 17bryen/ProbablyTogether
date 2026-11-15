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
		// A run that would end because of ONE player's perk does not end: that player is taken out (Systems.Perks).
		try
		{
			if (Perks.Enabled)
			{
				int num = 0;
				if (ending == "sentinel")
				{
					num = Perks.Actor;
					if (!Perks.Has(num, "sentinel"))
					{
						num = Perks.HolderOf("sentinel");
					}
				}
				else if (ending == "aug" && !Difficulty.Rules.augGameOverAtFullPresence)
				{
					num = Perks.HolderOf("aug_menace");
				}
				if (num > 0)
				{
					Perks.Died(num, ending);
					skipped = true;
					// The game checks augs last and saves only if the run goes on: make the save it left out,
					// or the morning is never recorded.
					if (ending == "aug" && Net.IsServer && DayNight.NightRunning)
					{
						try
						{
							PlayerStore.Instance.SaveGame();
						}
						catch (Exception ex2)
						{
							Log.Warn("perks: the save after a night the augs did not end threw: " + ex2.Message);
						}
					}
					return false;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("perks: looking at an ending (" + ending + ") threw: " + ex.Message);
		}
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
