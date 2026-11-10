using System;
using System.IO;
using System.Text;

namespace ProbablyTogether;

internal static class SaveCheck
{
	internal static readonly string[] AllowedTypes = new string[2] { "PlayerStore,Assembly-CSharp", "StoreStation,Assembly-CSharp" };

	private const string Key = "__type";

	internal static string AsEs3Reads(byte[] bytes)
	{
		using StreamReader streamReader = new StreamReader(new MemoryStream(bytes, writable: false));
		return streamReader.ReadToEnd();
	}

	internal static string TypesProblem(string s, out int types)
	{
		types = 0;
		if (s == null)
		{
			return "no save text";
		}
		for (int i = 0; i < s.Length; i++)
		{
			if (!IsQuote(s[i]) || BackslashesBefore(s, i) % 2 == 1)
			{
				continue;
			}
			int num = MatchKey(s, i + 1);
			if (num < 0 || num >= s.Length || !IsQuote(s[num]))
			{
				continue;
			}
			int num2 = SkipWhite(s, num + 1);
			if (num2 < s.Length && s[num2] == ':')
			{
				num2 = SkipWhite(s, num2 + 1);
				int end;
				string text = ReadString(s, num2, out end);
				if (text == null)
				{
					return "a __type entry without a type name (character " + i + ")";
				}
				if (Array.IndexOf(AllowedTypes, text) < 0)
				{
					return "the save asks for the type '" + Text.Clip(text, 80) + "'";
				}
				types++;
				i = end;
			}
		}
		return null;
	}

	private static int MatchKey(string s, int p)
	{
		for (int i = 0; i < "__type".Length; i++)
		{
			if (p < s.Length && s[p] == '\\')
			{
				p++;
			}
			if (p >= s.Length || s[p] != "__type"[i])
			{
				return -1;
			}
			p++;
		}
		return p;
	}

	private static string ReadString(string s, int p, out int end)
	{
		end = p;
		if (p >= s.Length || !IsQuote(s[p]))
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = p + 1; i < s.Length; i++)
		{
			char c = s[i];
			if (IsQuote(c))
			{
				end = i;
				return stringBuilder.ToString();
			}
			if (c == '\\')
			{
				if (++i >= s.Length)
				{
					return null;
				}
				c = s[i];
				switch (c)
				{
				case 'b':
					c = '\b';
					break;
				case 'f':
					c = '\f';
					break;
				case 'n':
					c = '\n';
					break;
				case 'r':
					c = '\r';
					break;
				case 't':
					c = '\t';
					break;
				}
			}
			if (stringBuilder.Length > 512)
			{
				return null;
			}
			stringBuilder.Append(c);
		}
		return null;
	}

	private static bool IsQuote(char c)
	{
		if (c != '"' && c != '“')
		{
			return c == '”';
		}
		return true;
	}

	private static int BackslashesBefore(string s, int i)
	{
		int j;
		for (j = 0; i - 1 - j >= 0 && s[i - 1 - j] == '\\'; j++)
		{
		}
		return j;
	}

	private static int SkipWhite(string s, int p)
	{
		while (p < s.Length && (s[p] == ' ' || s[p] == '\t' || s[p] == '\n' || s[p] == '\r'))
		{
			p++;
		}
		return p;
	}
}
