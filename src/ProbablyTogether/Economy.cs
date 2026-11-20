using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

public static class Economy
{
	public const string CommissaryBuy = "buy";

	public const string CommissaryStamps = "stamps";

	public const string CommissaryLottery = "lottery";

	public const string ReminderId = "wild_upgrade_reminder";

	public const string IntroId = "wild2";

	public const string CommissaryLock = "outing.commissary";

	public const string CommissaryLocation = "commissary";

	public const string RunOverWhy = "The run has ended.";

	private static long lastTickMs;

	private static long lastWindowTouchMs;

	private static float lastIntroFallback = -100f;

	private static readonly FieldInfo selectedIdField = AccessTools.Field(typeof(WildNetworkUI), "selectedId");

	private static readonly FieldInfo windowField = AccessTools.Field(typeof(WildNetworkUI), "window");

	private static readonly FieldInfo frameField = AccessTools.Field(typeof(WildNetworkUI), "frame");

	private static readonly FieldInfo tabViewsField = AccessTools.Field(typeof(WildNetworkUI), "tabViews");

	private static readonly FieldInfo slotsField = AccessTools.Field(typeof(WildNetworkUI), "slots");

	private static readonly FieldInfo legacySelectedField = AccessTools.Field(typeof(WildUIManager), "currentSelectedId");

	private static readonly FieldInfo tabletRowsField = AccessTools.Field(typeof(EndOfDayUIManager), "rows");

	private static int networkOpenBy = -1;

	private static int commissaryOpenBy = -1;

	private static bool windowAsked;

	private static bool replayWindow;

	public static int Applied { get; private set; }

	public static int Refusals { get; private set; }

	public static string LastRefusal { get; private set; } = "";

	public static int Mismatches { get; private set; }

	public static int Blocked { get; private set; }

	public static string LastBlocked { get; private set; } = "";

	public static int WindowsSkipped { get; private set; }

	public static bool NetworkWindowOpen
	{
		get
		{
			if (networkOpenBy < 0)
			{
				if (IntroBrowsing)
				{
					return CounterCustomer() == "wild2";
				}
				return false;
			}
			return true;
		}
	}

	public static bool CommissaryOpenedByGame => commissaryOpenBy >= 0;

	public static int ClosedWindowRefusals { get; private set; }

	internal static bool IntroBrowsing
	{
		get
		{
			TutorialUIManager instance = TutorialUIManager.Instance;
			if ((Object)(object)instance != (Object)null)
			{
				return instance.isIntroBrowsingUpgrade;
			}
			return false;
		}
		set
		{
			TutorialUIManager instance = TutorialUIManager.Instance;
			if ((Object)(object)instance != (Object)null)
			{
				instance.isIntroBrowsingUpgrade = value;
			}
		}
	}

	public static bool CommissaryWindowOpen
	{
		get
		{
			CommissaryUIManager instance = CommissaryUIManager.Instance;
			if ((Object)(object)instance != (Object)null && (Object)(object)instance.panel != (Object)null)
			{
				return instance.panel.activeSelf;
			}
			return false;
		}
	}

	public static int GameErrors { get; private set; }

	[SystemInit]
	private static void Init()
	{
		Net.On<MsgEconBuy>("econ.network.buy", OnBuyRequest, null);
		Net.On<MsgEconBought>("econ.network.bought", null, OnBought);
		Net.On<MsgEconBrowse>("econ.network.browse", OnBrowseRequest, null);
		Net.On<MsgEconBrowsed>("econ.network.browsed", null, OnBrowsed);
		Net.On<MsgEconClose>("econ.network.close", OnCloseRequest, null);
		Net.On<MsgEconClosed>("econ.network.closed", null, OnClosed);
		Net.On<MsgEconToggle>("econ.service.toggle", OnToggleRequest, null);
		Net.On<MsgEconService>("econ.service", null, OnService);
		Net.On<MsgEconCommissary>("econ.commissary", OnCommissaryRequest, null);
		Net.On<MsgEconCommissaryDone>("econ.commissary.done", null, OnCommissaryDone);
		Net.On<MsgEconRedeemHome>("econ.redeemHome", OnRedeemHomeRequest, null);
		Net.On<MsgEconRedeemHome>("econ.redeemedHome", null, OnRedeemedHome);
		Net.On<MsgEconWindow>("econ.commissary.open", OnWindowOpenRequest, null);
		Net.On<MsgEconWindow>("econ.commissary.close", OnWindowCloseRequest, null);
		Net.On<MsgEconFill>("econ.request.fill", OnFillRequest, null);
		Net.On<MsgEconFilled>("econ.request.filled", null, OnFilled);
		Net.On<MsgEconRefused>("econ.refused", null, OnRefused);
		Digest.AddPart("economy", Hash);
		Net.PlayerLeft += OnPlayerLeftServer;
		DayNight.DayEnded += delegate
		{
			if (Net.IsServer)
			{
				networkOpenBy = -1;
				commissaryOpenBy = -1;
			}
		};
		try
		{
			Presence.DescribeLock("outing.commissary", (string _) => "is trading at the commissary");
		}
		catch (Exception ex)
		{
			Log.Warn("economy: lock label: " + ex.Message);
		}
	}

	[SystemTick]
	private static void Tick()
	{
		if (Net.IsClient)
		{
			long ms = Clock.Ms;
			if (ms - lastTickMs >= 1000)
			{
				lastTickMs = ms;
				IntroFallback();
				KeepWindowLock(ms);
			}
		}
	}

	public static void RequestBuy(string id)
	{
		if (Net.IsClient && !string.IsNullOrEmpty(id))
		{
			Net.ToServer("econ.network.buy", new MsgEconBuy
			{
				id = id
			});
		}
	}

	private static void OnBuyRequest(MsgEconBuy m, int from)
	{
		ServerBuy(m?.id, from);
	}

	internal static string ServerBuy(string id, int by)
	{
		NetworkUpgrade up;
		string text = CheckBuy(id, out up);
		if (text == null && by > 0 && !NetworkWindowOpen)
		{
			ClosedWindowRefusals++;
			text = "the network window is not open";
		}
		if (text == null)
		{
			// An entry that belongs to a trade is its holder's alone.
			text = Systems.Specs.WildRefusal(by, id);
		}
		if (text != null)
		{
			Refuse(by, "network.buy " + id, text);
			return text;
		}
		PlayerStore store = Game.Store;
		int cost = up.GetCost();
		Seed seed;
		int favor;
		int cash;
		HashSet<GameItem> back = Systems.Specs.BackStorageNow();
		using (Replay.Server(out seed))
		{
			Pay(store, cost, out favor, out cash);
			int c = cash;
			Game_("network.buy " + id, delegate
			{
				RunStats.OnWildUpgradeBought(c);
				up.Unlock();
			});
		}
		// The server's own purchase (by 0) on a host is the host player's; it goes into their bay.
		int buyer = ((by <= 0 && Net.HasLocalPlayer) ? Net.LocalPlayer : by);
		Systems.Specs.WildSetToBay(buyer, id, back);
		Applied++;
		MsgEconBought msg = new MsgEconBought
		{
			id = id,
			by = buyer,
			seed = seed,
			cost = cost,
			favor = favor,
			cash = cash,
			d = Replay.Digest(),
			p = Parts()
		};
		Net.ToAll("econ.network.bought", msg);
		Systems.Specs.WildBought(buyer, id);
		HostRefresh("network", by);
		Log.Info("economy: " + Net.PlayerName(by) + " bought " + id + " for " + cost + " (favor " + favor + ", cash " + cash + "); state " + up.state + ", cash now " + store.playerCash + ", favor " + store.wildFavor);
		return null;
	}

	internal static string CheckBuy(string id, out NetworkUpgrade up)
	{
		up = null;
		if (!Game.ShopLoaded)
		{
			return "the shop is not open";
		}
		if (DayNightRun.RunOver)
		{
			return "The run has ended.";
		}
		if (string.IsNullOrEmpty(id) || id.Length > 64)
		{
			return "there is no such upgrade";
		}
		PlayerStore store = Game.Store;
		if (store.networkUpgrade == null || !store.networkUpgrade.TryGetValue(id, out up) || up == null)
		{
			return "there is no such upgrade";
		}
		if (NetworkUpgrade.IsHiddenForRun(id))
		{
			return "that upgrade is not offered in this run";
		}
		if (up.IsUnlocked())
		{
			return "it is already bought";
		}
		if (!up.IsReady())
		{
			return "it is not available yet";
		}
		int cost = up.GetCost();
		if (cost > store.playerCash + store.wildFavor)
		{
			return "the shop cannot afford it (" + cost + " needed)";
		}
		return null;
	}

	private static void Pay(PlayerStore ps, int cost, out int favor, out int cash)
	{
		if (ps.wildFavor >= cost)
		{
			ps.wildFavor -= cost;
			favor = cost;
			cash = 0;
		}
		else
		{
			favor = ps.wildFavor;
			cash = cost - ps.wildFavor;
			ps.wildFavor = 0;
			ps.playerCash -= cash;
		}
	}

	private static void OnBought(MsgEconBought m)
	{
		PlayerStore store = Game.Store;
		if (store?.networkUpgrade == null || m.id == null || !store.networkUpgrade.TryGetValue(m.id, out var up) || up == null)
		{
			NoteMismatch("network.bought: this copy has no upgrade " + m.id);
			return;
		}
		HashSet<GameItem> back = Systems.Specs.BackStorageNow();
		using (Replay.Client(m.seed))
		{
			store.wildFavor -= m.favor;
			store.playerCash -= m.cash;
			Game_("network.bought " + m.id, delegate
			{
				RunStats.OnWildUpgradeBought(m.cash);
				up.Unlock();
			});
		}
		Systems.Specs.WildSetToBay(m.by, m.id, back);
		Applied++;
		if (m.by == Net.LocalPlayer && !DayLog.Replaying)
		{
			try
			{
				EmporiumAnalytics.TrackWildUpgradePurchased(m.id);
			}
			catch
			{
			}
			try
			{
				if ((Object)(object)EmporiumAudioPlayer.Instance != (Object)null)
				{
					EmporiumAudioPlayer.Instance.PlayRegister();
				}
			}
			catch
			{
			}
		}
		if (!DayLog.Replaying)
		{
			RefreshNetworkUi();
		}
		Verify("network.bought " + m.id, m.d, m.p);
	}

	internal static string SelectedUpgrade()
	{
		return selectedIdField?.GetValue(null) as string;
	}

	internal static void SelectUpgrade(string id)
	{
		selectedIdField?.SetValue(null, id);
	}

	internal static string LegacySelected(WildUIManager ui)
	{
		if (!((Object)(object)ui == (Object)null))
		{
			return legacySelectedField?.GetValue(ui) as string;
		}
		return null;
	}

	/// Host mode: the host never receives its own broadcasts, so refresh its UI here after its own actions.
	private static void HostRefresh(string what, int by)
	{
		if (!Net.IsHost || by != Net.LocalPlayer || DayLog.Replaying)
		{
			return;
		}
		try
		{
			switch (what)
			{
			case "network":
				RefreshNetworkUi();
				if ((Object)(object)EmporiumAudioPlayer.Instance != (Object)null)
				{
					EmporiumAudioPlayer.Instance.PlayRegister();
				}
				break;
			case "service":
				ShowServiceOnTablet(null);
				break;
			case "commissary":
			{
				CommissaryUIManager instance = CommissaryUIManager.Instance;
				if (instance != null)
				{
					instance.RefreshUI();
				}
				break;
			}
			case "fill":
			{
				PCUIManager instance2 = PCUIManager.Instance;
				if (instance2 != null)
				{
					instance2.OnChange();
					StorePC storePC = instance2.storePC;
					if (storePC != null)
					{
						storePC.RefreshState();
					}
				}
				break;
			}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("economy: host refresh (" + what + "): " + ex.Message);
		}
	}

	private static void RefreshNetworkUi()
	{
		try
		{
			WildNetworkUI.RefreshAll();
		}
		catch (Exception ex)
		{
			Log.Warn("economy: network window refresh: " + ex.Message);
		}
	}

	internal static void BrowseState()
	{
		PlayerStore store = Game.Store;
		if (store?.networkUpgrade != null)
		{
			NetworkUpgrade.InvalidateDefinitions();
			NetworkUpgrade.AddMissingAfterLoad();
			store.daySinceLastBrowseUpgrade = 0;
		}
	}

	internal static void ServerWindowOpened(string which)
	{
		if (Net.IsServer)
		{
			int num = ((Net.Actor > 0) ? Net.Actor : (Net.IsHost ? Net.LocalPlayer : 0));
			if (which == "network")
			{
				networkOpenBy = num;
			}
			else
			{
				commissaryOpenBy = num;
			}
			Log.Info("economy: the " + which + " window opened on the server" + ((num > 0) ? (" for " + Net.PlayerName(num)) : ""));
		}
	}

	private static void ServerWindowClosed(string which, int from)
	{
		if (which == "network")
		{
			if (networkOpenBy == 0 || networkOpenBy == from)
			{
				networkOpenBy = -1;
			}
		}
		else if (commissaryOpenBy == 0 || commissaryOpenBy == from)
		{
			commissaryOpenBy = -1;
		}
	}

	private static void OnPlayerLeftServer(PlayerInfo p)
	{
		if (Net.IsServer && p != null)
		{
			bool flag = Net.Players.Count == 0 || (Net.Players.Count == 1 && Net.Players.ContainsKey(p.Id));
			if (networkOpenBy == p.Id || (networkOpenBy == 0 && flag))
			{
				networkOpenBy = -1;
			}
			if (commissaryOpenBy == p.Id || (commissaryOpenBy == 0 && flag))
			{
				commissaryOpenBy = -1;
			}
		}
	}

	public static bool UiForMe()
	{
		if ((Net.IsClient || Net.IsHost) && !DayLog.Replaying)
		{
			return !DialogueSync.UiForOthers;
		}
		return false;
	}

	internal static void SendBrowse()
	{
		if (Net.IsClient && Net.IsLive)
		{
			Net.ToServer("econ.network.browse", new MsgEconBrowse());
		}
	}

	private static void OnBrowseRequest(MsgEconBrowse m, int from)
	{
		ServerBrowse(from);
	}

	internal static void ServerBrowse(int by)
	{
		PlayerStore store = Game.Store;
		if (Game.ShopLoaded && store?.networkUpgrade != null && !DayNightRun.RunOver && store.daySinceLastBrowseUpgrade != 0)
		{
			using (Net.Apply())
			{
				BrowseState();
			}
			Net.ToAll("econ.network.browsed", new MsgEconBrowsed
			{
				by = by
			});
		}
	}

	private static void OnBrowsed(MsgEconBrowsed m)
	{
		using (Net.Apply())
		{
			BrowseState();
		}
		Applied++;
	}

	internal static void OnWindowClosedClient()
	{
		try
		{
			windowField?.SetValue(null, null);
			frameField?.SetValue(null, null);
			(tabViewsField?.GetValue(null) as IList)?.Clear();
			(slotsField?.GetValue(null) as IList)?.Clear();
		}
		catch (Exception ex)
		{
			Log.Warn("economy: clearing the network window: " + ex.Message);
		}
		Click();
		bool introBrowsing = IntroBrowsing;
		bool reminder = !introBrowsing && CounterCustomer() == "wild_upgrade_reminder";
		if (introBrowsing)
		{
			lastIntroFallback = Time.realtimeSinceStartup;
		}
		if (Net.IsLive && !DayLog.Replaying)
		{
			Net.ToServer("econ.network.close", new MsgEconClose
			{
				intro = introBrowsing,
				reminder = reminder
			});
		}
	}

	internal static string CounterCustomer()
	{
		try
		{
			PlayerStore store = Game.Store;
			object obj;
			if (store == null)
			{
				obj = null;
			}
			else
			{
				StoreClientInstance currentClientInstance = store.currentClientInstance;
				obj = ((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.identifier);
			}
			if (obj == null)
			{
				obj = "";
			}
			return (string)obj;
		}
		catch
		{
			return "";
		}
	}

	private static void OnCloseRequest(MsgEconClose m, int from)
	{
		if (!Game.ShopLoaded)
		{
			return;
		}
		bool flag = IntroBrowsing || (m != null && m.intro && CounterCustomer() == "wild2" && Game.Store.isNetworkIntroduced && (Object)(object)TutorialUIManager.Instance == (Object)null);
		bool flag2 = !flag && CounterCustomer() == "wild_upgrade_reminder";
		if (!flag && !flag2)
		{
			ServerWindowClosed("network", from);
			return;
		}
		if (DayNightRun.RunOver)
		{
			Refuse(from, "network.close", "The run has ended.");
			return;
		}
		if (!Customers.TryCounter(from, out var refusal))
		{
			Refuse(from, "network.close", refusal);
			return;
		}
		ServerWindowClosed("network", from);
		Net.ToAll("econ.network.closed", new MsgEconClosed
		{
			by = from,
			intro = flag,
			reminder = flag2
		});
		Applied++;
		Log.Info("economy: " + Net.PlayerName(from) + " closed the network window" + (flag ? " after the network intro: Wild goes on" : ": the upgrade reminder leaves"));
		if (flag)
		{
			IntroBrowsing = false;
			DialogUIManager.Instance.StartDialogue(StoreClientDialogList.WildNetworkContinue(), false);
		}
		else
		{
			Game.Store.DismissCurrentClient(false, true, true);
		}
	}

	private static void OnClosed(MsgEconClosed m)
	{
		Applied++;
		if (!m.intro)
		{
			return;
		}
		IntroBrowsing = false;
		try
		{
			DialogUIManager.Instance.StartDialogue(StoreClientDialogList.WildNetworkContinue(), false);
		}
		catch (Exception ex)
		{
			Log.Warn("economy: preparing Wild's continue dialogue: " + ex.Message);
		}
	}

	private static void IntroFallback()
	{
		if (!Net.IsClient || !Net.IsLive || DayLog.Replaying || Snapshot.IsLoading || Time.realtimeSinceStartup - lastIntroFallback < 10f || !IntroBrowsing || CounterCustomer() != "wild2" || WildNetworkUI.IsOpen)
		{
			return;
		}
		int num = Locks.HeldBy("counter");
		int localPlayer = Net.LocalPlayer;
		if (num != -1 && num != localPlayer)
		{
			return;
		}
		if (num == -1)
		{
			foreach (int key in Net.Players.Keys)
			{
				if (key < localPlayer)
				{
					return;
				}
			}
		}
		lastIntroFallback = Time.realtimeSinceStartup;
		Log.Info("economy: the network intro waits on a window nobody has open; opening it here");
		try
		{
			WildNetworkUI.Open(false);
		}
		catch (Exception ex)
		{
			Log.Warn("economy: opening the network window: " + ex.Message);
		}
	}

	public static void RequestToggle(string id, bool on)
	{
		if (Net.IsClient && !string.IsNullOrEmpty(id))
		{
			Net.ToServer("econ.service.toggle", new MsgEconToggle
			{
				id = id,
				on = on
			});
		}
	}

	private static void OnToggleRequest(MsgEconToggle m, int from)
	{
		ServerToggle(m?.id, m?.on ?? false, from);
	}

	internal static string ServerToggle(string id, bool on, int by)
	{
		StoreService s;
		string text = CheckToggle(id, out s);
		if (text != null)
		{
			Refuse(by, "service " + id, text);
			return text;
		}
		using (Net.Apply())
		{
			s.isUsing = on;
		}
		Applied++;
		Net.ToAll("econ.service", new MsgEconService
		{
			id = id,
			on = on,
			by = by,
			p = Parts()
		});
		HostRefresh("service", by);
		Log.Info("economy: " + Net.PlayerName(by) + " turned " + id + (on ? " on" : " off"));
		return null;
	}

	internal static string CheckToggle(string id, out StoreService s)
	{
		s = null;
		if (!Game.ShopLoaded)
		{
			return "the shop is not open";
		}
		if (DayNightRun.RunOver)
		{
			return "The run has ended.";
		}
		if (string.IsNullOrEmpty(id) || id.Length > 64)
		{
			return "there is no such service";
		}
		s = StoreService.FindServiceByID(id);
		if (s == null)
		{
			return "there is no such service";
		}
		if (!s.unlocked || (s.id == "POWER_SERVICE" && NetworkUpgrade.IsPowerHijackUnlocked()))
		{
			return "that service is not offered";
		}
		if (s.isMandatory)
		{
			return "that service cannot be turned off";
		}
		if (s.IsOnCooldown())
		{
			return "that service is not available tonight";
		}
		return null;
	}

	private static void OnService(MsgEconService m)
	{
		StoreService val = StoreService.FindServiceByID(m.id ?? "");
		if (val == null)
		{
			NoteMismatch("service: this copy has no service " + m.id);
			return;
		}
		using (Net.Apply())
		{
			val.isUsing = m.on;
		}
		Applied++;
		if (!DayLog.Replaying)
		{
			ShowServiceOnTablet(val);
		}
		Verify("service " + m.id, 0L, m.p);
	}

	internal static void ShowServiceOnTablet(StoreService s)
	{
		EndOfDayUIManager instance = EndOfDayUIManager.Instance;
		if ((Object)(object)instance == (Object)null || !instance.IsOpen)
		{
			return;
		}
		try
		{
			if (tabletRowsField?.GetValue(instance) is List<(StoreService, TabletCheckbox)> list)
			{
				foreach (var item2 in list)
				{
					if (s == null || item2.Item1 == s)
					{
						TabletCheckbox item = item2.Item2;
						if (item != null)
						{
							item.SetOn(item2.Item1.isUsing);
						}
					}
				}
			}
			instance.RefreshUI();
		}
		catch (Exception ex)
		{
			Log.Warn("economy: tablet refresh: " + ex.Message);
		}
	}

	internal static void HealServices()
	{
		try
		{
			if (Game.Store?.storeService != null)
			{
				StoreService.HealIntroducedAfterLoad();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("economy: service heal: " + ex.Message);
		}
	}

	internal static void NormalizeServices(PlayerStore ps)
	{
		if (ps?.storeService == null)
		{
			return;
		}
		StoreService debrisRemovalService = StoreService.GetDebrisRemovalService();
		if (debrisRemovalService != null)
		{
			debrisRemovalService.UpdateCost();
		}
		bool flag = NetworkUpgrade.IsPowerHijackUnlocked();
		foreach (StoreService item in ps.storeService)
		{
			if (item != null && item.unlocked && !(item.id == "POWER_SERVICE" && flag))
			{
				bool num = item.IsOnCooldown();
				if (item.isMandatory)
				{
					item.isUsing = true;
				}
				if (num)
				{
					item.isUsing = false;
				}
			}
		}
	}

	public static void RequestCommissary(string op, string item = null)
	{
		if (Net.IsClient)
		{
			Net.ToServer("econ.commissary", new MsgEconCommissary
			{
				op = op,
				item = item
			});
		}
	}

	private static void OnCommissaryRequest(MsgEconCommissary m, int from)
	{
		ServerCommissary(m?.op, m?.item, from);
	}

	internal static string ServerCommissary(string op, string item, int by)
	{
		string text = CheckCommissary(op, item, by);
		if (text != null)
		{
			Refuse(by, "commissary " + op, text);
			return text;
		}
		PlayerStore store = Game.Store;
		Seed seed;
		using (Replay.Server(out seed))
		{
			Game_("commissary " + op, delegate
			{
				RunCommissary(op, item);
			});
		}
		Applied++;
		Net.ToAll("econ.commissary.done", new MsgEconCommissaryDone
		{
			op = op,
			item = item,
			by = by,
			seed = seed,
			credit = store.commissaryData.redeemedCredit,
			cash = store.playerCash,
			d = Replay.Digest(),
			p = Parts()
		});
		HostRefresh("commissary", by);
		Log.Info("economy: " + Net.PlayerName(by) + " commissary " + op + ((item != null) ? (" " + item) : "") + ": credit now " + store.commissaryData.redeemedCredit + ", cash " + store.playerCash);
		return null;
	}

	internal static string CheckCommissary(string op, string item, int by)
	{
		if (!Game.ShopLoaded)
		{
			return "the shop is not open";
		}
		if (DayNightRun.RunOver)
		{
			return "The run has ended.";
		}
		PlayerStore store = Game.Store;
		if (store.commissaryData == null || !Systems.Perks.CommissaryOpenFor((by > 0) ? Systems.Members.SeatOf(by) : Systems.Perks.Actor))
		{
			return "the commissary is not open to you";
		}
		if (by > 0)
		{
			if (AtCommissary())
			{
				int num = Locks.Holder("outing.commissary");
				if (num >= 0 && num != by)
				{
					return Net.PlayerName(num) + " is trading at the commissary.";
				}
				if (num != by)
				{
					ClosedWindowRefusals++;
					return "the commissary trade window is not open";
				}
			}
			else if (!CommissaryOpenedByGame)
			{
				ClosedWindowRefusals++;
				return "the commissary is not open here (the group is not at the commissary)";
			}
		}
		switch (op)
		{
		case "buy":
		{
			if (string.IsNullOrEmpty(item) || item.Length > 64)
			{
				return "there is no such item";
			}
			CommissaryItemData itemData = store.commissaryData.GetItemData(item);
			if (itemData == null)
			{
				return "the commissary does not sell that";
			}
			if (itemData.StockCount <= 0)
			{
				return "out of stock";
			}
			if (store.commissaryData.redeemedCredit < itemData.price)
			{
				return "not enough tokens (" + itemData.price + " needed)";
			}
			return null;
		}
		case "stamps":
			if (CountOwned("FOOD_STAMP_TAG") <= 0)
			{
				return "the shop has no food stamps";
			}
			return null;
		case "lottery":
			if (CountOwned("LOTTERY_IS_WINNING_TAG") <= 0)
			{
				return "the shop has no winning tickets";
			}
			return null;
		default:
			return "unknown commissary action";
		}
	}

	private static int CountOwned(string tag)
	{
		int num = 0;
		foreach (GameItem allOwnedItem in EmporiumEntry.Instance.GetAllOwnedItems(false, true))
		{
			if (allOwnedItem != null && allOwnedItem.IsTag(tag))
			{
				num++;
			}
		}
		return num;
	}

	private static void RunCommissary(string op, string item)
	{
		PlayerStore store = Game.Store;
		switch (op)
		{
		case "buy":
			store.commissaryData.BuyItem(item);
			break;
		case "stamps":
			CommissaryData.RedeemAllStampInInventory();
			break;
		case "lottery":
			RedeemLottery();
			break;
		}
	}

	private static int RedeemLottery()
	{
		int num = 0;
		foreach (GameItem item in new List<GameItem>(EmporiumEntry.Instance.GetAllOwnedItems(false, true)))
		{
			if (item != null && item.IsTag("LOTTERY_IS_WINNING_TAG"))
			{
				num += (int)item.unitValue;
				item.Destroy();
			}
		}
		Game.Store.ModCash(num, true);
		return num;
	}

	/// Redeemed on own trips: carried items here, server requests at home.
	internal static int RedeemedCarried;

	internal static int RedeemedHomeAsked;

	internal static int RedeemedHome;

	private static string RedeemTag(string op)
	{
		if (op == "stamps")
		{
			return "FOOD_STAMP_TAG";
		}
		if (op == "lottery")
		{
			return "LOTTERY_IS_WINNING_TAG";
		}
		return null;
	}

	/// "Redeem all" on one's own trip: carried items redeem here; the server redeems what lies in the shop at home.
	internal static void RedeemOnTrip(string op)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		PlayerStore store = Game.Store;
		string text = RedeemTag(op);
		if ((Object)(object)instance == (Object)null || store == null || text == null)
		{
			return;
		}
		HashSet<GameItem> hashSet = Outings.CarriedNow();
		List<long> list = new List<long>();
		int num = 0;
		int num2 = 0;
		foreach (GameItem item in new List<GameItem>(instance.GetAllOwnedItems(false, true)))
		{
			if (item == null || !item.IsTag(text))
			{
				continue;
			}
			if (!hashSet.Contains(item))
			{
				long num3 = Ids.Of(item);
				if (num3 > 0)
				{
					list.Add(num3);
				}
				continue;
			}
			num++;
			GameItem it = item;
			Game_("commissary " + op + " (carried)", delegate
			{
				if (op == "stamps")
				{
					it.ActivateSlotItem();
				}
				else
				{
					num2 += (int)it.unitValue;
					it.Destroy();
				}
			});
		}
		if (num2 != 0)
		{
			store.ModCash(num2, true);
		}
		RedeemedCarried += num;
		if (list.Count > 0)
		{
			RedeemedHomeAsked += list.Count;
			Net.ToServer("econ.redeemHome", new MsgEconRedeemHome
			{
				op = op,
				ids = list.ToArray()
			});
		}
		Log.Info("economy: commissary " + op + " on this player's own trip: " + num + " carried redeemed here" + ((num2 != 0) ? (" (" + num2 + " cash)") : "") + ", " + list.Count + " in the shop at home asked of the server; credit here " + (store.commissaryData?.redeemedCredit ?? 0));
		RedeemUi(op, num2);
	}

	private static void RedeemUi(string op, int cash)
	{
		try
		{
			CommissaryUIManager instance = CommissaryUIManager.Instance;
			if ((Object)(object)instance != (Object)null && CommissaryWindowOpen)
			{
				instance.RefreshUI();
			}
			if (op == "lottery" && cash != 0)
			{
				ReputationUIManager instance2 = ReputationUIManager.Instance;
				if (instance2 != null)
				{
					instance2.Notify(LocHelper.GetLocalizedMechanic("mech_commissary_lottery_redeemed", new object[1] { cash }), "default");
				}
			}
		}
		catch
		{
		}
	}

	private static int RunRedeemHome(string op, long[] ids)
	{
		int num = 0;
		int num2 = 0;
		string text = RedeemTag(op);
		foreach (long id in ids)
		{
			GameItem val = Ids.Find(id);
			if (val == null || text == null || !val.IsTag(text))
			{
				Log.Warn("economy: commissary " + op + " at home: item #" + id + " is not here");
				continue;
			}
			if (op == "stamps")
			{
				val.ActivateSlotItem();
			}
			else
			{
				num += (int)val.unitValue;
				val.Destroy();
			}
			num2++;
		}
		if (num != 0)
		{
			Game.Store.ModCash(num, true);
		}
		RedeemedHome += num2;
		return num;
	}

	private static void OnRedeemHomeRequest(MsgEconRedeemHome m, int from)
	{
		PlayerStore store = Game.Store;
		string text = RedeemTag(m?.op);
		if (m?.ids == null || text == null || store?.commissaryData == null || !Game.ShopLoaded)
		{
			return;
		}
		if (!Outings.IsOut(from))
		{
			Refuse(from, "commissary " + m.op, "you are not at the commissary");
			return;
		}
		List<long> list = new List<long>();
		foreach (long id in m.ids)
		{
			GameItem val = Ids.Find(id);
			if (val != null && val.IsTag(text) && val.IsTag("IS_OWNED_TAG") && !list.Contains(id))
			{
				list.Add(id);
			}
		}
		if (list.Count == 0)
		{
			return;
		}
		long[] ids = list.ToArray();
		int credit = store.commissaryData.redeemedCredit;
		int playerCash = store.playerCash;
		Seed seed;
		using (Replay.Server(out seed))
		{
			Game_("commissary " + m.op + " (at home)", delegate
			{
				RunRedeemHome(m.op, ids);
			});
		}
		Applied++;
		Net.ToAll("econ.redeemedHome", new MsgEconRedeemHome
		{
			op = m.op,
			ids = ids,
			by = from,
			seed = seed
		});
		Log.Info("economy: " + Net.PlayerName(from) + " (at the commissary) redeemed " + ids.Length + " " + m.op + " item(s) lying in the shop: credit " + credit + " -> " + store.commissaryData.redeemedCredit + ", cash " + playerCash + " -> " + store.playerCash);
	}

	private static void OnRedeemedHome(MsgEconRedeemHome m)
	{
		PlayerStore store = Game.Store;
		if (m?.ids == null || store?.commissaryData == null)
		{
			return;
		}
		int num = 0;
		using (Replay.Client(m.seed ?? new Seed()))
		{
			Game_("commissary " + m.op + " (at home)", delegate
			{
				num = RunRedeemHome(m.op, m.ids);
			});
		}
		Applied++;
		if (!DayLog.Replaying && m.by == Net.LocalPlayer)
		{
			RedeemUi(m.op, num);
		}
	}

	private static void OnCommissaryDone(MsgEconCommissaryDone m)
	{
		PlayerStore store = Game.Store;
		if (store?.commissaryData == null)
		{
			NoteMismatch("commissary: no commissary data");
			return;
		}
		int playerCash = store.playerCash;
		using (Replay.Client(m.seed))
		{
			Game_("commissary " + m.op, delegate
			{
				RunCommissary(m.op, m.item);
			});
		}
		Applied++;
		if (!DayLog.Replaying)
		{
			try
			{
				CommissaryUIManager instance = CommissaryUIManager.Instance;
				if (instance != null)
				{
					instance.RefreshUI();
				}
			}
			catch
			{
			}
			if (m.op == "lottery")
			{
				try
				{
					ReputationUIManager instance2 = ReputationUIManager.Instance;
					if (instance2 != null)
					{
						instance2.Notify(LocHelper.GetLocalizedMechanic("mech_commissary_lottery_redeemed", new object[1] { store.playerCash - playerCash }), "default");
					}
				}
				catch
				{
				}
			}
		}
		Verify("commissary " + m.op, m.d, m.p);
	}

	public static bool AtCommissary()
	{
		if (DayNightOuting.IsOut)
		{
			return DayNightOuting.LocationId == "commissary";
		}
		return false;
	}

	internal static void NoteReplayWindow()
	{
		replayWindow = true;
	}

	internal static bool ClientWindowOpening()
	{
		if (!AtCommissary())
		{
			return true;
		}
		int num = Locks.HeldBy("outing.commissary");
		if (num >= 0 && num != Net.LocalPlayer)
		{
			string text = Net.PlayerName(num) + " is trading at the commissary.";
			Refusals++;
			LastRefusal = "commissary open: " + text;
			Log.Info("economy: " + text + " (the trade window stays closed here)");
			Notify(text);
			return false;
		}
		windowAsked = true;
		lastWindowTouchMs = Clock.Ms;
		if (Net.IsLive)
		{
			Net.ToServer("econ.commissary.open", new MsgEconWindow
			{
				name = "outing.commissary"
			});
		}
		return true;
	}

	internal static void ClientWindowClosed()
	{
		if (windowAsked || replayWindow)
		{
			windowAsked = false;
			replayWindow = false;
			if (Net.IsLive && !DayLog.Replaying)
			{
				Net.ToServer("econ.commissary.close", new MsgEconWindow
				{
					name = "outing.commissary"
				});
			}
		}
	}

	private static void KeepWindowLock(long now)
	{
		if (windowAsked || replayWindow)
		{
			if (!CommissaryWindowOpen)
			{
				ClientWindowClosed();
			}
			else if (windowAsked && now - lastWindowTouchMs >= 20000 && Net.IsLive)
			{
				lastWindowTouchMs = now;
				Net.ToServer("econ.commissary.open", new MsgEconWindow
				{
					name = "outing.commissary"
				});
			}
		}
	}

	private static void OnWindowOpenRequest(MsgEconWindow m, int from)
	{
		if (!Game.ShopLoaded)
		{
			return;
		}
		if (DayNightRun.RunOver)
		{
			Refuse(from, "commissary open", "The run has ended.");
			return;
		}
		if (!AtCommissary())
		{
			Log.Info("economy: " + Net.PlayerName(from) + " opened the commissary window away from the commissary: no lock");
			return;
		}
		int num = Locks.Holder("outing.commissary");
		if (num >= 0 && num != from)
		{
			Refuse(from, "commissary open", Net.PlayerName(num) + " is trading at the commissary.");
			return;
		}
		Locks.TryTake("outing.commissary", from);
		if (num != from)
		{
			Log.Info("economy: " + Net.PlayerName(from) + " opened the commissary trade window (lock outing.commissary)");
		}
	}

	private static void OnWindowCloseRequest(MsgEconWindow m, int from)
	{
		ServerWindowClosed("commissary", from);
		if (Locks.Holder("outing.commissary") == from)
		{
			Locks.Release("outing.commissary", from);
			Log.Info("economy: " + Net.PlayerName(from) + " closed the commissary trade window (lock outing.commissary free)");
		}
	}

	public static void RequestFill(StoreRequest r)
	{
		PlayerStore store = Game.Store;
		if (Net.IsClient && r != null && store?.storeRequests != null)
		{
			Net.ToServer("econ.request.fill", new MsgEconFill
			{
				index = store.storeRequests.IndexOf(r),
				id = r.id
			});
		}
	}

	private static void OnFillRequest(MsgEconFill m, int from)
	{
		ServerFill(m?.index ?? (-1), m?.id, from);
	}

	internal static string ServerFill(int index, string id, int by)
	{
		StoreRequest r;
		string text = CheckFill(index, id, out r);
		if (text != null)
		{
			Refuse(by, "request " + id, text);
			return text;
		}
		int[] array = FillPicks(r);
		Seed seed;
		using (Replay.Server(out seed))
		{
			Game_("request.fill " + id, (Action)r.FillRequest);
		}
		Applied++;
		PlayerStore store = Game.Store;
		Net.ToAll("econ.request.filled", new MsgEconFilled
		{
			index = index,
			id = id,
			by = by,
			seed = seed,
			gone = array,
			d = Replay.Digest(),
			p = Parts()
		});
		HostRefresh("fill", by);
		Log.Info("economy: " + Net.PlayerName(by) + " filled request " + id + " (" + array.Length + " item(s) handed over); cash now " + store.playerCash);
		return null;
	}

	internal static string CheckFill(int index, string id, out StoreRequest r)
	{
		r = null;
		if (!Game.ShopLoaded)
		{
			return "the shop is not open";
		}
		if (DayNightRun.RunOver)
		{
			return "The run has ended.";
		}
		List<StoreRequest> storeRequests = Game.Store.storeRequests;
		if (storeRequests == null || index < 0 || index >= storeRequests.Count || storeRequests[index] == null || storeRequests[index].id != id)
		{
			return "that request is gone";
		}
		r = storeRequests[index];
		if (!r.CanFillRequest())
		{
			return "the shop does not have the items";
		}
		return null;
	}

	private static int[] FillPicks(StoreRequest r)
	{
		List<GameItem> allOwnedItems = EmporiumEntry.Instance.GetAllOwnedItems(false, true);
		List<int> list = new List<int>();
		int num = allOwnedItems.Count - 1;
		while (num >= 0 && list.Count < r.requiredItemCount)
		{
			if (r.CanThisItemFillRequest(allOwnedItems[num]))
			{
				list.Add(allOwnedItems[num].uniqueId);
			}
			num--;
		}
		return list.ToArray();
	}

	private static void OnFilled(MsgEconFilled m)
	{
		List<StoreRequest> list = Game.Store?.storeRequests;
		if (list == null || m.index < 0 || m.index >= list.Count || list[m.index]?.id != m.id)
		{
			NoteMismatch("request.filled: this copy has no request " + m.id + " at " + m.index);
			return;
		}
		StoreRequest val = list[m.index];
		int[] values = FillPicks(val);
		if (m.gone != null && string.Join(",", values) != string.Join(",", m.gone))
		{
			NoteMismatch("request.filled " + m.id + ": this copy would hand over " + string.Join(",", values) + ", the server handed over " + string.Join(",", m.gone));
		}
		using (Replay.Client(m.seed))
		{
			Game_("request.filled " + m.id, (Action)val.FillRequest);
		}
		Applied++;
		if (!DayLog.Replaying)
		{
			try
			{
				PCUIManager instance = PCUIManager.Instance;
				if (instance != null)
				{
					instance.OnChange();
				}
				PCUIManager instance2 = PCUIManager.Instance;
				if (instance2 != null)
				{
					StorePC storePC = instance2.storePC;
					if (storePC != null)
					{
						storePC.RefreshState();
					}
				}
			}
			catch
			{
			}
		}
		Verify("request.filled " + m.id, m.d, m.p);
	}

	private static void Game_(string what, Action a)
	{
		try
		{
			a();
		}
		catch (Exception ex)
		{
			GameErrors++;
			Log.Error("economy: the game's code for " + what + " threw (" + (Net.IsServer ? "server" : "client") + "): " + ex);
		}
	}

	private static void Refuse(int player, string what, string why)
	{
		Refusals++;
		LastRefusal = what + ": " + why;
		Log.Info("economy: refused " + what + " from " + Net.PlayerName(player) + ": " + why);
		if (player > 0)
		{
			Net.ToPlayer(player, "econ.refused", new MsgEconRefused
			{
				what = what,
				why = why
			});
		}
	}

	private static void OnRefused(MsgEconRefused m)
	{
		Refusals++;
		LastRefusal = m.what + ": " + m.why;
		Log.Info("economy: the server refused " + m.what + ": " + m.why);
		if (m.what != null && m.what.StartsWith("service ", StringComparison.Ordinal))
		{
			ShowServiceOnTablet(null);
		}
		if (m.what != null && m.what.StartsWith("commissary", StringComparison.Ordinal) && m.why != null && m.why.EndsWith("trading at the commissary.", StringComparison.Ordinal))
		{
			windowAsked = false;
			try
			{
				if (CommissaryWindowOpen)
				{
					CommissaryUIManager.Instance.CloseUI();
				}
			}
			catch (Exception ex)
			{
				Log.Warn("economy: closing the commissary window: " + ex.Message);
			}
		}
		Notify(m.why);
	}

	private static void Notify(string why)
	{
		try
		{
			StoreUIManager instance = StoreUIManager.Instance;
			if (instance != null)
			{
				instance.Notify(Kit.Safe(Lang.Tr(why)), "default");
			}
		}
		catch
		{
		}
	}

	internal static void Click()
	{
		try
		{
			EmporiumAudioPlayer instance = EmporiumAudioPlayer.Instance;
			if ((Object)(object)instance != (Object)null && (Object)(object)instance.audioButton1 != (Object)null)
			{
				instance.audioButton1.Play();
			}
		}
		catch
		{
		}
	}

	internal static void SkippedWindow(string which)
	{
		WindowsSkipped++;
		if (!DayLog.Replaying)
		{
			Log.Info("economy: the " + which + " window belongs to another player's conversation; not opened here");
		}
	}

	private static void Verify(string what, long serverDigest, Dictionary<string, string> serverParts)
	{
		List<string> list = new List<string>();
		if (serverDigest != 0L && Replay.Digest() != serverDigest)
		{
			list.Add("replay digest");
		}
		if (serverParts != null)
		{
			Dictionary<string, string> dictionary = Parts();
			foreach (KeyValuePair<string, string> serverPart in serverParts)
			{
				if (!dictionary.TryGetValue(serverPart.Key, out var value) || value != serverPart.Value)
				{
					list.Add("economy " + serverPart.Key);
				}
			}
		}
		if (list.Count > 0)
		{
			NoteMismatch(what + ": " + string.Join(", ", list) + " (" + Describe() + ")" + (DayLog.Replaying ? " (replaying the day log)" : ""));
		}
	}

	private static void NoteMismatch(string text)
	{
		Mismatches++;
		Log.Warn("economy: differs from the server after " + text);
	}

	public static string Hash()
	{
		return Combine(Parts());
	}

	public static Dictionary<string, string> Parts()
	{
		return HashParts(RawParts());
	}

	private static Dictionary<string, string> HashParts(Dictionary<string, string> raw)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (KeyValuePair<string, string> item in raw)
		{
			dictionary[item.Key] = Fnv(item.Value);
		}
		return dictionary;
	}

	private static string Combine(Dictionary<string, string> parts)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<string, string> part in parts)
		{
			stringBuilder.Append(part.Key).Append('=').Append(part.Value)
				.Append(';');
		}
		return Fnv(stringBuilder.ToString());
	}

	public static Dictionary<string, string> RawParts()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		PlayerStore store = Game.Store;
		if (store == null)
		{
			return dictionary;
		}
		CultureInfo ci = CultureInfo.InvariantCulture;
		StringBuilder sb = new StringBuilder();
		A(store.playerCash);
		A(store.wildFavor);
		dictionary["money"] = Take();
		A(store.rentValue);
		A(store.temporaryRent);
		A(store.dayUntilRent);
		A(store.dayWithoutInsurance);
		A(store.isSecurityAlarmActive);
		dictionary["rent"] = Take();
		A(store.IsPropertyPaid);
		A(store.mortgageWeekLeft);
		A(store.isPayingMortgage);
		A(store.mortgageWeeklyPayment);
		A(store.mortgageDownPaid);
		A(store.mortgageTotalWeeks);
		A(store.mortgageInterestRateTaken);
		A(store.IsMortgageOfferDeclined);
		A(store.propertyBoughtAt);
		A(store.loanOfferedToGeorgio);
		A(store.dayWhenLoanOfferedGeorgio);
		dictionary["property"] = Take();
		if (store.networkUpgrade != null)
		{
			List<string> list = new List<string>(store.networkUpgrade.Keys);
			list.Sort(StringComparer.Ordinal);
			foreach (string item in list)
			{
				NetworkUpgrade val = store.networkUpgrade[item];
				if (val != null)
				{
					A(item);
					A(val.state);
					A(val.cost);
					A(val.cooldownCurrent);
				}
			}
		}
		dictionary["upgrades"] = Take();
		if (store.storeService != null)
		{
			foreach (StoreService item2 in store.storeService)
			{
				if (item2 != null)
				{
					A(item2.id);
					A((item2.id == "DEBRIS_REMOVAL_SERVICE") ? "-" : ((object)item2.unlocked));
					A(item2.isUsing);
					A(item2.currentCooldown);
				}
			}
		}
		dictionary["services"] = Take();
		if (store.commissaryData != null)
		{
			A(store.commissaryData.redeemedCredit);
			if (store.commissaryData.commissaryItemDatas != null)
			{
				foreach (CommissaryItemData commissaryItemData in store.commissaryData.commissaryItemDatas)
				{
					if (commissaryItemData != null)
					{
						A(commissaryItemData.identifier);
						A(commissaryItemData.StockCount);
					}
				}
			}
		}
		A(store.isStampIntroduced);
		dictionary["commissary"] = Take();
		if (store.storeReputations != null)
		{
			foreach (StoreReputation storeReputation in store.storeReputations)
			{
				if (storeReputation != null)
				{
					A(storeReputation.factionId);
					A(Math.Round(storeReputation.GetReputationExact(), 4).ToString("R", ci));
					A(storeReputation.strength);
					A(storeReputation.influence);
					A(storeReputation.actionCooldown);
				}
			}
		}
		dictionary["reputation"] = Take();
		if (store.PhoneClientDict != null)
		{
			List<long> list2 = new List<long>(store.PhoneClientDict.Keys);
			list2.Sort();
			foreach (long item3 in list2)
			{
				StorePhoneClient val2 = store.PhoneClientDict[item3];
				if (val2 != null)
				{
					A(item3);
					A((int)val2.phoneState);
					A(val2.currentCooldown);
					A(val2.dialedBefore);
					A(val2.amountSpent);
					A(val2.stockType);
				}
			}
		}
		dictionary["phone"] = Take();
		if (store.storeRequests != null)
		{
			foreach (StoreRequest storeRequest in store.storeRequests)
			{
				if (storeRequest != null)
				{
					A(storeRequest.id);
					A(storeRequest.duration);
					A(storeRequest.requiredItem);
					A(storeRequest.requiredItemCount);
					A(storeRequest.creditReward);
				}
			}
		}
		A(store.seenRequest?.Count ?? 0);
		A(store.marketplaceUnlockedDay);
		dictionary["requests"] = Take();
		A(store.hasBusinessPermit);
		if (store.ActivePermits != null)
		{
			foreach (var activePermit in store.ActivePermits)
			{
				A(activePermit.Item1);
				A(activePermit.Item2);
			}
		}
		StoreClientData storeClientData = store.storeClientData;
		if (storeClientData != null)
		{
			A(storeClientData.businessPermitOfficialState);
			A(storeClientData.businessPermitBribeAmount);
		}
		dictionary["permits"] = Take();
		A(store.baseStoreAttractiveness);
		A(store.isVendingMachineUnlocked);
		A(store.isVendingFountainUnlocked);
		A(store.isNetworkIntroduced);
		A(store.networkIntroducedDay);
		A(store.daySinceLastBrowseUpgrade);
		if (storeClientData != null)
		{
			A(storeClientData.jacksonInvestment);
			A(storeClientData.isJacksonIntroduced);
		}
		dictionary["flags"] = Take();
		return dictionary;
		void A(object o)
		{
			sb.Append(Convert.ToString(o, ci)).Append('|');
		}
		string Take()
		{
			string result = sb.ToString();
			sb.Length = 0;
			return result;
		}
	}

	public static string Describe()
	{
		PlayerStore store = Game.Store;
		if (store == null)
		{
			return "no store";
		}
		int num = 0;
		if (store.networkUpgrade != null)
		{
			foreach (NetworkUpgrade value in store.networkUpgrade.Values)
			{
				if (value != null && value.IsUnlocked())
				{
					num++;
				}
			}
		}
		return "cash " + store.playerCash + ", favor " + store.wildFavor + ", upgrades owned " + num + ", tokens " + (store.commissaryData?.redeemedCredit ?? 0) + ", requests " + (store.storeRequests?.Count ?? 0) + ", rent " + store.rentValue + "+" + store.temporaryRent;
	}

	private static string Fnv(string s)
	{
		ulong num = 14695981039346656037uL;
		foreach (char c in s)
		{
			num ^= c;
			num *= 1099511628211L;
		}
		return num.ToString("x16");
	}

	internal static bool Guard(string what)
	{
		if (!Net.IsClient || Net.Applying)
		{
			return true;
		}
		// The host's own new-run setup is the server deciding; outside a request a host otherwise counts as a client.
		if (ServerMode.SettingUpNewRun)
		{
			return true;
		}
		Blocked++;
		LastBlocked = what;
		if (Blocked <= 10)
		{
			Log.Info("economy: blocked " + what + " on this client (not a replicated action; the server decides)");
		}
		return false;
	}
}
