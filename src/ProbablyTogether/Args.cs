using System;
using System.Collections.Generic;
using ProbablyTogether.Bots;

namespace ProbablyTogether;

public static class Args
{
	private static bool parsed;

	private static readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private static readonly HashSet<string> flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static readonly HashSet<string> FlagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--br-server", "--br-host" };

	public static bool Server
	{
		get
		{
			Parse();
			return flags.Contains("--br-server");
		}
	}

	public static bool Host
	{
		get
		{
			Parse();
			return flags.Contains("--br-host");
		}
	}

	public static string Config => Get("--br-config");

	public static string Data => Get("--br-data");

	public static string Join => Get("--br-join");

	public static string Name => Get("--br-name");

	public static string Password => Get("--br-password");

	public static string BotArg => Get("--br-bot");

	public static string Bot
	{
		get
		{
			string botArg = BotArg;
			if (botArg == null || !BotRunner.LabAllowed(out var _))
			{
				return null;
			}
			return botArg;
		}
	}

	public static string SelfTest => Get("--br-selftest");

	public static string Get(string name)
	{
		Parse();
		if (!values.TryGetValue(name, out var value))
		{
			return null;
		}
		return value;
	}

	public static bool Has(string name)
	{
		Parse();
		if (!flags.Contains(name))
		{
			return values.ContainsKey(name);
		}
		return true;
	}

	private static void Parse()
	{
		if (parsed)
		{
			return;
		}
		parsed = true;
		string[] array;
		try
		{
			array = Environment.GetCommandLineArgs();
		}
		catch
		{
			array = new string[0];
		}
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i];
			if (text != null && text.StartsWith("--br-", StringComparison.OrdinalIgnoreCase))
			{
				int num = text.IndexOf('=');
				if (num > 0)
				{
					values[text.Substring(0, num)] = text.Substring(num + 1).Trim().Trim('"');
				}
				else if (FlagNames.Contains(text))
				{
					flags.Add(text);
				}
				else if (i + 1 < array.Length && array[i + 1] != null && !array[i + 1].StartsWith("-"))
				{
					values[text] = array[i + 1].Trim().Trim('"');
					i++;
				}
				else
				{
					flags.Add(text);
				}
			}
		}
	}

	public static string Describe()
	{
		Parse();
		List<string> list = new List<string>();
		foreach (string flag in flags)
		{
			list.Add(flag);
		}
		foreach (KeyValuePair<string, string> value in values)
		{
			list.Add(value.Key + "=" + (value.Key.Equals("--br-password", StringComparison.OrdinalIgnoreCase) ? "***" : value.Value));
		}
		if (list.Count != 0)
		{
			return string.Join(" ", list);
		}
		return "(none)";
	}
}
