using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ProbablyTogether.Systems;

public sealed class MsgNightBaseline
{
	public int day;

	/// One line per item: "uid|identifier|count|parent uid|tag=hash,tag=hash,..." (tags sorted).
	public List<string> items;
}

/// At night start the server sends a fingerprint of every item with a hash per tag; each copy compares it with
/// its own and names what differs.
internal static class NightCheck
{
	private static bool inited;

	/// The last comparison: items the same, different, only on the server, only here.
	internal static int Same;

	internal static int Differ;

	internal static int OnlyServer;

	internal static int OnlyHere;

	internal static int Checks;

	internal static string LastSummary = "";

	/// Tags that differed, by name, over every night this session.
	internal static readonly Dictionary<string, int> TagsDiffering = new Dictionary<string, int>(StringComparer.Ordinal);

	[SystemInit]
	private static void Init()
	{
		if (inited)
		{
			return;
		}
		inited = true;
		Net.On<MsgNightBaseline>("night.baseline", null, ClientBaseline);
	}

	/// Server, at night start: sends every item's fingerprint to every player.
	internal static void SendBaseline(int day)
	{
		if (!Net.IsServer || !Game.ShopLoaded)
		{
			return;
		}
		try
		{
			List<string> list = Lines().Values.ToList();
			Net.ToAll("night.baseline", new MsgNightBaseline
			{
				day = day,
				items = list
			});
			Log.Info("night: the starting fingerprint of " + list.Count + " item(s) sent for day " + day);
		}
		catch (Exception ex)
		{
			Log.Warn("night: the starting fingerprint threw: " + ex.Message);
		}
	}

	private static Dictionary<int, string> Lines()
	{
		Dictionary<int, string> dictionary = new Dictionary<int, string>();
		foreach (GameItem item in Items.All())
		{
			if (item == null || item.uniqueId == 0 || Items.IsGone(item))
			{
				continue;
			}
			int uniqueId = item.uniqueId;
			if (!dictionary.ContainsKey(uniqueId))
			{
				dictionary[uniqueId] = Line(item);
			}
		}
		return dictionary;
	}

	private static string Line(GameItem it)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(it.uniqueId.ToString(CultureInfo.InvariantCulture)).Append('|').Append(it.identifier)
			.Append('|')
			.Append(it.unitCount.ToString(CultureInfo.InvariantCulture))
			.Append('|')
			.Append(Items.RefOf((GraphNodeStorage)(object)it.parentInventory)?.ToString() ?? "-")
			.Append('|');
		Dictionary<string, TagState> dictionary = it.state?.dict;
		if (dictionary != null)
		{
			bool flag = true;
			foreach (string item in dictionary.Keys.OrderBy((string k) => k, StringComparer.Ordinal))
			{
				if (!flag)
				{
					stringBuilder.Append(',');
				}
				flag = false;
				stringBuilder.Append(item).Append('=').Append(TagHash(dictionary[item]));
			}
		}
		return stringBuilder.ToString();
	}

	private static string TagHash(TagState t)
	{
		ulong h = 14695981039346656037uL;
		S(t.valueEnabled ? "1" : "0");
		S(t.valueInt.ToString(CultureInfo.InvariantCulture));
		S(t.valueBool ? "1" : "0");
		S(t.valueLong.ToString(CultureInfo.InvariantCulture));
		S(t.valueFloat.ToString("R", CultureInfo.InvariantCulture));
		S(t.valueDouble.ToString("R", CultureInfo.InvariantCulture));
		S(t.valueString);
		return (h & 0xFFFFFFFFu).ToString("x8");
		void S(string s)
		{
			if (s != null)
			{
				foreach (char c in s)
				{
					h ^= c;
					h *= 1099511628211uL;
				}
			}
			h ^= 255uL;
			h *= 1099511628211uL;
		}
	}

	private static void ClientBaseline(MsgNightBaseline m)
	{
		if (m?.items == null || DayLog.Replaying || !Game.ShopLoaded || Net.IsServer)
		{
			return;
		}
		try
		{
			Compare(m);
		}
		catch (Exception ex)
		{
			Log.Warn("night: comparing the starting fingerprint threw: " + ex.Message);
		}
	}

	private static void Compare(MsgNightBaseline m)
	{
		Dictionary<int, string> dictionary = Lines();
		Dictionary<int, string> dictionary2 = new Dictionary<int, string>();
		foreach (string item in m.items)
		{
			int num = item.IndexOf('|');
			if (num > 0 && int.TryParse(item.Substring(0, num), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				dictionary2[result] = item;
			}
		}
		Same = (Differ = (OnlyServer = (OnlyHere = 0)));
		List<string> list = new List<string>();
		foreach (KeyValuePair<int, string> item2 in dictionary2.OrderBy((KeyValuePair<int, string> p) => p.Key))
		{
			if (!dictionary.TryGetValue(item2.Key, out var value))
			{
				OnlyServer++;
				if (list.Count < 12)
				{
					list.Add("only on the server: " + Head(item2.Value));
				}
			}
			else if (value == item2.Value)
			{
				Same++;
			}
			else
			{
				Differ++;
				string text = Explain(item2.Value, value);
				if (list.Count < 12)
				{
					list.Add(text);
				}
			}
		}
		foreach (KeyValuePair<int, string> item3 in dictionary)
		{
			if (!dictionary2.ContainsKey(item3.Key))
			{
				OnlyHere++;
				if (list.Count < 12)
				{
					list.Add("only here: " + Head(item3.Value));
				}
			}
		}
		Checks++;
		LastSummary = "day " + m.day + ": " + Same + " same, " + Differ + " different, " + OnlyServer + " only on the server, " + OnlyHere + " only here";
		Log.Info("night: starting fingerprint " + LastSummary + ((list.Count > 0) ? (": " + string.Join(" | ", list)) : ""));
		if (TagsDiffering.Count > 0)
		{
			Log.Info("night: tags that differed so far this session: " + string.Join(", ", from p in TagsDiffering.OrderByDescending((KeyValuePair<string, int> p) => p.Value).Take(20)
				select p.Key + " x" + p.Value));
		}
	}

	private static string Head(string line)
	{
		string[] array = line.Split(new char[1] { '|' }, 5);
		return "#" + array[0] + " " + ((array.Length > 1) ? array[1] : "?") + ((array.Length > 3) ? (" in " + array[3]) : "");
	}

	private static string Explain(string server, string here)
	{
		string[] array = server.Split(new char[1] { '|' }, 5);
		string[] array2 = here.Split(new char[1] { '|' }, 5);
		List<string> list = new List<string>();
		if (array.Length < 5 || array2.Length < 5)
		{
			return Head(server) + ": unreadable";
		}
		if (array[1] != array2[1])
		{
			list.Add("is " + array2[1] + " here");
		}
		if (array[2] != array2[2])
		{
			list.Add("count " + array[2] + "/" + array2[2]);
		}
		if (array[3] != array2[3])
		{
			list.Add("in " + array[3] + "/" + array2[3]);
		}
		Dictionary<string, string> dictionary = Tags(array[4]);
		Dictionary<string, string> dictionary2 = Tags(array2[4]);
		List<string> list2 = new List<string>();
		foreach (string item in dictionary.Keys.Union(dictionary2.Keys).OrderBy((string k) => k, StringComparer.Ordinal))
		{
			dictionary.TryGetValue(item, out var value);
			dictionary2.TryGetValue(item, out var value2);
			if (value != value2)
			{
				list2.Add(item + ((value == null) ? " (only here)" : ((value2 == null) ? " (only on the server)" : "")));
				TagsDiffering.TryGetValue(item, out var value3);
				TagsDiffering[item] = value3 + 1;
			}
		}
		if (list2.Count > 0)
		{
			list.Add("tags " + string.Join(", ", list2.Take(8)) + ((list2.Count > 8) ? (" +" + (list2.Count - 8)) : ""));
		}
		return Head(server) + ": " + string.Join("; ", list);
	}

	private static Dictionary<string, string> Tags(string s)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		if (string.IsNullOrEmpty(s))
		{
			return dictionary;
		}
		foreach (string item in s.Split(','))
		{
			int num = item.LastIndexOf('=');
			if (num > 0)
			{
				dictionary[item.Substring(0, num)] = item.Substring(num + 1);
			}
		}
		return dictionary;
	}
}
