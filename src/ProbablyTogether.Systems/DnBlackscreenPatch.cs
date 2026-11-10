using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

[HarmonyPatch]
internal static class DnBlackscreenPatch
{
	private static readonly MethodBase target = DayNightGate.Find(() => AccessTools.Method(typeof(StoreUIManager), "StartBlackscreenSequence", new Type[5]
	{
		typeof(bool),
		typeof(Action),
		typeof(Action),
		typeof(float),
		typeof(float)
	}, (Type[])null), "StoreUIManager.StartBlackscreenSequence");

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(StoreUIManager __instance, bool playFullSequence, ref Action actionDuringBlack, ref Action actionAfterBlack)
	{
		DayNightBlackscreen dayNightBlackscreen = DayNightBlackscreen.Take();
		if (dayNightBlackscreen == null)
		{
			return true;
		}
		if (dayNightBlackscreen.WrapWith != null)
		{
			actionDuringBlack = dayNightBlackscreen.WrapWith(actionDuringBlack);
			return true;
		}
		try
		{
			actionDuringBlack?.Invoke();
		}
		catch (Exception ex)
		{
			Log.Error("dayNight: blackscreen action threw (continuing, like the game): " + ex);
		}
		if (playFullSequence)
		{
			try
			{
				actionAfterBlack?.Invoke();
			}
			catch (Exception ex2)
			{
				Log.Error("dayNight: blackscreen after-action threw: " + ex2);
			}
		}
		if (dayNightBlackscreen.Visual)
		{
			try
			{
				__instance.FinishBlackscreenSequence(true);
			}
			catch
			{
			}
		}
		return false;
	}
}
