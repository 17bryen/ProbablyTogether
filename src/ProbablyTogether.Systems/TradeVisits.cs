using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ProbablyTogether.UI;
using HarmonyLib;

namespace ProbablyTogether.Systems;

/// A trade's scripted customers come for the player who holds it, counted from the first opening after the pick.
/// Runs inside the game's replayed queue build and reads only the player record.
public static class TradeVisits
{
	/// Member fact: the run day that is day 1 of this player's trade.
	internal const string DayFact = "sday";

	private static bool busy;

	/// True while the game picks the day's suppliers (TradeStartPredicatePatch).
	internal static bool InSuppliers;

	private static readonly Dictionary<string, int> forSeat = new Dictionary<string, int>(StringComparer.Ordinal);

	private static int forDay = -1;

	private static string announced = "";

	internal static int Added;

	internal static string LastAdded = "";

	private static readonly Dictionary<string, string> startPredicates = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		{ "IsJunkerStart", "SCAV" },
		{ "IsWaterMerchantStart", "WATER_MERCHANT" },
		{ "IsFarmerStart", "FARMER" },
		{ "IsGunsmithStart", "GUNSMITH" },
		{ "IsChemistStart", "DRUG_DEALER" },
		{ "IsRancherStart", "RANCHER" },
		{ "IsAquafarmerStart", "AQUAFARMER" }
	};

	internal static IEnumerable<string> StartPredicateNames => startPredicates.Keys;

	/// The seat today's customer of that name came for (0: nobody in particular).
	public static int SeatFor(string identifier)
	{
		int value;
		return (!string.IsNullOrEmpty(identifier) && forSeat.TryGetValue(identifier, out value)) ? value : 0;
	}

	internal static bool Held(string predicate)
	{
		string value;
		return predicate != null && startPredicates.TryGetValue(predicate, out value) && Specs.TakenBy(value) != 0;
	}

	private static string ShopScenario()
	{
		PlayerStore store = PlayerStore.Instance;
		return (store != null) ? store.startType.ToString() : "";
	}

	/// Server: day 1 of a new trade is today if picked before the opening, else tomorrow.
	internal static string DayForPick()
	{
		PlayerStore store = PlayerStore.Instance;
		int num = Game.Day;
		if (store == null || (int)store.storeState != 2)
		{
			num++;
		}
		return num.ToString(CultureInfo.InvariantCulture);
	}

	/// Server, before opening: players with no trade day on record start counting today.
	internal static void ServerBeforeOpen()
	{
		if (!Net.IsServer || !Bays.Enabled || !Game.ShopLoaded)
		{
			return;
		}
		try
		{
			string text = ShopScenario();
			string value = Game.Day.ToString(CultureInfo.InvariantCulture);
			foreach (Member item in Members.State.list.ToList())
			{
				if (item.x != null && item.x.TryGetValue("spec", out var value2) && value2 != text && !item.x.ContainsKey(DayFact))
				{
					Members.SetFact(item.s, DayFact, value);
					Log.Info("trade visits: " + item.n + " (seat " + item.s + ", " + value2 + ") has no first day on record; counting from today (day " + value + ")");
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("trade visits: stamping first days threw: " + ex.Message);
		}
	}

	/// Every copy, inside the queue build, right after the game's cartel step.
	internal static void AfterCartel(StoreClientManager mgr)
	{
		if (busy || !Net.IsOnline || !Bays.Enabled || mgr == null || mgr.clientStack == null)
		{
			return;
		}
		PlayerStore store = PlayerStore.Instance;
		StoreStation station = StoreStation.Instance;
		if (store == null || station == null)
		{
			return;
		}
		int dayCounter = station.dayCounter;
		if (forDay != dayCounter)
		{
			forDay = dayCounter;
			forSeat.Clear();
			Openers.Clear();
			announced = "";
		}
		string text = store.startType.ToString();
		foreach (Member item in Members.State.list.OrderBy((Member m) => m.s).ToList())
		{
			if (item.x == null)
			{
				continue;
			}
			// Only the starting trade has scripted visitors (the shop's own scenario is the game's); Joe Wild's trades have none.
			List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
			if (item.x.TryGetValue("spec", out var value) && value != text && item.x.TryGetValue(DayFact, out var value2))
			{
				list.Add(new KeyValuePair<string, string>(value, value2));
			}
			foreach (KeyValuePair<string, string> item2 in list)
			{
				if (!int.TryParse(item2.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
				{
					continue;
				}
				int num = dayCounter - result + 1;
				if (num < 1)
				{
					continue;
				}
				HashSet<StoreClient> hashSet = new HashSet<StoreClient>(mgr.clientStack);
				try
				{
					Run(mgr, store, station, item2.Key, num);
				}
				catch (Exception ex)
				{
					Log.Warn("trade visits: day " + num + " of " + item.n + "'s trade (" + item2.Key + ") threw: " + ex.Message);
				}
				List<string> list2 = new List<string>();
				foreach (StoreClient item3 in mgr.clientStack)
				{
					if (item3 != null && !hashSet.Contains(item3))
					{
						forSeat[item3.identifier ?? ""] = item.s;
						try
						{
							Greet(item3, item.n, item2.Key, dayCounter);
						}
						catch (Exception ex2)
						{
							Log.Warn("trade visits: giving " + item3.identifier + " its opening line threw: " + ex2.Message);
						}
						list2.Add(item3.identifier);
						Added++;
					}
				}
				if (list2.Count > 0)
				{
					LastAdded = string.Join(",", list2) + " for seat " + item.s;
					Log.Info("trade visits: day " + dayCounter + " is day " + num + " of " + item.n + "'s trade (" + item2.Key + "): " + string.Join(", ", list2) + " will come for them");
				}
			}
		}
	}

	private static readonly Dictionary<string, string> tradeWords = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		{ "DRUG_DEALER", "chemist" },
		{ "FENCE", "fence" },
		{ "GUNSMITH", "gunsmith" },
		{ "SCAV", "junker" },
		{ "WATER_MERCHANT", "water merchant" },
		{ "RANCHER", "rancher" },
		{ "FARMER", "farmer" },
		{ "AQUAFARMER", "aquafarmer" }
	};

	internal static string Word(string spec)
	{
		string value;
		return (spec != null && tradeWords.TryGetValue(spec, out value)) ? value : Specs.Label(spec).ToLowerInvariant();
	}

	private static readonly Dictionary<string, string> supplierTrades = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		{ "retired_water_merchant", "WATER_MERCHANT" },
		{ "retired_gunsmith", "GUNSMITH" },
		{ "retired_farmer", "FARMER" },
		{ "retired_rancher", "RANCHER" },
		{ "retired_aquafarmer", "AQUAFARMER" },
		{ "retired_chemist", "DRUG_DEALER" },
		{ "shadyFarmer", "DRUG_DEALER" },
		{ "street_dealer", "DRUG_DEALER" }
	};

	/// Every copy, after the game picks the day's suppliers: a trade's suppliers come for the player who holds it.
	internal static void AfterSuppliers(StoreClientManager mgr, HashSet<StoreClient> before)
	{
		if (mgr?.clientStack == null || before == null || !Net.IsOnline || !Bays.Enabled)
		{
			return;
		}
		StoreStation instance = StoreStation.Instance;
		int day = (instance != null) ? instance.dayCounter : 0;
		foreach (StoreClient item in mgr.clientStack)
		{
			if (item == null || before.Contains(item) || item.identifier == null || !supplierTrades.TryGetValue(item.identifier, out var value))
			{
				continue;
			}
			int num = Specs.TakenBy(value);
			Member member = (num > 0) ? Members.BySeat(num) : null;
			if (member == null || forSeat.ContainsKey(item.identifier))
			{
				continue;
			}
			forSeat[item.identifier] = num;
			Greet(item, member.n, value, day);
			Added++;
			LastAdded = item.identifier + " for seat " + num;
			Log.Info("trade visits: " + item.identifier + " comes for " + member.n + " (the shop's " + Word(value) + ")");
		}
	}

	private static readonly string[][] openers = new string[4][]
	{
		new string[2] { "I'm here to see {0}.", "Word is this shop has a {1} now." },
		new string[2] { "Which one of you is {0}?", "I was told to ask for the {1}." },
		new string[2] { "I'm looking for {0}.", "The {1}, right?" },
		new string[2] { "Is {0} in?", "My business is with the {1}." }
	};

	internal static readonly Dictionary<string, string> Openers = new Dictionary<string, string>(StringComparer.Ordinal);

	private static void Greet(StoreClient c, string player, string spec, int day)
	{
		Dialogue mainDialogue = c.mainDialogue;
		if (mainDialogue == null || string.IsNullOrEmpty(player))
		{
			return;
		}
		string arg = Word(spec);
		int num = day;
		foreach (char c2 in c.identifier ?? "")
		{
			num = num * 31 + c2;
		}
		string[] array = openers[(int)((uint)num % (uint)openers.Length)];
		List<string> list = new List<string>();
		Dialogue dialogue = null;
		Dialogue dialogue2 = null;
		foreach (string en in array)
		{
			string text = Lang.Trf(en, player, Lang.Tr(arg));
			list.Add(text);
			Dialogue dialogue3 = new Dialogue();
			dialogue3.SetText(mainDialogue.title ?? c.displayName, text);
			dialogue3.portrait = mainDialogue.portrait;
			dialogue3.priority = mainDialogue.priority;
			if (dialogue == null)
			{
				dialogue = dialogue3;
			}
			else
			{
				dialogue2.SetNextDialogue(dialogue3);
			}
			dialogue2 = dialogue3;
		}
		Openers[c.identifier ?? ""] = string.Join(" / ", list);
		if (dialogue == null)
		{
			return;
		}
		dialogue2.SetNextDialogue(mainDialogue);
		Action startAction = mainDialogue.startAction;
		mainDialogue.startAction = delegate
		{
			if (c.mainDialogue == dialogue)
			{
				c.mainDialogue = mainDialogue;
			}
			startAction?.Invoke();
		};
		dialogue.isMainDialog = mainDialogue.isMainDialog;
		c.mainDialogue = dialogue;
	}

	private static void Run(StoreClientManager mgr, PlayerStore store, StoreStation station, string spec, int day)
	{
		switch (spec)
		{
		case "DRUG_DEALER":
		{
			// The game's cartel calendar, run as on day <day> of a chemist's shop.
			EnsureCartel(store);
			NewGameData.StartType startType = store.startType;
			int dayCounter = station.dayCounter;
			busy = true;
			try
			{
				store.startType = NewGameData.StartType.DRUG_DEALER;
				station.dayCounter = day;
				mgr.HandleCartelClient();
				break;
			}
			finally
			{
				store.startType = startType;
				station.dayCounter = dayCounter;
				busy = false;
			}
		}
		case "FENCE":
			if (day == 1)
			{
				mgr.AddClient(StoreClientListStory.CreateJanitorUndergroundExchangeIntroductionFence());
			}
			break;
		case "GUNSMITH":
			if (day == 1 || day == 4)
			{
				mgr.AddClient(StoreClientListGun.CreateLLGunBuyer());
			}
			else if (day == 2)
			{
				mgr.AddClient(StoreClientListGun.CreateLLGunSeller());
			}
			break;
		case "SCAV":
			if (day == 2 && !store.isBrokenMachineIntroduced)
			{
				store.isBrokenMachineIntroduced = true;
				mgr.AddClient(StoreClientListStory.CreateBrokenMachineIntroduction());
			}
			break;
		}
	}

	/// The game gives cartel standing only to a shop that started as a chemist's.
	private static void EnsureCartel(PlayerStore store)
	{
		if (store.storeReputations == null || store.storeReputations.Any((StoreReputation r) => r != null && r.factionId == "FACTION_CARTEL"))
		{
			return;
		}
		StoreReputation storeReputation = new StoreReputation();
		storeReputation.factionDisplay = LocHelper.GetLocalizedClientName("faction_name_cartel");
		storeReputation.factionId = "FACTION_CARTEL";
		storeReputation.factionColor1 = "515b66";
		storeReputation.factionColor2 = "44464c";
		storeReputation.factionColor3 = "737064";
		storeReputation.perks = StoreReputation.CreatePerks("FACTION_CARTEL");
		store.storeReputations.Add(storeReputation);
		Log.Info("trade visits: the shop now has a standing with the cartel (a player is its chemist)");
	}

	[SystemTick]
	private static void Tick()
	{
		if (!Net.IsOnline || !Net.HasLocalPlayer || !Game.ShopLoaded || forSeat.Count == 0)
		{
			return;
		}
		PlayerStore store = Game.Store;
		object obj;
		if (store == null)
		{
			obj = null;
		}
		else
		{
			StoreClientInstance currentClientInstance = store.currentClientInstance;
			obj = ((currentClientInstance != null) ? currentClientInstance.GetClientBlueprint() : null);
		}
		StoreClient storeClient = (StoreClient)obj;
		string text = (storeClient != null && store.isClientArrived) ? (storeClient.identifier ?? "") : "";
		if (text == announced)
		{
			return;
		}
		announced = text;
	}
}

[HarmonyPatch]
internal static class TradeVisitsCartelPatch
{
	private static readonly MethodBase target = Find();

	private static MethodBase Find()
	{
		try
		{
			return AccessTools.Method(typeof(StoreClientManager), "HandleCartelClient", Type.EmptyTypes, (Type[])null);
		}
		catch
		{
			return null;
		}
	}

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix(StoreClientManager __instance)
	{
		try
		{
			TradeVisits.AfterCartel(__instance);
		}
		catch (Exception ex)
		{
			Log.Warn("trade visits: threw while the day's customers were being picked: " + ex);
		}
	}
}

/// While suppliers are picked, "is this a chemist's shop" (and the like) is also true when a player holds that trade.
[HarmonyPatch]
internal static class TradeSupplierWindowPatch
{
	private static readonly MethodBase target = Find();

	private static MethodBase Find()
	{
		try
		{
			// Weekly trade suppliers are picked here; HandleSupplierClient picks only the day-one supplier.
			return AccessTools.Method(typeof(StoreClientManager), "HandleContentUnlockClient", Type.EmptyTypes, (Type[])null);
		}
		catch
		{
			return null;
		}
	}

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Prefix(StoreClientManager __instance, out HashSet<StoreClient> __state)
	{
		TradeVisits.InSuppliers = Net.IsOnline && Bays.Enabled;
		__state = (TradeVisits.InSuppliers && __instance?.clientStack != null) ? new HashSet<StoreClient>(__instance.clientStack) : null;
	}

	private static void Finalizer(StoreClientManager __instance, HashSet<StoreClient> __state)
	{
		TradeVisits.InSuppliers = false;
		try
		{
			TradeVisits.AfterSuppliers(__instance, __state);
		}
		catch (Exception ex)
		{
			Log.Warn("trade visits: threw after the day's suppliers were picked: " + ex.Message);
		}
	}
}

[HarmonyPatch]
internal static class TradeStartPredicatePatch
{
	private static readonly List<MethodBase> targets = Find();

	private static List<MethodBase> Find()
	{
		List<MethodBase> list = new List<MethodBase>();
		foreach (string startPredicateName in TradeVisits.StartPredicateNames)
		{
			try
			{
				MethodInfo methodInfo = AccessTools.Method(typeof(PlayerStore), startPredicateName, Type.EmptyTypes, (Type[])null);
				if (methodInfo != null)
				{
					list.Add(methodInfo);
				}
			}
			catch
			{
			}
		}
		return list;
	}

	private static bool Prepare()
	{
		return targets.Count > 0;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return targets;
	}

	private static void Postfix(MethodBase __originalMethod, ref bool __result)
	{
		if (!__result && TradeVisits.InSuppliers)
		{
			try
			{
				__result = TradeVisits.Held(__originalMethod.Name);
			}
			catch
			{
			}
		}
	}
}

/// Joe Wild's list: an entry that belongs to a trade says who holds it.
[HarmonyPatch]
internal static class WildTradeNotePatch
{
	private static readonly MethodBase target = Find();

	private static MethodBase Find()
	{
		try
		{
			return AccessTools.Method(typeof(NetworkUpgrade), "GetLocalizedDescription", Type.EmptyTypes, (Type[])null);
		}
		catch
		{
			return null;
		}
	}

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix(NetworkUpgrade __instance, ref string __result)
	{
		try
		{
			string text = Specs.WildNote(__instance?.id);
			if (text.Length > 0)
			{
				__result = (string.IsNullOrEmpty(__result) ? "" : (__result + "\n\n")) + text;
			}
		}
		catch
		{
		}
	}
}
