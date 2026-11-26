using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.UI;

namespace ProbablyTogether.Systems;

/// Server to one player: what was just dealt to them at random (a trade, perks, or both).
public sealed class MsgRandomDealt
{
	public string trade;

	public string[] perks;

	public bool hasPerks;
}

/// Random trades and perks: the server draws, the normal pick path applies the draw on every copy.
/// The shop's rule (ForceTrade, ForcePerks) makes the draw the only way to pick for players who join.
public static class RandomStarts
{
	/// The trade id a client sends to be dealt one at random.
	public const string Pick = "RANDOM";

	internal const string TradeFact = "rt";

	internal const string PerksFact = "rp";

	/// Shop rules (server.json forceRandomTrade / forceRandomPerks; the welcome on a client).
	public static bool ForceTrade;

	public static bool ForcePerks;

	internal static int Draws;

	internal static string LastDraw = "";

	/// This copy's player: dealt and not yet shown.
	internal static string ShowTrade;

	internal static List<string> ShowPerks;

	internal static int Shown;

	/// Server: what the founder of a new run was dealt, for the member facts once seat 1 exists.
	private static string founderTrade;

	private static List<string> founderPerks;

	private static bool founderTell;

	private static readonly HashSet<int> restored = new HashSet<int>();

	/// Server: last run's draws per seat, dealt again in a new run.
	private static readonly Dictionary<int, KeyValuePair<string, string>> carried = new Dictionary<int, KeyValuePair<string, string>>();

	private static float nextLook;

	private static Random Dice(int seat)
	{
		int seed = unchecked(Environment.TickCount * 31 + seat * 7919 + Draws * 104729);
		Draws++;
		LastDraw = "seat " + seat + ", seed " + seed.ToString(CultureInfo.InvariantCulture);
		return new Random(seed);
	}

	/// Trades nobody holds, Rock Bottom included.
	/// founding: a shop not loaded yet, where nobody holds anything.
	internal static List<string> TradePool(bool founding = false)
	{
		return (from s in ServerMode.PlayableScenarioList()
			where founding || Specs.TakenBy(s.Key) == 0
			select s.Key).ToList();
	}

	internal static string DrawTrade(int seat, bool founding = false)
	{
		List<string> list = TradePool(founding);
		if (list.Count == 0)
		{
			return null;
		}
		string text = list[Dice(seat).Next(list.Count)];
		Log.Info("random: trade for seat " + seat + " from " + list.Count + " free: " + text + " (" + LastDraw + ")");
		return text;
	}

	/// A set the game's perk screen would accept: within 3 points and the slots, no two that exclude each other,
	/// none locked for this player. Aims for exactly 3 points with good and bad perks mixed.
	internal static List<string> DrawPerks(int seat, Func<string, string> lockOf)
	{
		Random random = Dice(seat);
		List<string> list = Perks.Known.Where((string id) => Perks.Def(id) != null && lockOf(id) == null).OrderBy((string id) => id, StringComparer.Ordinal).ToList();
		List<string> list2 = null;
		int num = int.MinValue;
		for (int i = 0; i < 400; i++)
		{
			List<string> list3 = list.OrderBy((string _) => random.Next()).ToList();
			int num2 = random.Next(2, 5);
			List<string> list4 = new List<string>();
			foreach (string id in list3)
			{
				if (list4.Count >= num2)
				{
					break;
				}
				if (list4.Any((string q) => Perks.Excludes(id, q)))
				{
					continue;
				}
				StartingPerk startingPerk = Perks.Def(id);
				list4.Add(id);
				if (startingPerk.maxSlot > 0)
				{
					// Colorful Character's slots may be filled with more perks.
					num2 += random.Next(1, startingPerk.maxSlot + 1);
				}
			}
			int num3 = Perks.Points(list4);
			if (num3 > 3 || list4.Count > Perks.Slots(list4))
			{
				continue;
			}
			bool flag = list4.Any((string id) => Perks.Def(id).cost > 0) && list4.Any((string id) => Perks.Def(id).cost < 0);
			int num4 = ((num3 == 3) ? 100 : (num3 * 20)) + (flag ? 50 : 0);
			if (num4 > num)
			{
				num = num4;
				list2 = list4;
			}
			if (num4 >= 150)
			{
				break;
			}
		}
		list2 = list2 ?? new List<string>();
		Log.Info("random: perks for seat " + seat + ": [" + string.Join(", ", list2) + "], " + Perks.Points(list2) + " point(s) (" + LastDraw + ")");
		return list2;
	}

	/// Server, a new run's start: the founder's random trade and perks, from server.json newRun or kept from the last run.
	internal static void Founding(NewGameData ngd, bool trade, bool perks)
	{
		founderTrade = null;
		founderPerks = null;
		founderTell = true;
		if (trade)
		{
			string text = DrawTrade(1, founding: true);
			if (text != null && Enum.TryParse<NewGameData.StartType>(text, ignoreCase: true, out var result))
			{
				SetScenario(ngd, result);
				founderTrade = text;
			}
		}
		if (perks)
		{
			SetPerks(ngd, DrawPerks(1, (string _) => null));
		}
	}

	private static void SetScenario(NewGameData ngd, NewGameData.StartType type)
	{
		ngd.startType = type;
		StartingScenario byStartType = StartingScenario.GetByStartType(type);
		ngd.startScenarioId = byStartType?.id ?? "";
		if (byStartType != null && byStartType.skipIntro)
		{
			ngd.skipIntro = true;
		}
	}

	private static void SetPerks(NewGameData ngd, List<string> ids)
	{
		ngd.startingPerks = ids.Select(Perks.Def).Where((StartingPerk p) => p != null).ToList();
		Perks.FounderPickedOnMenu = true;
		founderPerks = ids;
	}

	/// Server, a new run in place: the founder keeps what they were dealt last run.
	internal static void NewRunFounder(NewGameData ngd)
	{
		founderTrade = null;
		founderPerks = null;
		founderTell = false;
		restored.Clear();
		if (!carried.TryGetValue(1, out var value))
		{
			return;
		}
		if (!string.IsNullOrEmpty(value.Key) && Enum.TryParse<NewGameData.StartType>(value.Key, ignoreCase: true, out var result))
		{
			SetScenario(ngd, result);
			founderTrade = value.Key;
		}
		if (value.Value != null)
		{
			SetPerks(ngd, Split(value.Value));
		}
		carried.Remove(1);
		Log.Info("random: the founder keeps last run's draw (" + (founderTrade ?? "own trade") + "; " + ((founderPerks != null) ? ("[" + string.Join(", ", founderPerks) + "]") : "own perks") + ")");
	}

	private static List<string> Split(string text)
	{
		return (text ?? "").Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
	}

	internal static string TradeOf(int seat)
	{
		string text = Members.Fact(seat, TradeFact);
		return string.IsNullOrEmpty(text) ? null : text;
	}

	/// Null: this player's perks were not dealt.
	internal static List<string> PerksOf(int seat)
	{
		string text = Members.Fact(seat, PerksFact);
		return (text == null) ? null : Split(text);
	}

	/// This copy's player waits for the server to deal again what they had last run.
	internal static bool Redealing(int seat, bool trade)
	{
		return trade ? (TradeOf(seat) != null && Specs.Of(seat) == null) : (PerksOf(seat) != null && !Perks.Picked(seat));
	}

	/// Server, after a random pick went through: kept for a new run, told to the player.
	internal static void Dealt(int player, int seat, string trade, List<string> perks)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		if (trade != null)
		{
			dictionary[TradeFact] = trade;
		}
		if (perks != null)
		{
			dictionary[PerksFact] = string.Join(",", perks);
		}
		Members.SetFacts(seat, dictionary);
		if (player > 0)
		{
			Net.ToPlayer(player, "random.dealt", new MsgRandomDealt
			{
				trade = trade,
				perks = perks?.ToArray(),
				hasPerks = (perks != null)
			});
		}
	}

	/// Server: a hand-picked trade or perks end the "dealt" mark.
	internal static void HandPicked(int seat, bool trade)
	{
		if (Members.Fact(seat, trade ? TradeFact : PerksFact) != null)
		{
			ClearFact(seat, trade ? TradeFact : PerksFact);
		}
	}

	private static void ClearFact(int seat, string key)
	{
		Members.DropFacts(seat, key);
	}

	private static void ClientDealt(MsgRandomDealt m)
	{
		if (m == null)
		{
			return;
		}
		if (!string.IsNullOrEmpty(m.trade))
		{
			ShowTrade = m.trade;
		}
		if (m.hasPerks)
		{
			ShowPerks = (m.perks ?? new string[0]).ToList();
		}
		Log.Info("random: dealt to me: " + (m.trade ?? "-") + "; " + (m.hasPerks ? ("[" + string.Join(", ", m.perks ?? new string[0]) + "]") : "-"));
	}

	internal static void Showed()
	{
		ShowTrade = null;
		ShowPerks = null;
		Shown++;
	}

	[SystemInit]
	internal static void Init()
	{
		Net.On<MsgRandomDealt>("random.dealt", null, ClientDealt);
		ServerMode.NewRunStarting += delegate
		{
			carried.Clear();
			restored.Clear();
			foreach (Member item in Members.State.list)
			{
				string text = TradeOf(item.s);
				string text2 = Members.Fact(item.s, PerksFact);
				if (text != null || text2 != null)
				{
					carried[item.s] = new KeyValuePair<string, string>(text, text2);
				}
			}
			if (carried.Count > 0)
			{
				Log.Info("random: a new run; " + carried.Count + " player(s) keep what they were dealt");
			}
		};
		Net.Disconnected += delegate
		{
			ShowTrade = null;
			ShowPerks = null;
		};
	}

	[SystemTick]
	private static void Tick()
	{
		if (!Net.IsServer || !Game.ShopLoaded || !Server.ShopReady || ServerMode.SettingUpNewRun || Members.Carrying || Dispatch.InRequest)
		{
			return;
		}
		float realtimeSinceStartup = UnityEngine.Time.realtimeSinceStartup;
		if (realtimeSinceStartup < nextLook)
		{
			return;
		}
		nextLook = realtimeSinceStartup + 0.5f;
		try
		{
			if ((founderTrade != null || founderPerks != null) && Members.BySeat(1) != null)
			{
				Dealt((Net.IsHost && founderTell) ? Net.LocalPlayer : 0, 1, founderTrade, founderPerks);
				founderTrade = null;
				founderPerks = null;
			}
			if (carried.Count > 0)
			{
				Redeal();
			}
		}
		catch (Exception ex)
		{
			if (Log.Allow("random|tick", 30000L, out var skipped))
			{
				Log.Warn("random: the server's look-over threw" + Log.More(skipped) + ": " + ex);
			}
		}
	}

	/// Server, a new run: each player gets last run's draw again once their bay is there.
	private static void Redeal()
	{
		foreach (int item in carried.Keys.ToList())
		{
			if (item <= 1 || Members.BySeat(item) == null)
			{
				continue;
			}
			if (restored.Add(item))
			{
				// At once, so the player's copy waits for the deal instead of opening the screens.
				Dictionary<string, string> facts = new Dictionary<string, string>();
				if (carried[item].Key != null)
				{
					facts[TradeFact] = carried[item].Key;
				}
				if (carried[item].Value != null)
				{
					facts[PerksFact] = carried[item].Value;
				}
				Members.SetFacts(item, facts);
			}
			int num = PlayerOf(item);
			if (num == 0 || Bays.BayOf(item) == null || HostTrip.Out || Outings.AnyOut)
			{
				continue;
			}
			KeyValuePair<string, string> keyValuePair = carried[item];
			carried.Remove(item);
			if (keyValuePair.Key != null && Specs.Of(item) == null)
			{
				string id = (Specs.TakenBy(keyValuePair.Key) == 0) ? keyValuePair.Key : DrawTrade(item);
				if (id != null)
				{
					Specs.PickFor(num, id, dealt: true, tell: id != keyValuePair.Key);
				}
				if (Specs.Of(item) == null)
				{
					ClearFact(item, TradeFact);
				}
			}
			if (keyValuePair.Value != null && !Perks.Picked(item))
			{
				List<string> list = Split(keyValuePair.Value);
				if (Perks.Refusal(item, list) != null)
				{
					list = DrawPerks(item, (string id) => Perks.LockReason(item, id));
				}
				Perks.Deal(num, list, tell: list.Count != Split(keyValuePair.Value).Count || list.Except(Split(keyValuePair.Value)).Any());
				if (!Perks.Picked(item))
				{
					ClearFact(item, PerksFact);
				}
			}
			Log.Info("random: " + Members.NameOf(item) + " (seat " + item + ") keeps last run's draw: " + (Specs.Of(item) ?? "-") + "; [" + string.Join(", ", Perks.Of(item).Ids) + "]");
		}
	}

	private static int PlayerOf(int seat)
	{
		foreach (KeyValuePair<int, PlayerInfo> player in Net.Players)
		{
			if (player.Value != null && player.Value.Seat == seat)
			{
				return player.Key;
			}
		}
		return 0;
	}
}
