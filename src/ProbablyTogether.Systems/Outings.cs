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

	public Subtree pack;
}

public sealed class MsgOutCash
{
	public int by;

	public int cash;

	public int favor;

	/// Commissary credit redeemed (+) or spent (-); items[i] left the shelves by stock[i] (negative).
	public int credit;

	public string[] items;

	public int[] stock;
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

	public bool stable = true;

	public int waste;

	public bool jackson;

	/// How the shop's faction standings moved through the trip's deals (differences, not values).
	public List<MsgHostRep> rep;

	public Subtree[] sold;
}

public sealed class MsgOutReturned
{
	public int by;

	public bool went;

	public string[] visited;

	public int wound;

	public bool fresh;

	public bool stable = true;

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

	// Commissary state last reported by this trip, and as last seen at home (changes at home are not the trip's).
	private static int lastCredit;

	private static int[] lastStock = new int[0];

	private static int homeCredit;

	private static int[] homeStock = new int[0];

	internal static int CommissarySent;

	internal static int CommissaryTaken;

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
		Snapshot.ClientReady += delegate
		{
			NightLook();
		};
		Net.Disconnected += delegate
		{
			ResetClient("disconnected");
		};
	}

	internal static string LastDoorRefusal = "";

	internal static bool DropsWhilePrivate(string kind)
	{
		return false;
	}

	/// A copy reloading at night shows the closed shop the way a loaded save does: morning report on screen, shutter up.
	private static void NightLook()
	{
		try
		{
			PlayerStore store = Game.Store;
			if (store == null || (int)store.storeState != 0 || !Net.HasLocalPlayer)
			{
				return;
			}
			StartOfDayUIManager instance = StartOfDayUIManager.Instance;
			if ((Object)(object)instance != (Object)null && (Object)(object)instance.UIPanel != (Object)null && instance.UIPanel.activeSelf)
			{
				instance.UIPanel.SetActive(false);
			}
			if ((Object)(object)StoreShutterButton.Instance != (Object)null)
			{
				StoreShutterButton.Instance.ForceImmediateClose();
			}
			Log.Info("outing: reloaded at night: morning report hidden, shutter down");
		}
		catch (Exception ex)
		{
			Log.Warn("outing: tidying up after a reload at night: " + ex.Message);
		}
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
		Subtree pack = m.pack;
		if (Net.IsHost)
		{
			away.TryGetValue(Net.LocalPlayer, out var value);
			pack = value?.Pack;
			if (value == null)
			{
				Log.Warn("outing: the host was let go but the server has no record of it");
				return;
			}
		}
		if (!HostTrip.Begin(pack))
		{
			Log.Warn("outing: let go, but the trip could not start");
			return;
		}
		Private = true;
		// A private trip starts from fresh randomness, not the shared state every copy leaves with.
		int num = unchecked((Environment.TickCount * 397) ^ Guid.NewGuid().GetHashCode() ^ (Net.LocalPlayer * 7919));
		RNG.random = new System.Random(num);
		UnityEngine.Random.InitState(num ^ 0x5F3759DF);
		returning = false;
		backpackId = m.backpackId;
		lastCash = store.playerCash;
		lastFavor = store.wildFavor;
		CommissaryRead(store, ref lastCredit, ref lastStock);
		jackson = false;
		visited.Clear();
		Log.Info("outing: private trip starts (bag " + backpackId + ", cash " + lastCash + "); " + "the shop stays live while we are out");
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
		// Treat the host's own trip as the trip, not the shop at home.
		using (HostTrip.OnTrip())
		{
			TripTick();
		}
	}

	private static void TripTick()
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
		int num3 = 0;
		List<string> list = null;
		List<int> list2 = null;
		CommissaryData commissaryData = store.commissaryData;
		if (commissaryData?.commissaryItemDatas != null)
		{
			num3 = commissaryData.redeemedCredit - lastCredit;
			if (lastStock.Length != commissaryData.commissaryItemDatas.Count)
			{
				CommissaryRead(store, ref lastCredit, ref lastStock);
				num3 = 0;
			}
			for (int i = 0; i < lastStock.Length; i++)
			{
				CommissaryItemData commissaryItemData = commissaryData.commissaryItemDatas[i];
				int num4 = commissaryItemData.StockCount - lastStock[i];
				if (num4 != 0)
				{
					(list ?? (list = new List<string>())).Add(commissaryItemData.identifier);
					(list2 ?? (list2 = new List<int>())).Add(num4);
					lastStock[i] = commissaryItemData.StockCount;
				}
			}
			lastCredit = commissaryData.redeemedCredit;
		}
		if (num != 0 || num2 != 0 || num3 != 0 || list != null)
		{
			lastCash = store.playerCash;
			lastFavor = store.wildFavor;
			if (num3 != 0 || list != null)
			{
				CommissarySent++;
				Log.Info("outing: the commissary on this trip: credit " + ((num3 > 0) ? "+" : "") + num3 + ((list != null) ? (", shelves " + CommissaryText(list.ToArray(), list2.ToArray())) : "") + "; told the server");
			}
			Net.ToServer("outing.cash", new MsgOutCash
			{
				cash = num,
				favor = num2,
				credit = num3,
				items = list?.ToArray(),
				stock = list2?.ToArray()
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
		return true;
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
		return true;
	}

	/// Host: cash moved at home while the host is out is not the trip's doing.
	internal static void HomeCashMoved(int cash, int favor)
	{
		lastCash += cash;
		lastFavor += favor;
	}

	/// Snapshots the commissary when the game briefly switches to the shop at home.
	internal static void HomeCommissaryEnter(PlayerStore store)
	{
		CommissaryRead(store, ref homeCredit, ref homeStock);
	}

	/// Back on the trip: commissary changes since the snapshot came from home.
	internal static void HomeCommissaryLeave(PlayerStore store)
	{
		CommissaryData commissaryData = store?.commissaryData;
		if (commissaryData?.commissaryItemDatas == null || homeStock.Length != commissaryData.commissaryItemDatas.Count || lastStock.Length != homeStock.Length)
		{
			return;
		}
		lastCredit += commissaryData.redeemedCredit - homeCredit;
		for (int i = 0; i < homeStock.Length; i++)
		{
			lastStock[i] += commissaryData.commissaryItemDatas[i].StockCount - homeStock[i];
		}
	}

	private static void CommissaryRead(PlayerStore store, ref int credit, ref int[] stock)
	{
		CommissaryData commissaryData = store?.commissaryData;
		int num = commissaryData?.commissaryItemDatas?.Count ?? 0;
		if (stock.Length != num)
		{
			stock = new int[num];
		}
		credit = commissaryData?.redeemedCredit ?? 0;
		for (int i = 0; i < num; i++)
		{
			stock[i] = commissaryData.commissaryItemDatas[i].StockCount;
		}
	}

	private static string CommissaryText(string[] items, int[] stock)
	{
		if (items == null || stock == null)
		{
			return "-";
		}
		List<string> list = new List<string>();
		for (int i = 0; i < items.Length && i < stock.Length; i++)
		{
			list.Add(items[i] + " " + ((stock[i] > 0) ? "+" : "") + stock[i]);
		}
		return string.Join(", ", list);
	}

	/// Applies a trip's commissary credit and shelves, clamped at zero since two trips may spend the same credit or stock.
	private static int CommissaryApply(PlayerStore store, int credit, string[] items, int[] stock, string who)
	{
		CommissaryData commissaryData = store?.commissaryData;
		if (commissaryData == null || (credit == 0 && items == null))
		{
			return credit;
		}
		int num = credit;
		if (commissaryData.redeemedCredit + num < 0)
		{
			if (Net.IsServer)
			{
				Log.Warn("outing: " + who + " spent " + -num + " commissary credit but the shop has " + commissaryData.redeemedCredit + "; clamped");
			}
			num = -commissaryData.redeemedCredit;
		}
		commissaryData.redeemedCredit += num;
		for (int i = 0; items != null && stock != null && i < items.Length && i < stock.Length; i++)
		{
			CommissaryItemData itemData = commissaryData.GetItemData(items[i]);
			if (itemData != null)
			{
				itemData.StockCount = Math.Max(0, itemData.StockCount + stock[i]);
			}
		}
		return num;
	}

	internal static HashSet<GameItem> CarriedNow()
	{
		HashSet<GameItem> hashSet = new HashSet<GameItem>();
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return hashSet;
		}
		GraphNodeStorage[] array = new GraphNodeStorage[4]
		{
			(GraphNodeStorage)(object)instance.afterhourPocketSlotInvBackpack,
			(GraphNodeStorage)(object)instance.afterhourPocketSlotInvRight,
			(GraphNodeStorage)(object)instance.afterhourPocketSlotInvLeft,
			(GraphNodeStorage)(object)instance.afterhourInventory
		};
		foreach (GraphNodeStorage val in array)
		{
			if (val == null)
			{
				continue;
			}
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				if (item != null)
				{
					hashSet.Add(item);
				}
			}
		}
		return hashSet;
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
				GameItem val4 = instance.afterhourPocketSlotInvBackpack.childItem;
				if (val4 != null)
				{
					list.Add(val4);
				}
				if (instance.afterhourInventory != null)
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
			stable = (store?.healthData?.isWoundStable ?? true),
			waste = num,
			jackson = jackson
		};
		if (Net.HasLocalPlayer)
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
			Log.Info("outing: " + (Net.IsHost ? "the host is " : "") + (went ? "coming home" : "cancelling the trip") + " with " + list2.Count + " item(s)");
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
		if (!Net.IsServer)
		{
			HostTrip.Reset();
		}
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
		if (!Net.IsServer)
		{
			HostTrip.Reset();
		}
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
		if (m.by == Net.LocalPlayer && !DayLog.Replaying)
		{
			return;
		}
		using (Net.Apply())
		{
			store.playerCash += m.cash;
			store.wildFavor += m.favor;
			if (m.credit != 0 || m.items != null)
			{
				CommissaryApply(store, m.credit, m.items, m.stock, Net.PlayerName(m.by));
				CommissaryTaken++;
			}
		}
		if ((m.credit != 0 || m.items != null) && !DayLog.Replaying)
		{
			try
			{
				CommissaryUIManager instance = CommissaryUIManager.Instance;
				if ((Object)(object)instance != (Object)null && Economy.CommissaryWindowOpen)
				{
					instance.RefreshUI();
				}
			}
			catch
			{
			}
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
		if (Wounds.Active)
		{
			// Only the traveller's own wound is updated.
			Wounds.Returned(m.by, m.wound, m.stable, m.fresh);
		}
		else if (healthData != null)
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
			else
			{
				text = Bays.Refusal(from, (GraphNodeStorage)(object)val.parentInventory);
			}
		}
		if (text == null && StoreClientData.IsButcherHunting() && (val == null || !GunHelper.HasGunInside(val)))
		{
			text = LocHelper.GetLocalizedMechanic("response_afterhours_need_gun", new object[1] { LocHelper.GetLocalizedClientName("name_the_butcher") });
		}
		if (text != null)
		{
			LastDoorRefusal = Net.PlayerName(from) + ": " + text;
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
			ok = true,
			pack = ((Net.IsHost && from == Net.LocalPlayer) ? null : rec.Pack)
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
		if (m == null || store == null || !away.ContainsKey(from) || (m.cash == 0 && m.favor == 0 && m.credit == 0 && m.items == null))
		{
			return;
		}
		int num = m.cash;
		// The host's own game is the shop's cash, so its trip spending is already deducted.
		bool flag = Net.IsHost && from == Net.LocalPlayer;
		if (!flag && store.playerCash + num < 0)
		{
			Log.Warn("outing: " + Net.PlayerName(from) + " spent " + -num + " but the shop has " + store.playerCash + "; clamped");
			num = -store.playerCash;
		}
		int credit = m.credit;
		if (!(Net.IsHost && from == Net.LocalPlayer))
		{
			using (Net.Apply())
			{
				store.playerCash += num;
				store.wildFavor += m.favor;
				// Commissary credit and shelves are shared, like cash.
				credit = CommissaryApply(store, m.credit, m.items, m.stock, Net.PlayerName(from));
			}
		}
		if (m.credit != 0 || m.items != null)
		{
			CommissaryTaken++;
			Log.Info("outing: " + Net.PlayerName(from) + " at the commissary: credit " + ((credit > 0) ? "+" : "") + credit + ", shelves " + CommissaryText(m.items, m.stock) + "; the shop now has " + (store.commissaryData?.redeemedCredit ?? 0) + " credit");
		}
		Net.ToAll("outing.cashed", new MsgOutCash
		{
			by = from,
			cash = num,
			favor = m.favor,
			credit = credit,
			items = m.items,
			stock = m.stock
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
		if (!(Net.IsHost && from == Net.LocalPlayer))
		{
			try
			{
				HostTrip.ServerSettle(store, m, value.Name);
			}
			catch (Exception ex)
			{
				Log.Error("outing: settling " + value.Name + "'s trip with the shop threw: " + ex);
			}
		}
		MsgOutReturned msgOutReturned = new MsgOutReturned
		{
			by = from,
			went = m.went,
			wound = m.wound,
			fresh = m.fresh,
			stable = m.stable,
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
	// No snapshot for the traveller: its game kept the shop live while out.
	}

	/// Gives an item and everything in it new numbers from this copy's counter (the trip's own range while travelling).
	internal static void Renumber(GameItem val)
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
			long num;
			if (Net.IsServer)
			{
				num = Ids.Assign(item2);
			}
			else
			{
				// On a travelling player's game the counter is in the trip's own range (HostTrip.Begin).
				PlayerStore store = Game.Store;
				num = ++store.currentUniqueId;
				item2.uniqueId = (int)num;
				Ids.Bind(item2, num);
			}
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
					Renumber(val);
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
