using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ProbablyTogether.Bots.Scripts;

internal static class ModSyncBot
{
	internal static List<ModList.Info> Mine()
	{
		return ModList.Read((JToken)(object)ModList.Describe()) ?? new List<ModList.Info>();
	}

	internal static string Set()
	{
		List<string> list = (from m in Mine()
			select m.Id + "@" + ((m.Hash.Length >= 12) ? m.Hash.Substring(0, 12) : "nohash")).OrderBy<string, string>((string s) => s, StringComparer.Ordinal).ToList();
		if (list.Count != 0)
		{
			return string.Join(",", list);
		}
		return "-";
	}

	internal static string Describe(IEnumerable<ModList.Info> list)
	{
		if (list == null)
		{
			return "(no list)";
		}
		List<string> list2 = list.Select((ModList.Info m) => m.Name + " [" + m.Id + "] v" + ((m.Version.Length > 0) ? m.Version : "-") + " ws=" + m.Ws + " hash=" + ((m.Hash.Length >= 12) ? m.Hash.Substring(0, 12) : "-") + ((m.Why.Length > 0) ? (" why=" + m.Why) : "")).ToList();
		if (list2.Count != 0)
		{
			return string.Join("; ", list2);
		}
		return "(empty)";
	}
}
