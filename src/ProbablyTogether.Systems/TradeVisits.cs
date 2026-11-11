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
			announced = "";
		}
		string text = store.startType.ToString();
		foreach (Member item in Members.State.list.OrderBy((Member m) => m.s).ToList())
		{
			if (item.x == null || !item.x.TryGetValue("spec", out var value) || value == text)
			{
				continue;
			}
			if (!item.x.TryGetValue(DayFact, out var value2) || !int.TryParse(value2, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
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
				Run(mgr, store, station, value, num);
			}
			catch (Exception ex)
			{
				Log.Warn("trade visits: day " + num + " of " + item.n + "'s trade (" + value + ") threw: " + ex.Message);
			}
			List<string> list = new List<string>();
			foreach (StoreClient item2 in mgr.clientStack)
			{
				if (item2 != null && !hashSet.Contains(item2))
				{
					forSeat[item2.identifier ?? ""] = item.s;
					list.Add(item2.identifier);
					Added++;
				}
			}
			if (list.Count > 0)
			{
				LastAdded = string.Join(",", list) + " for seat " + item.s;
				Log.Info("trade visits: day " + dayCounter + " is day " + num + " of " + item.n + "'s trade (" + value + "): " + string.Join(", ", list) + " will come for them");
			}
		}
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
		int num = SeatFor(text);
		if (num > 0 && !Outings.Private)
		{
			Notices.Show(Lang.Trf("{0} is here for {1}.", storeClient.displayName ?? text, Kit.Name(Members.NameOf(num))), "trade");
		}
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
			return AccessTools.Method(typeof(StoreClientManager), "HandleSupplierClient", Type.EmptyTypes, (Type[])null);
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

	private static void Prefix()
	{
		TradeVisits.InSuppliers = Net.IsOnline && Bays.Enabled;
	}

	private static void Finalizer()
	{
		TradeVisits.InSuppliers = false;
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
