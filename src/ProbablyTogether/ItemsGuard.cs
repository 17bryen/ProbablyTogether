using System;
using System.Collections.Generic;
using System.Linq;

namespace ProbablyTogether;

internal static class ItemsGuard
{
	private static readonly HashSet<string> seen = new HashSet<string>();

	internal static void Saw(string what, GameItem item, GraphNodeStorage where)
	{
		if (!Net.IsClient || !Net.IsLive || Net.Applying || Snapshot.IsLoading || ItemsCapture.Current != null || seen.Count >= 60)
		{
			return;
		}
		try
		{
			if (Items.RootOf((GraphNodeStorage)(((object)where) ?? ((object)((item != null) ? item.parentInventory : null)))) != null)
			{
				string[] array = (from l in (Environment.StackTrace ?? "").Split('\n')
					select l.Trim() into l
					where l.StartsWith("at ", StringComparison.Ordinal) && !l.Contains("ProbablyTogether.") && !l.Contains("System.Environment") && !l.Contains("HarmonySharedState")
					select l).Take(4).ToArray();
				string item2 = what + "|" + ((array.Length != 0) ? array[0] : "");
				if (seen.Add(item2))
				{
					Log.Warn("items: this client changed the shop outside a server action: " + what + " " + (((item != null) ? item.identifier : null) ?? "?") + "#" + Ids.Of(item) + " (not synced; tell the items-move owner) via " + string.Join(" < ", array));
				}
			}
		}
		catch
		{
		}
	}
}
