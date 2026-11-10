using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

internal static class CstLab
{
	public const string KHi = "cst.hi";

	public const string KLock = "cst.lock";

	public const string KLockR = "cst.lockr";

	public const string KCmd = "cst.cmd";

	public const string KReport = "cst.report";

	public const string KActor = "cst.actor";

	public const string KGated = "cst.gated";

	public const string LockName = "cst.test";

	public const string GateText = "Lab gate: refused on purpose.";

	private static bool registered;

	public static int loads;

	public static string role = "";

	public static CstLockReply lastReply;

	public static int replies;

	public static float retryEvery;

	private static float nextRetry;

	public static bool leaveOrdered;

	public static bool doneOrdered;

	public static readonly List<string> actorSeen = new List<string>();

	public static int gateHandlerRan;

	public static readonly Dictionary<string, int> roles = new Dictionary<string, int>();

	public static readonly Dictionary<string, string> roleNames = new Dictionary<string, string>();

	public static readonly List<KeyValuePair<int, CstLockReply>> serverReplies = new List<KeyValuePair<int, CstLockReply>>();

	public static readonly Dictionary<int, float> takenAt = new Dictionary<int, float>();

	public static readonly List<string> lockEvents = new List<string>();

	public static float lastFreeAt = -1f;

	public static readonly Dictionary<string, CstReport> reports = new Dictionary<string, CstReport>();

	public static readonly Dictionary<int, Server.PeerState> lastState = new Dictionary<int, Server.PeerState>();

	public static readonly Dictionary<int, string> leftWhile = new Dictionary<int, string>();

	public static readonly Dictionary<int, float> leftAt = new Dictionary<int, float>();

	public static string LabPassword
	{
		get
		{
			if (!string.IsNullOrEmpty(Args.Password))
			{
				return Args.Password;
			}
			return "cst-lab-pw";
		}
	}

	internal static void Register()
	{
		if (registered)
		{
			return;
		}
		registered = true;
		Net.On<CstHi>("cst.hi", OnHi, null);
		Net.On<CstLock>("cst.lock", OnLock, null);
		Net.On("cst.lockr", null, delegate(CstLockReply r)
		{
			lastReply = r;
			replies++;
			if (r.ok && r.op == "take")
			{
				retryEvery = 0f;
			}
		});
		Net.On<CstCmd>("cst.cmd", null, OnCmd);
		Net.On("cst.report", delegate(CstReport r, int p)
		{
			r.detail = r.detail + " (player " + p + ")";
			reports[r.role + "." + r.stage] = r;
		}, null);
		Net.On<CstActor>("cst.actor", null, OnActor);
		Net.On<CstEmpty>("cst.gated", delegate
		{
			gateHandlerRan++;
			Log.Warn("cst: the gated request reached its handler (the gate did not stop it)");
		}, null);
		Snapshot.Loaded += delegate
		{
			loads++;
		};
		if (!Net.IsServer)
		{
			return;
		}
		Net.AddServerGate((int p, string kind) => (!(kind == "cst.gated")) ? null : "Lab gate: refused on purpose.");
		Locks.Changed += delegate(string name, int holder)
		{
			if (!(name != "cst.test"))
			{
				float realtimeSinceStartup = Time.realtimeSinceStartup;
				lockEvents.Add(holder + "@" + realtimeSinceStartup.ToString("F1", CultureInfo.InvariantCulture));
				if (holder < 0)
				{
					lastFreeAt = realtimeSinceStartup;
				}
			}
		};
		Net.PlayerLeft += delegate(PlayerInfo p)
		{
			leftWhile[p.Id] = (lastState.TryGetValue(p.Id, out var value) ? value.ToString() : "?");
			leftAt[p.Id] = Time.realtimeSinceStartup;
		};
	}

	private static void OnHi(CstHi m, int player)
	{
		if (m != null && m.role != null)
		{
			roles[m.role] = player;
			roleNames[m.role] = Net.PlayerName(player);
		}
	}

	private static void OnLock(CstLock m, int player)
	{
		if (m == null || m.name != "cst.test")
		{
			return;
		}
		bool flag;
		if (m.op == "take")
		{
			flag = Locks.TryTake(m.name, player);
			if (flag && !takenAt.ContainsKey(player))
			{
				takenAt[player] = Time.realtimeSinceStartup;
			}
		}
		else
		{
			if (!(m.op == "release"))
			{
				return;
			}
			Locks.Release(m.name, player);
			flag = Locks.Holder(m.name) != player;
			takenAt.Remove(player);
		}
		CstLockReply cstLockReply = new CstLockReply
		{
			op = m.op,
			name = m.name,
			ok = flag,
			holder = Locks.Holder(m.name)
		};
		serverReplies.Add(new KeyValuePair<int, CstLockReply>(player, cstLockReply));
		Net.ToPlayer(player, "cst.lockr", cstLockReply);
	}

	public static void Order(string roleName, string op, float every = 0f, string stage = "asked")
	{
		if (roles.TryGetValue(roleName, out var value))
		{
			Net.ToPlayer(value, "cst.cmd", new CstCmd
			{
				op = op,
				name = "cst.test",
				every = every,
				stage = stage
			});
		}
	}

	public static void TrackStates()
	{
		foreach (Server.Peer value in Server.ByPlayer.Values)
		{
			lastState[value.Id] = value.State;
		}
	}

	public static Server.Peer PeerNamed(string name)
	{
		return Server.ByPlayer.Values.FirstOrDefault((Server.Peer p) => p.Info.Name == name);
	}

	private static void OnCmd(CstCmd c)
	{
		if (c != null)
		{
			switch (c.op)
			{
			case "take":
				retryEvery = 0f;
				Net.ToServer("cst.lock", new CstLock
				{
					op = "take",
					name = "cst.test"
				});
				break;
			case "retry":
				retryEvery = Math.Max(0.5f, c.every);
				nextRetry = 0f;
				break;
			case "release":
				retryEvery = 0f;
				Net.ToServer("cst.lock", new CstLock
				{
					op = "release",
					name = "cst.test"
				});
				break;
			case "report":
				Report(string.IsNullOrEmpty(c.stage) ? "asked" : c.stage);
				break;
			case "leave":
				leaveOrdered = true;
				break;
			case "done":
				doneOrdered = true;
				break;
			}
		}
	}

	public static void ClientTick()
	{
		if (!(retryEvery <= 0f) && Net.IsLive && !(Time.realtimeSinceStartup < nextRetry))
		{
			nextRetry = Time.realtimeSinceStartup + retryEvery;
			Net.ToServer("cst.lock", new CstLock
			{
				op = "take",
				name = "cst.test"
			});
		}
	}

	public static void Report(string stage)
	{
		Net.ToServer("cst.report", new CstReport
		{
			role = role,
			stage = stage,
			digest = Hex(Replay.Digest()),
			loads = loads,
			lockView = Locks.HeldBy("cst.test"),
			features = Features(),
			detail = "actor seen " + string.Join(" ", actorSeen) + "; checks ok " + Replay.ChecksOk + " differ " + Replay.ChecksBad
		});
	}

	private static void OnActor(CstActor m)
	{
		if (m == null)
		{
			return;
		}
		int actor;
		if (m.overrideActor >= -1)
		{
			using (Replay.Client(m.seed, m.overrideActor))
			{
				actor = Net.Actor;
			}
		}
		else
		{
			using (Replay.Client(m.seed))
			{
				actor = Net.Actor;
			}
		}
		int actor2 = Net.Actor;
		actorSeen.Add(m.n + ":" + actor + "/" + m.expect + ((actor2 != -1) ? (":after" + actor2) : "") + (DayLog.Replaying ? ":r" : ""));
	}

	public static bool ActorsOk(int count, bool replayed)
	{
		List<string> list = actorSeen.Where((string s) => s.EndsWith(":r") == replayed).ToList();
		if (list.Count < count)
		{
			return false;
		}
		foreach (string item in list.Take(count))
		{
			string[] array = item.Split(':')[1].Split('/');
			if (array[0] != array[1] || item.Contains(":after"))
			{
				return false;
			}
		}
		return true;
	}

	public static string Features()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null || !Game.ShopLoaded)
		{
			return "";
		}
		List<GameItem> list = new List<GameItem>();
		GraphNodeStorage[] array = (GraphNodeStorage[])(object)new GraphNodeStorage[3]
		{
			(GraphNodeStorage)instance.hirelingInv,
			(GraphNodeStorage)instance.soldElement,
			(GraphNodeStorage)instance.cassettePlayerElement
		};
		foreach (GraphNodeStorage val in array)
		{
			if (val != null)
			{
				list.AddRange(GraphUtils.FindAllChildrenType<GameItem>(val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null));
			}
		}
		list.Sort((GameItem x, GameItem y) => x.uniqueId.CompareTo(y.uniqueId));
		StringBuilder stringBuilder = new StringBuilder();
		int num = 0;
		foreach (GameItem item in list)
		{
			stringBuilder.Append(item.uniqueId).Append(':').Append(item.identifier)
				.Append('[');
			if (item.itemFeatures != null)
			{
				foreach (ItemFeature itemFeature in item.itemFeatures)
				{
					num++;
					stringBuilder.Append(itemFeature.category).Append('|').Append((int)itemFeature.featureType)
						.Append('|')
						.Append(itemFeature.IsExposed() ? 'e' : 'h')
						.Append('|')
						.Append(itemFeature.IsFeatureMatch() ? 'm' : 'd')
						.Append('|')
						.Append(itemFeature.customIntValue1)
						.Append(';');
				}
			}
			stringBuilder.Append(']');
		}
		ulong num2 = 14695981039346656037uL;
		string text = stringBuilder.ToString();
		foreach (char c in text)
		{
			num2 ^= c;
			num2 *= 1099511628211L;
		}
		return "items=" + list.Count + " features=" + num + " hash=" + num2.ToString("x16");
	}

	public static string Water()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if (instance?.hirelingInv == null)
		{
			return "";
		}
		foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)instance.hirelingInv, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
		{
			ItemFeature val = item.itemFeatures?.FirstOrDefault();
			if (val != null)
			{
				return item.identifier + " uid " + item.uniqueId + " " + (val.IsExposed() ? "exposed" : "hidden") + ":" + (val.IsFeatureMatch() ? "match" : "differs") + ":" + val.customIntValue1;
			}
		}
		return "no featured item in the hireling inventory";
	}

	public static string Hex(long v)
	{
		return v.ToString("x16");
	}
}
