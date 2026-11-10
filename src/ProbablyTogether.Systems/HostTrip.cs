using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

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
				Arrived = s.isClientArrived
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

	private static readonly Scope scope = new Scope();

	private static Body home;

	private static Body trip;

	private static int homeCash;

	private static int homeFavor;

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
		PlayerStore store = Game.Store;
		if (store == null || home == null)
		{
			return null;
		}
		trip = Body.Capture(store);
		home.Restore(store);
		homeCash = store.playerCash;
		homeFavor = store.wildFavor;
		HomeView = true;
		Swaps++;
		return scope;
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
		Outings.HomeCashMoved(store.playerCash - homeCash, store.wildFavor - homeFavor);
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
		Log.Info("outing: the host's private trip starts; the shop stays open to the players at home");
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
		Out = false;
		home = null;
		trip = null;
		Outings.HostHome();
		Log.Info("outing: the host's private trip ends (" + Swaps + " look(s) at the shop at home while away)");
		Swaps = 0;
		if (pending != null && Net.IsHost)
		{
			Net.ToServer("outing.return", pending);
		}
	}

	internal static void Reset()
	{
		if (Out)
		{
			Log.Info("outing: the host's private trip is dropped");
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
