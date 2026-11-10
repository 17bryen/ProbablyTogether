using System;
using System.Globalization;
using System.Text;

namespace ProbablyTogether;

internal static class Text
{
	internal static string OneLine(string s)
	{
		if (s == null)
		{
			return "";
		}
		int i;
		for (i = 0; i < s.Length && !BreaksLine(s[i]); i++)
		{
		}
		if (i == s.Length)
		{
			return s;
		}
		char[] array;
		for (array = s.ToCharArray(); i < array.Length; i++)
		{
			if (BreaksLine(array[i]))
			{
				array[i] = ' ';
			}
		}
		return new string(array);
	}

	private static bool BreaksLine(char ch)
	{
		if (ch >= ' ' && (ch < '\u007f' || ch > '\u009f') && ch != '\u2028')
		{
			return ch == '\u2029';
		}
		return true;
	}

	internal static string Clip(string s, int max)
	{
		if (s == null)
		{
			return "";
		}
		s = OneLine(s);
		if (s.Length <= max)
		{
			return s;
		}
		int num = Math.Max(0, max);
		if (num > 0 && char.IsHighSurrogate(s[num - 1]))
		{
			num--;
		}
		return s.Substring(0, num) + "...";
	}

	internal static string CleanName(string n)
	{
		if (n == null)
		{
			return "";
		}
		n = n.Trim();
		StringBuilder stringBuilder = new StringBuilder(Math.Min(n.Length, 24));
		for (int i = 0; i < n.Length; i++)
		{
			if (stringBuilder.Length >= 24)
			{
				break;
			}
			char c = n[i];
			if (char.IsHighSurrogate(c))
			{
				if (i + 1 < n.Length && char.IsLowSurrogate(n[i + 1]))
				{
					if (stringBuilder.Length + 2 > 24)
					{
						break;
					}
					stringBuilder.Append(c).Append(n[i + 1]);
					i++;
				}
			}
			else if (!char.IsLowSurrogate(c) && c != '<' && c != '>')
			{
				UnicodeCategory unicodeCategory = char.GetUnicodeCategory(c);
				if (unicodeCategory != UnicodeCategory.Control && unicodeCategory != UnicodeCategory.Format && unicodeCategory != UnicodeCategory.LineSeparator && unicodeCategory != UnicodeCategory.ParagraphSeparator && unicodeCategory != UnicodeCategory.PrivateUse)
				{
					stringBuilder.Append(c);
				}
			}
		}
		return stringBuilder.ToString().Trim();
	}

	internal static string NameKey(string n)
	{
		string text = CleanName(n);
		try
		{
			text = text.Normalize(NormalizationForm.FormKC);
		}
		catch
		{
		}
		return text.ToUpperInvariant();
	}
}
