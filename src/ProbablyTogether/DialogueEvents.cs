using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

internal static class DialogueEvents
{
	[ThreadStatic]
	internal static int inNewspaper;

	private static readonly FieldInfo[] storyFields = (from f in typeof(StoreClientData).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
		where f.IsDefined(typeof(ES3Serializable), inherit: false) || f.IsPublic
		select f).OrderBy<FieldInfo, string>((FieldInfo f) => f.Name, StringComparer.Ordinal).ToArray();

	internal static void Register()
	{
		Net.On<MsgCalendar>("events.calendar", ServerCalendar, ClientCalendar);
	}

	private static void ServerCalendar(MsgCalendar m, int player)
	{
		if (!Game.ShopLoaded)
		{
			return;
		}
		AdvCalendarUIManager instance = AdvCalendarUIManager.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			Log.Warn("dialogue: events.calendar from " + Net.PlayerName(player) + " but the server has no calendar");
			return;
		}
		int day = Game.Day;
		using (Net.Apply())
		{
			instance.HandleInnateEvent();
		}
		Net.ToAll("events.calendar", new MsgCalendar
		{
			day = day
		});
	}

	private static void ClientCalendar(MsgCalendar m)
	{
		AdvCalendarUIManager instance = AdvCalendarUIManager.Instance;
		if ((Object)(object)instance == (Object)null || !Game.ShopLoaded)
		{
			return;
		}
		if (m.day != Game.Day)
		{
			Log.Warn("dialogue: events.calendar for day " + m.day + " on day " + Game.Day + ": skipped");
			return;
		}
		using (Net.Apply())
		{
			instance.HandleInnateEvent();
		}
	}

	internal static string StoryHash()
	{
		StoreClientData val = Game.Store?.storeClientData;
		if (val == null)
		{
			return "";
		}
		ulong h = 14695981039346656037uL;
		FieldInfo[] array = storyFields;
		foreach (FieldInfo fieldInfo in array)
		{
			object value;
			try
			{
				value = fieldInfo.GetValue(val);
			}
			catch
			{
				continue;
			}
			Mix(ref h, fieldInfo.Name);
			Mix(ref h, Simple(value));
		}
		return h.ToString("x16");
	}

	internal static string EventsHash()
	{
		StoreEventManager val = Game.Station?.storeEventManager;
		if (val == null)
		{
			return "";
		}
		List<string> list = new List<string>();
		try
		{
			foreach (StoreEvent activeEvent in val.GetActiveEvents())
			{
				list.Add("a|" + activeEvent.identifier + "|" + activeEvent.duration + "|" + activeEvent.startDay + "|" + activeEvent.IsKnown);
			}
			if (val.futurEvents != null)
			{
				for (int i = 0; i < val.futurEvents.Count; i++)
				{
					StoreEvent val2 = val.futurEvents[i];
					int num = ((val.futurEventsDay != null && i < val.futurEventsDay.Count) ? val.futurEventsDay[i] : (-1));
					list.Add("f|" + val2?.identifier + "|" + num + "|" + val2?.IsKnown);
				}
			}
			if (val.eventTracker != null)
			{
				foreach (KeyValuePair<string, int> item in val.eventTracker)
				{
					list.Add("t|" + item.Key + "|" + item.Value);
				}
			}
		}
		catch (Exception ex)
		{
			return "!" + ex.GetType().Name;
		}
		list.Sort(StringComparer.Ordinal);
		ulong h = 14695981039346656037uL;
		foreach (string item2 in list)
		{
			Mix(ref h, item2);
		}
		return h.ToString("x16") + "/" + list.Count;
	}

	internal static string RequestsHash()
	{
		PlayerStore store = Game.Store;
		if (store?.storeRequests == null)
		{
			return "";
		}
		List<string> list = store.storeRequests.Select((StoreRequest r) => r.id + "|" + r.duration).OrderBy<string, string>((string x) => x, StringComparer.Ordinal).ToList();
		ulong h = 14695981039346656037uL;
		foreach (string item in list)
		{
			Mix(ref h, item);
		}
		return h.ToString("x16") + "/" + list.Count;
	}

	private static string Simple(object v)
	{
		if (v != null)
		{
			if (!(v is string result))
			{
				if (!(v is bool))
				{
					if (!(v is IFormattable formattable))
					{
						if (v is IEnumerable enumerable)
						{
							StringBuilder stringBuilder = new StringBuilder("[");
							foreach (object item in enumerable)
							{
								stringBuilder.Append((item is IFormattable formattable2) ? formattable2.ToString(null, CultureInfo.InvariantCulture) : item?.ToString()).Append(',');
							}
							return stringBuilder.Append(']').ToString();
						}
						return v.GetType().Name;
					}
					return formattable.ToString(null, CultureInfo.InvariantCulture);
				}
				if (!(bool)v)
				{
					return "0";
				}
				return "1";
			}
			return result;
		}
		return "null";
	}

	private static void Mix(ref ulong h, string s)
	{
		if (s == null)
		{
			s = "";
		}
		string text = s;
		foreach (char c in text)
		{
			h ^= c;
			h *= 1099511628211uL;
		}
		h ^= 255uL;
		h *= 1099511628211uL;
	}
}
