using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ProbablyTogether.UI;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether.Systems;

/// Per-player starting perks: unique and stacking perks act for the shop, personal perks only for their holder.
/// The record is changed by replayed code on every copy, saved, and part of the fingerprint.
public static class Perks
{
	public const int Unique = 1;

	public const int Stacking = 2;

	public const int Personal = 3;

	private static readonly Dictionary<string, int> kinds = new Dictionary<string, int>(StringComparer.Ordinal)
	{
		{ "saving", Stacking },
		{ "bad_lease", Stacking },
		{ "convict", Unique },
		{ "model_citizen", Unique },
		{ "well_connected", Unique },
		{ "sycophant", Unique },
		{ "awful_negotiator", Stacking },
		{ "public_enemy", Unique },
		{ "alert", Unique },
		{ "sawyer_crew", Unique },
		{ "diplomatic", Unique },
		{ "intimidating", Unique },
		{ "side_street", Stacking },
		{ "nicotine_addiction", Stacking },
		{ "narcotic_addiction", Stacking },
		{ "alcohol_addiction", Stacking },
		{ "secret_admirer", Personal },
		{ "colorful_character", Personal },
		{ "blackmail", Unique },
		{ "blabbermouth", Unique },
		{ "minimalist", Unique },
		{ "ascetic", Unique },
		{ "sentinel", Personal },
		{ "agoraphobia", Personal },
		{ "welfare_program", Personal },
		{ "makeshift_reactor", Unique },
		{ "hoarder", Unique },
		{ "banned_commissary", Personal },
		{ "proficient_scavenger", Personal },
		{ "careful_scavenger", Personal },
		{ "maimed", Personal },
		{ "revolution_sympathizer", Unique },
		{ "gambling_addiction", Personal },
		{ "tapehead", Unique },
		{ "wild_favorite", Unique },
		{ "wild_bad_side", Unique },
		{ "aug_menace", Unique },
		{ "lucky", Stacking },
		{ "unlucky", Stacking },
		{ "handy", Stacking },
		{ "tinkerer", Personal },
		{ "vending", Unique },
		{ "short_sleeper", Personal },
		{ "working_pipes", Unique },
		{ "illiterate", Personal },
		{ "prime_real_estate", Stacking },
		{ "degenerative_osteoarthritis", Personal },
		{ "known_snitch", Unique }
	};

	/// Shop perks whose clock and cure are per player.
	private static readonly string[] addictions = new string[3] { "nicotine_addiction", "alcohol_addiction", "narcotic_addiction" };

	/// Personal perks that count for the shop when any player has them; the others need every player.
	private static readonly HashSet<string> anyone = new HashSet<string>(StringComparer.Ordinal) { "sentinel", "gambling_addiction", "welfare_program", "secret_admirer" };

	// Indexes into Rec.B.
	private const int BN = 0, BA = 1, BC = 2, BR = 3, BSD = 4, BSP = 5, BOut = 6, BSaDays = 7, BSaInj = 8, BSaBlack = 9, BLen = 10;

	public sealed class Rec
	{
		public bool Picked;

		public List<string> Ids = new List<string>();

		public int[] B = NewBody();

		public Rec Copy()
		{
			return new Rec
			{
				Picked = Picked,
				Ids = new List<string>(Ids),
				B = (int[])B.Clone()
			};
		}
	}

	private static int[] NewBody()
	{
		int[] array = new int[BLen];
		array[BR] = 999;
		return array;
	}

	private const string Key = "perks";

	private static string cachedRaw;

	private static Dictionary<int, Rec> cached = new Dictionary<int, Rec>();

	private static readonly Stack<int> scope = new Stack<int>();

	/// The seat whose clocks the game's fields hold now (0: nobody's).
	private static int shown;

	/// The seat whose turn of the night, or of the morning's gift, is running.
	private static int turnSeat;

	/// Above 0 while the shop as a whole is asked (the queue, the shutter opening).
	private static int nobody;

	/// True while the game's start-of-run code runs for one player's picks: the game's list is read as is.
	private static bool raw;

	private static bool night;

	private static float nextLook;

	private static readonly List<KeyValuePair<int, string>> deaths = new List<KeyValuePair<int, string>>();

	internal static int Picks;

	internal static string LastRefusal = "";

	internal static string LastApplied = "";

	internal static int Deaths;

	/// Server: the founder picked on the new game screens and is not asked again.
	internal static bool FounderPickedOnMenu;

	public static bool Enabled => Net.IsOnline && Bays.Enabled && Game.ShopLoaded && Game.Store != null && Game.Store.healthData != null;

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

	public static int KindOf(string id)
	{
		int value;
		return (id != null && kinds.TryGetValue(id, out value)) ? value : 0;
	}

	public static IEnumerable<string> Known => kinds.Keys;

	public static StartingPerk Def(string id)
	{
		try
		{
			if (StartingPerkList.Perks == null || StartingPerkList.Perks.Count == 0)
			{
				StartingPerkList.InitStartingPerk();
			}
			return StartingPerkList.Perks.FirstOrDefault((StartingPerk p) => p != null && p.id == id);
		}
		catch
		{
			return null;
		}
	}

	public static string NameOfPerk(string id)
	{
		try
		{
			return Def(id)?.GetLocalizedDisplayName() ?? id;
		}
		catch
		{
			return id;
		}
	}

	private static bool Excludes(string a, string b)
	{
		StartingPerk startingPerk = Def(a);
		StartingPerk startingPerk2 = Def(b);
		return (startingPerk?.incompatiblePerks != null && startingPerk.incompatiblePerks.Contains(b)) || (startingPerk2?.incompatiblePerks != null && startingPerk2.incompatiblePerks.Contains(a));
	}

	private static Dictionary<int, Rec> Parse(string text)
	{
		Dictionary<int, Rec> dictionary = new Dictionary<int, Rec>();
		foreach (string item in (text ?? "").Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries))
		{
			int num = item.IndexOf('=');
			if (num <= 0 || !int.TryParse(item.Substring(0, num), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				continue;
			}
			string[] array = item.Substring(num + 1).Split('|');
			Rec rec = new Rec
			{
				Picked = array[0] == "1"
			};
			if (array.Length > 1)
			{
				rec.Ids = array[1].Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
			}
			if (array.Length > 2)
			{
				string[] array2 = array[2].Split(':');
				for (int i = 0; i < array2.Length && i < BLen; i++)
				{
					int.TryParse(array2[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out rec.B[i]);
				}
			}
			dictionary[result] = rec;
		}
		return dictionary;
	}

	private static string Format(Dictionary<int, Rec> table)
	{
		return string.Join(";", from p in table
			orderby p.Key
			select p.Key.ToString(CultureInfo.InvariantCulture) + "=" + (p.Value.Picked ? "1" : "0") + "|" + string.Join(",", p.Value.Ids) + "|" + string.Join(":", p.Value.B.Select((int v) => v.ToString(CultureInfo.InvariantCulture))));
	}

	private static void Write(Dictionary<int, Rec> table)
	{
		string text = Format(table);
		using (Net.Apply())
		{
			ModHelper.SetModData("bellun_probably_together", Key, text);
		}
		cachedRaw = text;
		cached = table;
	}

	private static Dictionary<int, Rec> Table()
	{
		string text = null;
		try
		{
			text = ModHelper.GetModData("bellun_probably_together", Key, (string)null);
		}
		catch
		{
		}
		if (text == null)
		{
			// No record yet: the shop's list was the founder's pick, so it becomes seat 1's and personal perks leave it.
			Dictionary<int, Rec> dictionary = new Dictionary<int, Rec>();
			List<StartingPerk> list = Game.Store.startingPerks ?? new List<StartingPerk>();
			List<string> list2 = list.Where((StartingPerk p) => p != null && kinds.ContainsKey(p.id)).Select((StartingPerk p) => p.id).Distinct().ToList();
			bool onMenu = FounderPickedOnMenu && Net.IsServer;
			FounderPickedOnMenu = false;
			if (list2.Count > 0 || onMenu)
			{
				Rec rec = new Rec
				{
					Picked = true,
					Ids = list2
				};
				HealthData healthData = Game.Store.healthData;
				rec.B = Fields();
				dictionary[1] = rec;
				using (Net.Apply())
				{
					list.RemoveAll((StartingPerk p) => p != null && KindOf(p.id) == Personal);
					// The game's start code treats the founder's Commissary ban as the shop's; the ban is the founder's own.
					PlayerStore store = Game.Store;
					if (onMenu && list2.Contains("banned_commissary") && !store.isStampIntroduced && store.isAfterhoursIntroduced && store.isSecurityRecordIntroduced)
					{
						store.isStampIntroduced = true;
						Log.Info("perks: the founder is banned from the Commissary; the shop itself knows food stamps");
					}
				}
				Log.Info("perks: the shop's own perks (" + string.Join(", ", list2) + ") are now seat 1's");
			}
			Write(dictionary);
			return cached;
		}
		if ((object)text != cachedRaw && text != cachedRaw)
		{
			cachedRaw = text;
			cached = Parse(text);
		}
		return cached;
	}

	private static readonly Rec none = new Rec();

	public static Rec Of(int seat)
	{
		Rec value;
		return (seat > 0 && Table().TryGetValue(seat, out value)) ? value : none;
	}

	private static void Change(int seat, Action<Rec> change)
	{
		if (seat <= 0)
		{
			return;
		}
		Dictionary<int, Rec> dictionary = Table().ToDictionary((KeyValuePair<int, Rec> p) => p.Key, (KeyValuePair<int, Rec> p) => p.Value.Copy());
		if (!dictionary.TryGetValue(seat, out var value))
		{
			value = (dictionary[seat] = new Rec());
		}
		change(value);
		if (Format(dictionary) != (cachedRaw ?? ""))
		{
			Write(dictionary);
		}
	}

	public static bool Picked(int seat)
	{
		return Of(seat).Picked;
	}

	public static bool Has(int seat, string id)
	{
		return seat > 0 && Of(seat).Ids.Contains(id);
	}

	public static List<int> Holders(string id)
	{
		return (from p in Table()
			where p.Value.Ids.Contains(id) && Members.BySeat(p.Key) != null
			orderby p.Key
			select p.Key).ToList();
	}

	public static int Count(string id)
	{
		return Holders(id).Count;
	}

	public static int HolderOf(string id)
	{
		return Holders(id).FirstOrDefault();
	}

	private static bool TripViewLive => HostTrip.Out && !HostTrip.HomeView;

	private static int LocalSeat => Net.HasLocalPlayer ? Members.LocalSeat : 0;

	/// The seat whose doing this is: the requester or replayed actor, else this copy's player.
	public static int Actor
	{
		get
		{
			if (turnSeat > 0)
			{
				return turnSeat;
			}
			foreach (int item in scope)
			{
				if (item > 0)
				{
					return item;
				}
			}
			return LocalSeat;
		}
	}

	private static int[] Fields()
	{
		PlayerStore store = Game.Store;
		HealthData healthData = store.healthData;
		int[] array = NewBody();
		array[BN] = healthData.daysSinceNicotineConsumption;
		array[BA] = healthData.daysSinceAlcoholConsumption;
		array[BC] = healthData.daysSinceNarcoticConsumption;
		array[BR] = healthData.daysSinceNarcoticRelief;
		array[BSD] = healthData.daysSinceSentinelInjection;
		array[BSP] = healthData.sentinelPool;
		return array;
	}

	private static void SetFields(int[] b)
	{
		HealthData healthData = Game.Store.healthData;
		healthData.daysSinceNicotineConsumption = b[BN];
		healthData.daysSinceAlcoholConsumption = b[BA];
		healthData.daysSinceNarcoticConsumption = b[BC];
		healthData.daysSinceNarcoticRelief = b[BR];
		healthData.daysSinceSentinelInjection = b[BSD];
		healthData.sentinelPool = b[BSP];
	}

	/// Writes the game's fields back into the record of the seat they were showing.
	private static void Keep()
	{
		if (shown <= 0 || Game.Store?.healthData == null)
		{
			return;
		}
		int[] f = Fields();
		int seat = shown;
		Rec rec = Of(seat);
		if (rec.B[BN] == f[BN] && rec.B[BA] == f[BA] && rec.B[BC] == f[BC] && rec.B[BR] == f[BR] && rec.B[BSD] == f[BSD] && rec.B[BSP] == f[BSP])
		{
			return;
		}
		Change(seat, delegate(Rec r)
		{
			r.B[BN] = f[BN];
			r.B[BA] = f[BA];
			r.B[BC] = f[BC];
			r.B[BR] = f[BR];
			r.B[BSD] = f[BSD];
			r.B[BSP] = f[BSP];
		});
	}

	/// The game's fields hold this seat's clocks from now on.
	private static void Show(int seat)
	{
		if (!Enabled || TripViewLive || seat == shown)
		{
			return;
		}
		Keep();
		if (seat > 0)
		{
			SetFields(Of(seat).B);
		}
		shown = seat;
	}

	internal static void Enter(int player)
	{
		int num = 0;
		try
		{
			if (Enabled && player > 0 && !night)
			{
				num = Members.SeatOf(player);
			}
			scope.Push(num);
			if (num > 0)
			{
				Show(num);
			}
		}
		catch (Exception ex)
		{
			if (scope.Count == 0 || scope.Peek() != num)
			{
				scope.Push(0);
			}
			Log.Warn("perks: turning to player " + player + " threw: " + ex.Message);
		}
	}

	internal static void Leave()
	{
		if (scope.Count == 0)
		{
			return;
		}
		int num = scope.Pop();
		if (num <= 0)
		{
			return;
		}
		try
		{
			if (!night)
			{
				Show(Actor);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("perks: turning back from seat " + num + " threw: " + ex.Message);
		}
	}

	/// False: let the game read the shop's own list.
	internal static bool Answer(string id, out bool result)
	{
		result = false;
		if (raw || !Enabled || KindOf(id) != Personal)
		{
			return false;
		}
		result = ShopPassOfNight ? (id == "gambling_addiction" && ForTheShop(id)) : AnswerPersonal(id);
		return true;
	}

	private static bool AnswerPersonal(string id)
	{
		if (turnSeat > 0)
		{
			return Has(turnSeat, id);
		}
		// Lottery unlocks are the shop's; only the forced buying is the addict's (PerksGamblingPatch).
		if (nobody > 0 || id == "gambling_addiction")
		{
			return ForTheShop(id);
		}
		// At night with nobody acting, every copy must give the same answer.
		if (DayNight.NightRunning && !scope.Any((int s) => s > 0))
		{
			return ForTheShop(id);
		}
		return Has(Actor, id);
	}

	private static bool ForTheShop(string id)
	{
		List<Member> list = Members.State.list;
		if (list.Count == 0)
		{
			return false;
		}
		if (id == "short_sleeper")
		{
			// No customer is missed if everybody who went out last night is a short sleeper.
			// During the night the day has not moved on, so "last night" is tonight.
			int last = DayNight.NightRunning ? DayNight.NightDay : (Game.Day - 1);
			return list.Where((Member m) => Of(m.s).B[BOut] >= last && Of(m.s).B[BOut] > 0).All((Member m) => Has(m.s, id));
		}
		return anyone.Contains(id) ? list.Any((Member m) => Has(m.s, id)) : list.All((Member m) => Has(m.s, id));
	}

	/// Who is addicted (may consume, whose clock runs) is per player.
	internal static bool AnswerAddicted(string id, out bool result)
	{
		result = false;
		if (raw || !Enabled)
		{
			return false;
		}
		if (ShopPassOfNight)
		{
			result = false;
		}
		else if (turnSeat > 0)
		{
			result = Has(turnSeat, id);
		}
		else if (nobody > 0)
		{
			result = Count(id) > 0;
		}
		else
		{
			result = Has(Actor, id);
		}
		return true;
	}

	internal static void AsNobody(Action what)
	{
		nobody++;
		try
		{
			what();
		}
		finally
		{
			nobody--;
		}
	}

	internal static void NobodyEnter()
	{
		nobody++;
	}

	internal static void NobodyLeave()
	{
		if (nobody > 0)
		{
			nobody--;
		}
	}

	public static int Points(IEnumerable<string> ids)
	{
		return ids.Sum((string i) => Def(i)?.cost ?? 0);
	}

	public static int Slots(IEnumerable<string> ids)
	{
		return 4 + ids.Sum((string i) => Def(i)?.maxSlot ?? 0);
	}

	/// Why this player cannot take this perk (null: they can); shown on the perk screen and checked by the server.
	public static string LockReason(int seat, string id)
	{
		int num = KindOf(id);
		if (num == 0)
		{
			return "not a perk of this game";
		}
		if (num == Unique)
		{
			int num2 = HolderOf(id);
			if (num2 > 0 && num2 != seat)
			{
				return Lang.Trf("{0} has it", Members.NameOf(num2));
			}
		}
		if (num != Personal)
		{
			// Exclusive shop perks: whichever was picked first locks the other out for everybody.
			foreach (string key in kinds.Keys)
			{
				if (key == id || KindOf(key) == Personal || !Excludes(id, key))
				{
					continue;
				}
				int num3 = Holders(key).FirstOrDefault((int s) => s != seat);
				if (num3 > 0)
				{
					return Lang.Trf("{0} has {1}", Members.NameOf(num3), NameOfPerk(key));
				}
			}
		}
		if (id == "minimalist" || id == "ascetic")
		{
			int width = (id == "ascetic") ? 16 : 20;
			GameGridInventory inv = EmporiumEntry.Instance?.invElement;
			if (inv != null && inv.childItems.Any((GameItem i) => i != null && i.modifiedShape != null && i.modifiedShape.minX + i.modifiedShape.globalWidth > width))
			{
				return Lang.Trf("the shop's storage has things beyond column {0}", width.ToString(CultureInfo.InvariantCulture));
			}
		}
		return null;
	}

	/// Why this set of perks cannot be this player's pick (null: it can).
	public static string Refusal(int seat, List<string> ids, bool again = false)
	{
		if (!Enabled)
		{
			return "This shop does not use personal perks.";
		}
		if (seat <= 0)
		{
			return "Guests have no perks: join with your own account.";
		}
		if (Picked(seat) && !again)
		{
			return "You have already picked your perks in this shop.";
		}
		if (ids.Distinct().Count() != ids.Count)
		{
			return "A perk is in the list twice.";
		}
		foreach (string id in ids)
		{
			if (KindOf(id) == 0 || Def(id) == null)
			{
				return "'" + id + "' is not a perk of this game.";
			}
			string text = LockReason(seat, id);
			if (text != null)
			{
				return NameOfPerk(id) + ": " + text + ".";
			}
			foreach (string id2 in ids)
			{
				if (id2 != id && Excludes(id, id2))
				{
					return NameOfPerk(id) + " and " + NameOfPerk(id2) + " do not go together.";
				}
			}
		}
		if (Points(ids) > 3)
		{
			return "That is " + Points(ids) + " perk points; a player has 3.";
		}
		if (ids.Count > Slots(ids))
		{
			return "That is " + ids.Count + " perks; you have " + Slots(ids) + " slots.";
		}
		if (HostTrip.Out || Outings.AnyOut)
		{
			return "Pick them when everyone is back in the shop.";
		}
		return null;
	}

	public static void Pick(IEnumerable<string> ids)
	{
		if (!Net.IsOnline)
		{
			return;
		}
		Net.ToServer("perks.pick", new MsgPerksPick
		{
			ids = (ids ?? new string[0]).ToArray()
		});
	}

	internal static void RefuseTo(int player, string why)
	{
		Refuse(player, why);
	}

	private static void Refuse(int player, string why)
	{
		LastRefusal = why;
		Log.Info("perks: refused " + Net.PlayerName(player) + ": " + why);
		Net.ToPlayer(player, "perks.refused", new MsgPerksRefused
		{
			why = why
		});
	}

	private static void ServerPick(MsgPerksPick m, int from)
	{
		if (m == null || !Game.ShopLoaded)
		{
			return;
		}
		int num = Members.SeatOf(from);
		List<string> list = (m.ids ?? new string[0]).Where((string i) => !string.IsNullOrEmpty(i)).Select((string i) => i.Trim()).ToList();
		string text = Refusal(num, list);
		if (text == null && list.Count > 0 && Bays.BayOf(num) == null)
		{
			text = "Your storage bay is not here yet. Try again in a moment.";
		}
		if (text != null)
		{
			Refuse(from, text);
			return;
		}
		GameItem val = Bays.BayOf(num);
		MsgPerksSet msgPerksSet = new MsgPerksSet
		{
			seat = num,
			ids = list.ToArray(),
			bay = (val != null) ? Ids.Of(val) : 0
		};
		string text2;
		using (Replay.Server(out msgPerksSet.seed))
		{
			text2 = Apply(num, list, val);
		}
		Net.ToAll("perks.set", msgPerksSet);
		Log.Info("perks: " + Members.NameOf(num) + " (seat " + num + ") picked [" + string.Join(", ", list) + "]: " + text2);
	}

	private static void ClientSet(MsgPerksSet m)
	{
		if (m == null || !Game.ShopLoaded)
		{
			return;
		}
		List<string> list = (m.ids ?? new string[0]).ToList();
		string text;
		using (Replay.Client(m.seed ?? new Seed()))
		{
			text = m.again ? Swap(m.seat, list, (m.bay != 0) ? Ids.Find(m.bay) : null) : Apply(m.seat, list, (m.bay != 0) ? Ids.Find(m.bay) : null, m.lab);
		}
		Log.Info("perks: " + Members.NameOf(m.seat) + (m.again ? " now has [" : " picked [") + string.Join(", ", list) + "]: " + text);
		if (!DayLog.Replaying && m.again)
		{
			Notices.Show((list.Count > 0) ? Lang.Trf("{0} chose their perks again: {1}.", Kit.Name(Members.NameOf(m.seat)), string.Join(", ", list.Select(NameOfPerk))) : Lang.Trf("{0} chose their perks again: none.", Kit.Name(Members.NameOf(m.seat))), "perks");
		}
		else if (!DayLog.Replaying && list.Count > 0)
		{
			Notices.Show(Lang.Trf("{0} picked: {1}.", Kit.Name(Members.NameOf(m.seat)), string.Join(", ", list.Select(NameOfPerk))), "perks");
		}
		PerkScreens.Picked(m.seat);
	}

	private static Dictionary<string, double> RepSnapshot()
	{
		Dictionary<string, double> dictionary = new Dictionary<string, double>(StringComparer.Ordinal);
		try
		{
			foreach (StoreReputation storeReputation in Game.Store.storeReputations)
			{
				if (storeReputation != null && !string.IsNullOrEmpty(storeReputation.factionId))
				{
					dictionary[storeReputation.factionId] = Standing.RepNow(storeReputation.factionId);
				}
			}
		}
		catch
		{
		}
		return dictionary;
	}

	/// Credits the change in the shop's standing since the snapshot to that player's ledger row.
	internal static void CreditRep(int seat, Dictionary<string, double> before)
	{
		if (seat <= 0 || before == null)
		{
			return;
		}
		foreach (KeyValuePair<string, double> item in RepSnapshot())
		{
			double value;
			double num = item.Value - (before.TryGetValue(item.Key, out value) ? value : 0.0);
			if (Math.Abs(num) > 0.0001)
			{
				Standing.Credit(seat, item.Key, num);
			}
		}
	}

	/// Given once per shop, when the first player not banned from the Commissary has picked, or at the next opening.
	private static readonly string[] commissaryGuides = new string[2] { "stamp_guide", "logo_checker" };

	private const string GuidesKey = "commissaryGuides";

	internal static int GuidesGiven;

	internal static void CommissaryGuides(List<string> notes)
	{
		PlayerStore store = Game.Store;
		EmporiumEntry e = EmporiumEntry.Instance;
		if (!Enabled || store == null || (UnityEngine.Object)(object)e == (UnityEngine.Object)null || e.dossier == null || !store.skipIntro || store.IsRockBottomStart() || ForTheShop("banned_commissary"))
		{
			return;
		}
		try
		{
			if (ModHelper.GetModData("bellun_probably_together", GuidesKey, (string)null) == "1")
			{
				return;
			}
		}
		catch
		{
			return;
		}
		HashSet<string> owned;
		try
		{
			owned = new HashSet<string>(e.GetAllOwnedItems(false, true).Select((GameItem i) => i.identifier), StringComparer.Ordinal);
		}
		catch (Exception ex)
		{
			Log.Warn("perks: looking for the Commissary's guides threw: " + ex.Message);
			return;
		}
		List<string> given = new List<string>();
		using (Net.Apply())
		{
			foreach (string id in commissaryGuides)
			{
				if (owned.Contains(id))
				{
					continue;
				}
				GameItem item = DirectoryMaster.Item(id);
				if (item == null)
				{
					continue;
				}
				GraphUtils.TryAcceptAll(e.dossier, item);
				if (item.parentInventory == null)
				{
					GraphUtils.EmporiumTryAdd(item);
				}
				if (item.parentInventory != null)
				{
					given.Add(id);
				}
			}
			ModHelper.SetModData("bellun_probably_together", GuidesKey, "1");
		}
		if (given.Count > 0)
		{
			GuidesGiven += given.Count;
			notes.Add("the Commissary's guides into the dossier (" + string.Join(", ", given) + "): not every player is banned");
		}
	}

	/// Every copy, from the server's seed: applies these perks for one holder.
	private static string Apply(int seat, List<string> ids, GameItem bay, bool merge = false)
	{
		PlayerStore store = Game.Store;
		EmporiumEntry e = EmporiumEntry.Instance;
		if (store == null || (UnityEngine.Object)(object)e == (UnityEngine.Object)null)
		{
			return "the shop is not loaded";
		}
		Keep();
		List<string> notes = new List<string>();
		Dictionary<string, double> before = RepSnapshot();
		List<StartingPerk> real = store.startingPerks ?? (store.startingPerks = new List<StartingPerk>());
		GameGridInventory invElement = e.invElement;
		using (Net.Apply())
		{
			// 1. Shop perks: the game's start-of-run code into a scratch grid, then to the shop.
			// What comes with an addiction goes with the personal perks.
			List<string> listed = ids.Where((string i) => KindOf(i) != Personal).ToList();
			List<string> shared = listed.Where((string i) => !addictions.Contains(i)).ToList();
			List<string> personal = ids.Where((string i) => !shared.Contains(i)).ToList();
			List<GameItem> forShop = RunStart(store, e, shared, notes, seat);
			List<GameItem> forBay = RunStart(store, e, personal, notes, seat);
			PixelWindow w = bay?.contentWindow;
			GraphNodeStorage inside = w?.child;
			GraphNodeStorage[] shop = new GraphNodeStorage[4] { (GraphNodeStorage)invElement, (GraphNodeStorage)e.showcaseElement, (GraphNodeStorage)e.hiddenElement, (GraphNodeStorage)e.backInvinvElement };
			int lost = 0;
			foreach (GameItem item2 in forShop)
			{
				if (!Place(item2, shop) && !(inside != null && GraphUtils.TryAccept(inside, item2) != -1))
				{
					lost++;
					item2.Destroy();
				}
			}
			foreach (GameItem item3 in forBay)
			{
				if (!(inside != null && GraphUtils.TryAccept(inside, item3) != -1) && !Place(item3, shop))
				{
					lost++;
					item3.Destroy();
				}
			}
			if (forShop.Count + forBay.Count > 0)
			{
				notes.Add(forShop.Count + " thing(s) to the shop, " + forBay.Count + " to their bay" + ((lost > 0) ? (", " + lost + " found no room") : ""));
			}
			// 2. The shop's list: every unique or stacking perk somebody has, once.
			foreach (string id in listed)
			{
				StartingPerk startingPerk = Def(id);
				if (startingPerk != null && !real.Any((StartingPerk p) => p != null && p.id == id))
				{
					real.Add(startingPerk);
				}
			}
			if (listed.Contains("minimalist") || listed.Contains("ascetic"))
			{
				try
				{
					e.SetSmallInv();
				}
				catch (Exception ex)
				{
					Log.Warn("perks: making the shop's storage smaller threw: " + ex.Message);
				}
			}
		}
		CreditRep(seat, before);
		// 3. The record: a holder's clocks start as the game starts them (three days since the last dose).
		Change(seat, delegate(Rec r)
		{
			r.Picked = true;
			r.Ids = merge ? r.Ids.Union(ids).ToList() : ids.ToList();
			if (ids.Contains("nicotine_addiction"))
			{
				r.B[BN] = 3;
			}
			if (ids.Contains("alcohol_addiction"))
			{
				r.B[BA] = 3;
			}
			if (ids.Contains("narcotic_addiction"))
			{
				r.B[BC] = 3;
				r.B[BR] = 999;
			}
		});
		if (shown == seat)
		{
			SetFields(Of(seat).B);
		}
		CommissaryGuides(notes);
		Picks++;
		LastApplied = seat + ":" + string.Join(",", ids);
		return (notes.Count > 0) ? string.Join("; ", notes) : "nothing to hand out";
	}

	internal static string ServerSwap(int seat, List<string> ids)
	{
		GameItem val = Bays.BayOf(seat);
		MsgPerksSet msgPerksSet = new MsgPerksSet
		{
			seat = seat,
			ids = ids.ToArray(),
			bay = (val != null) ? Ids.Of(val) : 0,
			again = true
		};
		string text;
		using (Replay.Server(out msgPerksSet.seed))
		{
			text = Swap(seat, ids, val);
		}
		Net.ToAll("perks.set", msgPerksSet);
		Log.Info("perks: " + Members.NameOf(seat) + " (seat " + seat + ") chose again, now [" + string.Join(", ", ids) + "]: " + text);
		return text;
	}

	/// Kept perks are untouched, dropped ones stop as if their holder left, new ones act as a first pick.
	private static string Swap(int seat, List<string> ids, GameItem bay)
	{
		PlayerStore store = Game.Store;
		if (store == null)
		{
			return "the shop is not loaded";
		}
		Keep();
		List<string> old = Of(seat).Ids.ToList();
		List<string> gone = old.Where((string i) => !ids.Contains(i)).ToList();
		List<string> added = ids.Where((string i) => !old.Contains(i)).ToList();
		if (gone.Count > 0)
		{
			Change(seat, delegate(Rec r)
			{
				r.Ids.RemoveAll((string i) => gone.Contains(i));
			});
			using (Net.Apply())
			{
				foreach (string item in gone)
				{
					Stop(store, item);
				}
			}
		}
		string text = (added.Count > 0) ? Apply(seat, added, bay, merge: true) : "nothing to hand out";
		Change(seat, delegate(Rec r)
		{
			r.Picked = true;
			r.Ids = ids.ToList();
		});
		Show(Actor);
		return ((gone.Count > 0) ? ("gave up [" + string.Join(", ", gone) + "]; ") : "") + ((added.Count > 0) ? ("new [" + string.Join(", ", added) + "]: ") : "") + text;
	}

	/// Ends what a perk keeps doing for a holder who no longer has it.
	private static void Stop(PlayerStore store, string id)
	{
		switch (id)
		{
		case "bad_lease":
			store.rentValue = Math.Max(0, store.rentValue - 40);
			break;
		case "prime_real_estate":
			store.rentValue = Math.Max(0, store.rentValue - 75);
			break;
		case "lucky":
			store.healthData.luck -= 15;
			break;
		case "unlucky":
			store.healthData.luck += 15;
			break;
		}
		if (KindOf(id) != Personal && Count(id) == 0 && store.startingPerks != null)
		{
			store.startingPerks.RemoveAll((StartingPerk p) => p != null && p.id == id);
			if (id == "minimalist" || id == "ascetic")
			{
				try
				{
					EmporiumEntry.Instance.invElement.SetShape(24, 10);
					EmporiumEntry.Instance.invElement.Validate();
					EmporiumEntry.Instance.invWindow.Validate();
				}
				catch (Exception ex)
				{
					Log.Warn("perks: giving the shop's storage its width back threw: " + ex.Message);
				}
			}
		}
	}

	private static bool Place(GameItem item, GraphNodeStorage[] into)
	{
		foreach (GraphNodeStorage val in into)
		{
			if (val != null && GraphUtils.TryAccept(val, item) != -1)
			{
				return true;
			}
		}
		return false;
	}

	/// Runs the game's start-of-run perk code for these ids alone; returns what it put in the inventory.
	private static List<GameItem> RunStart(PlayerStore store, EmporiumEntry e, List<string> ids, List<string> notes, int seat)
	{
		List<GameItem> list = new List<GameItem>();
		if (ids.Count == 0)
		{
			return list;
		}
		List<StartingPerk> real = store.startingPerks;
		GameGridInventory invElement = e.invElement;
		GameGridInventory scratch = new GameGridInventory(60, 60);
		bool stamps = store.isStampIntroduced;
		bool augs = store.isAugIntroduced;
		int presence = store.augPresence;
		int nextAug = store.nextAugSpawnDay;
		List<string> run = ids.ToList();
		// Skip a vending machine the shop already has; the debris is placed by hand below.
		if (store.isVendingMachineUnlocked)
		{
			run.Remove("vending");
		}
		bool hoarder = run.Remove("hoarder");
		try
		{
			raw = true;
			store.startingPerks = run.Select(Def).Where((StartingPerk p) => p != null).ToList();
			e.invElement = scratch;
			if (StartingPerk.IsSentinel())
			{
				GraphUtils.EmporiumTryAdd((GraphNodeStorage)(object)scratch, PreBuiltItemHelper.CreateRealGenuineInjector());
				GraphUtils.EmporiumTryAdd((GraphNodeStorage)(object)scratch, DirectoryMaster.Item("injector_guide"));
				GraphUtils.EmporiumTryAdd((GraphNodeStorage)(object)scratch, IntelHelper.CreateSentinel());
				GraphUtils.EmporiumTryAdd((GraphNodeStorage)(object)scratch, DirectoryMaster.Item("magnifier"));
				store.isInjectorIntroduced = true;
			}
			if (StartingPerk.IsGamblingAddiction())
			{
				GraphUtils.EmporiumTryAdd((GraphNodeStorage)(object)scratch, DirectoryMaster.Item("scratcher"));
			}
			if (StartingPerk.IsWelfareProgram())
			{
				// Commissary access is asked per player (PerksCommissaryPatch); the shop's stamp flag stays as it was.
				store.isStampIntroduced = false;
				StartingPerkContent.InitWelfareProgram();
			}
			if (StartingPerk.IsAlcoholAddiction())
			{
				GraphUtils.EmporiumTryAdd((GraphNodeStorage)(object)scratch, DirectoryMaster.Item("nudka"));
			}
			if (StartingPerk.IsNicotineAddiction())
			{
				GraphUtils.EmporiumTryAdd((GraphNodeStorage)(object)scratch, PreBuiltItemHelper.CounterfeitCigarette());
			}
			if (StartingPerk.IsNarcoticAddiction())
			{
				GraphUtils.EmporiumTryAdd((GraphNodeStorage)(object)scratch, DirectoryMaster.Item("pink_injector"));
			}
			StartingPerkContent.HandleNewGamePerk();
		}
		catch (Exception ex)
		{
			Log.Error("perks: the game's start code for [" + string.Join(", ", ids) + "] threw part way: " + ex);
			notes.Add("the game's start code threw");
		}
		finally
		{
			raw = false;
			store.startingPerks = real;
			e.invElement = invElement;
			store.isStampIntroduced = stamps;
			if (augs)
			{
				// On a shop that already knows the augs, nothing starts over.
				store.augPresence = presence;
				store.nextAugSpawnDay = nextAug;
			}
		}
		list.AddRange(((GameInventory)scratch).childItems.ToList());
		foreach (GameItem item in list)
		{
			((GameInventory)scratch).Expel(item);
		}
		if (hoarder)
		{
			foreach (string id in new string[3] { "debris_pile1", "debris_pile2", "debris_pile3" })
			{
				try
				{
					GameItem val = DirectoryMaster.Item(id);
					if (val != null && GraphUtils.TryAccept((GraphNodeStorage)(object)invElement, val) == -1)
					{
						val.Destroy();
						notes.Add("no room for " + id);
					}
				}
				catch (Exception ex2)
				{
					Log.Warn("perks: placing " + id + " threw: " + ex2.Message);
				}
			}
		}
		return list;
	}

	/// True: also remove it from the shop's list.
	internal static bool OnRemove(string id)
	{
		if (raw || !Enabled || KindOf(id) == 0)
		{
			return true;
		}
		int num = Actor;
		int num2 = KindOf(id);
		if (num2 == Unique)
		{
			num = HolderOf(id);
		}
		else if (!Has(num, id))
		{
			num = HolderOf(id);
		}
		if (num > 0)
		{
			Change(num, delegate(Rec r)
			{
				r.Ids.Remove(id);
			});
		}
		// The shop's list keeps a stacking perk while somebody still has it.
		return num2 != Personal && Count(id) == 0;
	}

	/// The Mind Reprogrammer changed the shop's list, as the acting player's doing.
	internal static void AfterReprogram(string id)
	{
		if (!Enabled || KindOf(id) == 0)
		{
			return;
		}
		List<StartingPerk> list = Game.Store.startingPerks;
		bool flag = list != null && list.Any((StartingPerk p) => p != null && p.id == id);
		if (!flag)
		{
			return;
		}
		int actor = Actor;
		if (actor > 0 && !Has(actor, id))
		{
			Change(actor, delegate(Rec r)
			{
				r.Ids.Add(id);
			});
		}
		if (KindOf(id) == Personal)
		{
			using (Net.Apply())
			{
				list.RemoveAll((StartingPerk p) => p != null && p.id == id);
			}
		}
	}

	/// A player leaves or dies: what their perks keep doing ends; what they handed out once stays.
	internal static void Forget(int seat)
	{
		if (!Enabled || seat <= 0 || !Table().ContainsKey(seat))
		{
			return;
		}
		Rec rec = Of(seat);
		PlayerStore store = Game.Store;
		if (shown == seat)
		{
			shown = 0;
		}
		Dictionary<int, Rec> dictionary = Table().ToDictionary((KeyValuePair<int, Rec> p) => p.Key, (KeyValuePair<int, Rec> p) => p.Value.Copy());
		dictionary.Remove(seat);
		Write(dictionary);
		using (Net.Apply())
		{
			foreach (string id in rec.Ids)
			{
				Stop(store, id);
			}
		}
		Show(Actor);
		Log.Info("perks: seat " + seat + "'s perks are gone with them (" + string.Join(", ", rec.Ids) + ")");
	}

	internal static void WentOut(int player, int day)
	{
		if (!Enabled)
		{
			return;
		}
		int num = Members.SeatOf(player);
		if (num > 0 && Of(num).B[BOut] != day)
		{
			Change(num, delegate(Rec r)
			{
				r.B[BOut] = day;
			});
		}
	}

	public static int LastOuting(int seat)
	{
		return Of(seat).B[BOut];
	}

	/// Open if the shop knows food stamps or this player is on the welfare program, and they are not banned.
	public static bool CommissaryOpenFor(int seat)
	{
		PlayerStore store = Game.Store;
		if (store == null)
		{
			return false;
		}
		if (!Enabled)
		{
			return store.isStampIntroduced && !StartingPerk.IsBannedCommissary();
		}
		return (store.isStampIntroduced || Has(seat, "welfare_program")) && !Has(seat, "banned_commissary");
	}

	/// Eight percent for every player in withdrawal, per substance.
	internal static int AddictionPenalty()
	{
		Keep();
		int num = 0;
		foreach (Member item in Members.State.list)
		{
			Rec rec = Of(item.s);
			if (rec.Ids.Contains("nicotine_addiction") && rec.B[BN] >= 3)
			{
				num += 8;
			}
			if (rec.Ids.Contains("alcohol_addiction") && rec.B[BA] >= 3)
			{
				num += 8;
			}
			if (rec.Ids.Contains("narcotic_addiction") && Math.Min(rec.B[BC], rec.B[BR]) >= 3)
			{
				num += 8;
			}
		}
		return num;
	}

	/// Points the game at one seat's account for a moment.
	internal static int TurnTo(int seat)
	{
		int result = turnSeat;
		turnSeat = seat;
		return result;
	}

	/// Runs the shop's part of the night once, then each player's clocks in seat order.
	internal static bool Night(HealthData health)
	{
		if (night || !Enabled)
		{
			return true;
		}
		PlayerStore store = Game.Store;
		Keep();
		shown = 0;
		night = true;
		try
		{
			// The shop's part: wounds, liquid luck, the lottery draw; nobody is addicted or ill in this pass.
			turnSeat = -1;
			health.OnSleep();
			foreach (Member item in Members.State.list.OrderBy((Member m) => m.s).ToList())
			{
				Rec rec = Of(item.s);
				if (!rec.Ids.Any((string i) => i == "sentinel" || addictions.Contains(i)))
				{
					continue;
				}
				turnSeat = item.s;
				SetFields(rec.B);
				shown = item.s;
				int num = (store.nightLogs != null) ? store.nightLogs.Count : 0;
				health.OnSleep();
				Keep();
				shown = 0;
				if (store.nightLogs != null)
				{
					for (int i = num; i < store.nightLogs.Count; i++)
					{
						string text = store.nightLogs[i] ?? "";
						int num2 = text.StartsWith("<color=", StringComparison.Ordinal) ? (text.IndexOf('>') + 1) : 0;
						store.nightLogs[i] = text.Substring(0, num2) + item.n + ": " + text.Substring(num2);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("perks: the night's clocks threw: " + ex);
		}
		finally
		{
			turnSeat = 0;
			night = false;
			shown = 0;
			Show(Actor);
		}
		return false;
	}

	/// In a player's own pass of the night only their clocks run.
	internal static bool ShopPartOfNight => !night || turnSeat <= 0;

	internal static bool PlayerPassOfNight => night && turnSeat > 0;

	internal static bool ShopPassOfNight => night && turnSeat < 0;

	internal static bool SecretAdmirer()
	{
		if (!Enabled || turnSeat != 0)
		{
			return true;
		}
		PlayerStore store = Game.Store;
		StoreClientManager mgr = store.storeClientManager;
		EmporiumEntry e = EmporiumEntry.Instance;
		List<int> list = Holders("secret_admirer");
		if (list.Count == 0 || mgr == null)
		{
			return false;
		}
		int days = mgr.secretAdmirerDaysWithoutGift;
		bool inj = mgr.secretAdmirerInjectorGiven;
		bool black = mgr.secretAdmirerBlackInjectorGiven;
		Keep();
		int was = shown;
		try
		{
			foreach (int seat in list)
			{
				Rec rec = Of(seat);
				turnSeat = seat;
				SetFields(rec.B);
				shown = seat;
				mgr.secretAdmirerDaysWithoutGift = rec.B[BSaDays];
				mgr.secretAdmirerInjectorGiven = rec.B[BSaInj] != 0;
				mgr.secretAdmirerBlackInjectorGiven = rec.B[BSaBlack] != 0;
				HashSet<GameItem> before = new HashSet<GameItem>(((GameInventory)e.frontInvinvElement).childItems);
				StartingPerkContent.HandleSecretAdmirerOnOpenShutter();
				int d = mgr.secretAdmirerDaysWithoutGift;
				bool i2 = mgr.secretAdmirerInjectorGiven;
				bool b2 = mgr.secretAdmirerBlackInjectorGiven;
				Change(seat, delegate(Rec r)
				{
					r.B[BSaDays] = d;
					r.B[BSaInj] = i2 ? 1 : 0;
					r.B[BSaBlack] = b2 ? 1 : 0;
				});
				// The gift goes to the admirer's bay when there is room.
				List<GameItem> gifts = ((GameInventory)e.frontInvinvElement).childItems.Where((GameItem g) => g != null && !before.Contains(g)).ToList();
				if (gifts.Count == 0)
				{
					continue;
				}
				GameItem bay = Bays.BayOf(seat);
				GraphNodeStorage inside = bay?.contentWindow?.child;
				using (Net.Apply())
				{
					foreach (GameItem gift in gifts)
					{
						try
						{
							gift.name = Lang.Trf("{0} (for {1})", gift.name, Members.NameOf(seat));
							if (inside != null)
							{
								GameInventory from = gift.parentInventory;
								from?.Expel(gift);
								if (GraphUtils.TryAccept(inside, gift) == -1)
								{
									GraphUtils.TryAccept((GraphNodeStorage)(object)from, gift);
								}
							}
						}
						catch (Exception ex)
						{
							Log.Warn("perks: handing a secret admirer's gift to seat " + seat + " threw: " + ex.Message);
						}
					}
				}
				if (!DayLog.Replaying)
				{
					Log.Info("perks: a secret admirer left " + gifts.Count + " thing(s) for " + Members.NameOf(seat));
					Notices.Show(Lang.Trf("A secret admirer left something for {0}.", Kit.Name(Members.NameOf(seat))), "perks");
				}
			}
		}
		catch (Exception ex2)
		{
			Log.Error("perks: the secret admirer's morning threw: " + ex2);
		}
		finally
		{
			turnSeat = 0;
			mgr.secretAdmirerDaysWithoutGift = days;
			mgr.secretAdmirerInjectorGiven = inj;
			mgr.secretAdmirerBlackInjectorGiven = black;
			shown = 0;
			Show((was > 0) ? was : Actor);
		}
		return false;
	}

	/// A perk ended one player's run: the server takes that player out.
	internal static void Died(int seat, string cause)
	{
		if (seat <= 0 || deaths.Any((KeyValuePair<int, string> d) => d.Key == seat))
		{
			return;
		}
		deaths.Add(new KeyValuePair<int, string>(seat, cause));
		Log.Info("perks: " + Members.NameOf(seat) + " (seat " + seat + ") does not survive: " + cause);
	}

	internal static string CauseText(string cause)
	{
		return (cause == "aug") ? "the augs they let get away" : "Sentinel Collapse Disorder";
	}

	private static void ServeDeaths()
	{
		if (deaths.Count == 0 || !Net.IsServer)
		{
			return;
		}
		PlayerStore store = Game.Store;
		string text = store.storeState.ToString();
		if ((text != "MORNING" && text != "CLOSED") || DayNight.NightRunning || Outings.AnyOut || HostTrip.Out || store.currentClientInstance != null || Snapshot.IsLoading)
		{
			return;
		}
		KeyValuePair<int, string> keyValuePair = deaths[0];
		deaths.RemoveAt(0);
		if (Members.BySeat(keyValuePair.Key) == null)
		{
			return;
		}
		Deaths++;
		string text2 = Leaving.Death(keyValuePair.Key, keyValuePair.Value);
		Log.Info("perks: " + text2);
	}

	/// Bots only: adds these perks, ignoring points, slots and locks.
	public static void LabPick(IEnumerable<string> ids)
	{
		Net.ToServer("perks.labpick", new MsgPerksPick
		{
			ids = (ids ?? new string[0]).ToArray()
		});
	}

	private static void ServerLabPick(MsgPerksPick m, int from)
	{
		if (m == null || !Bots.BotRunner.Enabled || !Game.ShopLoaded || !Enabled)
		{
			return;
		}
		int num = Members.SeatOf(from);
		List<string> list = (m.ids ?? new string[0]).Where((string i) => KindOf(i) != 0 && Def(i) != null && !Has(num, i)).Distinct().ToList();
		if (num <= 0)
		{
			return;
		}
		GameItem val = Bays.BayOf(num);
		MsgPerksSet msgPerksSet = new MsgPerksSet
		{
			seat = num,
			ids = list.ToArray(),
			bay = (val != null) ? Ids.Of(val) : 0,
			lab = true
		};
		string text;
		using (Replay.Server(out msgPerksSet.seed))
		{
			text = Apply(num, list, val, merge: true);
		}
		Net.ToAll("perks.set", msgPerksSet);
		Log.Info("perks: lab: " + Members.NameOf(num) + " (seat " + num + ") also has [" + string.Join(", ", list) + "]: " + text);
	}

	/// Bots only: sets one clock on every copy (0 nicotine, 1 alcohol, 2 narcotic, 3 relief,
	/// 4 days since the sentinel's injection, 5 the sentinel's pool, 6 the day of the last outing).
	internal static void LabClock(int seat, int index, int value)
	{
		if (!Net.IsServer || !Bots.BotRunner.Enabled)
		{
			return;
		}
		MsgPerksLab msgPerksLab = new MsgPerksLab
		{
			seat = seat,
			i = index,
			v = value
		};
		ApplyLab(msgPerksLab);
		Net.ToAll("perks.lab", msgPerksLab);
	}

	private static void ApplyLab(MsgPerksLab m)
	{
		if (m == null || !Bots.BotRunner.Enabled || !Enabled || m.i < 0 || m.i >= BLen)
		{
			return;
		}
		Keep();
		Change(m.seat, delegate(Rec r)
		{
			r.B[m.i] = m.v;
		});
		if (shown == m.seat)
		{
			SetFields(Of(m.seat).B);
		}
	}

	/// "/perks" lists who has what; "/perks none" settles one's own pick without perks.
	internal static string ChatCommand(int from, string what)
	{
		if (!Enabled)
		{
			return "This shop does not use personal perks.";
		}
		if ((what ?? "").Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
		{
			int num = Members.SeatOf(from);
			string text = Refusal(num, new List<string>());
			if (text != null)
			{
				return text;
			}
			ServerPick(new MsgPerksPick
			{
				ids = new string[0]
			}, from);
			return "You start without perks.";
		}
		List<string> list = new List<string>();
		foreach (Member item in Members.State.list.OrderBy((Member m) => m.s))
		{
			Rec rec = Of(item.s);
			list.Add(item.n + ": " + ((!rec.Picked) ? "has not picked yet" : ((rec.Ids.Count == 0) ? "no perks" : string.Join(", ", rec.Ids.Select(NameOfPerk)))));
		}
		return string.Join("\n", list);
	}

	internal static string Describe()
	{
		return string.Join(";", Members.State.list.OrderBy((Member m) => m.s).Select((Member m) => m.n.Replace(';', ' ') + "=" + (Of(m.s).Picked ? string.Join(",", Of(m.s).Ids) : "?") + "/" + string.Join(":", Of(m.s).B)));
	}

	[SystemInit]
	internal static void Init()
	{
		Net.On<MsgPerksPick>("perks.pick", ServerPick, null);
		Net.On<MsgPerksSet>("perks.set", null, ClientSet);
		Net.On<MsgPerksLab>("perks.lab", null, ApplyLab);
		Net.On<MsgPerksPick>("perks.labpick", ServerLabPick, null);
		Net.On<MsgPerksRefused>("perks.refused", null, delegate(MsgPerksRefused m)
		{
			LastRefusal = m?.why ?? "";
			Notices.Show(Lang.Tr(m?.why ?? ""), "perks");
			PerkScreens.Refused(m?.why ?? "");
		});
		Digest.AddPart("perks", () => Enabled ? Raw : "");
		Digest.AddExplain("perks", () => Enabled ? Describe() : "");
		Snapshot.Loaded += delegate
		{
			cachedRaw = null;
			scope.Clear();
			shown = 0;
			turnSeat = 0;
			nobody = 0;
			night = false;
			nextLook = 0f;
		};
		Snapshot.BeforeMorningCapture += delegate
		{
			if (Enabled)
			{
				Keep();
			}
		};
	}

	[SystemTick]
	private static void Tick()
	{
		if (!Enabled || night || Snapshot.IsLoading || Dispatch.InRequest || scope.Count > 0)
		{
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (realtimeSinceStartup < nextLook)
		{
			return;
		}
		nextLook = realtimeSinceStartup + 1f;
		try
		{
			Table();
			Show(Actor);
			ServeDeaths();
		}
		catch (Exception ex)
		{
			if (Log.Allow("perks|tick", 30000L, out var skipped))
			{
				Log.Warn("perks: the look-over threw" + Log.More(skipped) + ": " + ex.Message);
			}
		}
	}
}

public sealed class MsgPerksPick
{
	public string[] ids;
}

public sealed class MsgPerksSet
{
	public int seat;

	public string[] ids;

	public long bay;

	/// Bots: added to what the seat has.
	public bool lab;

	/// A repick: ids is the whole new pick.
	public bool again;

	public Seed seed;
}

public sealed class MsgPerksLab
{
	public int seat;

	public int i;

	public int v;
}

public sealed class MsgPerksRefused
{
	public string why;
}
