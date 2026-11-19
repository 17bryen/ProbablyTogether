using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ProbablyTogether.UI;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

public sealed class MsgSpecPick
{
	public string id;
}

public sealed class MsgSpecKit
{
	public int seat;

	public string id;

	public long bay;

	public Seed seed;
}

public sealed class MsgSpecStock
{
	public int seat;
}

public sealed class MsgSpecRefused
{
	public string why;
}

/// Each player picks one starting scenario: its kit goes to their bay and its machine kinds become theirs alone.
public static class Specs
{
	/// True while the new-game routine runs only to produce a kit; the guards below skip everything else.
	internal static bool KitOnly;

	private static readonly Dictionary<string, string[]> extraTools = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
	{
		{ "WATER_MERCHANT", new string[3] { "bottle_printer", "portable_water_purifier", "water_filter" } },
		{ "RANCHER", new string[1] { "animal_cage" } },
		// Not in any kit or on Joe Wild's list: the Fence's only while somebody is the Fence.
		{ "FENCE", new string[1] { "desequencer" } }
	};

	private static MembersState typesFor;

	private static readonly Dictionary<string, int> typeOwner = new Dictionary<string, int>(StringComparer.Ordinal);

	private static float nextServerLook;

	private static float nextPrompt;

	private static CustomUIWindow window;

	private const string WindowId = "bellun_probably_together.spec";

	private static float holdUntil;

	internal static int Reoffers;

	/// Asks the game, since the cached window reference goes stale when the shop is left or reloaded.
	private static bool WindowUp
	{
		get
		{
			try
			{
				CustomUIManager instance = CustomUIManager.Instance;
				return (Object)(object)instance != (Object)null && instance.IsOpen(WindowId);
			}
			catch
			{
				return false;
			}
		}
	}

	internal static bool WindowShown => WindowUp;

	internal static int OfferedCount => offered.Count;

	internal static bool ChooseOffered(int index)
	{
		if (index < 0 || index >= offered.Count)
		{
			return false;
		}
		Pick(offered[index].Key);
		return true;
	}

	private static void AskAgainSoon()
	{
		window = null;
		holdUntil = 0f;
		nextPrompt = 0f;
	}

	private static List<KeyValuePair<string, string>> offered = new List<KeyValuePair<string, string>>();

	private static int pickIndex;

	internal static int Kits;

	internal static string LastRefusal = "";

	public static string Of(int seat)
	{
		return Members.Fact(seat, "spec");
	}

	/// The seat holding this trade (0: nobody).
	public static int TakenBy(string id)
	{
		foreach (Member item in Members.State.list)
		{
			if (item.x.TryGetValue("spec", out var value) && string.Equals(value, id, StringComparison.OrdinalIgnoreCase))
			{
				return item.s;
			}
			if (item.x.TryGetValue(MoreFact, out var value2) && value2.Split(',').Any((string t) => string.Equals(t, id, StringComparison.OrdinalIgnoreCase)))
			{
				return item.s;
			}
		}
		return 0;
	}

	/// Member fact: trades taken through Joe Wild, comma separated.
	internal const string MoreFact = "more";

	/// Member fact: the items each kit put into the shop, "TRADE:id,id;TRADE:id".
	internal const string KitFact = "kit";

	internal static string LastKitIds = "";

	internal static string KitFactWith(int seat, string trade, string ids)
	{
		string text = Members.Fact(seat, KitFact);
		string text2 = trade + ":" + (ids ?? "");
		return string.IsNullOrEmpty(text) ? text2 : (text + ";" + text2);
	}

	public static bool Holds(int seat, string id)
	{
		return seat > 0 && TakenBy(id) == seat;
	}

	private static readonly Dictionary<string, string> wildTrades = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		{ "RETIRED_FARMER", "FARMER" },
		{ "PLANT_DNA", "FARMER" },
		{ "RETIRED_GUNSMITH", "GUNSMITH" },
		{ "GUN_PERMIT_I", "GUNSMITH" },
		{ "GUN_PERMIT_II", "GUNSMITH" },
		{ "GUN_PERMIT_III", "GUNSMITH" },
		{ "RETIRED_CHEMIST", "DRUG_DEALER" },
		{ "RETIRED_RANCHER", "RANCHER" },
		{ "WATER_TRADER", "WATER_MERCHANT" },
		{ "AQUACULTURE", "AQUAFARMER" },
		{ "AQUACULTURE_II", "AQUAFARMER" }
	};

	/// The machines of each trade, used when a trade is taken through Joe Wild (no kit runs then).
	private static readonly Dictionary<string, string[]> tradeMachines = new Dictionary<string, string[]>(StringComparer.Ordinal)
	{
		{ "WATER_MERCHANT", new string[3] { "bottle_printer", "portable_water_purifier", "water_filter" } },
		{ "FENCE", new string[1] { "desequencer" } },
		{ "FARMER", new string[1] { "hydroponic" } },
		{ "GUNSMITH", new string[1] { "printer" } },
		{ "DRUG_DEALER", new string[4] { "blender", "heating_plate", "evaporator", "chem_finisher" } },
		{ "RANCHER", new string[1] { "animal_cage" } },
		{ "AQUAFARMER", new string[1] { "ras" } }
	};

	internal static List<string> WildEntriesOf(string trade)
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, string> wildTrade in wildTrades)
		{
			if (wildTrade.Value == trade)
			{
				list.Add(wildTrade.Key);
			}
		}
		return list;
	}

	internal static int WildTaken;

	internal static string LastNoRoom = "";

	/// The trade an entry belongs to (null: the shop's).
	public static string WildTrade(string upgradeId)
	{
		string value;
		return (upgradeId != null && wildTrades.TryGetValue(upgradeId, out value)) ? value : null;
	}

	/// Server: null when this player may buy that entry, else the refusal.
	internal static string WildRefusal(int player, string upgradeId)
	{
		string text = WildTrade(upgradeId);
		if (text == null || !Bays.Enabled || player <= 0)
		{
			return null;
		}
		int num = Members.SeatOf(player);
		if (num <= 0)
		{
			return "Guests cannot take a trade: join with your own account.";
		}
		int num2 = TakenBy(text);
		if (num2 != 0 && num2 != num)
		{
			return Members.NameOf(num2) + " is the shop's " + TradeVisits.Word(text) + ". Only they can buy this.";
		}
		if (num2 == 0 && (HostTrip.Out || Outings.AnyOut))
		{
			return "Take up a new trade when everyone is back in the shop.";
		}
		return null;
	}

	/// Server: the buyer takes the trade if nobody holds it.
	internal static void WildBought(int player, string upgradeId)
	{
		string text = WildTrade(upgradeId);
		if (text == null || !Bays.Enabled || !Net.IsServer || player <= 0)
		{
			return;
		}
		int num = Members.SeatOf(player);
		if (num <= 0 || TakenBy(text) != 0)
		{
			return;
		}
		Member member = Members.BySeat(num);
		if (member == null)
		{
			return;
		}
		member.x.TryGetValue(MoreFact, out var value);
		member.x.TryGetValue("mach", out var value2);
		List<string> list = new List<string>((value2 ?? "").Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries));
		string text2 = "no storage bay: no kit";
		string kitFact = null;
		GameItem val = Bays.BayOf(num);
		if (val != null)
		{
			MsgSpecKit msgSpecKit = new MsgSpecKit
			{
				seat = num,
				id = text,
				bay = Ids.Of(val)
			};
			string machines;
			using (Replay.Server(out msgSpecKit.seed))
			{
				text2 = RunKit(num, text, val, out machines);
			}
			Kits++;
			kitFact = KitFactWith(num, text, LastKitIds);
			Net.ToAll("spec.kit", msgSpecKit);
			foreach (string item3 in (machines ?? "").Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries))
			{
				if (!list.Contains(item3))
				{
					list.Add(item3);
				}
			}
		}
		if (tradeMachines.TryGetValue(text, out var value3))
		{
			foreach (string item in value3)
			{
				if (!list.Contains(item))
				{
					list.Add(item);
				}
			}
		}
		if (extraTools.TryGetValue(text, out var value4))
		{
			foreach (string item2 in value4)
			{
				if (!list.Contains(item2))
				{
					list.Add(item2);
				}
			}
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>
		{
			{ MoreFact, string.IsNullOrEmpty(value) ? text : (value + "," + text) },
			{ "mach", string.Join(",", list) }
		};
		if (kitFact != null)
		{
			dictionary[KitFact] = kitFact;
		}
		Members.SetFacts(num, dictionary);
		// A trade taken through Joe Wild brings no scripted visitors.
		WildTaken++;
		Log.Info("specs: " + Members.NameOf(num) + " (seat " + num + ") bought " + upgradeId + " from Joe Wild and is now the shop's " + TradeVisits.Word(text) + "; kit " + text2 + "; machine kinds: " + string.Join(",", list));
	}

	public static string WildNote(string upgradeId)
	{
		string text = WildTrade(upgradeId);
		if (text == null || !Net.IsOnline || !Bays.Enabled || !Game.ShopLoaded)
		{
			return "";
		}
		int num = TakenBy(text);
		if (num == 0)
		{
			return Lang.Trf("Whoever buys this becomes the shop's {0}: the trade's kit goes to their storage bay and its machines will be theirs alone.", Lang.Tr(TradeVisits.Word(text)));
		}
		if (num == Members.LocalSeat)
		{
			return Lang.Trf("You are the shop's {0}: only you can buy this.", Lang.Tr(TradeVisits.Word(text)));
		}
		return Lang.Trf("{0} is the shop's {1}: only they can buy this.", Members.NameOf(num), Lang.Tr(TradeVisits.Word(text)));
	}

	/// The seat that owns every machine of this kind (0: nobody).
	public static int OwnerOfType(string identifier)
	{
		if (string.IsNullOrEmpty(identifier))
		{
			return 0;
		}
		MembersState state = Members.State;
		if (state != typesFor)
		{
			typesFor = state;
			typeOwner.Clear();
			foreach (Member item in state.list)
			{
				if (!item.x.TryGetValue("mach", out var value) || string.IsNullOrEmpty(value))
				{
					continue;
				}
				string[] array = value.Split(',');
				foreach (string text in array)
				{
					if (text.Length > 0 && !typeOwner.ContainsKey(text))
					{
						typeOwner[text] = item.s;
					}
				}
			}
		}
		if (!typeOwner.TryGetValue(identifier, out var value2))
		{
			return 0;
		}
		return value2;
	}

	public static List<KeyValuePair<string, string>> Choices()
	{
		List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
		foreach (KeyValuePair<string, string> item in ServerMode.PlayableScenarioList())
		{
			if (!string.Equals(item.Key, "ROCK_BOTTOM", StringComparison.OrdinalIgnoreCase) && TakenBy(item.Key) == 0)
			{
				list.Add(item);
			}
		}
		return list;
	}

	public static string Label(string id)
	{
		foreach (KeyValuePair<string, string> item in ServerMode.PlayableScenarioList())
		{
			if (string.Equals(item.Key, id, StringComparison.OrdinalIgnoreCase))
			{
				return item.Value;
			}
		}
		return id ?? "";
	}

	[SystemInit]
	internal static void Init()
	{
		Net.On<MsgSpecPick>("spec.pick", ServerPick, null);
		Net.On<MsgSpecKit>("spec.kit", null, ClientKit);
		Net.On<MsgSpecStock>("spec.stock", ServerStock, null);
		Snapshot.Loaded += AskAgainSoon;
		Net.Disconnected += delegate
		{
			AskAgainSoon();
		};
		Net.On<MsgSpecRefused>("spec.refused", null, delegate(MsgSpecRefused m)
		{
			LastRefusal = m?.why ?? "";
			Notices.Show(Lang.Tr(LastRefusal), "spec");
			PerkScreens.Refused(LastRefusal);
		});
		ItemsUse.RegisterValidator("owned", (UseContext ctx) => (ctx == null || ctx.Player <= 0 || !ctx.OnServer) ? null : Bays.Refusal(ctx.Player, (GraphNodeStorage)(object)ctx.Source, (GraphNodeStorage)(object)ctx.Target));
	}

	public static void Pick(string id)
	{
		if (Net.IsOnline && !string.IsNullOrEmpty(id))
		{
			Net.ToServer("spec.pick", new MsgSpecPick
			{
				id = id
			});
		}
	}

	internal static bool Machine(GameItem item)
	{
		try
		{
			if (item == null)
			{
				return false;
			}
			return item.IsGameItemType("MACHINE") || item.IsTag("STANDARD_MACHINE_TAG");
		}
		catch
		{
			return false;
		}
	}

	/// The shop's standings before a kit is made; the kit's standing changes are credited to its player.
	private static Dictionary<StoreReputation, double> RepBefore(PlayerStore store)
	{
		Dictionary<StoreReputation, double> dictionary = new Dictionary<StoreReputation, double>();
		if (store?.storeReputations != null)
		{
			foreach (StoreReputation storeReputation in store.storeReputations)
			{
				if (storeReputation != null)
				{
					dictionary[storeReputation] = storeReputation.GetReputationExact();
				}
			}
		}
		return dictionary;
	}

	private static void RepCredit(int seat, Dictionary<StoreReputation, double> before)
	{
		foreach (KeyValuePair<StoreReputation, double> item in before)
		{
			double num = item.Key.GetReputationExact() - item.Value;
			if (Math.Abs(num) > 0.0001)
			{
				Standing.Credit(seat, item.Key.factionId, num);
			}
		}
	}

	/// Puts the standings back after kits made only for probing.
	private static void RepBack(Dictionary<StoreReputation, double> before)
	{
		foreach (KeyValuePair<StoreReputation, double> item in before)
		{
			try
			{
				if (item.Key.GetReputationExact() != item.Value)
				{
					item.Key.SetReputation(item.Value);
				}
			}
			catch (Exception ex)
			{
				Log.Warn("specs: putting back the standing with " + item.Key.factionId + " threw: " + ex.Message);
			}
		}
	}

	/// Runs the game's starting-kit code for one scenario into a storage bay.
	/// Must run inside a Replay scope so every copy makes the same items with the same ids.
	private static string RunKit(int seat, string id, GameItem bay, out string machines)
	{
		machines = "";
		PlayerStore store = Game.Store;
		EmporiumEntry instance = EmporiumEntry.Instance;
		NewGameData instance2 = NewGameData.Instance;
		if (store == null || (Object)(object)instance == (Object)null || (Object)(object)instance2 == (Object)null)
		{
			return "the shop is not loaded";
		}
		if (!Enum.TryParse<NewGameData.StartType>(id ?? "", ignoreCase: true, out NewGameData.StartType result) || (int)result == 0)
		{
			return "no scenario '" + id + "'";
		}
		GameGridInventory invElement = instance.invElement;
		GameGridInventory val = new GameGridInventory(60, 60);
		NewGameData.StartType startType = instance2.startType;
		int playerCash = store.playerCash;
		int startingRent = store.startingRent;
		int rentValue = store.rentValue;
		Dictionary<StoreReputation, double> repBefore = RepBefore(store);
		HashSet<GameItem> hashSet = new HashSet<GameItem>(((GameInventory)instance.showcaseElement).childItems);
		GameItem dossier = instance.dossier;
		HashSet<GameItem> hashSet2 = ((dossier != null) ? new HashSet<GameItem>(GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)dossier, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null)) : new HashSet<GameItem>());
		List<GameItem> list = new List<GameItem>();
		using (Net.Apply())
		{
			try
			{
				KitOnly = true;
				instance.invElement = val;
				instance2.startType = result;
				NewGameData.HandleInitialItem();
			}
			catch (Exception ex)
			{
				Log.Error("specs: the game's kit code for " + id + " threw part way: " + ex);
			}
			finally
			{
				KitOnly = false;
				instance.invElement = invElement;
				instance2.startType = startType;
				store.playerCash = playerCash;
				store.startingRent = startingRent;
				store.rentValue = rentValue;
				RepCredit(seat, repBefore);
			}
			list.AddRange(((GameInventory)val).childItems);
			foreach (GameItem childItem in ((GameInventory)instance.showcaseElement).childItems)
			{
				if (!hashSet.Contains(childItem))
				{
					list.Add(childItem);
				}
			}
			if (dossier != null)
			{
				foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)dossier, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
				{
					if (!hashSet2.Contains(item))
					{
						item.Destroy();
					}
				}
			}
			Bays.TakeAnything(bay);
			PixelWindow obj = ((bay != null) ? bay.contentWindow : null);
			GraphNodeStorage val2 = ((obj != null) ? obj.child : null);
			GraphNodeStorage[] array = (GraphNodeStorage[])(object)new GraphNodeStorage[4]
			{
				(GraphNodeStorage)instance.invElement,
				(GraphNodeStorage)instance.showcaseElement,
				(GraphNodeStorage)instance.hiddenElement,
				(GraphNodeStorage)instance.backInvinvElement
			};
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int refund = 0;
			List<string> noRoom = new List<string>();
			List<string> list2 = new List<string>();
			List<string> kept = new List<string>();
			foreach (GameItem item2 in list)
			{
				if (Machine(item2) && !list2.Contains(item2.identifier))
				{
					list2.Add(item2.identifier);
				}
				if ((object)item2.parentInventory != val)
				{
					continue;
				}
				((GameInventory)val).Expel(item2);
				// A kit's own storage bay cannot go inside a bay: it joins the shop's storage.
				if (val2 != null && GraphUtils.TryAccept(val2, item2) != -1)
				{
					num++;
					kept.Add(Ids.Of(item2).ToString(CultureInfo.InvariantCulture));
					continue;
				}
				bool flag = false;
				foreach (GraphNodeStorage val3 in array)
				{
					if (val3 != null && GraphUtils.TryAccept(val3, item2) != -1)
					{
						flag = true;
						break;
					}
				}
				if (flag)
				{
					num2++;
					kept.Add(Ids.Of(item2).ToString(CultureInfo.InvariantCulture));
					continue;
				}
				// Nothing fits anywhere: turn the item into its worth in cash (replayed, so the same on every copy).
				num3++;
				long num4 = 0L;
				try
				{
					num4 = Math.Max(0L, (long)item2.unitValue) * Math.Max(1, item2.unitCount);
					foreach (GameItem item3 in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)item2, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
					{
						num4 += Math.Max(0L, (long)item3.unitValue) * Math.Max(1, item3.unitCount);
					}
				}
				catch
				{
				}
				refund += (int)Math.Min(100000L, num4);
				noRoom.Add(item2.identifier);
				item2.Destroy();
			}
			if (refund > 0)
			{
				store.playerCash += refund;
			}
			LastNoRoom = (num3 > 0) ? (num3 + " (" + string.Join(", ", noRoom) + ") for " + refund) : "";
			if (num3 > 0 && Net.HasLocalPlayer && !DayLog.Replaying && !Outings.Private)
			{
				Notices.Show(Lang.Trf("{0} thing(s) of the kit found no room in the shop ({1}). Their worth, {2}, went into the till.", num3.ToString(CultureInfo.InvariantCulture), string.Join(", ", noRoom), refund.ToString(CultureInfo.InvariantCulture)), "spec");
			}
			if (extraTools.TryGetValue(id, out var value))
			{
				foreach (string text in value)
				{
					if (!list2.Contains(text))
					{
						list2.Add(text);
					}
				}
			}
			machines = string.Join(",", list2);
			foreach (string item4 in new List<string>(kept))
			{
				GameItem val4 = Ids.Find(long.Parse(item4, CultureInfo.InvariantCulture));
				if (val4 == null)
				{
					continue;
				}
				foreach (GameItem item5 in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)val4, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
				{
					string text2 = Ids.Of(item5).ToString(CultureInfo.InvariantCulture);
					if (item5 != null && Ids.Of(item5) > 0 && !kept.Contains(text2))
					{
						kept.Add(text2);
					}
				}
			}
			LastKitIds = string.Join(",", kept);
			return list.Count + " item(s): " + num + " into the bay, " + num2 + " into the shop's storage" + ((num3 > 0) ? (", " + num3 + " found no room anywhere and went into the till as " + refund) : "") + "; machine kinds: " + ((machines.Length > 0) ? machines : "none");
		}
	}

	/// Makes and destroys every scenario's kit on this copy only; for a server nobody is connected to.
	internal static List<string> ProbeKits()
	{
		List<string> list = new List<string>();
		PlayerStore store = Game.Store;
		EmporiumEntry instance = EmporiumEntry.Instance;
		NewGameData instance2 = NewGameData.Instance;
		if (store == null || (Object)(object)instance == (Object)null || (Object)(object)instance2 == (Object)null)
		{
			return list;
		}
		foreach (KeyValuePair<string, string> item in ServerMode.PlayableScenarioList())
		{
			if (!Enum.TryParse<NewGameData.StartType>(item.Key, ignoreCase: true, out NewGameData.StartType result) || (int)result == 0)
			{
				continue;
			}
			GameGridInventory invElement = instance.invElement;
			GameGridInventory val = new GameGridInventory(60, 60);
			NewGameData.StartType startType = instance2.startType;
			int playerCash = store.playerCash;
			int startingRent = store.startingRent;
			int rentValue = store.rentValue;
			Dictionary<StoreReputation, double> repBefore = RepBefore(store);
			HashSet<GameItem> hashSet = new HashSet<GameItem>(((GameInventory)instance.showcaseElement).childItems);
			using (Net.Apply())
			{
				try
				{
					KitOnly = true;
					instance.invElement = val;
					instance2.startType = result;
					NewGameData.HandleInitialItem();
				}
				catch (Exception ex)
				{
					list.Add(item.Key + ": threw " + ex.Message);
				}
				finally
				{
					KitOnly = false;
					instance.invElement = invElement;
					instance2.startType = startType;
					store.playerCash = playerCash;
					store.startingRent = startingRent;
					store.rentValue = rentValue;
					RepBack(repBefore);
				}
				List<GameItem> list2 = new List<GameItem>(((GameInventory)val).childItems);
				foreach (GameItem childItem in ((GameInventory)instance.showcaseElement).childItems)
				{
					if (!hashSet.Contains(childItem))
					{
						list2.Add(childItem);
					}
				}
				List<string> list3 = new List<string>();
				List<string> list4 = new List<string>();
				foreach (GameItem item2 in list2)
				{
					string text = item2.identifier + ((item2.contentWindow != null) ? ((item2.contentWindow.style == WindowStyle.Machinery) ? "[panel]" : "[box]") : "");
					if (Machine(item2))
					{
						if (!list3.Contains(item2.identifier))
						{
							list3.Add(item2.identifier);
						}
					}
					else if (!list4.Contains(text))
					{
						list4.Add(text);
					}
					item2.Destroy();
				}
				if (extraTools.TryGetValue(item.Key, out var value))
				{
					foreach (string text2 in value)
					{
						if (!list3.Contains(text2))
						{
							list3.Add(text2 + "*");
						}
					}
				}
				list.Add(item.Key + " (" + item.Value + "): LOCKED " + ((list3.Count > 0) ? string.Join(", ", list3) : "nothing") + " | open to all: " + string.Join(", ", list4));
			}
		}
		return list;
	}

	private static void ClientKit(MsgSpecKit m)
	{
		if (m == null || !Game.ShopLoaded)
		{
			return;
		}
		string text;
		using (Replay.Client(m.seed))
		{
			text = RunKit(m.seat, m.id, Ids.Find(m.bay), out var _);
		}
		Kits++;
		Log.Info("specs: " + Members.NameOf(m.seat) + " is the shop's " + m.id + "; kit " + text);
		if (!DayLog.Replaying)
		{
			Notices.Show(Lang.Trf("{0} is now the shop's {1}.", Kit.Name(Members.NameOf(m.seat)), Label(m.id)), "spec");
		}
		PerkScreens.TradePicked(m.seat);
		if (m.seat == Members.LocalSeat)
		{
			CloseWindow();
		}
		else if (WindowUp)
		{
			// Somebody else picked: reopen the window without the trade they took.
			CloseWindow();
			AskAgainSoon();
			Reoffers++;
		}
	}

	internal static void RefuseTo(int player, string why)
	{
		Refuse(player, why);
	}

	internal static void PickFor(int player, string id)
	{
		ServerPick(new MsgSpecPick
		{
			id = id
		}, player);
	}

	private static void Refuse(int player, string why)
	{
		LastRefusal = why;
		Log.Info("specs: refused " + Net.PlayerName(player) + ": " + why);
		Net.ToPlayer(player, "spec.refused", new MsgSpecRefused
		{
			why = why
		});
	}

	private static void ServerPick(MsgSpecPick m, int from)
	{
		int num = Members.SeatOf(from);
		if (m == null || !Game.ShopLoaded)
		{
			return;
		}
		if (!Bays.Enabled)
		{
			Refuse(from, "This shop does not use personal trades.");
			return;
		}
		if (num <= 0)
		{
			Refuse(from, "Guests have no specialization: join with your own account.");
			return;
		}
		if (Of(num) != null)
		{
			Refuse(from, "You already have a specialization in this shop.");
			return;
		}
		string id = (m.id ?? "").Trim().ToUpperInvariant();
		if (!ServerMode.PlayableScenarioList().Any((KeyValuePair<string, string> s) => string.Equals(s.Key, id, StringComparison.OrdinalIgnoreCase)) || id == "ROCK_BOTTOM")
		{
			Refuse(from, "That is not a specialization this shop offers.");
			return;
		}
		int num2 = TakenBy(id);
		if (num2 != 0)
		{
			Refuse(from, Members.NameOf(num2) + " already has that specialization.");
			return;
		}
		GameItem val = Bays.BayOf(num);
		if (val == null)
		{
			Refuse(from, "Your storage bay is not here yet. Try again in a moment.");
			return;
		}
		if (HostTrip.Out || Outings.IsOut(from))
		{
			Refuse(from, "Pick it when everyone concerned is back in the shop.");
			return;
		}
		MsgSpecKit msgSpecKit = new MsgSpecKit
		{
			seat = num,
			id = id,
			bay = Ids.Of(val)
		};
		string machines;
		string text;
		using (Replay.Server(out msgSpecKit.seed))
		{
			text = RunKit(num, id, val, out machines);
		}
		Kits++;
		Net.ToAll("spec.kit", msgSpecKit);
		Members.SetFacts(num, new Dictionary<string, string>
		{
			{ "spec", id },
			{ "mach", machines },
			{ KitFact, KitFactWith(num, id, LastKitIds) },
			{ TradeVisits.DayFact, TradeVisits.DayForPick() }
		});
		Log.Info("specs: " + Members.NameOf(num) + " (seat " + num + ") is the shop's " + id + "; kit " + text);
	}

	/// Chat: "/trade" lists what can still be picked, "/trade <name>" picks it.
	internal static string ChatCommand(int from, string what)
	{
		int num = Members.SeatOf(from);
		if (!Bays.Enabled)
		{
			return "This shop does not use personal trades.";
		}
		if (num > 0 && Of(num) != null)
		{
			return "Your trade in this shop is " + Label(Of(num)) + ". Only " + Members.NameOf(1) + " can let you choose another (/retrade).";
		}
		List<KeyValuePair<string, string>> list = Choices();
		if (string.IsNullOrEmpty(what))
		{
			return "Trades still free: " + string.Join(", ", list.Select((KeyValuePair<string, string> c) => c.Value)) + ". Type /trade and the name to pick one (it cannot be changed later).";
		}
		string text = what.Trim();
		List<KeyValuePair<string, string>> list2 = ServerMode.PlayableScenarioList().Where((KeyValuePair<string, string> c) => string.Equals(c.Key, text.Replace(' ', '_'), StringComparison.OrdinalIgnoreCase) || string.Equals(c.Value, text, StringComparison.OrdinalIgnoreCase)).ToList();
		if (list2.Count == 0)
		{
			list2 = ServerMode.PlayableScenarioList().Where((KeyValuePair<string, string> c) => c.Value.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 || c.Key.IndexOf(text.Replace(' ', '_'), StringComparison.OrdinalIgnoreCase) >= 0).ToList();
		}
		if (list2.Count != 1)
		{
			return ((list2.Count == 0) ? ("No trade is called '" + text + "'. ") : ("'" + text + "' fits several trades. ")) + "Free ones: " + string.Join(", ", list.Select((KeyValuePair<string, string> c) => c.Value)) + ".";
		}
		LastRefusal = "";
		ServerPick(new MsgSpecPick
		{
			id = list2[0].Key
		}, from);
		if (num > 0 && Of(num) != null)
		{
			return "You are now the shop's " + Label(Of(num)) + ". Your kit is in your storage bay.";
		}
		return (LastRefusal.Length > 0) ? LastRefusal : "That did not work. Try again in a moment.";
	}

	/// Seat 1 gets the shop's scenario: on a new shop, the kit the game put in storage moves into seat 1's bay.
	private static void ServerSeatOne()
	{
		Member member = Members.BySeat(1);
		PlayerStore store = Game.Store;
		if (member == null || store == null || member.x.ContainsKey("spec") || (int)store.startType == 0)
		{
			return;
		}
		string text = store.startType.ToString();
		if (Game.Day != 1 || member.d != 1)
		{
			Members.SetFacts(1, new Dictionary<string, string>
			{
				{ "spec", text },
				{ "mach", "" }
			});
			Log.Info("specs: seat 1 keeps the shop's own scenario (" + text + "); this shop was already running, so nothing is moved and no machine is claimed");
		}
		else if (Bays.BayOf(1) != null)
		{
			MsgSpecStock msgSpecStock = new MsgSpecStock
			{
				seat = 1
			};
			if (Net.IsHost)
			{
				Net.ToServer("spec.stock", msgSpecStock);
			}
			else
			{
				ServerStock(msgSpecStock, 0);
			}
		}
	}

	private static void ServerStock(MsgSpecStock m, int from)
	{
		Member member = Members.BySeat(1);
		PlayerStore store = Game.Store;
		EmporiumEntry e = EmporiumEntry.Instance;
		GameItem bay = Bays.BayOf(1);
		if (m == null || !Net.IsServer || member == null || store == null || bay == null || member.x.ContainsKey("spec") || Game.Day != 1)
		{
			return;
		}
		string text = store.startType.ToString();
		List<string> list = new List<string>();
		foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)e.invElement, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
		{
			if (Machine(item) && GeneralHelper.IsItemOwned(item) && !list.Contains(item.identifier))
			{
				list.Add(item.identifier);
			}
		}
		if (extraTools.TryGetValue(text, out var value))
		{
			foreach (string text2 in value)
			{
				if (!list.Contains(text2))
				{
					list.Add(text2);
				}
			}
		}
		int moved = 0;
		int stayed = 0;
		Bays.TakeAnything(bay);
		PixelWindow contentWindow = bay.contentWindow;
		GraphNodeStorage inside = ((contentWindow != null) ? contentWindow.child : null);
		// Items every intro-skipping shop gets stay in storage; the game's fanny pack goes to the founder unless the bay has one.
		HashSet<string> theShops = store.skipIntro ? new HashSet<string>(StringComparer.Ordinal) { "magnifier", "labeler", "topical_bandage_item" } : new HashSet<string>(StringComparer.Ordinal);
		bool pack = GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)bay, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).Any((GameItem i) => i != null && i.identifier == "fanny_pack");
		if (inside != null)
		{
			ItemsMove.RunServerAction("spec.stock", delegate
			{
				foreach (GameItem childItem in ((GameInventory)e.invElement).childItems.ToList())
				{
					if (childItem != bay && Bays.TagOwner(childItem) == 0 && GeneralHelper.IsItemOwned(childItem))
					{
						if (theShops.Remove(childItem.identifier ?? ""))
						{
							stayed++;
							continue;
						}
						if (childItem.identifier == "fanny_pack")
						{
							if (pack)
							{
								stayed++;
								continue;
							}
							pack = true;
						}
						GraphUtils.TryAcceptAll(inside, childItem, -1);
						if ((object)childItem.parentInventory != e.invElement)
						{
							moved++;
						}
						else
						{
							stayed++;
						}
					}
				}
			}, 0);
		}
		Members.SetFacts(1, new Dictionary<string, string>
		{
			{ "spec", text },
			{ "mach", string.Join(",", list) }
		});
		Log.Info("specs: seat 1 (" + member.n + ") is the shop's " + text + ": " + moved + " item(s) of the starting kit moved into its bay, " + stayed + " stayed in the shop's storage; machine kinds: " + string.Join(",", list));
	}

	[SystemTick]
	private static void Tick()
	{
		if (!Net.IsOnline || !Game.ShopLoaded || Dispatch.InRequest || Snapshot.IsLoading)
		{
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (Bays.Enabled && Net.IsServer && Server.ShopReady && realtimeSinceStartup >= nextServerLook && !HostTrip.Out)
		{
			nextServerLook = realtimeSinceStartup + 2f;
			try
			{
				ServerSeatOne();
			}
			catch (Exception ex)
			{
				if (Log.Allow("specs|seat1", 30000L, out var skipped))
				{
					Log.Warn("specs: settling seat 1 threw" + Log.More(skipped) + ": " + ex.Message);
				}
			}
		}
		if (!Bays.Enabled || !Net.HasLocalPlayer || !Net.IsLive || realtimeSinceStartup < nextPrompt || Outings.Private)
		{
			return;
		}
		nextPrompt = realtimeSinceStartup + 3f;
		int localSeat = Members.LocalSeat;
		if (localSeat > 0 && Of(localSeat) != null && WindowUp)
		{
			CloseWindow();
		}
		if (localSeat <= 0 || Of(localSeat) != null || Bays.BayOf(localSeat) == null)
		{
			return;
		}
		if (localSeat == 1 && Game.Day == 1 && Game.Store != null && (int)Game.Store.startType != 0)
		{
			return;
		}
		string text = (Args.Bot != null) ? Args.Get("--br-spec") : null;
		if (!string.IsNullOrEmpty(text))
		{
			nextPrompt = realtimeSinceStartup + 20f;
			Pick(text);
		}
		else if ((Args.Bot == null || Args.Has("--br-spec-window")) && !WindowUp && realtimeSinceStartup >= holdUntil && !PerkScreens.HandlesTrade)
		{
			// Fallback for when the game's own trade screen cannot be used.
			window = null;
			OpenWindow();
		}
	}

	private static void CloseWindow()
	{
		try
		{
			if (window != null || WindowUp)
			{
				CustomUIManager instance = CustomUIManager.Instance;
				if (instance != null)
				{
					instance.CloseWindow(WindowId);
				}
			}
		}
		catch
		{
		}
		window = null;
	}

	private static void OpenWindow()
	{
		try
		{
			CustomUIManager instance = CustomUIManager.Instance;
			offered = Choices();
			if ((Object)(object)instance == (Object)null || offered.Count == 0)
			{
				return;
			}
			pickIndex = 0;
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, string> item in offered)
			{
				list.Add(item.Value);
			}
			CustomUIBuilder val = instance.CreateWindow(WindowId, Lang.Tr("Your trade in this shop"), "meta", (GameObject)null, (CustomUIManager.Style)0).SetCloseOnEscape(true).SetSize(560f, 360f);
			val.AddLabel(Lang.Tr("Pick your specialization. Its starting kit goes into your storage bay, and its machines are yours alone. This cannot be changed later. (You can also type /trade in chat.)"), (string)null).AddDropdown((IList<string>)list, 0, (Action<int>)delegate(int i)
			{
				pickIndex = i;
			}, "spec").AddButton(Lang.Tr("Choose"), (Action)delegate
			{
				ChooseOffered(pickIndex);
			}, "choose");
			val.OnClose((Action)delegate
			{
				window = null;
				holdUntil = Time.realtimeSinceStartup + 30f;
			}).Center();
			window = val.Show();
			Log.Info("specs: asked the local player to pick (" + offered.Count + " on offer)");
		}
		catch (Exception ex)
		{
			window = null;
			Log.Warn("specs: the pick window did not open: " + ex.Message);
		}
	}
}

// While the new-game routine runs for a kit only, skip everything but the kit.
// Targets are named by written-out type (the mod scanner refuses reflection on unread types); missing ones are skipped.
internal static class SpecKitGuards
{
	internal static bool SkipIfKit()
	{
		return !Specs.KitOnly;
	}

	internal static bool AnswerIfKit(ref bool result, bool answer)
	{
		if (!Specs.KitOnly)
		{
			return true;
		}
		result = answer;
		return false;
	}

	internal static MethodBase Safe(Func<MethodBase> find)
	{
		try
		{
			return find();
		}
		catch
		{
			return null;
		}
	}
}

[HarmonyPatch]
internal static class SpecKitNoPerkItems
{
	private static List<MethodBase> Targets()
	{
		List<MethodBase> list = new List<MethodBase>();
		MethodBase[] array = new MethodBase[5]
		{
			SpecKitGuards.Safe(() => AccessTools.Method(typeof(StartingPerk), "IsSentinel", (Type[])null, (Type[])null)),
			SpecKitGuards.Safe(() => AccessTools.Method(typeof(StartingPerk), "IsGamblingAddiction", (Type[])null, (Type[])null)),
			SpecKitGuards.Safe(() => AccessTools.Method(typeof(StartingPerk), "IsAlcoholAddiction", (Type[])null, (Type[])null)),
			SpecKitGuards.Safe(() => AccessTools.Method(typeof(StartingPerk), "IsNicotineAddiction", (Type[])null, (Type[])null)),
			SpecKitGuards.Safe(() => AccessTools.Method(typeof(StartingPerk), "IsNarcoticAddiction", (Type[])null, (Type[])null))
		};
		foreach (MethodBase methodBase in array)
		{
			if (methodBase != null)
			{
				list.Add(methodBase);
			}
		}
		return list;
	}

	private static bool Prepare()
	{
		return Targets().Count > 0;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return Targets();
	}

	private static bool Prefix(ref bool __result)
	{
		return SpecKitGuards.AnswerIfKit(ref __result, answer: false);
	}
}

[HarmonyPatch]
internal static class SpecKitNoCassettes
{
	private static readonly MethodBase target = SpecKitGuards.Safe(() => AccessTools.Method(typeof(StartingPerk), "IsTapehead", (Type[])null, (Type[])null));

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix(ref bool __result)
	{
		return SpecKitGuards.AnswerIfKit(ref __result, answer: true);
	}
}

[HarmonyPatch]
internal static class SpecKitSkips
{
	private static List<MethodBase> Targets()
	{
		List<MethodBase> list = new List<MethodBase>();
		MethodBase[] array = new MethodBase[3]
		{
			SpecKitGuards.Safe(() => AccessTools.Method(typeof(StartingPerkContent), "InitWelfareProgram", (Type[])null, (Type[])null)),
			SpecKitGuards.Safe(() => AccessTools.Method(typeof(ModHook), "FireOnNewGameStarted", (Type[])null, (Type[])null)),
			SpecKitGuards.Safe(() => AccessTools.Method(typeof(StoreUIManager), "FinishBlackscreenSequence", (Type[])null, (Type[])null))
		};
		foreach (MethodBase methodBase in array)
		{
			if (methodBase != null)
			{
				list.Add(methodBase);
			}
		}
		return list;
	}

	private static bool Prepare()
	{
		return Targets().Count > 0;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return Targets();
	}

	private static bool Prefix()
	{
		return SpecKitGuards.SkipIfKit();
	}
}
