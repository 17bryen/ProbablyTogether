using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class MorningEventsInnatePatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(AdvCalendarUIManager), "HandleInnateEvent", Type.EmptyTypes, (Type[])null);

	private static bool Prepare()
	{
		if (target == null)
		{
			Log.Error("dialogue: AdvCalendarUIManager.HandleInnateEvent() not found; the server's morning events are not sent");
		}
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Prefix(out string __state)
	{
		__state = null;
		if (MorningEvents.ServerRunsAlone && Server.ShopReady && Game.ShopLoaded)
		{
			__state = DialogueEvents.EventsHash();
		}
	}

	private static void Postfix(bool __runOriginal, string __state)
	{
		if (!__runOriginal || __state == null || !MorningEvents.ServerRunsAlone)
		{
			return;
		}
		try
		{
			MorningEvents.AfterServerRun(__state);
		}
		catch (Exception ex)
		{
			Log.Error("dialogue: sending the server's morning events failed: " + ex);
		}
	}
}
