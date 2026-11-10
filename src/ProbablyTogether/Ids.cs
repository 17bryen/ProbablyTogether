using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

public static class Ids
{
	public const long DossierId = -101L;

	public const long TrashcanId = -102L;

	public const long CassetteId = -103L;

	public const long DrainId = -1104L;

	public const long FaucetId = -1105L;

	private static readonly Dictionary<int, GameItem> index = new Dictionary<int, GameItem>();

	private static int lastRebuildFrame = -1;

	private static int uidAtRebuild = int.MinValue;

	internal static int StandInsSkipped;

	public static int Count => index.Count;

	public static long Of(GameItem item)
	{
		if (item == null)
		{
			return 0L;
		}
		int uniqueId = item.uniqueId;
		if (uniqueId == 0)
		{
			if (!Net.IsServer)
			{
				return 0L;
			}
			return Assign(item);
		}
		if (!index.ContainsKey(uniqueId))
		{
			index[uniqueId] = item;
		}
		return uniqueId;
	}

	public static GameItem Find(long id)
	{
		if (id == 0L || id > int.MaxValue || id < int.MinValue)
		{
			return null;
		}
		int num = (int)id;
		if (index.TryGetValue(num, out var value) && Live(value, num))
		{
			return value;
		}
		if (lastRebuildFrame != Time.frameCount || UidMoved())
		{
			Rebuild();
		}
		if (!index.TryGetValue(num, out value) || !Live(value, num))
		{
			return null;
		}
		return value;
	}

	public static long Assign(GameItem item)
	{
		if (item == null)
		{
			return 0L;
		}
		if (Net.IsClient)
		{
			Log.Warn("Ids.Assign called on a client (clients never mint ids: use the server's via Replay or Bind)");
			return item.uniqueId;
		}
		int num = item.uniqueId;
		if (num == 0 && IsStandIn(item))
		{
			StandInsSkipped++;
			string text = Text.Clip(item.identifier ?? "", 64);
			if (Log.Allow("ids|standin|" + text, 60000L, out var skipped))
			{
				Log.Warn("server: '" + text + "' is not an item this game version knows (the game made a stand-in): it gets no id, so it stays id 0 on every copy and no request can move it" + Log.More(skipped));
			}
			return 0L;
		}
		if (num == 0 || (index.TryGetValue(num, out var value) && value != item && Live(value, num)))
		{
			PlayerStore store = Game.Store;
			if (store == null)
			{
				return num;
			}
			num = (item.uniqueId = store.GetUniqueID());
		}
		index[num] = item;
		return num;
	}

	public static void Bind(GameItem item, long id)
	{
		if (item != null && id != 0L)
		{
			item.uniqueId = (int)id;
			index[(int)id] = item;
		}
	}

	public static void Forget(GameItem item)
	{
		if (item != null && index.TryGetValue(item.uniqueId, out var value) && value == item)
		{
			index.Remove(item.uniqueId);
		}
	}

	internal static bool IsStandIn(GameItem it)
	{
		if (it != null && it.spritePath == "unknown")
		{
			return it.spriteAtlasPath == "Items/itematlas";
		}
		return false;
	}

	private static bool UidMoved()
	{
		PlayerStore store = Game.Store;
		if (store != null)
		{
			return store.currentUniqueId != uidAtRebuild;
		}
		return false;
	}

	private static bool Live(GameItem it, int u)
	{
		if (it != null && it.uniqueId == u)
		{
			GameItemElement val = (GameItemElement)(object)((it is GameItemElement) ? it : null);
			if (val != null)
			{
				return !val.IsDestroyed();
			}
			return true;
		}
		return false;
	}

	internal static List<GameItem> All()
	{
		return Collect(transient: true);
	}

	internal static List<GameItem> Saved()
	{
		return Collect(transient: false);
	}

	private static List<GameItem> Collect(bool transient)
	{
		List<GameItem> list = new List<GameItem>();
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return list;
		}
		HashSet<GraphNodeStorage> hashSet = new HashSet<GraphNodeStorage>();
		foreach (GraphNodeStorage item in transient ? AllRoots(instance) : SavedRoots(instance))
		{
			if (item != null && hashSet.Add(item))
			{
				list.AddRange(GraphUtils.FindAllChildrenType<GameItem>(item, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null));
			}
		}
		return list;
	}

	private static IEnumerable<GraphNodeStorage> SavedRoots(EmporiumEntry e)
	{
		foreach (GraphNodeStorage item in Game.Roots())
		{
			yield return item;
		}
		yield return (GraphNodeStorage)(object)e.soldElement;
		yield return (GraphNodeStorage)(object)e.hirelingInv;
		yield return (GraphNodeStorage)(object)e.cassettePlayerElement;
		yield return (GraphNodeStorage)(object)e.faucetElement;
		yield return (GraphNodeStorage)(object)e.drainInvElement;
	}

	private static IEnumerable<GraphNodeStorage> AllRoots(EmporiumEntry e)
	{
		foreach (GraphNodeStorage item in SavedRoots(e))
		{
			yield return item;
		}
		yield return (GraphNodeStorage)(object)e.afterhourInventory;
		yield return (GraphNodeStorage)(object)e.afterhourPocketSlotInvLeft;
		yield return (GraphNodeStorage)(object)e.afterhourPocketSlotInvBackpack;
		yield return (GraphNodeStorage)(object)e.afterhourPocketSlotInvRight;
		yield return (GraphNodeStorage)(object)e.backInvinvElementCounter;
		yield return (GraphNodeStorage)(object)e.bazarLeftinvElement;
		yield return (GraphNodeStorage)(object)e.swapBufferElement;
		yield return (GraphNodeStorage)(object)e.responseInventory;
		yield return (GraphNodeStorage)(object)e.responseInventoryClosable;
	}

	internal static void Rebuild()
	{
		lastRebuildFrame = Time.frameCount;
		uidAtRebuild = Game.Store?.currentUniqueId ?? int.MinValue;
		index.Clear();
		foreach (GameItem item in All())
		{
			int uniqueId = item.uniqueId;
			if (uniqueId != 0 && !index.ContainsKey(uniqueId))
			{
				index[uniqueId] = item;
			}
		}
	}

	internal static void AfterLoad()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance != (Object)null)
		{
			if (instance.drain != null)
			{
				instance.drain.uniqueId = -1104;
			}
			if (instance.faucet != null)
			{
				instance.faucet.uniqueId = -1105;
			}
		}
		Rebuild();
	}

	internal static int Normalize()
	{
		PlayerStore store = Game.Store;
		if (store == null)
		{
			return 0;
		}
		HashSet<int> hashSet = new HashSet<int>();
		int num = 0;
		int num2 = 0;
		string text = null;
		foreach (GameItem item in Saved())
		{
			int uniqueId = item.uniqueId;
			bool flag = (long)uniqueId == -101 || (long)uniqueId == -102 || (long)uniqueId == -103 || (long)uniqueId == -1104 || (long)uniqueId == -1105;
			if (uniqueId == 0 && IsStandIn(item))
			{
				num2++;
				if (text == null)
				{
					text = item.identifier ?? "";
				}
			}
			else if ((uniqueId == 0 || !hashSet.Add(uniqueId)) && !(uniqueId != 0 && flag))
			{
				item.uniqueId = store.GetUniqueID();
				hashSet.Add(item.uniqueId);
				num++;
			}
		}
		if (num > 0)
		{
			Log.Info("server: gave " + num + " item(s) a fresh id (uniqueId 0 or a duplicate)");
		}
		if (num2 > 0 && Log.Allow("ids|normalize-standin", 60000L, out var skipped))
		{
			Log.Warn("server: " + num2 + " stand-in item(s) in the shop (identifiers this game version does not know, first '" + Text.Clip(text, 64) + "') keep id 0" + Log.More(skipped));
		}
		Rebuild();
		return num;
	}
}
