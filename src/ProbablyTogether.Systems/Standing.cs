using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;

namespace ProbablyTogether.Systems;

/// Per-seat ledger of whose deals moved the shop's cash and standing: a deal counts for whoever last dragged the item
/// onto the players' side of the counter (seat 0: the shop). Changed by replayed code, so every copy has the same.
public static class Standing
{
	public sealed class Row
	{
		public int Earned;

		public int Spent;

		public int Sold;

		public int Bought;

		/// Faction -> standing earned, in ten-thousandths of a point so sums match on every copy.
		public readonly SortedDictionary<string, long> Rep = new SortedDictionary<string, long>(StringComparer.Ordinal);

		/// "FACTION/TAG" (a kind asked for), "FACTION/#id" (an item asked for) or "FACTION/*" -> count and last item.
		public readonly SortedDictionary<string, Good> Goods = new SortedDictionary<string, Good>(StringComparer.Ordinal);

		public int Net => Earned - Spent;
	}

	public sealed class Good
	{
		public int N;

		public string Last = "";
	}

	/// The most goods kinds kept per player; the least sold go first.
	private const int GoodsKept = 40;

	private const string Key = "ledger";

	private static string cachedRaw;

	private static Dictionary<int, Row> cached = new Dictionary<int, Row>();

	private static readonly Dictionary<long, int> placer = new Dictionary<long, int>();

	internal static int Credited;

	internal static int Unclaimed;

	public static bool Active => Net.IsOnline && Bays.Enabled && Game.ShopLoaded && Game.Store != null;

	public static string Raw
	{
		get
		{
			try
			{
				return ModHelper.GetModData("bellun_probably_together", Key, (string)null) ?? "";
			}
			catch
			{
				return "";
			}
		}
	}

	private static Dictionary<int, Row> Parse(string raw)
	{
		Dictionary<int, Row> dictionary = new Dictionary<int, Row>();
		foreach (string item in (raw ?? "").Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries))
		{
			string[] array = item.Split(':');
			if (array.Length < 5 || !int.TryParse(array[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				continue;
			}
			Row row = new Row();
			int.TryParse(array[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out row.Earned);
			int.TryParse(array[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out row.Spent);
			int.TryParse(array[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out row.Sold);
			int.TryParse(array[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out row.Bought);
			if (array.Length > 5)
			{
				foreach (string item2 in array[5].Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries))
				{
					int num = item2.IndexOf('=');
					if (num > 0 && long.TryParse(item2.Substring(num + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
					{
						row.Rep[item2.Substring(0, num)] = result2;
					}
				}
			}
			if (array.Length > 6)
			{
				foreach (string item3 in array[6].Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries))
				{
					int num2 = item3.IndexOf('=');
					if (num2 <= 0)
					{
						continue;
					}
					string[] array2 = item3.Substring(num2 + 1).Split('~');
					if (int.TryParse(array2[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result3) && result3 > 0)
					{
						row.Goods[item3.Substring(0, num2)] = new Good
						{
							N = result3,
							Last = (array2.Length > 1) ? array2[1] : ""
						};
					}
				}
			}
			dictionary[result] = row;
		}
		return dictionary;
	}

	private static string Format(Dictionary<int, Row> table)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<int, Row> item in table.OrderBy((KeyValuePair<int, Row> p) => p.Key))
		{
			Row value = item.Value;
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append(';');
			}
			stringBuilder.Append(item.Key.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value.Earned.ToString(CultureInfo.InvariantCulture)).Append(':')
				.Append(value.Spent.ToString(CultureInfo.InvariantCulture))
				.Append(':')
				.Append(value.Sold.ToString(CultureInfo.InvariantCulture))
				.Append(':')
				.Append(value.Bought.ToString(CultureInfo.InvariantCulture))
				.Append(':')
				.Append(string.Join(",", value.Rep.Where((KeyValuePair<string, long> r) => r.Value != 0).Select((KeyValuePair<string, long> r) => r.Key + "=" + r.Value.ToString(CultureInfo.InvariantCulture))));
			if (value.Goods.Count > 0)
			{
				stringBuilder.Append(':').Append(string.Join(",", value.Goods.Select((KeyValuePair<string, Good> g) => g.Key + "=" + g.Value.N.ToString(CultureInfo.InvariantCulture) + "~" + g.Value.Last)));
			}
		}
		return stringBuilder.ToString();
	}

	private static Dictionary<int, Row> Table()
	{
		string raw = Raw;
		if ((object)raw != cachedRaw && raw != cachedRaw)
		{
			cachedRaw = raw;
			cached = Parse(raw);
		}
		return cached;
	}

	private static void Change(int seat, Action<Row> change)
	{
		if (seat < 0)
		{
			return;
		}
		Dictionary<int, Row> dictionary = Parse(Raw);
		if (!dictionary.TryGetValue(seat, out var value))
		{
			value = (dictionary[seat] = new Row());
		}
		change(value);
		string text = Format(dictionary);
		using (Net.Apply())
		{
			ModHelper.SetModData("bellun_probably_together", Key, text);
		}
		cachedRaw = text;
		cached = dictionary;
	}

	/// A seat's row (seat 0: the shop).
	public static Row Of(int seat)
	{
		Row value;
		return Table().TryGetValue(seat, out value) ? value : new Row();
	}

	public static double RepOf(int seat, string faction)
	{
		long value;
		return Of(seat).Rep.TryGetValue(faction ?? "", out value) ? ((double)value / 10000.0) : 0.0;
	}

	internal static void Drop(int seat)
	{
		if (!Active || seat <= 0 || !Table().ContainsKey(seat))
		{
			return;
		}
		Dictionary<int, Row> dictionary = Parse(Raw);
		dictionary.Remove(seat);
		string text = Format(dictionary);
		using (Net.Apply())
		{
			ModHelper.SetModData("bellun_probably_together", Key, text);
		}
		cachedRaw = text;
		cached = dictionary;
	}

	/// Every copy: items moved onto the players' side of the counter become the actor's; items moved off become nobody's.
	internal static void NoteMoves(int actor, List<ItemsOp> ops)
	{
		if (ops == null || !Game.ShopLoaded)
		{
			return;
		}
		int num = (actor > 0) ? Members.SeatOf(actor) : 0;
		foreach (ItemsOp op in ops)
		{
			Note(op.i, num);
			Note(op.nw, num);
		}
		if (placer.Count > 400)
		{
			foreach (long item in placer.Keys.ToList())
			{
				if (!OnOurSide(Items.Find(item)))
				{
					placer.Remove(item);
				}
			}
		}
	}

	private static void Note(long id, int seat)
	{
		if (id == 0L)
		{
			return;
		}
		GameItem val = Items.Find(id);
		if (val != null && OnOurSide(val))
		{
			if (seat > 0)
			{
				placer[id] = seat;
			}
		}
		else
		{
			placer.Remove(id);
		}
	}

	private static bool OnOurSide(GameItem item)
	{
		try
		{
			return item != null && item.parentInventory != null && Items.RootOf((GraphNodeStorage)(object)item.parentInventory) == "back";
		}
		catch
		{
			return false;
		}
	}

	/// Clears counter claims when a morning starts loading, before its moves replay ("loaded" would wipe the rebuilt claims).
	internal static void NewScene()
	{
		placer.Clear();
		cachedRaw = null;
	}

	/// The seat a deal about this item counts for (0: the shop).
	public static int PlacedBy(GameItem item)
	{
		if (item == null)
		{
			return 0;
		}
		int value;
		return (placer.TryGetValue(Ids.Of(item), out value) && Members.BySeat(value) != null) ? value : 0;
	}

	internal static void Sold(GameItem item, int cost)
	{
		// A deal on a private trip reaches the shop as the trip's cash and is counted there.
		if (!Active || item == null || cost <= 0 || Outings.Private)
		{
			return;
		}
		int num = PlacedBy(item);
		Count(num);
		List<string> kinds = (num > 0) ? KindsFor(item) : null;
		string id = Clean(item.identifier);
		Change(num, delegate(Row r)
		{
			r.Sold++;
			r.Earned = Crew.Cap((long)r.Earned + cost);
			if (kinds != null)
			{
				foreach (string kind in kinds)
				{
					if (!r.Goods.TryGetValue(kind, out var value))
					{
						value = (r.Goods[kind] = new Good());
					}
					value.N++;
					value.Last = id;
				}
				while (r.Goods.Count > GoodsKept)
				{
					r.Goods.Remove(r.Goods.OrderBy((KeyValuePair<string, Good> g) => g.Value.N).ThenBy((KeyValuePair<string, Good> g) => g.Key, StringComparer.Ordinal).First().Key);
				}
			}
		});
	}

	private static string Clean(string s)
	{
		return (s ?? "").Replace(':', '_').Replace(';', '_').Replace(',', '_').Replace('=', '_').Replace('~', '_').Replace('/', '_');
	}

	/// Keys a sale is remembered under: what the customer came for that this item is, plus "anything" for the faction.
	private static List<string> KindsFor(GameItem item)
	{
		try
		{
			StoreClient c = Game.Store?.currentClientInstance?.GetClientBlueprint();
			if (c == null || string.IsNullOrEmpty(c.clientFaction) || c.clientFaction == "NONE")
			{
				return null;
			}
			string faction = Clean(c.clientFaction);
			List<string> list = new List<string> { faction + "/*" };
			if (c.clientBuyingIdList != null && c.clientBuyingIdList.Contains(item.identifier))
			{
				list.Add(faction + "/#" + Clean(item.identifier));
			}
			if (c.clientBuyingTagList != null)
			{
				foreach (string tag in c.clientBuyingTagList)
				{
					if (!string.IsNullOrEmpty(tag) && !tag.StartsWith("ALL_TYPE", StringComparison.Ordinal) && item.IsTag(tag) && !list.Contains(faction + "/" + Clean(tag)))
					{
						list.Add(faction + "/" + Clean(tag));
					}
				}
			}
			return list;
		}
		catch
		{
			return null;
		}
	}

	public static Good GoodsOf(int seat, string key)
	{
		Good value;
		return Of(seat).Goods.TryGetValue(key, out value) ? value : null;
	}

	/// The customer wish this player has served most often, and how often (null: none).
	public static KeyValuePair<string, Good>? BestFor(int seat, StoreClient c)
	{
		if (c == null || string.IsNullOrEmpty(c.clientFaction))
		{
			return null;
		}
		string faction = Clean(c.clientFaction);
		Row row = Of(seat);
		KeyValuePair<string, Good>? best = null;
		List<string> keys = new List<string>();
		bool any = false;
		if (c.clientBuyingIdList != null)
		{
			keys.AddRange(c.clientBuyingIdList.Where((string i) => !string.IsNullOrEmpty(i)).Select((string i) => faction + "/#" + Clean(i)));
		}
		if (c.clientBuyingTagList != null)
		{
			foreach (string tag in c.clientBuyingTagList)
			{
				if (string.IsNullOrEmpty(tag))
				{
					continue;
				}
				if (tag.StartsWith("ALL_TYPE", StringComparison.Ordinal))
				{
					any = true;
				}
				else
				{
					keys.Add(faction + "/" + Clean(tag));
				}
			}
		}
		if (any)
		{
			keys.Add(faction + "/*");
		}
		foreach (string key in keys.Distinct().OrderBy((string k) => k, StringComparer.Ordinal))
		{
			if (row.Goods.TryGetValue(key, out var value) && (best == null || value.N > best.Value.Value.N))
			{
				best = new KeyValuePair<string, Good>(key, value);
			}
		}
		return best;
	}

	internal static void Bought(GameItem item, int cost)
	{
		// A deal on a private trip reaches the shop as the trip's cash and is counted there.
		if (!Active || item == null || cost <= 0 || Outings.Private)
		{
			return;
		}
		int num = PlacedBy(item);
		Count(num);
		Change(num, delegate(Row r)
		{
			r.Bought++;
			r.Spent = Crew.Cap((long)r.Spent + cost);
		});
	}

	private static void Count(int seat)
	{
		if (seat > 0)
		{
			Credited++;
		}
		else
		{
			Unclaimed++;
		}
	}

	/// Cash a player spent (negative) or brought in (positive) on a private trip.
	internal static void Cash(int player, int amount)
	{
		if (!Active || amount == 0 || player <= 0)
		{
			return;
		}
		int num = Members.SeatOf(player);
		if (num <= 0)
		{
			return;
		}
		Change(num, delegate(Row r)
		{
			if (amount > 0)
			{
				r.Earned = Crew.Cap((long)r.Earned + amount);
			}
			else
			{
				r.Spent = Crew.Cap((long)r.Spent - amount);
			}
		});
	}

	private static Dictionary<string, double> hostTripStart;

	/// Host: the host's game is the shop, so its trip's standing change is the difference between leaving and coming home.
	internal static void HostTripStarts()
	{
		hostTripStart = new Dictionary<string, double>(StringComparer.Ordinal);
		PlayerStore store = Game.Store;
		if (store?.storeReputations == null)
		{
			return;
		}
		foreach (StoreReputation storeReputation in store.storeReputations)
		{
			if (storeReputation != null && storeReputation.factionId != null)
			{
				hostTripStart[storeReputation.factionId] = storeReputation.GetReputationExact();
			}
		}
	}

	internal static List<MsgHostRep> HostTripRep()
	{
		List<MsgHostRep> list = new List<MsgHostRep>();
		PlayerStore store = Game.Store;
		if (hostTripStart == null || store?.storeReputations == null)
		{
			return list;
		}
		foreach (StoreReputation storeReputation in store.storeReputations)
		{
			if (storeReputation != null && storeReputation.factionId != null && hostTripStart.TryGetValue(storeReputation.factionId, out var value) && storeReputation.GetReputationExact() != value)
			{
				list.Add(new MsgHostRep
				{
					f = storeReputation.factionId,
					v = storeReputation.GetReputationExact() - value
				});
			}
		}
		hostTripStart = null;
		return list;
	}

	/// Every copy: a returning player's trip standing changes are credited to them.
	internal static void TripRep(int player, List<MsgHostRep> rep)
	{
		if (!Active || rep == null || rep.Count == 0 || player <= 0)
		{
			return;
		}
		int num = Members.SeatOf(player);
		if (num <= 0)
		{
			return;
		}
		Change(num, delegate(Row r)
		{
			int num2 = 0;
			foreach (MsgHostRep item in rep)
			{
				if (item == null || string.IsNullOrEmpty(item.f) || double.IsNaN(item.v) || double.IsInfinity(item.v) || num2++ >= 16)
				{
					continue;
				}
				long num3 = (long)Math.Round(Math.Max(-50.0, Math.Min(50.0, item.v)) * 10000.0);
				if (num3 != 0L)
				{
					r.Rep.TryGetValue(item.f, out var value);
					r.Rep[item.f] = value + num3;
				}
			}
		});
	}

	internal static double RepNow(string factionId)
	{
		try
		{
			return StoreReputation.GetStoreReputation(factionId)?.GetReputationExact() ?? 0.0;
		}
		catch
		{
			return 0.0;
		}
	}

	internal static void Traded(string factionId, GameItem item, double delta)
	{
		if (!Active || string.IsNullOrEmpty(factionId) || delta == 0.0 || Outings.Private)
		{
			return;
		}
		long num = (long)Math.Round(delta * 10000.0);
		if (num == 0L)
		{
			return;
		}
		int seat = PlacedBy(item);
		Change(seat, delegate(Row r)
		{
			r.Rep.TryGetValue(factionId, out var value);
			r.Rep[factionId] = value + num;
		});
	}

	private static string Money(int v)
	{
		return v.ToString("N0", CultureInfo.InvariantCulture);
	}

	/// Chat: "/ledger".
	internal static string ChatCommand()
	{
		if (!Active)
		{
			return "This shop keeps no ledger.";
		}
		List<string> list = new List<string>();
		foreach (Member item in Members.State.list.OrderBy((Member m) => m.s))
		{
			list.Add(Line(item.n, Of(item.s)));
		}
		Row row = Of(0);
		if (row.Earned != 0 || row.Spent != 0)
		{
			list.Add(Line("Nobody in particular", row));
		}
		return string.Join("  |  ", list);
	}

	internal static string Line(string name, Row r)
	{
		string text = string.Join(", ", r.Rep.Where((KeyValuePair<string, long> p) => Math.Abs(p.Value) >= 500).Select((KeyValuePair<string, long> p) => FactionName(p.Key) + " " + ((p.Value > 0) ? "+" : "") + ((double)p.Value / 10000.0).ToString("0.0", CultureInfo.InvariantCulture)));
		return name + ": in " + Money(r.Earned) + " (" + r.Sold + " sold), out " + Money(r.Spent) + " (" + r.Bought + " bought), net " + ((r.Net > 0) ? "+" : "") + Money(r.Net) + ((text.Length > 0) ? ("; standing " + text) : "");
	}

	internal static string FactionName(string factionId)
	{
		try
		{
			string text = StoreReputation.GetStoreReputation(factionId)?.factionDisplay;
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
		}
		catch
		{
		}
		return (factionId ?? "").Replace("FACTION_", "").Replace('_', ' ').ToLowerInvariant();
	}

	/// Credits a standing change outside a deal (a perk) to that player's row.
	internal static void Credit(int seat, string factionId, double delta)
	{
		if (!Active || seat <= 0 || string.IsNullOrEmpty(factionId) || double.IsNaN(delta) || double.IsInfinity(delta))
		{
			return;
		}
		long num = (long)Math.Round(delta * 10000.0);
		if (num == 0L)
		{
			return;
		}
		Change(seat, delegate(Row r)
		{
			r.Rep.TryGetValue(factionId, out var value);
			r.Rep[factionId] = value + num;
		});
	}

	internal static void LabGrant(int seat, string faction, double points)
	{
		if (!Net.IsServer || !Bots.BotRunner.Enabled)
		{
			return;
		}
		MsgStandingLab msgStandingLab = new MsgStandingLab
		{
			seat = seat,
			f = faction,
			v = points
		};
		ApplyLab(msgStandingLab);
		Net.ToAll("standing.lab", msgStandingLab);
	}

	internal static void LabGoods(int seat, string key, int n, string last)
	{
		if (!Net.IsServer || !Bots.BotRunner.Enabled)
		{
			return;
		}
		MsgStandingLab msgStandingLab = new MsgStandingLab
		{
			seat = seat,
			f = key,
			v = n,
			g = last ?? ""
		};
		ApplyLab(msgStandingLab);
		Net.ToAll("standing.lab", msgStandingLab);
	}

	private static void ApplyLab(MsgStandingLab m)
	{
		if (m == null || !Bots.BotRunner.Enabled || string.IsNullOrEmpty(m.f))
		{
			return;
		}
		Change(m.seat, delegate(Row r)
		{
			if (m.g != null)
			{
				r.Goods[m.f] = new Good
				{
					N = (int)m.v,
					Last = m.g
				};
			}
			else
			{
				r.Rep[m.f] = (long)Math.Round(m.v * 10000.0);
			}
		});
	}

	[SystemInit]
	internal static void Init()
	{
		Net.On<MsgStandingLab>("standing.lab", null, ApplyLab);
		Digest.AddPart("standing", () => Active ? (Raw + "|" + Regulars.Raw) : "");
		Digest.AddExplain("standing", () => Active ? string.Join(";", Members.State.list.OrderBy((Member m) => m.s).Select((Member m) => Line(m.n.Replace(';', ' '), Of(m.s)))) : "");
		Snapshot.Loaded += delegate
		{
			cachedRaw = null;
		};
		// Server side of NewScene: a captured morning starts with no counter claims.
		Snapshot.BeforeMorningCapture += delegate
		{
			placer.Clear();
		};
	}
}

public sealed class MsgStandingLab
{
	public int seat;

	public string f;

	public double v;

	/// Set: f is a goods key, v how many, g the last item.
	public string g;
}

[HarmonyPatch(typeof(StoreReputation), "OnItemTraded")]
internal static class StandingTradedPatch
{
	private static void Prefix(string factionId, out double __state)
	{
		__state = Standing.RepNow(factionId);
	}

	private static void Postfix(string factionId, GameItem gameItem, double __state)
	{
		try
		{
			Standing.Traded(factionId, gameItem, Standing.RepNow(factionId) - __state);
		}
		catch (Exception ex)
		{
			if (Log.Allow("standing|traded", 30000L, out var skipped))
			{
				Log.Warn("standing: counting a deal's standing threw" + Log.More(skipped) + ": " + ex.Message);
			}
		}
	}
}
