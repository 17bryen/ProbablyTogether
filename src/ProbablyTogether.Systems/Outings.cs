using System;
using System.Collections.Generic;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

public sealed class MsgOutGo
{
	public long backpackId;

	public bool ok;
}

public sealed class MsgOutCash
{
	public int by;

	public int cash;

	public int favor;
}

public sealed class MsgOutLeft
{
	public int by;

	public int day;

	public int visits;

	public long backpackId;
}

public sealed class MsgOutReturn
{
	public Subtree[] items;

	public bool went;

	public string[] visited;

	public int wound;

	public bool fresh;

	public int waste;

	public bool jackson;
}

public sealed class MsgOutReturned
{
	public int by;

	public bool went;

	public string[] visited;

	public int wound;

	public bool fresh;

	public int waste;

	public bool jackson;

	public int visits;
}

/// One player goes out while the others stay. A remote client runs the base game's outing privately and hands
/// its bag to the server on return; the server removes the bag at departure and creates what comes back.
public static class Outings
{
	private sealed class Rec
	{
		public Subtree Pack;

		public long PackId;

		public string Name;
	}

	/// Server setting (server.json individualOutings), sent to clients in the welcome.
	public static bool Individual;

	private static bool returning;

	private static long backpackId;

	private static int lastCash;

	private static int lastFavor;

	private static float lastCashCheck;

	private static bool jackson;

	private static readonly HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);

	private static readonly Dictionary<int, Rec> away = new Dictionary<int, Rec>();

	private static readonly HashSet<int> outPlayers = new HashSet<int>();

	private static readonly HashSet<string> passWhilePrivate = new HashSet<string>(StringComparer.Ordinal)
	{
		"outing.go", "chat.line", "chat.notice", "presence.cursor", "presence.state", "ping.show", "ping.no", "voice.frame", "voice.config"
	};

	internal static int Dropped;

	private static bool privateTrip;

	/// This game is on a private trip: the mod's interception stands aside (see Net.Applying).
	public static bool Private
	{
		get
		{
			return privateTrip && !HostTrip.HomeView;
		}
		private set
		{
			privateTrip = value;
		}
	}

	public static bool Returning => returning;

	internal static int OutCount => away.Count;

	public static bool AnyOut
	{
		get
		{
			if (Net.IsServer)
			{
				return away.Count > 0;
			}
			return outPlayers.Count > 0;
		}
	}

	public static string WhoIsOut()
	{
		foreach (int item in Net.IsServer ? new List<int>(away.Keys) : new List<int>(outPlayers))
		{
			return Net.PlayerName(item);
		}
		return "Someone";
	}

	/// Server: tonight's trips, one per player in the shop at closing; 0 leaves the game's single group trip.
	internal static int NightBudget()
	{
		if (!Individual || !Net.IsServer)
		{
			return 0;
		}
		return Math.Max(1, Server.PlayerCount);
	}

	public static bool IsOut(int player)
	{
		return Net.IsServer ? away.ContainsKey(player) : outPlayers.Contains(player);
	}

	[SystemInit]
	internal static void Init()
	{
		Net.On<MsgOutGo>("outing.go", ServerGo, ClientGo);
		Net.On<MsgOutCash>("outing.cash", ServerCash, null);
		Net.On<MsgOutCash>("outing.cashed", null, ClientCashed);
		Net.On<MsgOutLeft>("outing.left", null, ClientLeft);
		Net.On<MsgOutReturn>("outing.return", ServerReturn, null);
		Net.On<MsgOutReturned>("outing.returned", null, ClientReturned);
		Net.PlayerLeft += ServerPlayerLeft;
		Net.Disconnected += delegate
		{
			ResetClient("disconnected");
		};
	}

	internal static bool DropsWhilePrivate(string kind)
	{
		if (!Private || passWhilePrivate.Contains(kind) || kind.StartsWith("bot.", StringComparison.Ordinal))
		{
			return false;
		}
		Dropped++;
		return true;
	}

	/// Replaces the group "go out" when outings are individual. True = handled.
	internal static bool RequestGo()
	{
		if (!Individual)
		{
			return false;
		}
		if (Private || HostTrip.Out || !Net.IsLive)
		{
			return true;
		}
		EmporiumEntry instance = EmporiumEntry.Instance;
		GameItem val = (((Object)(object)instance != (Object)null) ? instance.selectedBackpack : null);
		Net.ToServer("outing.go", new MsgOutGo
		{
			backpackId = ((val != null) ? Ids.Of(val) : 0)
		});
		Log.Info("outing: asked the server to go out" + ((val != null) ? (" with " + val.identifier + "#" + Ids.Of(val)) : " without a bag"));
		return true;
	}

	private static void ClientGo(MsgOutGo m)
	{
		if (m == null || !m.ok || Private)
		{
			return;
		}
		PlayerStore store = Game.Store;
		EmporiumEntry instance = EmporiumEntry.Instance;
		MapUIManager instance2 = MapUIManager.Instance;
		if (store == null || (Object)(object)instance == (Object)null || (Object)(object)instance2 == (Object)null)
		{
			Log.Warn("outing: the server let us go but there is no shop here");
			return;
		}
		if (Net.IsHost)
		{
			away.TryGetValue(Net.LocalPlayer, out var value);
			if (value == null || !HostTrip.Begin(value.Pack))
			{
				Log.Warn("outing: the host was let go but its trip could not start");
				return;
			}
		}
		Private = true;
		returning = false;
		backpackId = m.backpackId;
		lastCash = store.playerCash;
		lastFavor = store.wildFavor;
		jackson = false;
		visited.Clear();
		if (!Net.IsHost)
		{
			instance.selectedBackpack = ((backpackId != 0L) ? Ids.Find(backpackId) : null);
		}
		Log.Info("outing: private trip starts (bag " + backpackId + ", cash " + lastCash + "); " + (Net.IsHost ? "the shop stays live for the players at home" : "shop updates are ignored until the return"));
		try
		{
			instance2.OpenUI();
		}
		catch (Exception ex)
		{
			Log.Error("outing: opening the map threw: " + ex);
		}
	}

	[SystemTick]
	private static void Tick()
	{
		if (!Private || returning)
		{
			return;
		}
		string locationId = DayNightOuting.LocationId;
		if (!string.IsNullOrEmpty(locationId) && visited.Add(locationId))
		{
			Log.Info("outing: arrived at " + locationId);
			if (locationId == "dr_jackson")
			{
				jackson = true;
			}
		}
		if (Time.realtimeSinceStartup - lastCashCheck >= 0.25f)
		{
			lastCashCheck = Time.realtimeSinceStartup;
			FlushCash();
		}
	}

	private static void FlushCash()
	{
		PlayerStore store = Game.Store;
		if (store == null)
		{
			return;
		}
		int num = store.playerCash - lastCash;
		int num2 = store.wildFavor - lastFavor;
		if (num != 0 || num2 != 0)
		{
			lastCash = store.playerCash;
			lastFavor = store.wildFavor;
			Net.ToServer("outing.cash", new MsgOutCash
			{
				cash = num,
				favor = num2
			});
		}
	}

	/// Closing the map without travelling cancels the trip. False = skip the game's CloseUI.
	internal static bool OnLocalCloseMap()
	{
		MapUIManager instance = MapUIManager.Instance;
		if (returning || (Object)(object)instance == (Object)null || instance.Current != null || DayNightOuting.InTransit() || !TransitUI.IsOpen)
		{
			return true;
		}
		SendReturn(went: false);
		return Net.IsHost;
	}

	/// Hands everything carried to the server and waits for the resync. False = skip the game's return.
	internal static bool OnLocalReturn()
	{
		MapUIManager instance = MapUIManager.Instance;
		if (returning || (Object)(object)instance == (Object)null || instance.Current == null || DayNightOuting.InTransit())
		{
			return true;
		}
		SendReturn(went: true);
		return Net.IsHost;
	}

	/// Host: cash moved at home while the host is out is not the trip's doing.
	internal static void HomeCashMoved(int cash, int favor)
	{
		lastCash += cash;
		lastFavor += favor;
	}

	internal static void HostHome()
	{
		Private = false;
		returning = false;
	}

	private static void SendReturn(bool went)
	{
		returning = true;
		PlayerStore store = Game.Store;
		EmporiumEntry instance = EmporiumEntry.Instance;
		List<GameItem> list = new List<GameItem>();
		int num = 0;
		try
		{
			if (went)
			{
				GameSlotInventory[] array = new GameSlotInventory[3] { instance.afterhourPocketSlotInvBackpack, instance.afterhourPocketSlotInvRight, instance.afterhourPocketSlotInvLeft };
				foreach (GameSlotInventory val in array)
				{
					GameItem val2 = ((val != null) ? val.childItem : null);
					if (val2 != null)
					{
						list.Add(val2);
					}
				}
				string locationId = DayNightOuting.LocationId;
				if (locationId == "dr_jackson")
				{
					GameGridInventory[] array2 = new GameGridInventory[2] { instance.frontInvinvElement, instance.backInvinvElement };
					foreach (GameGridInventory val3 in array2)
					{
						if (val3 == null)
						{
							continue;
						}
						foreach (GameItem childItem in ((GameInventory)val3).childItems)
						{
							if (GeneralHelper.IsItemOwned(childItem))
							{
								list.Add(childItem);
							}
						}
					}
					if (instance.afterhourInventory != null)
					{
						list.AddRange(((GameInventory)instance.afterhourInventory).childItems);
					}
				}
				else if (locationId == "dumping_ground" && instance.afterhourInventory != null)
				{
					foreach (GameItem childItem2 in ((GameInventory)instance.afterhourInventory).childItems)
					{
						if (childItem2.identifier == "nuclear_waste")
						{
							num++;
						}
					}
				}
			}
			else
			{
				GameItem val4 = (Net.IsHost ? instance.afterhourPocketSlotInvBackpack.childItem : ((backpackId != 0L) ? Ids.Find(backpackId) : null));
				if (val4 != null)
				{
					list.Add(val4);
				}
				if (Net.IsHost && instance.afterhourInventory != null)
				{
					list.AddRange(((GameInventory)instance.afterhourInventory).childItems);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("outing: collecting what comes home threw: " + ex);
		}
		List<Subtree> list2 = new List<Subtree>();
		foreach (GameItem item in list)
		{
			try
			{
				Subtree subtree = Items.Encode(item);
				if (subtree != null)
				{
					subtree.ids = null;
					list2.Add(subtree);
				}
				else
				{
					Log.Warn("outing: could not encode " + item.identifier + "; it stays behind");
				}
			}
			catch (Exception ex2)
			{
				Log.Warn("outing: encoding " + item.identifier + " threw: " + ex2.Message);
			}
		}
		FlushCash();
		string[] array3 = new string[visited.Count];
		visited.CopyTo(array3);
		MsgOutReturn msgOutReturn = new MsgOutReturn
		{
			items = list2.ToArray(),
			went = (went && visited.Count > 0),
			visited = array3,
			wound = (store?.healthData?.woundState ?? 0),
			fresh = (store?.healthData?.isWoundedFresh ?? false),
			waste = num,
			jackson = jackson
		};
		if (Net.IsHost)
		{
			// The game's own return walks the traveller home after this; carried items join the shop in HostTrip.Finish.
			foreach (GameItem item2 in list)
			{
				try
				{
					List<GameItem> list3 = new List<GameItem> { item2 };
					list3.AddRange(GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)item2, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null));
					item2.CloseContentWindow(true);
					item2.Destroy();
					foreach (GameItem item3 in list3)
					{
						Ids.Forget(item3);
					}
				}
				catch (Exception ex3)
				{
					Log.Warn("outing: taking " + item2.identifier + " out of the host's hands threw: " + ex3.Message);
				}
			}
			HostTrip.PendingWent = went;
			HostTrip.Pending = msgOutReturn;
			Log.Info("outing: the host is " + (went ? "coming home" : "cancelling the trip") + " with " + list2.Count + " item(s)");
			return;
		}
		Net.ToServer("outing.return", msgOutReturn);
		Log.Info("outing: " + (went ? "coming home" : "trip cancelled") + " with " + list2.Count + " item(s); waiting for the shop (" + Dropped + " shop update(s) were ignored)");
	}

	internal static void OnSnapshot()
	{
		outPlayers.Clear();
		if (Private)
		{
			Log.Info("outing: private trip ends (the shop is reloading from the server)");
		}
		Private = false;
		returning = false;
	}

	private static void ResetClient(string why)
	{
		if (Private)
		{
			Log.Info("outing: private trip dropped (" + why + ")");
		}
		Private = false;
		returning = false;
		outPlayers.Clear();
	}

	private static void ClientLeft(MsgOutLeft m)
	{
		PlayerStore store = Game.Store;
		if (m == null || store == null)
		{
			return;
		}
		outPlayers.Add(m.by);
		using (Net.Apply())
		{
			store.visitLeftTonight = m.visits;
			store.lastAfterhourOutingDay = m.day;
			if (store.healthData != null)
			{
				store.healthData.wentToAfterhoursLastNight = true;
			}
		}
		if (!DayLog.Replaying && m.by != Net.LocalPlayer)
		{
			Notices.Show(Lang.Trf("{0} went out.", Kit.Name(Net.PlayerName(m.by))), "outing");
		}
	}

	private static void ClientCashed(MsgOutCash m)
	{
		PlayerStore store = Game.Store;
		if (m == null || store == null)
		{
			return;
		}
		using (Net.Apply())
		{
			store.playerCash += m.cash;
			store.wildFavor += m.favor;
		}
	}

	private static void ClientReturned(MsgOutReturned m)
	{
		PlayerStore store = Game.Store;
		if (m == null || store == null)
		{
			return;
		}
		outPlayers.Remove(m.by);
		using (Net.Apply())
		{
			ApplyReturned(store, m);
			store.visitLeftTonight = m.visits;
		}
		if (!DayLog.Replaying && m.by != Net.LocalPlayer)
		{
			Notices.Show(Lang.Trf("{0} is back.", Kit.Name(Net.PlayerName(m.by))), "outing");
		}
	}

	private static void ApplyReturned(PlayerStore store, MsgOutReturned m)
	{
		HealthData healthData = store.healthData;
		if (healthData != null)
		{
			if (m.wound > healthData.woundState)
			{
				healthData.woundState = m.wound;
			}
			if (m.fresh)
			{
				healthData.isWoundedFresh = true;
			}
		}
		if (m.visited != null)
		{
			foreach (string text in m.visited)
			{
				if (string.IsNullOrEmpty(text) || AfterhourLocation.Get(text) == null)
				{
					continue;
				}
				AfterhourLocationState afterhourState = store.GetAfterhourState(text);
				if (afterhourState != null)
				{
					afterhourState.visits++;
					afterhourState.lastVisitDay = StoreStation.Instance.dayCounter;
				}
			}
		}
		if (m.jackson)
		{
			store.daySinceLastBrowseUpgrade = 0;
		}
		if (m.waste > 0)
		{
			StoreClientData storeClientData = PlayerStore.GetStoreClientData();
			if (storeClientData != null)
			{
				storeClientData.nuclearWasteDumped += m.waste;
			}
		}
	}

	private static void ServerGo(MsgOutGo m, int from)
	{
		PlayerStore store = Game.Store;
		string text = (Individual ? DayNightOuting.Common(store) : "Outings are a group trip on this server.");
		if (text == null && away.ContainsKey(from))
		{
			text = "You are already out.";
		}
		if (text == null && (DayNightOuting.MapOpen || DayNightOuting.IsOut))
		{
			text = "The group is out with the host.";
		}
		if (text == null)
		{
			if (store.visitLeftTonight <= 0)
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
		Rec rec = new Rec
		{
			PackId = ((val != null) ? Ids.Of(val) : 0),
			Pack = ((val != null) ? Items.Encode(val) : null),
			Name = Net.PlayerName(from)
		};
		away[from] = rec;
		Net.ToPlayer(from, "outing.go", new MsgOutGo
		{
			backpackId = rec.PackId,
			ok = true
		});
		if (val != null)
		{
			GameItem pack = val;
			ItemsMove.RunServerAction("outing.take", delegate
			{
				pack.Destroy();
			}, from);
		}
		using (Net.Apply())
		{
			store.visitLeftTonight--;
			store.lastAfterhourOutingDay = StoreStation.Instance.dayCounter;
			if (store.healthData != null)
			{
				store.healthData.wentToAfterhoursLastNight = true;
			}
		}
		Net.ToAll("outing.left", new MsgOutLeft
		{
			by = from,
			day = store.lastAfterhourOutingDay,
			visits = store.visitLeftTonight,
			backpackId = rec.PackId
		});
		Log.Info("outing: " + rec.Name + " went out alone (bag " + rec.PackId + "); visits left tonight " + store.visitLeftTonight + "; " + away.Count + " player(s) out");
	}

	private static void ServerCash(MsgOutCash m, int from)
	{
		PlayerStore store = Game.Store;
		if (m == null || store == null || !away.ContainsKey(from) || (m.cash == 0 && m.favor == 0))
		{
			return;
		}
		int num = m.cash;
		if (store.playerCash + num < 0)
		{
			Log.Warn("outing: " + Net.PlayerName(from) + " spent " + -num + " but the shop has " + store.playerCash + "; clamped");
			num = -store.playerCash;
		}
		if (!(Net.IsHost && from == Net.LocalPlayer))
		{
			using (Net.Apply())
			{
				store.playerCash += num;
				store.wildFavor += m.favor;
			}
		}
		Net.ToAll("outing.cashed", new MsgOutCash
		{
			by = from,
			cash = num,
			favor = m.favor
		});
	}

	private static void ServerReturn(MsgOutReturn m, int from)
	{
		PlayerStore store = Game.Store;
		if (m == null || store == null || !away.TryGetValue(from, out var value))
		{
			DayNight.Refuse(from, "return", "You are not out.");
			return;
		}
		away.Remove(from);
		int num = 0;
		int num2 = 0;
		if (m.items != null)
		{
			Subtree[] items = m.items;
			foreach (Subtree subtree in items)
			{
				if (subtree == null || num + num2 >= 96)
				{
					continue;
				}
				subtree.ids = null;
				if (Bring(subtree, fresh: true) != null)
				{
					num++;
				}
				else
				{
					num2++;
				}
			}
		}
		MsgOutReturned msgOutReturned = new MsgOutReturned
		{
			by = from,
			went = m.went,
			wound = m.wound,
			fresh = m.fresh,
			waste = Mathf.Clamp(m.waste, 0, 64),
			jackson = m.jackson
		};
		List<string> list = new List<string>();
		if (m.went && m.visited != null)
		{
			string[] array = m.visited;
			foreach (string text in array)
			{
				if (!string.IsNullOrEmpty(text) && list.Count < 4 && AfterhourLocation.Get(text) != null)
				{
					list.Add(text);
				}
			}
		}
		msgOutReturned.visited = list.ToArray();
		using (Net.Apply())
		{
			ApplyReturned(store, msgOutReturned);
			if (!m.went)
			{
				store.visitLeftTonight++;
			}
		}
		msgOutReturned.visits = store.visitLeftTonight;
		Net.ToAll("outing.returned", msgOutReturned);
		foreach (KeyValuePair<string, int> item in Locks.Table())
		{
			if (item.Value == from && item.Key.StartsWith("outing.", StringComparison.Ordinal))
			{
				Locks.Release(item.Key, from);
			}
		}
		Log.Info("outing: " + value.Name + (m.went ? (" came back from " + string.Join(",", list)) : " cancelled the trip") + " with " + num + " item(s)" + ((num2 > 0) ? (", " + num2 + " could not be brought in") : "") + "; visits left tonight " + store.visitLeftTonight + "; " + away.Count + " player(s) still out");
		if (!(Net.IsHost && from == Net.LocalPlayer))
		{
			Snapshot.SendTo(from, "outing return");
		}
	}

	private static GameItem Bring(Subtree subtree, bool fresh)
	{
		GameItem val = null;
		try
		{
			using (Net.Apply())
			{
				val = Items.Decode(subtree);
				if (val == null)
				{
					return null;
				}
				if (fresh)
				{
					List<GameItem> list = new List<GameItem> { val };
					list.AddRange(GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null));
					foreach (GameItem item in list)
					{
						Ids.Forget(item);
						item.uniqueId = 0;
					}
					foreach (GameItem item2 in list)
					{
						long num = Ids.Assign(item2);
						if (item2.itemFeatures != null)
						{
							foreach (ItemFeature itemFeature in item2.itemFeatures)
							{
								if (itemFeature != null)
								{
									itemFeature.parentItemUniqueId = (int)num;
								}
							}
						}
					}
				}
				EmporiumEntry instance = EmporiumEntry.Instance;
				if (HostTrip.CountersBorrowed)
				{
					HostTrip.AddAtHome(val);
				}
				else
				{
					instance.TryAddToPlayerInv(val);
					if (val.parentInventory == null)
					{
						GraphUtils.EmporiumTryAdd(instance.backInvWindow, val);
					}
				}
			}
			if (val.parentInventory == null)
			{
				Log.Warn("outing: no room in the shop for " + val.identifier + "; it is lost");
				return null;
			}
			ItemsMove.BroadcastSubtree(val, "outing");
			return val;
		}
		catch (Exception ex)
		{
			Log.Error("outing: bringing " + (val?.identifier ?? "an item") + " into the shop threw: " + ex);
			return null;
		}
	}

	private static void ServerPlayerLeft(PlayerInfo p)
	{
		if (!Net.IsServer || p == null || !away.TryGetValue(p.Id, out var value))
		{
			return;
		}
		away.Remove(p.Id);
		bool flag = value.Pack != null && Bring(value.Pack, fresh: false) != null;
		PlayerStore store = Game.Store;
		if (store != null)
		{
			Net.ToAll("outing.returned", new MsgOutReturned
			{
				by = p.Id,
				went = false,
				visited = new string[0],
				visits = store.visitLeftTonight
			});
		}
		Log.Info("outing: " + value.Name + " left the server while out; " + ((value.Pack == null) ? "they carried no bag" : (flag ? "their bag is back in the shop as it was when they left" : "their bag could not be restored")));
	}

	internal static void ServerReset()
	{
		HostTrip.Reset();
		away.Clear();
		outPlayers.Clear();
		Private = false;
		returning = false;
	}
}
