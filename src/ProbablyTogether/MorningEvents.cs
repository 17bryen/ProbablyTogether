using System;

namespace ProbablyTogether;

internal static class MorningEvents
{
	[ThreadStatic]
	internal static int inMorningReport;

	internal static int Sent;

	internal static int LastDay;

	internal static bool ServerRunsAlone
	{
		get
		{
			if (Net.IsServer)
			{
				if (Net.Applying)
				{
					return inMorningReport > 0;
				}
				return true;
			}
			return false;
		}
	}

	internal static void AfterServerRun(string before)
	{
		if (Server.ShopReady && Game.ShopLoaded && !Snapshot.Capturing)
		{
			int day = Game.Day;
			string text = DialogueEvents.EventsHash();
			if (DayLog.Day != day)
			{
				Log.Info("dialogue: the server ran the calendar's innate events on day " + day + " before that morning's snapshot (log day " + DayLog.Day + "); events " + before + " -> " + text + ": the snapshot will carry them, nothing sent");
			}
			else
			{
				Net.ToAll("events.calendar", new MsgCalendar
				{
					day = day
				});
				Sent++;
				LastDay = day;
				Log.Info("dialogue: the server ran the calendar's innate events outside a replicated action (" + ((inMorningReport > 0) ? "morning report" : "server call") + ", day " + day + "); events " + before + " -> " + text + "; events.calendar sent to everyone (day log " + DayLog.Count + ")");
			}
		}
	}
}
