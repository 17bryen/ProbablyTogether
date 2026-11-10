using System;
using System.Collections.Generic;

namespace ProbablyTogether.UI;

internal static class UiErrors
{
	private static readonly HashSet<string> seen = new HashSet<string>();

	internal static void Report(string where, Exception e)
	{
		string item = where + ":" + e.GetType().Name + ":" + e.Message;
		if (seen.Count < 200 && seen.Add(item))
		{
			Log.Error("ui: " + where + ": " + e);
		}
	}
}
