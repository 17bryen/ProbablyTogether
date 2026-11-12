using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

/// While the host is out, the shop's state exists twice: the trip body is live on screen, the home body is what players at home see.
/// Server-side work runs inside AtHome(), which swaps the home body in for its duration.
public sealed class MsgHostRep
{
	public string f;

	public double v;

	public int s;

	public int i;

	public int c;
}

public sealed class MsgHostSync
{
	public List<MsgHostRep> rep = new List<MsgHostRep>();
}

internal static class HostTrip
{
	private sealed class Body
	{
		public PlayerStore.PlayerLocation Location;

		public int Visits;

		public int Searches;

		public int LastDay;

		public int MinorImmunity;

		public int MajorImmunity;

		public int Browse;

		public int Waste;

		public int Wound;

		public bool Stable;

		public bool Fresh;

		public bool Went;

		public Dictionary<string, AfterhourLocationState> Places;

		public StoreClientInstance Customer;

		public bool Arrived;

		public AfterhourLocation Current;

		/// Swapped only on a client: the server owns the shop's ids, so a client's trip numbers items from a private range.
		public int Uid;

		public static Body Capture(PlayerStore s)
		{
			Body body = new Body
			{
				Location = s.playerLocation,
				Visits = s.visitLeftTonight,
				Searches = s.scavengingAttempts,
				LastDay = s.lastAfterhourOutingDay,
				MinorImmunity = s.dumpingGroundMinorWoundImmunitySearchesLeft,
				MajorImmunity = s.dumpingGroundMajorWoundImmunitySearchesLeft,
				Browse = s.daySinceLastBrowseUpgrade,
				Places = s.afterhourState,
				Customer = s.currentClientInstance,
				Arrived = s.isClientArrived,
				Uid = s.currentUniqueId
			};
			StoreClientData storeClientData = PlayerStore.GetStoreClientData();
			body.Waste = ((storeClientData != null) ? storeClientData.nuclearWasteDumped : 0);
			HealthData healthData = s.healthData;
			if (healthData != null)
			{
				body.Wound = healthData.woundState;
				body.Stable = healthData.isWoundStable;
				body.Fresh = healthData.isWoundedFresh;
				body.Went = healthData.wentToAfterhoursLastNight;
			}
			MapUIManager instance = MapUIManager.Instance;
			body.Current = (((Object)(object)instance != (Object)null) ? instance.Current : null);
			return body;
		}

		public void Restore(PlayerStore s)
		{
			s.playerLocation = Location;
			s.visitLeftTonight = Visits;
			s.scavengingAttempts = Searches;
			s.lastAfterhourOutingDay = LastDay;
			s.dumpingGroundMinorWoundImmunitySearchesLeft = MinorImmunity;
			s.dumpingGroundMajorWoundImmunitySearchesLeft = MajorImmunity;
			s.daySinceLastBrowseUpgrade = Browse;
			s.afterhourState = Places;
			s.currentClientInstance = Customer;
			s.isClientArrived = Arrived;
			if (!Net.IsServer)
			{
				s.currentUniqueId = Uid;
			}
			StoreClientData storeClientData = PlayerStore.GetStoreClientData();
			if (storeClientData != null)
			{
				storeClientData.nuclearWasteDumped = Waste;
			}
			HealthData healthData = s.healthData;
			if (healthData != null)
			{
				healthData.woundState = Wound;
				healthData.isWoundStable = Stable;
				healthData.isWoundedFresh = Fresh;
				healthData.wentToAfterhoursLastNight = Went;
			}
			MapUIManager instance = MapUIManager.Instance;
			if ((Object)(object)instance != (Object)null && currentField != null)
			{
				currentField.SetValue(instance, Current);
			}
		}
	}

	private sealed class Scope : IDisposable
	{
		public void Dispose()
		{
			Leave();
		}
	}

	private static readonly FieldInfo currentField = AccessTools.Field(typeof(MapUIManager), "<Current>k__BackingField");

	private sealed class TripScope : IDisposable
	{
		public void Dispose()
		{
			Enter();
		}
	}

	private static readonly Scope scope = new Scope();

	private static readonly TripScope tripScope = new TripScope();

	private static Body home;

	private static Body trip;

	private static int homeCash;

	private static int homeFavor;

	private static readonly HashSet<GameItem> soldBefore = new HashSet<GameItem>();

	private static Dictionary<string, string> shopBefore;

	internal static string LastLeak = "";

	/// Item ids below this are the server's; a client's trip numbers from here up.
	internal const int PrivateIdBase = 1000000000;

	// Client: faction standing at trip start, and how much of the movement since came from the shop at home.
	private static Dictionary<string, double[]> repStart;

	private static Dictionary<string, double[]> repAtEnter;

	private static readonly Dictionary<string, double[]> repHome = new Dictionary<string, double[]>(StringComparer.Ordinal);

	/// An item the client's own trip created; it becomes the shop's when the trip is handed over.
	internal static bool NotAtHome(GameItem item)
	{
		return HomeView && !Net.IsServer && item != null && item.uniqueId >= PrivateIdBase;
	}

	private static Dictionary<string, double[]> RepNow(PlayerStore store)
	{
		Dictionary<string, double[]> dictionary = new Dictionary<string, double[]>(StringComparer.Ordinal);
		if (store?.storeReputations != null)
		{
			foreach (StoreReputation storeReputation in store.storeReputations)
			{
				if (storeReputation != null && storeReputation.factionId != null)
				{
					dictionary[storeReputation.factionId] = new double[4] { storeReputation.GetReputationExact(), storeReputation.strength, storeReputation.influence, storeReputation.actionCooldown };
				}
			}
		}
		return dictionary;
	}

	private static GameGridInventory frontHold;

	private static GameGridInventory nothing;

	internal static MsgOutReturn Pending;

	internal static bool PendingWent;

	internal static int Swaps;

	/// From the moment the map opens until the shop is back on screen.
	internal static bool Out { get; private set; }

	/// True while code runs against the home body.
	internal static bool HomeView { get; private set; }

	/// At Dr. Jackson's the stall uses the real counters and the shop's back counter sits in the game's holding grid.
	internal static bool CountersBorrowed { get; private set; }

	/// The front counter lives in a clone for the whole trip; the game empties the real one when the player leaves.
	internal static bool FrontAway { get; private set; }

	/// Swaps the home body in for the returned scope; no-op when the host is not out.
	internal static IDisposable AtHome()
	{
		if (!Out || HomeView)
		{
			return null;
		}
		return Enter() ? scope : null;
	}

	private static bool Enter()
	{
		if (!Out || HomeView)
		{
			return false;
		}
		PlayerStore store = Game.Store;
		if (store == null || home == null)
		{
			return false;
		}
		trip = Body.Capture(store);
		home.Restore(store);
		if (!Net.IsServer)
		{
			repAtEnter = RepNow(store);
		}
		homeCash = store.playerCash;
		homeFavor = store.wildFavor;
		Outings.HomeCommissaryEnter(store);
		HomeView = true;
		Swaps++;
		return true;
	}

	/// Steps back into the trip body for the returned scope; no-op when not out or the trip is already live.
	internal static IDisposable OnTrip()
	{
		if (!Out || !HomeView)
		{
			return null;
		}
		Leave();
		return tripScope;
	}

	private static void Leave()
	{
		if (!HomeView)
		{
			return;
		}
		HomeView = false;
		PlayerStore store = Game.Store;
		if (store == null || trip == null)
		{
			return;
		}
		home = Body.Capture(store);
		trip.Restore(store);
		trip = null;
		if (!Net.IsServer && repAtEnter != null)
		{
			foreach (KeyValuePair<string, double[]> item in RepNow(store))
			{
				if (repAtEnter.TryGetValue(item.Key, out var value))
				{
					if (!repHome.TryGetValue(item.Key, out var value2))
					{
						value2 = (repHome[item.Key] = new double[4]);
					}
					for (int i = 0; i < 4; i++)
					{
						value2[i] += item.Value[i] - value[i];
					}
				}
			}
			repAtEnter = null;
		}
		Outings.HomeCashMoved(store.playerCash - homeCash, store.wildFavor - homeFavor);
		Outings.HomeCommissaryLeave(store);
	}

	internal static GraphNodeStorage Back(EmporiumEntry e)
	{
		return (GraphNodeStorage)(object)((HomeView && CountersBorrowed) ? e.backInvinvElementCounter : e.backInvinvElement);
	}

	internal static GraphNodeStorage Front(EmporiumEntry e)
	{
		return (GraphNodeStorage)(object)((HomeView && FrontAway && frontHold != null) ? frontHold : e.frontInvinvElement);
	}

	/// While it stands in for the back counter, nothing else may resolve the same items under a second name.
	internal static GraphNodeStorage Holding(EmporiumEntry e)
	{
		if (HomeView && CountersBorrowed)
		{
			if (nothing == null)
			{
				nothing = new GameGridInventory(1, 1);
			}
			return (GraphNodeStorage)(object)nothing;
		}
		return (GraphNodeStorage)(object)e.backInvinvElementCounter;
	}

	/// What the host carries on the trip does not exist for the players at home.
	internal static GraphNodeStorage Carried(GraphNodeStorage real)
	{
		if (!HomeView)
		{
			return real;
		}
		if (nothing == null)
		{
			nothing = new GameGridInventory(1, 1);
		}
		return (GraphNodeStorage)(object)nothing;
	}

	/// Clone of the front counter, so its items keep their ids on both sides.
	internal static GraphNodeStorage FrontClone()
	{
		return (GraphNodeStorage)(object)(FrontAway ? frontHold : null);
	}

	/// Adds to the shop like the game's "to inventory" helper, but in the home view.
	internal static bool AddAtHome(GameItem item)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null || item == null)
		{
			return false;
		}
		if (GraphUtils.TryAccept(Back(instance), item) != -1 || GraphUtils.TryAccept((GraphNodeStorage)(object)instance.invElement, item) != -1)
		{
			return true;
		}
		return GraphUtils.TryAccept((GraphNodeStorage)(object)instance.showcaseElement, item) != -1;
	}

	private static void SetFrontAside()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		if (frontHold == null)
		{
			frontHold = new GameGridInventory(18, 4);
		}
		int num = 0;
		foreach (GameItem childItem in ((GameInventory)instance.frontInvinvElement).childItems)
		{
			((GameInventory)instance.frontInvinvElement).Expel(childItem);
			((GameInventory)frontHold).UncheckedAccept(childItem);
			num++;
		}
		FrontAway = true;
		Log.Info("outing: " + num + " item(s) on the front counter stay there for the players at home");
	}

	private static void PutFrontBack()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		FrontAway = false;
		if ((Object)(object)instance == (Object)null || frontHold == null)
		{
			return;
		}
		int num = 0;
		foreach (GameItem childItem in ((GameInventory)frontHold).childItems)
		{
			((GameInventory)frontHold).Expel(childItem);
			((GameInventory)instance.frontInvinvElement).UncheckedAccept(childItem);
			num++;
		}
		Log.Info("outing: the front counter is the shop's again (" + num + " item(s))");
	}

	[HarmonyPatch(typeof(DrJacksonLocation), "Arrive")]
	private static class JacksonArrivePatch
	{
		private static void Prefix()
		{
			if (Out && !HomeView)
			{
				Log.Info("outing: the host is at Dr. Jackson's: the stall takes the counters; " + ((GameInventory)EmporiumEntry.Instance.backInvinvElement).childItems.Count + " item(s) on the back counter stay with the players at home");
			}
		}

		private static void Postfix()
		{
			if (Out && !HomeView)
			{
				CountersBorrowed = true;
			}
		}
	}

	[HarmonyPatch(typeof(DrJacksonLocation), "Leave")]
	private static class JacksonLeavePatch
	{
		private static void Postfix()
		{
			if (CountersBorrowed)
			{
				CountersBorrowed = false;
				Log.Info("outing: the host left Dr. Jackson's: the back counter is the shop's again (" + ((GameInventory)EmporiumEntry.Instance.backInvinvElement).childItems.Count + " item(s))");
			}
		}
	}

	/// The server has released the host; the home body is fixed from here.
	internal static bool Begin(Subtree pack)
	{
		PlayerStore store = Game.Store;
		EmporiumEntry instance = EmporiumEntry.Instance;
		if (store == null || (Object)(object)instance == (Object)null)
		{
			return false;
		}
		home = Body.Capture(store);
		soldBefore.Clear();
		if (instance.soldElement != null)
		{
			soldBefore.UnionWith(((GameInventory)instance.soldElement).childItems);
		}
		try
		{
			shopBefore = Economy.RawParts();
		}
		catch
		{
			shopBefore = null;
		}
		Dictionary<string, AfterhourLocationState> dictionary = new Dictionary<string, AfterhourLocationState>();
		if (store.afterhourState != null)
		{
			foreach (KeyValuePair<string, AfterhourLocationState> item in store.afterhourState)
			{
				AfterhourLocationState value = item.Value;
				if (value != null)
				{
					dictionary[item.Key] = new AfterhourLocationState
					{
						visits = value.visits,
						lastVisitDay = value.lastVisitDay,
						counters = ((value.counters != null) ? new Dictionary<string, int>(value.counters) : new Dictionary<string, int>()),
						activeEvents = ((value.activeEvents != null) ? new List<AfterhourActiveEvent>(value.activeEvents) : new List<AfterhourActiveEvent>())
					};
				}
			}
		}
		store.afterhourState = dictionary;
		trip = null;
		Pending = null;
		CountersBorrowed = false;
		Out = true;
		if (!Net.IsServer)
		{
			repStart = RepNow(store);
			repHome.Clear();
			repAtEnter = null;
			store.currentUniqueId = PrivateIdBase + Math.Max(0, Net.LocalPlayer % 20) * 40000000;
			if (pack != null)
			{
				// The server removes the shop's bag next; the trip travels with a renumbered copy.
				pack.ids = null;
			}
		}
		try
		{
			SetFrontAside();
		}
		catch (Exception ex2)
		{
			Log.Error("outing: setting the front counter aside threw: " + ex2);
		}
		instance.selectedBackpack = null;
		if (pack != null)
		{
			try
			{
				GameItem val = Items.Decode(pack);
				if (val != null)
				{
					if (!Net.IsServer)
					{
						Outings.Renumber(val);
					}
					GraphUtils.TryAcceptAll((GraphNodeStorage)(object)instance.afterhourPocketSlotInvBackpack, val, -1);
					if (instance.afterhourPocketSlotInvBackpack.childItem != val)
					{
						Log.Warn("outing: the host's bag " + val.identifier + " did not fit its slot; the host travels without it and it comes home at the end");
						GraphUtils.TryAccept((GraphNodeStorage)(object)instance.afterhourInventory, val);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("outing: packing the host's bag threw: " + ex);
			}
		}
		Log.Info(Net.IsHost ? "outing: the host's private trip starts; the shop stays open to the players at home" : "outing: this player's private trip starts; the shop stays live underneath (its changes keep arriving)");
		return true;
	}

	/// Called once the game is home: hands the trip to the server side.
	internal static void Finish()
	{
		MsgOutReturn pending = Pending;
		Pending = null;
		if (!Out)
		{
			return;
		}
		if (HomeView)
		{
			Leave();
		}
		PlayerStore store = Game.Store;
		CountersBorrowed = false;
		if (FrontAway)
		{
			PutFrontBack();
		}
		if (store != null && home != null)
		{
			home.Restore(store);
			if (store.currentClientInstance == null)
			{
				try
				{
					EmporiumEntry.Instance.RemoveTemporaryClientFeatureFromItems();
				}
				catch (Exception ex)
				{
					Log.Warn("outing: clearing the stall's marks from the shop's items: " + ex.Message);
				}
			}
		}
		if (!Net.IsServer && pending != null)
		{
			try
			{
				ClientSettle(store, pending);
			}
			catch (Exception ex3)
			{
				Log.Error("outing: writing down what the trip did to the shop threw: " + ex3);
			}
		}
		Out = false;
		home = null;
		trip = null;
		Outings.HostHome();
		try
		{
			Settle(store);
		}
		catch (Exception ex2)
		{
			Log.Error("outing: settling the host's trip with the shop threw: " + ex2);
		}
		Log.Info("outing: " + (Net.IsHost ? "the host's" : "this player's") + " private trip ends (" + Swaps + " look(s) at the shop at home while away)");
		Swaps = 0;
		if (pending != null)
		{
			Net.ToServer("outing.return", pending);
		}
	}

	/// Stall deals on the host's trip are shop deals: faction standing and sold items go to everyone once home.
	private static void Settle(PlayerStore store)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if (store == null || (Object)(object)instance == (Object)null || !Net.IsServer)
		{
			return;
		}
		List<string> list = new List<string>();
		if (shopBefore != null)
		{
			foreach (KeyValuePair<string, string> item in Economy.RawParts())
			{
				if (item.Key != "money" && (!shopBefore.TryGetValue(item.Key, out var value) || value != item.Value))
				{
					list.Add(item.Key);
				}
			}
		}
		shopBefore = null;
		MsgHostSync msgHostSync = new MsgHostSync();
		if (store.storeReputations != null)
		{
			foreach (StoreReputation storeReputation in store.storeReputations)
			{
				if (storeReputation != null)
				{
					msgHostSync.rep.Add(new MsgHostRep
					{
						f = storeReputation.factionId,
						v = storeReputation.GetReputationExact(),
						s = storeReputation.strength,
						i = storeReputation.influence,
						c = storeReputation.actionCooldown
					});
				}
			}
		}
		Net.ToAll("host.sync", msgHostSync);
		int num = 0;
		if (instance.soldElement != null)
		{
			foreach (GameItem childItem in ((GameInventory)instance.soldElement).childItems)
			{
				if (!soldBefore.Contains(childItem))
				{
					ItemsMove.BroadcastSubtree(childItem, "sold on a trip");
					num++;
				}
			}
		}
		soldBefore.Clear();
		LastLeak = string.Join(",", list) + ((num > 0) ? ((list.Count > 0) ? "," : "") + "sold:" + num : "");
		if (LastLeak.Length > 0)
		{
			Log.Info("outing: the host's trip changed the shop itself (" + LastLeak + "); reputation and " + num + " sold item(s) sent to everyone" + ((list.Exists((string x) => x != "reputation")) ? "; other records are not sent yet and wait for the morning" : ""));
		}
	}

	/// Client: faction standing goes to the server as differences, stall sales as items, with the return.
	private static void ClientSettle(PlayerStore store, MsgOutReturn pending)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if (store == null || (Object)(object)instance == (Object)null)
		{
			return;
		}
		List<string> list = new List<string>();
		if (repStart != null)
		{
			foreach (KeyValuePair<string, double[]> item in RepNow(store))
			{
				if (!repStart.TryGetValue(item.Key, out var value))
				{
					continue;
				}
				repHome.TryGetValue(item.Key, out var value2);
				double[] array = new double[4];
				bool flag = false;
				for (int i = 0; i < 4; i++)
				{
					array[i] = item.Value[i] - value[i] - ((value2 != null) ? value2[i] : 0.0);
					flag |= Math.Abs(array[i]) > 1E-09;
				}
				if (flag)
				{
					if (pending.rep == null)
					{
						pending.rep = new List<MsgHostRep>();
					}
					pending.rep.Add(new MsgHostRep
					{
						f = item.Key,
						v = array[0],
						s = (int)Math.Round(array[1]),
						i = (int)Math.Round(array[2]),
						c = (int)Math.Round(array[3])
					});
					list.Add(item.Key + " " + array[0].ToString("+0.##;-0.##", System.Globalization.CultureInfo.InvariantCulture));
				}
			}
		}
		repStart = null;
		repHome.Clear();
		List<Subtree> list2 = new List<Subtree>();
		if (instance.soldElement != null)
		{
			foreach (GameItem item2 in new List<GameItem>(((GameInventory)instance.soldElement).childItems))
			{
				if (item2 == null || soldBefore.Contains(item2) || item2.uniqueId < PrivateIdBase)
				{
					continue;
				}
				Subtree subtree = Items.Encode(item2);
				if (subtree != null)
				{
					subtree.ids = null;
					list2.Add(subtree);
				}
				// It comes back as the shop's item, with the shop's id, once the server has it.
				List<GameItem> list3 = new List<GameItem> { item2 };
				list3.AddRange(GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)item2, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null));
				using (Net.Apply())
				{
					item2.Destroy();
				}
				foreach (GameItem item3 in list3)
				{
					Ids.Forget(item3);
				}
			}
		}
		soldBefore.Clear();
		if (list2.Count > 0)
		{
			pending.sold = list2.ToArray();
		}
		LastLeak = string.Join(",", list) + ((list2.Count > 0) ? (((list.Count > 0) ? "," : "") + "sold:" + list2.Count) : "");
		if (LastLeak.Length > 0)
		{
			Log.Info("outing: this trip's deals changed the shop's records (" + LastLeak + "); sent to the server with the return");
		}
	}

	internal static void ServerSettle(PlayerStore store, MsgOutReturn m, string who)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if (store == null || m == null || (Object)(object)instance == (Object)null || ((m.rep == null || m.rep.Count == 0) && (m.sold == null || m.sold.Length == 0)))
		{
			return;
		}
		int num = 0;
		if (m.rep != null && store.storeReputations != null)
		{
			using (Net.Apply())
			{
				foreach (MsgHostRep item in m.rep)
				{
					if (item == null || num >= 16)
					{
						continue;
					}
					StoreReputation storeReputation = store.storeReputations.Find((StoreReputation r) => r != null && r.factionId == item.f);
					if (storeReputation != null && !double.IsNaN(item.v) && !double.IsInfinity(item.v))
					{
						// Caps one night's change to a standing.
						double num2 = Math.Max(-50.0, Math.Min(50.0, item.v));
						if (num2 != 0.0)
						{
							storeReputation.SetReputation(storeReputation.GetReputationExact() + num2);
						}
						storeReputation.strength += Math.Max(-20, Math.Min(20, item.s));
						storeReputation.influence += Math.Max(-20, Math.Min(20, item.i));
						storeReputation.actionCooldown = Math.Max(0, storeReputation.actionCooldown + Math.Max(-20, Math.Min(20, item.c)));
						num++;
					}
				}
			}
		}
		PublishRep(store);
		int num3 = 0;
		if (m.sold != null && instance.soldElement != null)
		{
			Subtree[] sold = m.sold;
			foreach (Subtree subtree in sold)
			{
				if (subtree == null || num3 >= 64)
				{
					continue;
				}
				subtree.ids = null;
				GameItem val = null;
				using (Net.Apply())
				{
					val = Items.Decode(subtree);
					if (val != null)
					{
						Outings.Renumber(val);
						GeneralHelper.SetItemOwned(val, false);
						((GameInventory)instance.soldElement).UncheckedAccept(val);
					}
				}
				if (val != null)
				{
					ItemsMove.BroadcastSubtree(val, "sold on a trip");
					num3++;
				}
			}
		}
		LastLeak = ((num > 0) ? "reputation" : "") + ((num3 > 0) ? (((num > 0) ? "," : "") + "sold:" + num3) : "");
		Log.Info("outing: " + who + "'s trip changed the shop's records (" + LastLeak + "); sent to everyone");
	}

	private static void PublishRep(PlayerStore store)
	{
		MsgHostSync msgHostSync = new MsgHostSync();
		if (store.storeReputations != null)
		{
			foreach (StoreReputation storeReputation in store.storeReputations)
			{
				if (storeReputation != null)
				{
					msgHostSync.rep.Add(new MsgHostRep
					{
						f = storeReputation.factionId,
						v = storeReputation.GetReputationExact(),
						s = storeReputation.strength,
						i = storeReputation.influence,
						c = storeReputation.actionCooldown
					});
				}
			}
		}
		Net.ToAll("host.sync", msgHostSync);
	}

	[SystemInit]
	internal static void Init()
	{
		Net.On<MsgHostSync>("host.sync", null, delegate(MsgHostSync m)
		{
			PlayerStore store = Game.Store;
			if (m?.rep == null || store?.storeReputations == null)
			{
				return;
			}
			using (Net.Apply())
			{
				foreach (MsgHostRep item in m.rep)
				{
					StoreReputation storeReputation = store.storeReputations.Find((StoreReputation r) => r != null && r.factionId == item.f);
					if (storeReputation != null)
					{
						if (storeReputation.GetReputationExact() != item.v)
						{
							storeReputation.SetReputation(item.v);
						}
						storeReputation.strength = item.s;
						storeReputation.influence = item.i;
						storeReputation.actionCooldown = item.c;
					}
				}
			}
		});
	}

	internal static void Reset()
	{
		if (Out)
		{
			Log.Info("outing: " + (Net.IsHost ? "the host's" : "this player's") + " private trip is dropped");
		}
		if (HomeView)
		{
			Leave();
		}
		PlayerStore store = Game.Store;
		if (Out && store != null && home != null && Game.ShopLoaded)
		{
			try
			{
				if (FrontAway)
				{
					PutFrontBack();
				}
				home.Restore(store);
			}
			catch (Exception ex)
			{
				Log.Warn("outing: restoring the shop after a dropped host trip: " + ex.Message);
			}
		}
		Out = false;
		HomeView = false;
		CountersBorrowed = false;
		FrontAway = false;
		home = null;
		trip = null;
		Pending = null;
		frontHold = null;
		nothing = null;
	}

	[HarmonyPatch(typeof(MapUIManager), "OnReturning")]
	private static class ReturningPatch
	{
		private static void Postfix()
		{
			if (Out && Pending != null && PendingWent)
			{
				try
				{
					Finish();
				}
				catch (Exception ex)
				{
					Log.Error("outing: finishing the host's trip threw: " + ex);
				}
			}
		}
	}

	[HarmonyPatch(typeof(MapUIManager), "CloseUI", new Type[] { typeof(bool) })]
	private static class MapClosedPatch
	{
		private static void Postfix()
		{
			if (Out && Pending != null && !PendingWent)
			{
				try
				{
					Finish();
				}
				catch (Exception ex)
				{
					Log.Error("outing: finishing the host's cancelled trip threw: " + ex);
				}
			}
		}
	}
}
