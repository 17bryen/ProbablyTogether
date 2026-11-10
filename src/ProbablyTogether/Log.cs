using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProbablyTogether;

public static class Log
{
	private sealed class Gate
	{
		public long Ms;

		public int Skipped;
	}

	private static readonly Dictionary<string, Gate> gates = new Dictionary<string, Gate>();

	private const int MaxGates = 512;

	internal static int GateCount => gates.Count;

	public static void Info(string m)
	{
		Debug.Log((object)("[ProbablyTogether] " + Text.OneLine(m)));
	}

	public static void Warn(string m)
	{
		Debug.LogWarning((object)("[ProbablyTogether] " + Text.OneLine(m)));
	}

	public static void Error(string m)
	{
		Debug.LogError((object)("[ProbablyTogether] " + Text.OneLine(m)));
	}

	internal static bool Allow(string key, long intervalMs, out int skipped)
	{
		long ms = Clock.Ms;
		skipped = 0;
		if (!gates.TryGetValue(key, out var value))
		{
			if (gates.Count >= 512)
			{
				Prune(ms, intervalMs);
			}
			gates[key] = new Gate
			{
				Ms = ms
			};
			return true;
		}
		if (ms - value.Ms < intervalMs)
		{
			value.Skipped++;
			return false;
		}
		skipped = value.Skipped;
		value.Skipped = 0;
		value.Ms = ms;
		return true;
	}

	internal static string More(int skipped)
	{
		if (skipped <= 0)
		{
			return "";
		}
		return " (" + skipped + " more like it held back)";
	}

	internal static void ForgetPrefix(string prefix)
	{
		List<string> list = null;
		foreach (string key in gates.Keys)
		{
			if (key.StartsWith(prefix, StringComparison.Ordinal))
			{
				(list ?? (list = new List<string>())).Add(key);
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (string item in list)
		{
			gates.Remove(item);
		}
	}

	private static void Prune(long now, long intervalMs)
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, Gate> gate in gates)
		{
			if (now - gate.Value.Ms >= Math.Max(intervalMs, 10000L))
			{
				list.Add(gate.Key);
			}
		}
		foreach (string item in list)
		{
			gates.Remove(item);
		}
		if (gates.Count >= 512)
		{
			gates.Clear();
		}
	}
}
