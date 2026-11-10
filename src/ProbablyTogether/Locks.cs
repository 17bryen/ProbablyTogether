using System;
using System.Collections.Generic;

namespace ProbablyTogether;

public static class Locks
{
	private sealed class Entry
	{
		public int Player;

		public long TouchedMs;

		public string Name;

		public long WantedMs;

		public int WantedBy;
	}

	private static readonly Dictionary<string, Entry> held = new Dictionary<string, Entry>();

	private static readonly Dictionary<string, int> view = new Dictionary<string, int>();

	private static long lastSweepMs;

	public static int TimeoutSeconds = 60;

	public static int WantedTimeoutSeconds = 30;

	private const long WantedWindowMs = 15000L;

	public static event Action<string, int> Changed;

	/// Counter takeover: another player may take a held counter once its holder has been idle this long.
	/// Negative = strict (wait for the idle timeout or a release).
	public static double TakeoverSeconds = -1.0;

	public static bool CanTakeOver(string name, int player)
	{
		if (TakeoverSeconds < 0.0)
		{
			return false;
		}
		if (!Net.IsServer)
		{
			return true;
		}
		if (name == null || player <= 0)
		{
			return false;
		}
		if (!held.TryGetValue(name, out var value) || value.Player == player)
		{
			return true;
		}
		return (double)(Clock.Ms - value.TouchedMs) >= TakeoverSeconds * 1000.0;
	}

	/// Server: hand a held lock to another player (see CanTakeOver).
	public static bool TakeOver(string name, int player)
	{
		if (!Net.IsServer || !CanTakeOver(name, player))
		{
			return false;
		}
		int num = (held.TryGetValue(name, out var value) ? value.Player : (-1));
		if (num == player)
		{
			value.TouchedMs = Clock.Ms;
			return true;
		}
		held[name] = new Entry
		{
			Player = player,
			TouchedMs = Clock.Ms,
			Name = Net.PlayerName(player)
		};
		Broadcast(name, player);
		Log.Info("server: lock '" + name + "' taken over by " + Net.PlayerName(player) + ((num >= 0) ? (" from " + Net.PlayerName(num)) : ""));
		return true;
	}

	public static bool TryTake(string name, int player)
	{
		if (!Net.IsOnline)
		{
			return true;
		}
		if (!Net.IsServer)
		{
			Log.Warn("Locks.TryTake(" + name + ") on a client: only the server takes locks");
			return false;
		}
		if (string.IsNullOrEmpty(name))
		{
			return false;
		}
		if (held.TryGetValue(name, out var value))
		{
			if (value.Player != player)
			{
				return false;
			}
			value.TouchedMs = Clock.Ms;
			return true;
		}
		held[name] = new Entry
		{
			Player = player,
			TouchedMs = Clock.Ms,
			Name = Net.PlayerName(player)
		};
		Broadcast(name, player);
		return true;
	}

	public static void Touch(string name, int player)
	{
		if (Net.IsServer && name != null && held.TryGetValue(name, out var value) && value.Player == player)
		{
			value.TouchedMs = Clock.Ms;
		}
	}

	public static void Wanted(string name, int player)
	{
		if (Net.IsServer && name != null && player > 0 && held.TryGetValue(name, out var value) && value.Player != player)
		{
			value.WantedMs = Clock.Ms;
			value.WantedBy = player;
		}
	}

	public static int SecondsLeft(string name)
	{
		if (name == null || !held.TryGetValue(name, out var value))
		{
			return -1;
		}
		long ms = Clock.Ms;
		long num = ms - value.TouchedMs;
		long num2 = ((ms - value.WantedMs < 15000) ? ((long)WantedTimeoutSeconds * 1000L) : ((long)TimeoutSeconds * 1000L));
		return (int)Math.Max(0L, (num2 - num + 999) / 1000);
	}

	public static void Release(string name, int player)
	{
		if (Net.IsServer && name != null && held.TryGetValue(name, out var value) && (value.Player == player || player == -1))
		{
			held.Remove(name);
			Broadcast(name, -1);
		}
	}

	public static int Holder(string name)
	{
		if (name == null || !held.TryGetValue(name, out var value))
		{
			return -1;
		}
		return value.Player;
	}

	public static int HeldBy(string name)
	{
		if (Net.IsServer)
		{
			return Holder(name);
		}
		if (name == null || !view.TryGetValue(name, out var value))
		{
			return -1;
		}
		return value;
	}

	public static bool LocalHoldsAny()
	{
		if (Net.IsHost)
		{
			foreach (Entry value2 in held.Values)
			{
				if (value2.Player == Net.LocalPlayer)
				{
					return true;
				}
			}
			return false;
		}
		if (!Net.IsClient)
		{
			return false;
		}
		foreach (int value in view.Values)
		{
			if (value == Net.LocalPlayer)
			{
				return true;
			}
		}
		return false;
	}

	internal static void ReleaseAll(int player)
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, Entry> item in held)
		{
			if (item.Value.Player == player)
			{
				list.Add(item.Key);
			}
		}
		foreach (string item2 in list)
		{
			string text = held[item2].Name ?? Net.PlayerName(player);
			held.Remove(item2);
			Broadcast(item2, -1);
			Log.Info("server: lock '" + item2 + "' released (" + text + " left)");
		}
	}

	internal static void Tick()
	{
		long ms = Clock.Ms;
		if (ms - lastSweepMs < 1000)
		{
			return;
		}
		lastSweepMs = ms;
		List<string> list = null;
		foreach (KeyValuePair<string, Entry> item in held)
		{
			long num = ms - item.Value.TouchedMs;
			bool flag = ms - item.Value.WantedMs < 15000;
			if (num > (long)TimeoutSeconds * 1000L || (flag && num > (long)WantedTimeoutSeconds * 1000L))
			{
				(list ?? (list = new List<string>())).Add(item.Key);
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (string item2 in list)
		{
			Entry entry = held[item2];
			bool flag2 = ms - entry.TouchedMs <= (long)TimeoutSeconds * 1000L;
			held.Remove(item2);
			Broadcast(item2, -1);
			Log.Info("server: lock '" + item2 + "' of " + (entry.Name ?? Net.PlayerName(entry.Player)) + " timed out after " + (flag2 ? (WantedTimeoutSeconds + " s idle while " + Net.PlayerName(entry.WantedBy) + " waited") : (TimeoutSeconds + " s idle")));
		}
	}

	internal static Dictionary<string, int> Table()
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (KeyValuePair<string, Entry> item in held)
		{
			dictionary[item.Key] = item.Value.Player;
		}
		return dictionary;
	}

	private static void Broadcast(string name, int holder)
	{
		if (Server.PlayerCount > 0)
		{
			Server.SendToJoined(Wire.Encode("sys.lock", new MsgLock
			{
				name = name,
				holder = holder
			}), -1);
		}
		Raise(name, holder);
	}

	internal static void ClientSet(string name, int holder)
	{
		if (!string.IsNullOrEmpty(name))
		{
			if (holder < 0)
			{
				view.Remove(name);
			}
			else
			{
				view[name] = holder;
			}
			Raise(name, holder);
		}
	}

	internal static void ClientReset(Dictionary<string, int> table)
	{
		List<string> list = new List<string>(view.Keys);
		view.Clear();
		if (table != null)
		{
			foreach (KeyValuePair<string, int> item in table)
			{
				view[item.Key] = item.Value;
			}
		}
		foreach (string item2 in list)
		{
			if (!view.ContainsKey(item2))
			{
				Raise(item2, -1);
			}
		}
		foreach (KeyValuePair<string, int> item3 in view)
		{
			Raise(item3.Key, item3.Value);
		}
	}

	internal static void ServerClear()
	{
		held.Clear();
		view.Clear();
	}

	internal static void ClientClear()
	{
		ClientReset(null);
	}

	private static void Raise(string name, int holder)
	{
		try
		{
			Locks.Changed?.Invoke(name, holder);
		}
		catch (Exception ex)
		{
			Log.Error("Locks.Changed handler: " + ex);
		}
	}
}
