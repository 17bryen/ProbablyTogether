using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProbablyTogether.Bots;

public static class BotProbes
{
	private sealed class Pending
	{
		public float First;

		public float Last;

		public int Sends;
	}

	public sealed class Ask
	{
		public int id;
	}

	public sealed class Reply
	{
		public int id;

		public Dictionary<string, string> values;
	}

	public const string MenuScene = "EmporiumMenu";

	public const string ShopScene = "InventoryScene";

	private static readonly Dictionary<string, Func<string>> probes = new Dictionary<string, Func<string>>(StringComparer.Ordinal);

	private static readonly Dictionary<int, Dictionary<string, string>> replies = new Dictionary<int, Dictionary<string, string>>();

	private static readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();

	private static int nextId;

	private static bool wired;

	public const float ResendSeconds = 3f;

	public const float GiveUpSeconds = 120f;

	public static int Resends { get; private set; }

	public static int Held { get; private set; }

	public static string Scene
	{
		get
		{
			try
			{
				Scene activeScene = SceneManager.GetActiveScene();
				return activeScene.name;
			}
			catch
			{
				return "";
			}
		}
	}

	public static bool InShop
	{
		get
		{
			if (Scene == "InventoryScene")
			{
				return PlayerStore.HasInstance;
			}
			return false;
		}
	}

	private static bool CanSend
	{
		get
		{
			if (Net.IsClient)
			{
				return Net.IsLive;
			}
			return true;
		}
	}

	public static void Add(string key, Func<string> read)
	{
		probes[key] = read;
	}

	public static string Read(string key)
	{
		if (!probes.TryGetValue(key, out var value))
		{
			return "";
		}
		try
		{
			return value() ?? "";
		}
		catch (Exception ex)
		{
			return "!" + ex.GetType().Name;
		}
	}

	public static Dictionary<string, string> ReadAll()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (string item in probes.Keys.ToList())
		{
			dictionary[item] = Read(item);
		}
		return dictionary;
	}

	public static string Format(IReadOnlyDictionary<string, string> v, params string[] keys)
	{
		List<string> list = new List<string>();
		string[] array = ((keys.Length != 0) ? keys : v.Keys.OrderBy<string, string>((string x) => x, StringComparer.Ordinal).ToArray());
		foreach (string text in array)
		{
			if (v.TryGetValue(text, out var value))
			{
				list.Add(text + "=" + ((value == "") ? "-" : value));
			}
		}
		return string.Join(" ", list);
	}

	internal static void Init()
	{
		if (!BotRunner.LabAllowed(out var _))
		{
			return;
		}
		Add("role", () => Net.Role.ToString());
		Add("scene", () => Scene);
		Add("players", () => Net.Players.Count.ToString(CultureInfo.InvariantCulture));
		Add("shop", () => (!InShop) ? "0" : "1");
		Add("day", () => (!InShop) ? "" : StoreStation.Instance.dayCounter.ToString(CultureInfo.InvariantCulture));
		Add("cash", () => (!PlayerStore.HasInstance) ? "" : PlayerStore.Instance.playerCash.ToString(CultureInfo.InvariantCulture));
		Add("favor", () => (!PlayerStore.HasInstance) ? "" : PlayerStore.Instance.wildFavor.ToString(CultureInfo.InvariantCulture));
		Add("bot.resends", () => Resends.ToString(CultureInfo.InvariantCulture));
		Add("bot.held", () => Held.ToString(CultureInfo.InvariantCulture));
		foreach (Type item in BotRunner.OwnTypes())
		{
			if (typeof(BotProbeSet).IsAssignableFrom(item) && !item.IsAbstract)
			{
				try
				{
					((BotProbeSet)Activator.CreateInstance(item)).Register();
				}
				catch (Exception ex)
				{
					BotLog.Info("bot", "probe set " + item.Name + " failed to register: " + ex.Message);
				}
			}
		}
		if (wired)
		{
			return;
		}
		wired = true;
		Net.On("bot.probe", delegate(Ask msg, int from)
		{
			Net.ToPlayer(from, "bot.probe.r", new Reply
			{
				id = msg.id,
				values = ReadAll()
			});
		}, null);
		Net.On("bot.probe.r", null, delegate(Reply msg)
		{
			lock (replies)
			{
				if (pending.ContainsKey(msg.id) && !replies.ContainsKey(msg.id))
				{
					replies[msg.id] = msg.values ?? new Dictionary<string, string>();
				}
			}
		});
	}

	internal static int AskServer()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		Pending pending = new Pending
		{
			First = realtimeSinceStartup
		};
		int num;
		lock (replies)
		{
			num = ++nextId;
			BotProbes.pending[num] = pending;
		}
		if (CanSend)
		{
			Send(num, pending, realtimeSinceStartup);
		}
		else
		{
			Held++;
		}
		return num;
	}

	private static void Send(int id, Pending p, float now)
	{
		if (p.Sends == 1)
		{
			BotLog.Info("bot", "probe ask #" + id + " got no answer in " + 3f.ToString("F0", CultureInfo.InvariantCulture) + " s; asking again (same id) until it does");
		}
		if (p.Sends > 0)
		{
			Resends++;
		}
		p.Sends++;
		p.Last = now;
		Net.ToServer("bot.probe", new Ask
		{
			id = id
		});
	}

	internal static void Tick()
	{
		List<KeyValuePair<int, Pending>> list = null;
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		lock (replies)
		{
			if (pending.Count == 0)
			{
				return;
			}
			List<int> list2 = null;
			foreach (KeyValuePair<int, Pending> item in pending)
			{
				if (!replies.ContainsKey(item.Key))
				{
					Pending value = item.Value;
					if (realtimeSinceStartup - value.First > 120f)
					{
						List<int> obj = list2 ?? new List<int>();
						list2 = obj;
						obj.Add(item.Key);
					}
					else if (value.Sends == 0 || realtimeSinceStartup - value.Last >= 3f)
					{
						List<KeyValuePair<int, Pending>> obj2 = list ?? new List<KeyValuePair<int, Pending>>();
						list = obj2;
						obj2.Add(item);
					}
				}
			}
			if (list2 != null)
			{
				foreach (int item2 in list2)
				{
					pending.Remove(item2);
				}
			}
		}
		if (list == null || !CanSend)
		{
			return;
		}
		foreach (KeyValuePair<int, Pending> item3 in list)
		{
			Send(item3.Key, item3.Value, realtimeSinceStartup);
		}
	}

	internal static void ForgetAsks()
	{
		lock (replies)
		{
			pending.Clear();
			replies.Clear();
		}
	}

	internal static Dictionary<string, string> TakeReply(int id)
	{
		lock (replies)
		{
			if (!replies.TryGetValue(id, out var value))
			{
				return null;
			}
			replies.Remove(id);
			pending.Remove(id);
			return value;
		}
	}
}
