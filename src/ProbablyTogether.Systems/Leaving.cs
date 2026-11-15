using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

public sealed class MsgLeave
{
	public int seat;

	/// True: the player leaves for good (bay and seat too). False: only their trade goes.
	public bool whole;

	/// Set (the cause): a perk killed the player; their perks go too.
	public string died;

	public Seed seed;
}

/// A trade or a whole player leaves the shop (/retrade, /remove): the trade's machines and remaining kit go,
/// anything else inside goes to the shop's storage or the till. One seeded step every copy runs the same way.
public static class Leaving
{
	/// Joe Wild's entries a trade's starting kit marks as held (NewGameData.HandleInitialItem).
	private static readonly Dictionary<string, string[]> kitEntries = new Dictionary<string, string[]>(StringComparer.Ordinal)
	{
		{ "WATER_MERCHANT", new string[1] { "WATER_TRADER" } },
		{ "SCAV", new string[1] { "RUINED_MACHINE_UNLOCK" } },
		{ "FARMER", new string[1] { "RETIRED_FARMER" } },
		{ "GUNSMITH", new string[2] { "RETIRED_GUNSMITH", "GUN_PERMIT_I" } },
		{ "DRUG_DEALER", new string[1] { "RETIRED_CHEMIST" } },
		{ "RANCHER", new string[1] { "RETIRED_RANCHER" } },
		{ "AQUAFARMER", new string[1] { "AQUACULTURE" } }
	};

	/// Kits with no item record: kind and count per trade, taken only from the player's own storage bay.
	private static readonly Dictionary<string, string> oldKits = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		{ "WATER_MERCHANT", "water_guide:1,water_test_container:1,water_tablet_container:1,large_bottled_water:2,water_jug:1,bottled_water:1,small_bottled_water:1,water_pitcher:1,sign_water:1" },
		{ "APPRENTICE", "processed_meat:2,processed_juice:1,toothpaste:2,toilet_paper:2,sign_food:1,sign_household:1" },
		{ "SCAV", "scav_token:1,ingot_book:1,metal_scanner:1,flashlight:1,satchel:1,topical_bandage_item:2,sign_material:1" },
		{ "FENCE", "smuggler_bay:1,dream_dust:2,heavy_handmade_pistol:1,heavy_pistol_ammo:2,med_keycard:1,eng_keycard:1,sec_keycard:1,black_lamp:2" },
		{ "FARMER", "hydroponic_guide:1,water_ration_small:2,nutrifruit_seed:2,nutrient_tablet_container:1,fillerweed_seed:2" },
		{ "GUNSMITH", "sign_firearm:1,printer_plastic:4,storage_bay_gun:1,small_pistol_ammo_nl:6,small_pistol_ammo:5,handmade_pistol:2,printed_gun:2,silencer:1,rds:1,mag_22:2,mag_22_high:1,laser_toy:1,silencer_makeshift:1,laser:1,internal_springs_improved:1,screwdriver:1,gun_part:1" },
		{ "DRUG_DEALER", "1ml_dropper:1,5ml_dropper:1,25ml_cylinder:1,50ml_cylinder:1,100ml_cylinder:1,bottled_water:2,coffee_bean:1,water_pitcher:1,kotton_fabric:1,chem_scanner:1,smuggler_bay_mini:1,chemistry_guide:1" },
		{ "RANCHER", "rat:6,kitchen_cleaver:1,labeler_blue:1,marking_pen:1,serum_green:1,nutrifruit:6" },
		{ "AQUAFARMER", "industrial_fish_feed:2,scrap_metal:1,common_electronic:1,kitchen_knife:1,marking_pen:1,water_jug:2" }
	};

	internal static int Done;

	internal static string LastRefusal = "";

	internal static string LastResult = "";

	[SystemInit]
	internal static void Init()
	{
		Net.On<MsgLeave>("members.leave", null, ClientLeave);
	}

	/// The starting trade first, then those taken through Joe Wild.
	public static List<string> TradesOf(int seat)
	{
		List<string> list = new List<string>();
		string text = Members.Fact(seat, "spec");
		if (!string.IsNullOrEmpty(text))
		{
			list.Add(text);
		}
		string text2 = Members.Fact(seat, Specs.MoreFact);
		if (!string.IsNullOrEmpty(text2))
		{
			foreach (string item in text2.Split(','))
			{
				if (item.Length > 0 && !list.Contains(item))
				{
					list.Add(item);
				}
			}
		}
		return list;
	}

	/// "/retrade <player>" and "/remove <player>"; returns the reply.
	internal static string ChatCommand(int from, bool whole, string who)
	{
		string text = whole ? "/remove" : "/retrade";
		if (!Bays.Enabled)
		{
			return "This shop does not use personal trades.";
		}
		if (!Game.ShopLoaded || Game.Store == null)
		{
			return "The shop is not loaded.";
		}
		if (Members.SeatOf(from) != 1)
		{
			return "Only " + Members.NameOf(1) + ", who founded the shop, can do that.";
		}
		List<Member> list = Members.State.list.Where((Member m) => m.s != 1).ToList();
		string text2 = string.Join(", ", list.Select((Member m) => m.n + ((TradesOf(m.s).Count > 0) ? (" (" + string.Join(", ", TradesOf(m.s).Select(Specs.Label)) + ")") : " (no trade)")));
		if (string.IsNullOrWhiteSpace(who))
		{
			return (whole ? "/remove <player>: the player leaves this shop for good; their trade and storage bay go, what was inside stays in the shop. " : "/retrade <player>: the player gives up their trade and its machines, keeps their storage bay, and picks a new trade. ") + ((list.Count > 0) ? ("Players: " + text2 + ".") : "Nobody else has a place in this shop.");
		}
		string name = who.Trim();
		List<Member> list2 = list.Where((Member m) => string.Equals(m.n, name, StringComparison.OrdinalIgnoreCase)).ToList();
		if (list2.Count == 0)
		{
			list2 = list.Where((Member m) => (m.n ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
		}
		if (string.Equals(Members.NameOf(1), name, StringComparison.OrdinalIgnoreCase))
		{
			return "The shop itself is " + Members.NameOf(1) + "'s: the founder's trade and place stay.";
		}
		if (list2.Count != 1)
		{
			return ((list2.Count == 0) ? ("Nobody called '" + name + "' has a place in this shop. ") : ("'" + name + "' fits several players. ")) + "Players: " + text2 + ".";
		}
		Member member = list2[0];
		string text3 = Check(member.s, whole);
		if (text3 != null)
		{
			LastRefusal = text3;
			Log.Info("leaving: " + text + " " + member.n + " refused: " + text3);
			return text3;
		}
		LastRefusal = "";
		return ServerLeave(member.s, whole);
	}

	private static string Check(int seat, bool whole)
	{
		PlayerStore store = Game.Store;
		string text = Members.NameOf(seat);
		string text2 = store.storeState.ToString();
		if (text2 != "MORNING" && text2 != "CLOSED")
		{
			return "Do that while the shop is closed: in the morning before opening, or at night.";
		}
		if (DayNight.NightRunning || Outings.AnyOut || HostTrip.Out)
		{
			return "Do that when everyone is back in the shop.";
		}
		if (store.currentClientInstance != null)
		{
			return "Do that when nobody is at the counter.";
		}
		if (whole && Members.Online(seat))
		{
			return text + " is in the shop right now. A player is removed while they are away: ask them to leave the game first.";
		}
		if (!whole && TradesOf(seat).Count == 0)
		{
			return text + " has no trade to give up.";
		}
		return null;
	}

	/// Server: a perk killed this player. Away, they leave for good; in the game, they start over in their seat.
	internal static string Death(int seat, string cause)
	{
		return ServerLeave(seat, !Members.Online(seat), cause ?? "");
	}

	private static string ServerLeave(int seat, bool whole, string died = null)
	{
		string text = Members.NameOf(seat);
		string text2 = string.Join(", ", TradesOf(seat).Select(Specs.Label));
		MsgLeave msgLeave = new MsgLeave
		{
			seat = seat,
			whole = whole,
			died = died
		};
		string text3;
		using (Replay.Server(out msgLeave.seed))
		{
			text3 = Run(seat, whole, died != null);
		}
		Net.ToAll("members.leave", msgLeave);
		// The record changes after the step: every copy needs the old record to do the same work.
		if (whole)
		{
			Members.Forget(seat);
		}
		else
		{
			Members.DropFacts(seat, "spec", "mach", Specs.MoreFact, Specs.KitFact, TradeVisits.DayFact);
		}
		Done++;
		LastResult = text3;
		Log.Info("leaving: " + text + " (seat " + seat + ") " + ((died != null) ? ("did not survive (" + died + "), and ") : "") + (whole ? "left the shop for good" : "gave up their trade") + ((text2.Length > 0) ? (" (" + text2 + ")") : "") + ": " + text3);
		if (died != null)
		{
			return text + " did not survive " + Perks.CauseText(died) + (whole ? " and is no longer part of this shop. " : " and starts over. ") + text3 + ".";
		}
		return whole ? (text + " is no longer part of this shop. " + text3 + ".") : (text + " gave up " + ((text2.Length > 0) ? text2 : "their trade") + " and can pick a new trade. " + text3 + ".");
	}

	private static void ClientLeave(MsgLeave m)
	{
		if (m == null || !Game.ShopLoaded)
		{
			return;
		}
		string text = Members.NameOf(m.seat);
		string text2 = string.Join(", ", TradesOf(m.seat).Select(Specs.Label));
		string text3;
		using (Replay.Client(m.seed ?? new Seed()))
		{
			text3 = Run(m.seat, m.whole, m.died != null);
		}
		Done++;
		LastResult = text3;
		Log.Info("leaving: " + text + " (seat " + m.seat + ") " + (m.whole ? "left the shop for good" : "gave up their trade") + ((text2.Length > 0) ? (" (" + text2 + ")") : "") + ": " + text3);
		if (!DayLog.Replaying && m.died != null)
		{
			Notices.Show(Lang.Trf(m.whole ? "{0} did not survive {1} and is no longer part of this shop." : "{0} did not survive {1}. They start over: a new trade, new perks.", Kit.Name(text), Lang.Tr(Perks.CauseText(m.died))), "perks");
		}
		else if (!DayLog.Replaying)
		{
			Notices.Show(m.whole ? Lang.Trf("{0} is no longer part of this shop.", Kit.Name(text)) : Lang.Trf("{0} gave up their trade ({1}).", Kit.Name(text), text2), "spec");
		}
	}

	private static string Run(int seat, bool whole, bool perksToo = false)
	{
		PlayerStore store = Game.Store;
		EmporiumEntry instance = EmporiumEntry.Instance;
		if (store == null || (Object)(object)instance == (Object)null || Members.BySeat(seat) == null)
		{
			return "nothing to do";
		}
		List<string> list = TradesOf(seat);
		HashSet<string> kinds = new HashSet<string>((Members.Fact(seat, "mach") ?? "").Split(',').Where((string k) => k.Length > 0), StringComparer.Ordinal);
		int moved = 0;
		int cashed = 0;
		int refund = 0;
		int num = 0;
		int kit = 0;
		List<string> list2 = new List<string>();
		HashSet<GameItem> gone = new HashSet<GameItem>();
		using (Net.Apply())
		{
			// Going: the trade's machines anywhere, the rest of its kit, and a whole player's bay.
			List<GameItem> list3 = Items.All().Where((GameItem i) => i != null && kinds.Contains(i.identifier) && Owned(i)).OrderBy((GameItem i) => Ids.Of(i)).ToList();
			List<GameItem> list4 = KitItems(seat, list, kinds);
			GameItem val = whole ? Bays.BayOf(seat) : null;
			HashSet<GameItem> doomed = new HashSet<GameItem>(list3);
			foreach (GameItem item in list4)
			{
				doomed.Add(item);
			}
			if (val != null)
			{
				doomed.Add(val);
			}
			List<GameItem> list5 = doomed.OrderBy((GameItem i) => Ids.Of(i)).ToList();
			// First, everything not going is moved out of what is going, into the shop's storage.
			foreach (GameItem item3 in list5)
			{
				Spill(instance, item3, doomed, gone, ref moved, ref cashed, ref refund);
			}
			foreach (GameItem item4 in list5)
			{
				if (gone.Contains(item4))
				{
					continue;
				}
				Remove(item4, gone);
			}
			num = list3.Count;
			kit = list4.Count((GameItem i) => !list3.Contains(i));
			foreach (string item2 in list)
			{
				try
				{
					string text = Undo(store, item2);
					if (text.Length > 0)
					{
						list2.Add(text);
					}
				}
				catch (Exception ex)
				{
					Log.Error("leaving: taking " + item2 + " out of the shop threw part way: " + ex);
				}
			}
			bool flag = val != null;
			if (whole)
			{
				Wounds.Drop(seat);
				Standing.Drop(seat);
			}
			if (whole || perksToo)
			{
				Perks.Forget(seat);
			}
			if (refund > 0)
			{
				store.playerCash += refund;
			}
			return num + " machine(s) removed, " + kit + " other thing(s) of the kit" + (flag ? ", the storage bay too" : "") + "; " + moved + " thing(s) from inside went to the shop's storage" + ((cashed > 0) ? (", " + cashed + " found no room and went into the till as " + refund) : "") + ((list2.Count > 0) ? ("; " + string.Join("; ", list2)) : "");
		}
	}

	/// What is left of these trades' kits, machines aside (those go by kind).
	private static List<GameItem> KitItems(int seat, List<string> trades, HashSet<string> machineKinds)
	{
		List<GameItem> list = new List<GameItem>();
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (string item in (Members.Fact(seat, Specs.KitFact) ?? "").Split(';'))
		{
			int num = item.IndexOf(':');
			if (num > 0)
			{
				dictionary[item.Substring(0, num)] = item.Substring(num + 1);
			}
		}
		GameItem val = Bays.BayOf(seat);
		List<GameItem> list2 = null;
		foreach (string trade in trades)
		{
			if (dictionary.TryGetValue(trade, out var value))
			{
				// A recorded kit: exactly its items, wherever they are now.
				foreach (string item2 in value.Split(','))
				{
					if (long.TryParse(item2, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
					{
						GameItem val2 = Ids.Find(result);
						if (val2 != null && Owned(val2) && Bays.TagOwner(val2) == 0 && !list.Contains(val2))
						{
							list.Add(val2);
						}
					}
				}
				continue;
			}
			if (val == null || !oldKits.TryGetValue(trade, out var value2))
			{
				continue;
			}
			if (list2 == null)
			{
				list2 = GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).Where((GameItem i) => i != null && Owned(i)).OrderBy((GameItem i) => Ids.Of(i)).ToList();
			}
			foreach (string item3 in value2.Split(','))
			{
				int num2 = item3.IndexOf(':');
				string kind = item3.Substring(0, num2);
				int num3 = int.Parse(item3.Substring(num2 + 1), CultureInfo.InvariantCulture);
				foreach (GameItem item4 in list2)
				{
					if (num3 <= 0)
					{
						break;
					}
					if (item4.identifier == kind && !machineKinds.Contains(kind) && !list.Contains(item4))
					{
						list.Add(item4);
						num3--;
					}
				}
			}
		}
		return list;
	}

	private static bool Owned(GameItem item)
	{
		try
		{
			return GeneralHelper.IsItemOwned(item);
		}
		catch
		{
			return false;
		}
	}

	/// Moves direct contents to the shop's storage; with no room, their worth goes to the till.
	private static void Spill(EmporiumEntry e, GameItem container, HashSet<GameItem> doomed, HashSet<GameItem> gone, ref int moved, ref int cashed, ref int refund)
	{
		List<GameItem> list;
		try
		{
			list = GraphUtils.FindChildrenType<GameItem>((GraphNodeStorage)(object)container, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).OrderBy((GameItem i) => Ids.Of(i)).ToList();
		}
		catch (Exception ex)
		{
			Log.Warn("leaving: could not look inside " + container.identifier + ": " + ex.Message);
			return;
		}
		GraphNodeStorage[] array = (GraphNodeStorage[])(object)new GraphNodeStorage[2]
		{
			(GraphNodeStorage)e.invElement,
			(GraphNodeStorage)e.backInvinvElement
		};
		foreach (GameItem item in list)
		{
			if (item == null || gone.Contains(item) || doomed.Contains(item))
			{
				continue;
			}
			try
			{
				GameInventory parentInventory = item.parentInventory;
				if (parentInventory != null)
				{
					parentInventory.Expel(item);
				}
				bool flag = false;
				foreach (GraphNodeStorage val in array)
				{
					if (val != null && GraphUtils.TryAccept(val, item) != -1)
					{
						flag = true;
						break;
					}
				}
				if (flag)
				{
					moved++;
					continue;
				}
				long num = Math.Max(0L, (long)item.unitValue) * Math.Max(1, item.unitCount);
				foreach (GameItem item2 in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)item, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
				{
					num += Math.Max(0L, (long)item2.unitValue) * Math.Max(1, item2.unitCount);
					gone.Add(item2);
				}
				refund += (int)Math.Min(100000L, num);
				cashed++;
				gone.Add(item);
				item.Destroy();
			}
			catch (Exception ex2)
			{
				Log.Warn("leaving: moving " + item.identifier + " out of " + container.identifier + " threw: " + ex2.Message);
			}
		}
	}

	private static void Remove(GameItem item, HashSet<GameItem> gone)
	{
		try
		{
			foreach (GameItem item2 in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)item, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				gone.Add(item2);
			}
			gone.Add(item);
			item.Destroy();
		}
		catch (Exception ex)
		{
			Log.Warn("leaving: removing " + item.identifier + " threw: " + ex.Message);
		}
	}

	/// Undoes what the trade set up in the game; returns what was done, in words.
	private static string Undo(PlayerStore store, string trade)
	{
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		if (kitEntries.TryGetValue(trade, out var value))
		{
			list2.AddRange(value);
		}
		foreach (string item in Specs.WildEntriesOf(trade))
		{
			if (!list2.Contains(item))
			{
				list2.Add(item);
			}
		}
		int num = 0;
		if (store.networkUpgrade != null)
		{
			foreach (string item2 in list2)
			{
				if (store.networkUpgrade.TryGetValue(item2, out var value2) && value2 != null && value2.state == 2)
				{
					// 1 = on Joe Wild's list, not bought.
					value2.state = 1;
					num++;
				}
			}
		}
		if (num > 0)
		{
			list.Add(num + " entr" + ((num == 1) ? "y" : "ies") + " of Joe Wild's list free again");
		}
		if (trade == "DRUG_DEALER")
		{
			StoreService cartelProtectionService = StoreService.GetCartelProtectionService();
			if (cartelProtectionService != null && (cartelProtectionService.unlocked || cartelProtectionService.isUsing))
			{
				cartelProtectionService.unlocked = false;
				cartelProtectionService.isUsing = false;
				list.Add("the cartel's protection service is gone");
			}
			if (store.startType != NewGameData.StartType.DRUG_DEALER)
			{
				if (store.storeReputations != null && store.storeReputations.Any((StoreReputation r) => r != null && r.factionId == "FACTION_CARTEL"))
				{
					StoreReputation.RemoveCartelFaction();
					list.Add("the shop has no standing with the cartel any more");
				}
				// The game's cartel exit flag would shut the cartel out for good; clear it so the next chemist starts with it.
				store.isCartelExited = false;
				store.cartelRequiredCount = 10;
				store.cartelRequiredPurity = 40;
				store.cartelExitStage = 0;
			}
		}
		else if (trade == "GUNSMITH" && store.startType != NewGameData.StartType.GUNSMITH && store.ActivePermits != null)
		{
			int num2 = 0;
			// A gun permit covers firearms and ammunition (PreBuiltItemHelper.GunPermit1..3).
			string[] array = new string[2] { "FIREARM", "AMMUNITION" };
			for (int num3 = 3; num3 >= 1; num3--)
			{
				int level = num3;
				foreach (string kind in array)
				{
					if (store.ActivePermits.Exists(((string, int) p) => p.Item1 == kind && p.Item2 == level))
					{
						PermitHelper.OnPermitRevoked(level, kind);
						num2++;
					}
				}
			}
			if (num2 > 0)
			{
				list.Add(num2 + " firearm and ammunition permit(s) revoked");
			}
		}
		return string.Join(", ", list);
	}
}
