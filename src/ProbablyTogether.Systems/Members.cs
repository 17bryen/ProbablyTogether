using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ProbablyTogether.UI;
using HarmonyLib;
using Newtonsoft.Json;

namespace ProbablyTogether.Systems;

/// One person who has played in this shop, kept whether connected or not.
public sealed class Member
{
	/// "steam:<id>", or "name:<name>" without a Steam id.
	public string k;

	/// Seat: 1 for the shop's first player, then 2, 3...; never reused.
	public int s;

	/// Name last seen.
	public string n = "";

	/// Shop day of the first join.
	public int d;

	/// Per-player facts (specialization, storage bay...).
	public Dictionary<string, string> x = new Dictionary<string, string>();
}

public sealed class MembersState
{
	public int v = 1;

	/// Rent goes up by this many percent for every player after the first.
	public int rent = 50;

	/// Extra customers a day for every player after the first.
	public double cust = 2.0;

	public List<Member> list = new List<Member>();
}

public sealed class MsgMembers
{
	public string json;

	/// The seat that was just created (0: something else changed).
	public int joined;
}

/// The shop's players, kept in the save's mod data; only the server changes it, through one logged message per change.
public static class Members
{
	private const string DataKey = "members";

	private static string cachedJson;

	private static MembersState cached = new MembersState();

	private static bool configApplied;

	internal static int Refused;

	public static MembersState State
	{
		get
		{
			string text = Read();
			if (!ReferenceEquals(text, cachedJson) && text != cachedJson)
			{
				cachedJson = text;
				cached = Parse(text);
			}
			return cached;
		}
	}

	public static int Count => State.list.Count;

	public static int Extra => Math.Max(0, Count - 1);

	public static Member BySeat(int seat)
	{
		foreach (Member item in State.list)
		{
			if (item.s == seat)
			{
				return item;
			}
		}
		return null;
	}

	public static string NameOf(int seat)
	{
		return BySeat(seat)?.n ?? ("player " + seat);
	}

	/// The seat of a connected player (0: a guest, or unknown).
	public static int SeatOf(int player)
	{
		if (!Net.Players.TryGetValue(player, out var value))
		{
			return 0;
		}
		return value.Seat;
	}

	public static int LocalSeat => SeatOf(Net.LocalPlayer);

	public static int Rent(int baseRent)
	{
		MembersState state = State;
		int extra = Extra;
		if (extra <= 0 || state.rent <= 0 || baseRent <= 0)
		{
			return baseRent;
		}
		return (int)Math.Round((double)baseRent * (100.0 + (double)(state.rent * extra)) / 100.0, MidpointRounding.AwayFromZero);
	}

	public static int ExtraCustomers()
	{
		MembersState state = State;
		return (int)Math.Round(Math.Max(0.0, state.cust) * (double)Extra, MidpointRounding.AwayFromZero);
	}

	public static string Describe()
	{
		MembersState state = State;
		List<string> list = new List<string>();
		foreach (Member item in state.list)
		{
			list.Add(item.s + "=" + item.n);
		}
		return state.list.Count + " player(s) [" + string.Join(", ", list) + "], rent +" + state.rent + "% and +" + state.cust.ToString("0.##", CultureInfo.InvariantCulture) + " customer(s) a day per added player";
	}

	private static string Read()
	{
		if (!Game.ShopLoaded || Game.Store == null)
		{
			return null;
		}
		try
		{
			return ModHelper.GetModData("bellun_probably_together", "members", (string)null);
		}
		catch
		{
			return null;
		}
	}

	private static MembersState Parse(string json)
	{
		if (string.IsNullOrEmpty(json))
		{
			return new MembersState();
		}
		try
		{
			MembersState membersState = JsonConvert.DeserializeObject<MembersState>(json) ?? new MembersState();
			if (membersState.list == null)
			{
				membersState.list = new List<Member>();
			}
			foreach (Member item in membersState.list)
			{
				if (item.x == null)
				{
					item.x = new Dictionary<string, string>();
				}
			}
			return membersState;
		}
		catch (Exception ex)
		{
			Log.Warn("members: the shop's player record is unreadable (" + ex.Message + "); starting an empty one");
			return new MembersState();
		}
	}

	private static void Write(string json)
	{
		using (Net.Apply())
		{
			ModHelper.SetModData("bellun_probably_together", "members", json);
		}
		cachedJson = null;
	}

	[SystemInit]
	internal static void Init()
	{
		Net.On<MsgMembers>("members.set", null, ClientSet);
		Digest.AddPart("members", delegate
		{
			string text = Read() ?? "";
			ulong num = 14695981039346656037uL;
			foreach (char c in text)
			{
				num ^= c;
				num *= 1099511628211uL;
			}
			return text.Length + ":" + (num & 0xFFFFFFFFu).ToString("x8");
		});
	}

	private static void ClientSet(MsgMembers m)
	{
		if (m == null || m.json == null || !Game.ShopLoaded)
		{
			return;
		}
		int count = Count;
		Write(m.json);
		if (m.joined > 0 && !DayLog.Replaying && Count > count && Count > 1)
		{
			MembersState state = State;
			Notices.Show(Lang.Trf("{0} is now player {1} of this shop: rent and daily customers go up with every player.", Kit.Name(NameOf(m.joined)), m.joined.ToString(CultureInfo.InvariantCulture)), "members");
		}
	}

	internal static void ServerReset()
	{
		configApplied = false;
		cachedJson = null;
	}

	private static string KeyFor(ulong steamId, string name)
	{
		if (steamId != 0L)
		{
			return "steam:" + steamId.ToString(CultureInfo.InvariantCulture);
		}
		return "name:" + (name ?? "").Trim().ToLowerInvariant();
	}

	private static bool SeatOnline(int seat)
	{
		foreach (PlayerInfo value in Net.Players.Values)
		{
			if (value.Seat == seat)
			{
				return true;
			}
		}
		return false;
	}

	/// Server: gives a joining player their seat, or why not.
	/// Seat 0 = guest (the same account is already playing); guests own nothing and do not count.
	internal static bool Admit(ulong steamId, string name, out int seat, out string why)
	{
		seat = 0;
		why = null;
		if (!Game.ShopLoaded || Game.Store == null)
		{
			return true;
		}
		MembersState membersState = Parse(Read());
		string text = KeyFor(steamId, name);
		Member member = membersState.list.Find((Member x) => x.k == text);
		if (member != null)
		{
			if (SeatOnline(member.s))
			{
				Log.Info("members: " + name + " uses the account of seat " + member.s + " (" + member.n + "), who is already playing: joins as a guest");
				return true;
			}
			seat = member.s;
			if (member.n != name && !string.IsNullOrEmpty(name))
			{
				member.n = name;
				Publish(membersState, 0);
			}
			Log.Info("members: " + name + " is back in seat " + seat + " (first joined on day " + member.d + ")");
			return true;
		}
		int num = Server.Config?.maxPlayers ?? 4;
		if (membersState.list.Count >= num)
		{
			List<string> list = new List<string>();
			foreach (Member item in membersState.list)
			{
				list.Add(item.n);
			}
			why = "This shop already has its " + num + " player(s): " + string.Join(", ", list) + ". A shop keeps a place for everyone who has played in it, connected or not.";
			Refused++;
			Log.Info("members: refused " + name + " (" + text + "): all " + num + " seat(s) are taken");
			return false;
		}
		int num2 = 0;
		foreach (Member item2 in membersState.list)
		{
			num2 = Math.Max(num2, item2.s);
		}
		member = new Member
		{
			k = text,
			s = num2 + 1,
			n = (name ?? ""),
			d = Game.Day
		};
		membersState.list.Add(member);
		seat = member.s;
		Publish(membersState, seat);
		Log.Info("members: " + name + " takes seat " + seat + " (" + text + "); " + Describe());
		return true;
	}

	/// Server: frees a seat for good; what the player owned is the caller's business.
	internal static bool Forget(int seat)
	{
		MembersState membersState = Parse(Read());
		if (membersState.list.RemoveAll((Member x) => x.s == seat) == 0)
		{
			return false;
		}
		Publish(membersState, 0);
		Log.Info("members: seat " + seat + " is free again; " + Describe());
		return true;
	}

	internal static bool SetFact(int seat, string key, string value)
	{
		MembersState membersState = Parse(Read());
		Member member = membersState.list.Find((Member x) => x.s == seat);
		if (member == null)
		{
			return false;
		}
		if (value == null)
		{
			member.x.Remove(key);
		}
		else
		{
			member.x[key] = value;
		}
		Publish(membersState, 0);
		return true;
	}

	internal static bool SetFacts(int seat, Dictionary<string, string> facts)
	{
		MembersState membersState = Parse(Read());
		Member member = membersState.list.Find((Member x) => x.s == seat);
		if (member == null || facts == null)
		{
			return false;
		}
		foreach (KeyValuePair<string, string> fact in facts)
		{
			member.x[fact.Key] = fact.Value ?? "";
		}
		Publish(membersState, 0);
		return true;
	}

	public static string Fact(int seat, string key)
	{
		Member member = BySeat(seat);
		if (member == null || !member.x.TryGetValue(key, out var value))
		{
			return null;
		}
		return value;
	}

	private static void Publish(MembersState state, int joined)
	{
		string json = JsonConvert.SerializeObject((object)state, (Formatting)0);
		Write(json);
		Net.ToAll("members.set", new MsgMembers
		{
			json = json,
			joined = joined
		});
	}

	/// Server: the host has no join handshake, and the shop's settings may have changed since the save was written.
	[SystemTick]
	private static void Tick()
	{
		if (!Net.IsServer || !Server.ShopReady || !Game.ShopLoaded || Dispatch.InRequest)
		{
			return;
		}
		if (!configApplied)
		{
			configApplied = true;
			ServerConfig config = Server.Config;
			MembersState membersState = Parse(Read());
			if (config != null && (membersState.rent != config.rentPercentPerPlayer || Math.Abs(membersState.cust - config.customersPerPlayer) > 0.001))
			{
				membersState.rent = config.rentPercentPerPlayer;
				membersState.cust = config.customersPerPlayer;
				Publish(membersState, 0);
			}
			Log.Info("members: " + Describe());
		}
		foreach (Server.Peer value in Server.ByPlayer.Values)
		{
			if (value.Info != null && value.Info.Seat == 0 && !value.SeatAsked && value.Conn != null && value.Conn.IsLoopback)
			{
				value.SeatAsked = true;
				if (Admit(value.Info.SteamId, value.Info.Name, out var seat, out var why))
				{
					value.Info.Seat = seat;
				}
				else
				{
					Log.Warn("members: the host has no seat in this shop (" + why + "); it plays as a guest");
				}
				Server.RosterChanged();
				break;
			}
		}
	}

	// The game's base rent stays as written; what is shown, checked and paid is the base times the player factor.

	[HarmonyPatch(typeof(PlayerStore), "GetRentValue")]
	private static class RentValuePatch
	{
		private static void Postfix(ref int __result)
		{
			if (Net.IsOnline)
			{
				__result = Rent(__result);
			}
		}
	}

	[HarmonyPatch(typeof(PlayerStore), "CanPayRent")]
	private static class CanPayRentPatch
	{
		private static bool Prefix(PlayerStore __instance, ref bool __result)
		{
			if (!Net.IsOnline)
			{
				return true;
			}
			__result = __instance.playerCash >= Rent(__instance.rentValue + __instance.temporaryRent);
			return false;
		}
	}

	[HarmonyPatch(typeof(PlayerStore), "OnRentPaid")]
	private static class RentPaidPatch
	{
		private static void Prefix(PlayerStore __instance, out int __state)
		{
			int num = __instance.rentValue + __instance.temporaryRent;
			__state = (Net.IsOnline ? (Rent(num) - num) : 0);
		}

		private static void Postfix(PlayerStore __instance, int __state)
		{
			if (__state != 0)
			{
				__instance.playerCash -= __state;
			}
		}
	}
}
