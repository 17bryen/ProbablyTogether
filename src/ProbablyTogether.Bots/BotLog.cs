using UnityEngine;

namespace ProbablyTogether.Bots;

public static class BotLog
{
	public const string Prefix = "[ProbablyTogether][bot] ";

	public static void Pass(string script, string step, string detail)
	{
		Write("PASS", script, step, detail);
	}

	public static void Fail(string script, string step, string detail)
	{
		Write("FAIL", script, step, detail);
	}

	public static void Info(string script, string text)
	{
		Debug.Log((object)("[ProbablyTogether][bot] INFO " + Token(script) + " " + OneLine(text)));
	}

	private static void Write(string verdict, string script, string step, string detail)
	{
		Debug.Log((object)("[ProbablyTogether][bot] " + verdict + " " + Token(script) + " " + Token(step) + " " + OneLine(detail)));
	}

	internal static string Token(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return s.Trim().Replace(' ', '-');
		}
		return "-";
	}

	internal static string OneLine(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "-";
		}
		s = s.Replace("\r", "").Replace("\n", " / ").Trim();
		if (s.Length <= 600)
		{
			return s;
		}
		return s.Substring(0, 600) + "...";
	}
}
