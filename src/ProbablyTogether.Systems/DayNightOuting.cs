using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

public static class DayNightOuting
{
	public const string KGoOut = "dayNight.goOut";

	public const string KOutingStarted = "dayNight.outingStarted";

	public const string KCloseMap = "dayNight.closeMap";

	public const string KMapClosed = "dayNight.mapClosed";

	public const string KTravel = "dayNight.travel";

	public const string KTraveled = "dayNight.traveled";

	public const string KScavenge = "dayNight.scavenge";

	public const string KScavenged = "dayNight.scavenged";

	public const string KReturn = "dayNight.return";

	public const string KReturned = "dayNight.returned";

	public const string LockPrefix = "outing.";

	private static bool mapOpen;

	private static readonly FieldInfo inTransitField = AccessTools.Field(typeof(MapUIManager), "inTransit");

	public static bool MapOpen
	{
		get
		{
			if (!Net.IsClient)
			{
				return mapOpen;
			}
			return TransitUI.IsOpen;
		}
	}

	public static bool IsOut
	{
		get
		{
			if ((Object)(object)MapUIManager.Instance != (Object)null)
			{
				return MapUIManager.Instance.Current != null;
			}
			return false;
		}
	}

	public static string LocationId
	{
		get
		{
			MapUIManager instance = MapUIManager.Instance;
			object obj;
			if (instance == null)
			{
				obj = null;
			}
			else
			{
				AfterhourLocation current = instance.Current;
				obj = ((current != null) ? current.Id : null);
			}
			if (obj == null)
			{
				obj = "";
			}
			return (string)obj;
		}
	}

	public static event Action<int, int> OutingStarted;

	public static event Action<string, string, int> OutingStep;

	internal static bool InTransit()
	{
		try
		{
			return (Object)(object)MapUIManager.Instance != (Object)null && inTransitField != null && (bool)inTransitField.GetValue(MapUIManager.Instance);
		}
		catch
		{
			return false;
		}
	}

	internal static void SetMapView(bool open)
	{
		mapOpen = open;
	}

	internal static void Register()
	{
		Net.On<MsgDnGoOut>("dayNight.goOut", ServerGoOut, null);
		Net.On<MsgDnOuting>("dayNight.outingStarted", null, ClientStep);
		Net.On<MsgDnEmpty>("dayNight.closeMap", ServerCloseMap, null);
		Net.On<MsgDnOuting>("dayNight.mapClosed", null, ClientStep);
		Net.On<MsgDnTravel>("dayNight.travel", ServerTravel, null);
		Net.On<MsgDnOuting>("dayNight.traveled", null, ClientStep);
		Net.On<MsgDnEmpty>("dayNight.scavenge", ServerScavenge, null);
		Net.On<MsgDnOuting>("dayNight.scavenged", null, ClientStep);
		Net.On<MsgDnEmpty>("dayNight.return", ServerReturn, null);
		Net.On<MsgDnOuting>("dayNight.returned", null, ClientStep);
	}

	internal static long OutingHash()
	{
		PlayerStore store = Game.Store;
		if (store == null || !Game.ShopLoaded)
		{
			return 0L;
		}
		ulong h = 14695981039346656037uL;
		Mix(ref h, store.visitLeftTonight);
		Mix(ref h, store.scavengingAttempts);
		Mix(ref h, store.lastAfterhourOutingDay);
		Mix(ref h, (long)store.playerLocation);
		Mix(ref h, store.dumpingGroundMinorWoundImmunitySearchesLeft);
		Mix(ref h, store.dumpingGroundMajorWoundImmunitySearchesLeft);
		HealthData healthData = store.healthData;
		if (healthData != null)
		{
			Mix(ref h, healthData.woundState);
			Mix(ref h, healthData.isWoundedFresh ? 1 : 0);
			Mix(ref h, healthData.wentToAfterhoursLastNight ? 1 : 0);
		}
		Mix(ref h, LocationId);
		if (store.afterhourState != null)
		{
			List<string> list = new List<string>(store.afterhourState.Keys);
			list.Sort(StringComparer.Ordinal);
			foreach (string item in list)
			{
				AfterhourLocationState val = store.afterhourState[item];
				Mix(ref h, item);
				Mix(ref h, val?.visits ?? (-1));
				Mix(ref h, val?.lastVisitDay ?? (-1));
			}
		}
		return (long)h;
	}

	internal static long BagHash()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null || !Game.ShopLoaded)
		{
			return 0L;
		}
		ulong h = 14695981039346656037uL;
		try
		{
			GraphNodeStorage[] array = (GraphNodeStorage[])(object)new GraphNodeStorage[4]
			{
				(GraphNodeStorage)instance.afterhourInventory,
				(GraphNodeStorage)instance.afterhourPocketSlotInvBackpack,
				(GraphNodeStorage)instance.afterhourPocketSlotInvLeft,
				(GraphNodeStorage)instance.afterhourPocketSlotInvRight
			};
			foreach (GraphNodeStorage val in array)
			{
				if (val == null)
				{
					Mix(ref h, -9L);
					continue;
				}
				foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
				{
					Mix(ref h, item.uniqueId);
					Mix(ref h, item.identifier);
					Mix(ref h, item.unitCount);
				}
				Mix(ref h, -1L);
			}
		}
		catch (Exception ex)
		{
			Mix(ref h, ex.GetType().Name);
		}
		return (long)h;
	}

	private static void Mix(ref ulong h, long v)
	{
		for (int i = 0; i < 8; i++)
		{
			h ^= (byte)(v >> i * 8);
			h *= 1099511628211uL;
		}
	}

	private static void Mix(ref ulong h, string s)
	{
		if (s == null)
		{
			Mix(ref h, -7L);
			return;
		}
		foreach (char c in s)
		{
			h ^= c;
			h *= 1099511628211uL;
		}
		Mix(ref h, s.Length);
	}

	private static string ModalHeldByOther(int from)
	{
		foreach (KeyValuePair<string, int> item in Locks.Table())
		{
			if (item.Value != from && item.Value >= 0 && (item.Key.StartsWith("outing.", StringComparison.Ordinal) || (item.Key == "counter" && IsOut)))
			{
				return Net.PlayerName(item.Value) + " is still busy here (" + item.Key + ").";
			}
		}
		return null;
	}

	internal static string Common(PlayerStore ps)
	{
		if (DayNightRun.RunOver)
		{
			return "The run has ended.";
		}
		if (ps == null || !Game.ShopLoaded || (Object)(object)MapUIManager.Instance == (Object)null)
		{
			return "The shop is not ready.";
		}
		if (DayNight.NightRunning)
		{
			return "The night is running.";
		}
		if ((int)ps.storeState != 0)
		{
			return "Close the shop first.";
		}
		if (InTransit())
		{
			return "Already on the way.";
		}
		return null;
	}

	private static void ServerGoOut(MsgDnGoOut m, int from)
	{
		PlayerStore store = Game.Store;
		bool hostTrip = Net.IsHost && from == Net.LocalPlayer;
		string text = ((Outings.Individual && !hostTrip) ? "Outings are individual on this server: update the mod to go out." : Common(store));
		if (text == null && Outings.AnyOut)
		{
			text = Outings.WhoIsOut() + " is still out.";
		}
		if (text == null)
		{
			if (mapOpen)
			{
				text = "The map is already open.";
			}
			else if (IsOut)
			{
				text = "The group is already out.";
			}
			else if (store.visitLeftTonight <= 0)
			{
				text = "There is no time for another trip tonight.";
			}
			else if (!store.isAfterhoursIntroduced)
			{
				text = "Nobody knows where to go yet.";
			}
			else if (HealthData.IsSeriouslyWounded())
			{
				text = "Too badly hurt to go out tonight.";
			}
			else if (StoreStation.Instance.storeEventManager.IsEventActive("crackdown"))
			{
				text = "The crackdown keeps everyone inside tonight.";
			}
			else if (StartingPerkContent.IsAgoraphobiaResting())
			{
				text = "Not tonight: the agoraphobia needs a rest.";
			}
		}
		GameItem val = null;
		if (text == null && m.backpackId != 0L)
		{
			val = Ids.Find(m.backpackId);
			if (val == null)
			{
				text = "That backpack is gone.";
			}
			else if (!EmporiumEntry.Instance.GetAllOwnedItemsByTag("BACKPACK_TAG").Contains(val))
			{
				text = "That is not one of the shop's backpacks.";
			}
		}
		if (text == null && StoreClientData.IsButcherHunting() && (val == null || !GunHelper.HasGunInside(val)))
		{
			text = LocHelper.GetLocalizedMechanic("response_afterhours_need_gun", new object[1] { LocHelper.GetLocalizedClientName("name_the_butcher") });
		}
		if (text != null)
		{
			DayNight.Refuse(from, "goOut", text);
			return;
		}
		DayNight.ClearReady("outing");
		MsgDnOuting msgDnOuting = new MsgDnOuting
		{
			what = "goOut",
			day = Game.Day,
			by = from,
			backpackId = ((val != null) ? Ids.Of(val) : 0)
		};
		EmporiumEntry.Instance.selectedBackpack = val;
		using (Net.Apply())
		{
			RunInstant("open the map", delegate
			{
				MapUIManager.Instance.OpenUI();
			});
		}
		mapOpen = true;
		msgDnOuting.rd = Replay.Digest();
		msgDnOuting.parts = DayNight.Parts();
		Net.ToAll("dayNight.outingStarted", msgDnOuting);
		Log.Info("dayNight: " + Net.PlayerName(from) + " opened the map (backpack " + msgDnOuting.backpackId + "); " + Digest.Compute().Describe());
		DayNight.Raise(DayNightOuting.OutingStarted, msgDnOuting.day, from);
	}

	private static void ServerCloseMap(MsgDnEmpty m, int from)
	{
		string text = Common(Game.Store);
		if (text == null && !mapOpen)
		{
			text = "The map is not open.";
		}
		if (text == null && IsOut)
		{
			text = "The group is out: return instead.";
		}
		if (text == null)
		{
			text = ModalHeldByOther(from);
		}
		if (text != null)
		{
			DayNight.Refuse(from, "closeMap", text);
			return;
		}
		using (Net.Apply())
		{
			try
			{
				MapUIManager.Instance.CloseUI(true);
			}
			catch (Exception ex)
			{
				Log.Error("dayNight: closing the map threw: " + ex);
			}
		}
		mapOpen = false;
		MsgDnOuting msg = new MsgDnOuting
		{
			what = "closeMap",
			day = Game.Day,
			by = from,
			rd = Replay.Digest(),
			parts = DayNight.Parts()
		};
		Net.ToAll("dayNight.mapClosed", msg);
		Log.Info("dayNight: " + Net.PlayerName(from) + " closed the map");
		DayNight.Raise(DayNightOuting.OutingStep, "closeMap", "", from);
	}

	private static void ServerTravel(MsgDnTravel m, int from)
	{
		string text = Common(Game.Store);
		AfterhourLocation loc = null;
		if (text == null && !mapOpen)
		{
			text = (IsOut ? "The group is already out." : "Open the map first (the door).");
		}
		if (text == null)
		{
			loc = ((string.IsNullOrEmpty(m.locationId) || m.locationId.Length > 128) ? null : AfterhourLocation.Get(m.locationId));
			if (loc == null)
			{
				text = "There is no such place.";
			}
			else if (!loc.IsDiscovered())
			{
				text = "Nobody knows the way there yet.";
			}
			else if (!loc.IsAvailable())
			{
				text = loc.GetTitle() + " is closed tonight.";
			}
		}
		if (text == null)
		{
			text = ModalHeldByOther(from);
		}
		if (text != null)
		{
			DayNight.Refuse(from, "travel", text);
			return;
		}
		GameItem selectedBackpack = EmporiumEntry.Instance.selectedBackpack;
		MsgDnOuting msgDnOuting = new MsgDnOuting
		{
			what = "travel",
			day = Game.Day,
			by = from,
			locationId = loc.Id,
			backpackId = ((selectedBackpack != null) ? Ids.Of(selectedBackpack) : 0)
		};
		using (Replay.Server(out msgDnOuting.seed))
		{
			RunInstant("travel", delegate
			{
				MapUIManager.Instance.Travel(loc);
			});
		}
		mapOpen = false;
		msgDnOuting.rd = Replay.Digest();
		msgDnOuting.parts = DayNight.Parts();
		Net.ToAll("dayNight.traveled", msgDnOuting);
		Log.Info("dayNight: " + Net.PlayerName(from) + " took the group to " + loc.Id + "; " + Digest.Compute().Describe());
		DayNight.Raise(DayNightOuting.OutingStep, "travel", loc.Id, from);
	}

	private static void ServerScavenge(MsgDnEmpty m, int from)
	{
		PlayerStore store = Game.Store;
		string text = Common(store);
		if (text == null && LocationId != "dumping_ground")
		{
			text = "Searching works only at the dumping ground.";
		}
		if (text == null && store.healthData != null && store.healthData.isWoundedFresh)
		{
			text = "Too hurt to keep searching tonight.";
		}
		if (text == null && ScavHelper.GetScavTimeLeft() <= 0)
		{
			text = "No time left to search tonight.";
		}
		if (text != null)
		{
			DayNight.Refuse(from, "scavenge", text);
			return;
		}
		MsgDnOuting msgDnOuting = new MsgDnOuting
		{
			what = "scavenge",
			day = Game.Day,
			by = from,
			locationId = LocationId
		};
		using (Replay.Server(out msgDnOuting.seed))
		{
			try
			{
				ScavHelper.ScavengeDumpingGrounds();
			}
			catch (Exception ex)
			{
				Log.Error("dayNight: searching threw: " + ex);
			}
		}
		msgDnOuting.attempts = store.scavengingAttempts;
		msgDnOuting.wound = store.healthData?.woundState ?? 0;
		msgDnOuting.rd = Replay.Digest();
		msgDnOuting.parts = DayNight.Parts();
		Net.ToAll("dayNight.scavenged", msgDnOuting);
		Log.Info("dayNight: " + Net.PlayerName(from) + " searched the dumping ground (attempt " + msgDnOuting.attempts + ", wound " + msgDnOuting.wound + ", bag " + BagCount() + " item(s))");
		DayNight.Raise(DayNightOuting.OutingStep, "scavenge", msgDnOuting.locationId, from);
	}

	private static void ServerReturn(MsgDnEmpty m, int from)
	{
		PlayerStore store = Game.Store;
		string text = Common(store);
		if (text == null && !IsOut)
		{
			text = (mapOpen ? "The group has not left yet: close the map instead." : "The group is not out.");
		}
		if (text == null)
		{
			text = ModalHeldByOther(from);
		}
		if (text != null)
		{
			DayNight.Refuse(from, "return", text);
			return;
		}
		string locationId = LocationId;
		MsgDnOuting msgDnOuting = new MsgDnOuting
		{
			what = "return",
			day = Game.Day,
			by = from,
			locationId = locationId
		};
		using (Replay.Server(out msgDnOuting.seed))
		{
			RunInstant("return", delegate
			{
				MapUIManager.Instance.ReturnNow();
			});
		}
		foreach (KeyValuePair<string, int> item in Locks.Table())
		{
			if (item.Key.StartsWith("outing.", StringComparison.Ordinal) || item.Key == "counter")
			{
				Locks.Release(item.Key, -1);
			}
		}
		msgDnOuting.rd = Replay.Digest();
		msgDnOuting.parts = DayNight.Parts();
		Net.ToAll("dayNight.returned", msgDnOuting);
		Log.Info("dayNight: " + Net.PlayerName(from) + " brought the group back from " + locationId + "; visits left tonight " + store.visitLeftTonight + "; " + Digest.Compute().Describe());
		DayNight.Raise(DayNightOuting.OutingStep, "return", locationId, from);
	}

	private static int BagCount()
	{
		try
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			int? obj;
			if (instance == null)
			{
				obj = null;
			}
			else
			{
				GameGridInventory afterhourInventory = instance.afterhourInventory;
				obj = ((afterhourInventory == null) ? ((int?)null) : ((GameInventory)afterhourInventory).childItems?.Count);
			}
			int? num = obj;
			return num.GetValueOrDefault();
		}
		catch
		{
			return -1;
		}
	}

	private static void RunInstant(string what, Action step)
	{
		using DayNightBlackscreen dayNightBlackscreen = DayNightBlackscreen.Instant(!Net.IsDedicated && !DayLog.Replaying);
		try
		{
			step();
		}
		catch (Exception ex)
		{
			Log.Error("dayNight: " + what + " threw: " + ex);
		}
		if (!dayNightBlackscreen.Consumed)
		{
			Log.Warn("dayNight: " + what + " did not start (the game's own guard refused it)");
		}
	}

	private static void ClientStep(MsgDnOuting m)
	{
		PlayerStore store = Game.Store;
		MapUIManager map = MapUIManager.Instance;
		if (store == null || (Object)(object)map == (Object)null || (Object)(object)EmporiumEntry.Instance == (Object)null)
		{
			Log.Warn("dayNight: outing step " + m.what + " arrived with no shop");
			return;
		}
		string text = null;
		switch (m.what)
		{
		case "goOut":
			if (MapOpen || IsOut)
			{
				text = "the map closed and the group home";
				break;
			}
			EmporiumEntry.Instance.selectedBackpack = ((m.backpackId != 0L) ? Ids.Find(m.backpackId) : null);
			if (m.backpackId != 0L && EmporiumEntry.Instance.selectedBackpack == null)
			{
				Log.Warn("dayNight: backpack " + m.backpackId + " not found here");
			}
			using (Net.Apply())
			{
				RunInstant("open the map", delegate
				{
					map.OpenUI();
				});
			}
			mapOpen = true;
			break;
		case "closeMap":
			if (!MapOpen)
			{
				text = "the map open";
				break;
			}
			using (Net.Apply())
			{
				try
				{
					map.CloseUI(true);
				}
				catch (Exception ex)
				{
					Log.Error("dayNight: closing the map threw: " + ex);
				}
			}
			mapOpen = false;
			break;
		case "travel":
		{
			AfterhourLocation loc = AfterhourLocation.Get(m.locationId ?? "");
			if (loc == null)
			{
				text = "location " + m.locationId + " (a mod missing here?)";
				break;
			}
			if (IsOut)
			{
				text = "the group home";
				break;
			}
			EmporiumEntry.Instance.selectedBackpack = ((m.backpackId != 0L) ? Ids.Find(m.backpackId) : null);
			using (Replay.Client(m.seed))
			{
				RunInstant("travel", delegate
				{
					map.Travel(loc);
				});
			}
			mapOpen = false;
			break;
		}
		case "scavenge":
			if (LocationId != "dumping_ground")
			{
				text = "the group at the dumping ground";
				break;
			}
			using (Replay.Client(m.seed))
			{
				try
				{
					ScavHelper.ScavengeDumpingGrounds();
				}
				catch (Exception ex2)
				{
					Log.Error("dayNight: replaying the search threw: " + ex2);
				}
			}
			try
			{
				if (!DayLog.Replaying)
				{
					map.UpdateScavUI();
				}
			}
			catch
			{
			}
			break;
		case "return":
			if (!IsOut)
			{
				text = "the group out";
				break;
			}
			using (Replay.Client(m.seed))
			{
				RunInstant("return", delegate
				{
					map.ReturnNow();
				});
			}
			break;
		default:
			Log.Warn("dayNight: unknown outing step " + m.what);
			return;
		}
		if (text != null)
		{
			DayNight.Mismatches++;
			Log.Warn("dayNight: outing step " + m.what + " expects " + text + (DayLog.Replaying ? "" : "; asking for a resync"));
			if (!DayLog.Replaying)
			{
				Snapshot.RequestResync("dayNight: outing step " + m.what + " expects " + text);
			}
			return;
		}
		DayNight.CheckAfter("the outing step " + m.what, m.rd, m.parts);
		if (m.by != Net.LocalPlayer && m.by > 0)
		{
			switch (m.what)
			{
			case "goOut":
				DayNight.Notice(Net.PlayerName(m.by) + " opened the door: pick where to go.");
				break;
			case "travel":
				DayNight.Notice(Net.PlayerName(m.by) + " led the group out.");
				break;
			case "return":
				DayNight.Notice(Net.PlayerName(m.by) + " brought the group home.");
				break;
			}
		}
		if (m.what == "goOut")
		{
			DayNight.Raise(DayNightOuting.OutingStarted, m.day, m.by);
		}
		else
		{
			DayNight.Raise(DayNightOuting.OutingStep, m.what, m.locationId ?? "", m.by);
		}
	}
}
