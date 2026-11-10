using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

public static class Crew
{
	private sealed class Row
	{
		public string Key;

		public string Name;

		public int LastId;

		public int Order;

		public int Deals;

		public int Bought;

		public int Spent;

		public int Sold;

		public int Earned;

		public int Caught;

		public int Repairs;

		public int Uses;

		public int Made;

		public readonly HashSet<string> Visits = new HashSet<string>(StringComparer.Ordinal);

		public bool Any => Deals + Bought + Sold + Caught + Repairs + Uses + Made + Spent + Earned != 0;
	}

	public const string KTally = "crew.tally";

	internal const int MaxRows = 12;

	internal const float SendEvery = 2f;

	private const int MaxName = 24;

	private const int MaxWho = 40;

	private const int MaxValue = 100000000;

	private static readonly Dictionary<string, Row> rows = new Dictionary<string, Row>(StringComparer.Ordinal);

	private static readonly Dictionary<int, Row> byId = new Dictionary<int, Row>();

	private static readonly CrewDeals deals = new CrewDeals();

	private static int day = -1;

	private static int seq;

	private static int order;

	private static int lastMorning = -1;

	private static bool dirty;

	private static bool sentThisClose;

	private static bool capWarned;

	private static float lastSend = -100f;

	private static int pendPlayer = -1;

	private static int pendMachines;

	private static int pendProd;

	private static int pendUid;

	private static string pendKind = "";

	internal const string TrapCrate = "luxury_supply_crate";

	private const int MaxPlacers = 16;

	private static bool pendCrateScan;

	private static readonly HashSet<int> pendCrates = new HashSet<int>();

	private static readonly Dictionary<int, int> cratePlacer = new Dictionary<int, int>();

	private static MsgCrewTally table;

	private static bool inited;

	public static int Counted { get; private set; }

	public static int Sent { get; private set; }

	public static MsgCrewTally LastSent { get; private set; }

	public static int Received { get; private set; }

	public static int ReceivedInReplay { get; private set; }

	public static MsgCrewTally Today
	{
		get
		{
			MsgCrewTally msgCrewTally = table;
			if (msgCrewTally == null || !Game.ShopLoaded || msgCrewTally.day != Game.Day)
			{
				return null;
			}
			return msgCrewTally;
		}
	}

	internal static bool Counting
	{
		get
		{
			if (Net.IsServer && Game.ShopLoaded)
			{
				return Server.ShopReady;
			}
			return false;
		}
	}

	[SystemInit]
	internal static void Init()
	{
		if (inited)
		{
			return;
		}
		inited = true;
		Net.On<MsgCrewTally>("crew.tally", null, OnClientTally);
		Net.Disconnected += delegate
		{
			table = null;
		};
		if (Net.IsServer)
		{
			Net.AddServerGate(ObserveRequest);
			ServerMode.NewRunStarting += delegate(string why)
			{
				Reset(-1, "new run (" + why + ")");
			};
			Net.PlayerJoined += delegate
			{
				dirty = true;
			};
			Net.PlayerLeft += delegate
			{
				dirty = true;
			};
		}
	}

	private static string ObserveRequest(int player, string kind)
	{
		try
		{
			Finish();
			pendPlayer = player;
			pendKind = kind ?? "";
			if (pendKind == "machine.button" || pendKind == "hopper.run" || pendKind == "production.req")
			{
				pendMachines = Machines.AppliedCount;
				pendProd = Production.AppliedCount;
				pendUid = ((Game.Store != null) ? Game.Store.currentUniqueId : 0);
			}
			pendCrateScan = Customers.SomeoneAtCounter;
			pendCrates.Clear();
			if (pendCrateScan)
			{
				CountersCrates(owned: true, pendCrates);
			}
		}
		catch (Exception ex)
		{
			if (Log.Allow("crew|gate", 10000L, out var skipped))
			{
				Log.Warn("crew: request observer: " + ex.Message + Log.More(skipped));
			}
		}
		return null;
	}

	private static void Finish()
	{
		int num = pendPlayer;
		string text = pendKind;
		bool flag = pendCrateScan;
		pendPlayer = -1;
		pendKind = "";
		pendCrateScan = false;
		if (num <= 0 || !Game.ShopLoaded)
		{
			return;
		}
		if (flag)
		{
			NotePlacedCrates(num);
		}
		bool flag2 = text == "machine.button";
		bool flag3 = text == "hopper.run";
		bool flag4 = text == "production.req";
		if (!flag2 && !flag3 && !flag4)
		{
			return;
		}
		bool flag5 = Machines.AppliedCount > pendMachines;
		bool flag6 = Production.AppliedCount > pendProd;
		int newIds = Math.Max(0, (Game.Store?.currentUniqueId ?? pendUid) - pendUid);
		if ((flag2 || flag3) && flag5)
		{
			Add(num, delegate(Row r)
			{
				r.Uses++;
			}, "machine " + (flag2 ? "button" : "hopper"));
		}
		if (((flag2 && flag5) || (flag4 && flag6)) && newIds > 0)
		{
			Add(num, delegate(Row r)
			{
				r.Made += newIds;
			}, newIds + " item(s) made");
		}
	}

	internal static int Who()
	{
		int actor = Net.Actor;
		if (actor > 0)
		{
			return actor;
		}
		int requester = Net.Requester;
		if (requester <= 0)
		{
			return 0;
		}
		return requester;
	}

	private static void CountersCrates(bool owned, ICollection<int> into)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		GameGridInventory[] array = (GameGridInventory[])(object)new GameGridInventory[2] { instance.backInvinvElement, instance.frontInvinvElement };
		foreach (GameGridInventory val in array)
		{
			if (val == null)
			{
				continue;
			}
			foreach (GameItem childItem in ((GameInventory)val).childItems)
			{
				if (childItem != null && childItem.uniqueId != 0 && childItem.identifier == "luxury_supply_crate" && GeneralHelper.IsItemOwned(childItem) == owned)
				{
					into.Add(childItem.uniqueId);
				}
			}
		}
	}

	private static void NotePlacedCrates(int player)
	{
		List<int> list = new List<int>();
		CountersCrates(owned: true, list);
		foreach (int item in list)
		{
			if (!pendCrates.Contains(item))
			{
				if (cratePlacer.Count >= 16 && !cratePlacer.ContainsKey(item))
				{
					cratePlacer.Clear();
				}
				cratePlacer[item] = player;
				Log.Info("crew: " + Net.PlayerName(player) + " put a luxury crate (#" + item + ") on the counter");
			}
		}
	}

	private static int TrapPlacer(out int crate)
	{
		crate = 0;
		List<int> list = new List<int>();
		CountersCrates(owned: false, list);
		foreach (int item in list)
		{
			if (cratePlacer.TryGetValue(item, out var value))
			{
				cratePlacer.Remove(item);
				crate = item;
				return value;
			}
		}
		return 0;
	}

	private static Row RowOf(int player)
	{
		if (player <= 0)
		{
			return null;
		}
		if (byId.TryGetValue(player, out var value))
		{
			return value;
		}
		PlayerInfo value2;
		string text = ((Net.Players.TryGetValue(player, out value2) && !string.IsNullOrEmpty(value2.Name)) ? value2.Name : ("player " + player));
		string text2 = Text.NameKey(text);
		if (text2.Length == 0)
		{
			text2 = "#" + player.ToString(CultureInfo.InvariantCulture);
		}
		if (!rows.TryGetValue(text2, out value))
		{
			if (rows.Count >= 12)
			{
				if (!capWarned)
				{
					capWarned = true;
					Log.Warn("crew: today's table is full (" + 12 + " names); " + text + " is not counted today");
				}
				return null;
			}
			value = new Row
			{
				Key = text2,
				Name = Clip(text, 24),
				Order = ++order
			};
			rows[text2] = value;
		}
		value.LastId = player;
		value.Name = Clip(text, 24);
		byId[player] = value;
		return value;
	}

	private static void Add(int player, Action<Row> change, string what, bool always = false)
	{
		Row row = RowOf(player);
		if (row != null)
		{
			change(row);
			Counted++;
			dirty = true;
			int skipped;
			if (always)
			{
				Log.Info("crew: " + row.Name + ": " + what);
			}
			else if (Log.Allow("crew|count|" + player, 1000L, out skipped))
			{
				Log.Info("crew: " + row.Name + ": " + what + Log.More(skipped));
			}
		}
	}

	internal static void Sold(GameItem item, int cost, StoreClient client)
	{
		int p = Who();
		if (p > 0)
		{
			int gain = cost - FirstPrice(item, cost);
			Add(p, delegate(Row r)
			{
				r.Sold++;
				r.Earned = Cap(r.Earned + cost);
				Visit(r, p, client, gain);
			}, "sold " + Id(item) + " for " + cost + " (gain " + gain + ")");
		}
	}

	internal static void Bought(GameItem item, int cost, StoreClient client)
	{
		int p = Who();
		if (p > 0)
		{
			int gain = FirstPrice(item, cost) - cost;
			Add(p, delegate(Row r)
			{
				r.Bought++;
				r.Spent = Cap(r.Spent + cost);
				Visit(r, p, client, gain);
			}, "bought " + Id(item) + " for " + cost + " (gain " + gain + ")");
		}
	}

	internal static void Caught(int items)
	{
		int num = Who();
		string text = "";
		if (num <= 0)
		{
			num = TrapPlacer(out var crate);
			if (num > 0)
			{
				text = " (Eleanor's crate trap: crate #" + crate + " was put on the counter by this player)";
			}
		}
		if (num <= 0)
		{
			Log.Info("crew: a correct report with nobody to credit (no request, no actor, no crate placer)");
			return;
		}
		int n = Math.Max(1, items);
		Add(num, delegate(Row r)
		{
			r.Caught = Cap(r.Caught + n);
		}, "caught " + n + " stolen or contraband item(s)" + text, always: true);
	}

	internal static void Repaired(string what)
	{
		int num = Who();
		if (num > 0)
		{
			Add(num, delegate(Row r)
			{
				r.Repairs++;
			}, "repaired " + what);
		}
	}

	private static void Visit(Row r, int player, StoreClient client, int gain)
	{
		string text = (Customers.SomeoneAtCounter ? Customers.VisitKey() : (Game.Day + ":-")) + ":" + (client?.identifier ?? "?");
		if (r.Visits.Add(text))
		{
			r.Deals++;
		}
		deals.Trade(r.Key + "|" + text, player, r.Name, Clip(client?.displayName ?? "", 40), gain);
	}

	private static int FirstPrice(GameItem item, int paid)
	{
		if (item == null)
		{
			return paid;
		}
		try
		{
			return BargainUIManager.GetPreBargainValue(item, (int)item.GetNegociatedValue());
		}
		catch
		{
			return paid;
		}
	}

	[SystemTick]
	internal static void Tick()
	{
		if (Net.IsClient)
		{
			if (table != null && Snapshot.IsLoading)
			{
				table = null;
			}
		}
		else
		{
			if (!Net.IsServer)
			{
				return;
			}
			Finish();
			if (!Game.ShopLoaded || !Server.ShopReady)
			{
				return;
			}
			int num = Game.Day;
			if (num != day)
			{
				Reset(num, (day < 0) ? "shop up" : "new day");
			}
			if (DayLog.MorningSnapshot != lastMorning)
			{
				lastMorning = DayLog.MorningSnapshot;
				sentThisClose = false;
			}
			PlayerStore store = Game.Store;
			if (store == null)
			{
				return;
			}
			if ((int)store.storeState != 0)
			{
				sentThisClose = false;
				return;
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if ((!sentThisClose || dirty) && realtimeSinceStartup - lastSend >= 2f)
			{
				Send((!sentThisClose) ? "closed" : "change");
			}
		}
	}

	private static void Send(string why)
	{
		MsgCrewTally msgCrewTally = Build(why);
		msgCrewTally.seq = ++seq;
		Net.ToAll("crew.tally", msgCrewTally);
		dirty = false;
		sentThisClose = true;
		lastSend = Time.realtimeSinceStartup;
		Sent++;
		LastSent = msgCrewTally;
		Log.Info("crew: day " + msgCrewTally.day + " table #" + msgCrewTally.seq + " (" + why + ") to " + Net.Players.Count + " player(s): " + Canon(msgCrewTally));
	}

	internal static MsgCrewTally Build(string why)
	{
		if (Net.IsServer)
		{
			foreach (int item in new List<int>(Net.Players.Keys))
			{
				RowOf(item);
			}
		}
		List<MsgCrewRow> list = (from r in rows.Values
			where r.Any || Net.Players.ContainsKey(r.LastId)
			orderby r.Order
			select new MsgCrewRow
			{
				p = r.LastId,
				n = r.Name,
				@on = Net.Players.ContainsKey(r.LastId),
				deals = r.Deals,
				bought = r.Bought,
				spent = r.Spent,
				sold = r.Sold,
				earned = r.Earned,
				caught = r.Caught,
				repairs = r.Repairs,
				uses = r.Uses,
				made = r.Made
			}).ToList();
		return new MsgCrewTally
		{
			day = day,
			seq = seq,
			why = why,
			rows = list,
			best = BestMsg(deals.Best)
		};
	}

	private static MsgCrewBest BestMsg(CrewDeals.Deal b)
	{
		if (b != null && b.Gain > 0)
		{
			return new MsgCrewBest
			{
				p = b.Player,
				n = b.Name,
				gain = b.Gain,
				items = b.Items,
				who = b.Who
			};
		}
		return null;
	}

	private static void Reset(int newDay, string why)
	{
		if (rows.Values.Any((Row r) => r.Any))
		{
			Log.Info("crew: day " + day + " closed with " + Canon(Build("reset")) + "; starting " + why);
		}
		rows.Clear();
		byId.Clear();
		deals.Clear();
		cratePlacer.Clear();
		order = 0;
		dirty = false;
		sentThisClose = false;
		capWarned = false;
		Counted = 0;
		day = newDay;
	}

	private static void OnClientTally(MsgCrewTally m)
	{
		if (m == null || m.day <= 0 || m.rows == null)
		{
			return;
		}
		MsgCrewTally msgCrewTally = new MsgCrewTally
		{
			day = m.day,
			seq = m.seq,
			why = Clip(m.why ?? "", 20),
			rows = new List<MsgCrewRow>()
		};
		foreach (MsgCrewRow row in m.rows)
		{
			if (row != null && msgCrewTally.rows.Count < 12)
			{
				msgCrewTally.rows.Add(new MsgCrewRow
				{
					p = row.p,
					n = Clip(Text.CleanName(row.n ?? ""), 24),
					on = row.on,
					deals = Pos(row.deals),
					bought = Pos(row.bought),
					spent = Pos(row.spent),
					sold = Pos(row.sold),
					earned = Pos(row.earned),
					caught = Pos(row.caught),
					repairs = Pos(row.repairs),
					uses = Pos(row.uses),
					made = Pos(row.made)
				});
			}
		}
		if (m.best != null && m.best.gain > 0)
		{
			msgCrewTally.best = new MsgCrewBest
			{
				p = m.best.p,
				n = Clip(Text.CleanName(m.best.n ?? ""), 24),
				gain = Pos(m.best.gain),
				items = Pos(m.best.items),
				who = Clip(m.best.who ?? "", 40)
			};
		}
		table = msgCrewTally;
		Received++;
		if (DayLog.Replaying)
		{
			ReceivedInReplay++;
		}
	}

	public static string Canon(MsgCrewTally t)
	{
		if (t == null)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("d=").Append(t.day);
		foreach (MsgCrewRow item in (t.rows ?? new List<MsgCrewRow>()).Where(Active).OrderBy<MsgCrewRow, string>((MsgCrewRow r) => Text.NameKey(r.n ?? ""), StringComparer.Ordinal))
		{
			stringBuilder.Append(';').Append(item.n).Append(':')
				.Append(item.deals)
				.Append(',')
				.Append(item.bought)
				.Append(',')
				.Append(item.spent)
				.Append(',')
				.Append(item.sold)
				.Append(',')
				.Append(item.earned)
				.Append(',')
				.Append(item.caught)
				.Append(',')
				.Append(item.repairs)
				.Append(',')
				.Append(item.uses)
				.Append(',')
				.Append(item.made);
		}
		stringBuilder.Append(";best=");
		if (t.best != null)
		{
			stringBuilder.Append(t.best.n).Append(':').Append(t.best.gain)
				.Append(':')
				.Append(t.best.items);
		}
		else
		{
			stringBuilder.Append('-');
		}
		return stringBuilder.ToString();
	}

	public static bool Active(MsgCrewRow r)
	{
		if (r != null)
		{
			return r.deals + r.bought + r.spent + r.sold + r.earned + r.caught + r.repairs + r.uses + r.made != 0;
		}
		return false;
	}

	private static int Pos(int v)
	{
		if (v >= 0)
		{
			if (v <= 100000000)
			{
				return v;
			}
			return 100000000;
		}
		return 0;
	}

	internal static int Cap(long v)
	{
		if (v <= 100000000)
		{
			if (v >= -100000000)
			{
				return (int)v;
			}
			return -100000000;
		}
		return 100000000;
	}

	private static string Id(GameItem it)
	{
		if (it != null)
		{
			return it.identifier + "#" + it.uniqueId.ToString(CultureInfo.InvariantCulture);
		}
		return "?";
	}

	private static string Clip(string s, int max)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		s = Text.OneLine(s);
		if (s.Length <= max)
		{
			return s;
		}
		int num = max;
		if (char.IsHighSurrogate(s[num - 1]))
		{
			num--;
		}
		return s.Substring(0, num);
	}
}
