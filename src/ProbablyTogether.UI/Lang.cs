using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace ProbablyTogether.UI;

internal static class Lang
{
	private static string code = "en";

	private static float checkedAt = -10f;

	private static Dictionary<string, string> table;

	private static List<(Regex re, string en, string tr)> templates;

	private static readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);

	internal static readonly string[] Languages = new string[3] { "en", "zh", "ru" };

	internal static string LabOverride;

	internal static string Code
	{
		get
		{
			Refresh();
			return code;
		}
	}

	internal static int Version { get; private set; }

	internal static string Tr(string en)
	{
		if (string.IsNullOrEmpty(en))
		{
			return en ?? "";
		}
		Refresh();
		if (table == null)
		{
			return en;
		}
		if (table.TryGetValue(en, out var value))
		{
			return value;
		}
		if (cache.TryGetValue(en, out value))
		{
			return value;
		}
		value = FromTemplate(en) ?? en;
		if (cache.Count > 600)
		{
			cache.Clear();
		}
		cache[en] = value;
		return value;
	}

	internal static string Trf(string en, params object[] args)
	{
		string format = Tr(en);
		try
		{
			return string.Format(CultureInfo.InvariantCulture, format, args);
		}
		catch (FormatException)
		{
			return string.Format(CultureInfo.InvariantCulture, en, args);
		}
	}

	internal static string Upper(string s)
	{
		return (s ?? "").ToUpper(CultureInfo.InvariantCulture);
	}

	private static string FromTemplate(string text)
	{
		if (templates == null || templates.Count == 0)
		{
			return null;
		}
		foreach (var template in templates)
		{
			Regex item = template.re;
			string item2 = template.tr;
			Match match = item.Match(text);
			if (match.Success)
			{
				object[] array = new object[match.Groups.Count - 1];
				for (int i = 1; i < match.Groups.Count; i++)
				{
					string value = match.Groups[i].Value;
					array[i - 1] = ((table != null && table.TryGetValue(value, out var value2)) ? value2 : value);
				}
				try
				{
					return string.Format(CultureInfo.InvariantCulture, item2, array);
				}
				catch (FormatException)
				{
					return null;
				}
			}
		}
		return null;
	}

	private static void Refresh()
	{
		if (!(Time.realtimeSinceStartup - checkedAt < 1f) || !(checkedAt > 0f))
		{
			checkedAt = Time.realtimeSinceStartup;
			string text = LabOverride ?? ReadGame();
			if (!(text == code) || (table == null && !(text == "en") && Has(text)))
			{
				Set(text);
			}
		}
	}

	private static bool Has(string c)
	{
		if (!(c == "zh"))
		{
			return c == "ru";
		}
		return true;
	}

	private static void Set(string now)
	{
		bool num = now != code;
		code = now;
		table = ((now == "zh") ? LangZh.Table : ((now == "ru") ? LangRu.Table : null));
		templates = ((table == null) ? null : Compile(table));
		cache.Clear();
		if (num)
		{
			Version++;
			Log.Info("lang: our texts now follow the game's language '" + code + "'" + ((table != null) ? (" (" + table.Count + " entries)") : " (no table: English)"));
		}
	}

	internal static void ForceRefresh()
	{
		checkedAt = -10f;
		Refresh();
	}

	private static string ReadGame()
	{
		try
		{
			Locale selectedLocale = LocalizationSettings.SelectedLocale;
			object c;
			if (!((Object)(object)selectedLocale != (Object)null))
			{
				c = "";
			}
			else
			{
				LocaleIdentifier identifier = selectedLocale.Identifier;
				c = identifier.Code ?? "";
			}
			return Normalize((string)c);
		}
		catch
		{
			return "en";
		}
	}

	internal static string Normalize(string c)
	{
		c = (c ?? "").Trim().ToLowerInvariant();
		if (c.Length == 0)
		{
			return "en";
		}
		if (c.StartsWith("zh", StringComparison.Ordinal))
		{
			return "zh";
		}
		int num = c.IndexOfAny(new char[2] { '-', '_' });
		if (num <= 0 || c.StartsWith("pt", StringComparison.Ordinal))
		{
			return c;
		}
		return c.Substring(0, num);
	}

	private static List<(Regex, string, string)> Compile(Dictionary<string, string> t)
	{
		List<(Regex, string, string)> list = new List<(Regex, string, string)>();
		foreach (KeyValuePair<string, string> item in t)
		{
			if (item.Key.IndexOf('{') < 0 || Regex.Replace(item.Key, "\\{\\d\\}", "").Trim().Length < 6)
			{
				continue;
			}
			StringBuilder stringBuilder = new StringBuilder("^");
			int num = 0;
			foreach (Match item2 in Regex.Matches(item.Key, "\\{(\\d)\\}"))
			{
				stringBuilder.Append(Regex.Escape(item.Key.Substring(num, item2.Index - num)));
				stringBuilder.Append("(.+?)");
				num = item2.Index + item2.Length;
			}
			stringBuilder.Append(Regex.Escape(item.Key.Substring(num))).Append('$');
			try
			{
				list.Add((new Regex(stringBuilder.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant), item.Key, item.Value));
			}
			catch
			{
			}
		}
		list.Sort(((Regex, string, string) a, (Regex, string, string) b) => b.Item2.Length.CompareTo(a.Item2.Length));
		return list;
	}
}
