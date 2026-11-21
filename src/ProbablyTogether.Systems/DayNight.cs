using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ProbablyTogether.UI;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

public static class DayNight
{
	public const string KOpen = "dayNight.open";

	public const string KOpened = "dayNight.opened";

	public const string KClose = "dayNight.close";

	public const string KClosed = "dayNight.closed";

	public const string KReady = "dayNight.ready";

	public const string KReadyChanged = "dayNight.readyChanged";

	public const string KDayEnded = "dayNight.dayEnded";

	public const string KRefused = "dayNight.refused";

	public const string KState = "dayNight.state";

	public const string KBot = "dayNight.bot";

	public const string KBotDo = "dayNight.bot.do";

	public const string Counter = "counter";

	private static readonly List<int> ready = new List<int>();

	private static readonly List<int> waiting = new List<int>();

	private static long deadlineMs;

	private static int nightDay;

	internal static int NightDay => nightDay;

	private static int timeoutOverride;

	private static bool pendingReady;

	internal static bool SuppressUnready;

	private static long nightBlackSinceMs;

	private static bool blackedOut;

	public static int Refusals;

	public static int Mismatches;

	public static int Applied;

	public static readonly Dictionary<string, int> Sent = new Dictionary<string, int>();

	public static int RefusedMessages;

	public static int DeniedMessages;

	public static string LastRefusedWhat = "";

	public static string LastRefusedWhy = "";

	public static string ServerLastRefused = "";

	public static string LastDayEndedWhy = "";

	private static readonly List<KeyValuePair<string, Func<long>>> parts = new List<KeyValuePair<string, Func<long>>>();

	private static bool inited;

	private static readonly FieldInfo endDayButtonField = AccessTools.Field(typeof(EndOfDayUIManager), "endDayButton");

	internal static string LabNightEnding;

	public static IReadOnlyList<int> ReadyPlayers => ready;

	public static IReadOnlyList<int> WaitingPlayers => waiting;

	public static int ReadySecondsLeft
	{
		get
		{
			if (deadlineMs > 0)
			{
				return (int)Math.Max(0L, (deadlineMs - Clock.Ms + 999) / 1000);
			}
			return -1;
		}
	}

	public static bool NightRunning { get; private set; }

	public static int ReadyTimeoutSeconds
	{
		get
		{
			if (timeoutOverride <= 0)
			{
				return ServerMode.Config?.readyTimeoutSeconds ?? 120;
			}
			return timeoutOverride;
		}
	}

	public static event Action<int, int> Opened;

	public static event Action<int, int> Closed;

	public static event Action ReadyChanged;

	public static event Action<int, string> DayEnded;

	public static event Action<string, string> Refused;

	public static bool IsReady(int player)
	{
		return ready.Contains(player);
	}

	public static void AddDigestPart(string name, Func<long> part)
	{
		if (!string.IsNullOrEmpty(name) && part != null)
		{
			parts.RemoveAll((KeyValuePair<string, Func<long>> p) => p.Key == name);
			parts.Add(new KeyValuePair<string, Func<long>>(name, part));
		}
	}

	internal static Dictionary<string, long> Parts()
	{
		Dictionary<string, long> dictionary = new Dictionary<string, long>();
		foreach (KeyValuePair<string, Func<long>> part in parts)
		{
			try
			{
				dictionary[part.Key] = part.Value();
			}
			catch (Exception ex)
			{
				Log.Warn("dayNight: digest part " + part.Key + " threw: " + ex.Message);
				dictionary[part.Key] = -1L;
			}
		}
		return dictionary;
	}

	internal static void CheckAfter(string what, long rd, Dictionary<string, long> serverParts)
	{
		Applied++;
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		long num = Replay.Digest();
		if (rd != 0L && num != rd)
		{
			list.Add("state digest " + rd.ToString("x16") + "/" + num.ToString("x16") + " (" + Digest.Compute().Describe() + ")");
			list2.Add("state");
		}
		if (serverParts != null)
		{
			Dictionary<string, long> dictionary = Parts();
			foreach (KeyValuePair<string, long> serverPart in serverParts)
			{
				if (dictionary.TryGetValue(serverPart.Key, out var value) && value != serverPart.Value)
				{
					list.Add(serverPart.Key + " " + serverPart.Value.ToString("x16") + "/" + value.ToString("x16"));
					list2.Add(serverPart.Key);
				}
			}
		}
		if (list.Count != 0)
		{
			Mismatches++;
			Log.Warn("dayNight: after " + what + " our shop differs from the server (server/mine): " + string.Join(", ", list) + (DayLog.Replaying ? " (during the log replay)" : "; asking for a resync"));
			if (!DayLog.Replaying)
			{
				Snapshot.RequestResync("dayNight: after " + what + " differs in " + string.Join(", ", list2));
			}
		}
	}

	[SystemInit]
	private static void Init()
	{
		if (inited)
		{
			return;
		}
		inited = true;
		Net.On<MsgDnOpen>("dayNight.open", ServerOpen, null);
		Net.On<MsgDnShutter>("dayNight.opened", null, ClientOpened);
		Net.On<MsgDnEmpty>("dayNight.close", ServerClose, null);
		Net.On<MsgDnShutter>("dayNight.closed", null, ClientClosed);
		Net.On<MsgDnReady>("dayNight.ready", ServerReady, null);
		Net.On<MsgDnReadyState>("dayNight.readyChanged", null, ClientReadyChanged);
		Net.On<MsgDnDayEnded>("dayNight.dayEnded", null, ClientDayEnded);
		Net.On<MsgDnRefused>("dayNight.refused", null, ClientRefused);
		Net.On<MsgDnState>("dayNight.state", null, ClientState);
		DayNightOuting.Register();
		DayNightRun.Register();
		if (!string.IsNullOrEmpty(Args.Bot))
		{
			Net.On<MsgDnBot>("dayNight.bot", ServerBot, null);
			Net.On("dayNight.bot.do", null, delegate(MsgDnBot m)
			{
				using (Net.Apply())
				{
					LabDo(m.op);
				}
			});
		}
		AddDigestPart("outing", DayNightOuting.OutingHash);
		AddDigestPart("bag", DayNightOuting.BagHash);
		Digest.AddPart("daynight", DigestText);
		Net.PlayerLeft += OnPlayerLeft;
		Net.Disconnected += delegate
		{
			ResetClientView();
		};
		Net.Denied += ClientDenied;
		Snapshot.ClientReady += OnClientReady;
		ReadyBar.Bind(ReadyView, RequestReady);
	}

	internal static string DigestText()
	{
		if (!Game.ShopLoaded)
		{
			return "";
		}
		return DayNightOuting.OutingHash().ToString("x16") + (DayNightRun.RunOver ? (":ended:" + DayNightRun.Ending) : "");
	}

	internal static void Send<T>(string kind, T msg) where T : class
	{
		Sent.TryGetValue(kind, out var value);
		Sent[kind] = value + 1;
		Net.ToServer(kind, msg);
	}

	public static void RequestReady(bool on)
	{
		if (Net.IsClient && (!on || (!pendingReady && !ready.Contains(Net.LocalPlayer))) && (on || pendingReady || ready.Contains(Net.LocalPlayer)))
		{
			pendingReady = on;
			Send("dayNight.ready", new MsgDnReady
			{
				ready = on
			});
			Log.Info("dayNight: asked the server: " + (on ? "ready to end the day" : "not ready"));
		}
	}

	internal static void Notice(string text)
	{
		if (string.IsNullOrEmpty(text) || DayLog.Replaying)
		{
			return;
		}
		text = text.Replace('<', '‹');
		try
		{
			if ((Object)(object)StoreUIManager.Instance != (Object)null)
			{
				StoreUIManager.Instance.Notify(Lang.Tr(text), "default");
			}
		}
		catch (Exception ex)
		{
			Log.Warn("dayNight: notice failed: " + ex.Message);
		}
	}

	internal static void Refuse(int player, string what, string why)
	{
		NoteRefused(what);
		Log.Info("dayNight: refused " + what + " from " + Net.PlayerName(player) + ": " + why);
		if (player > 0)
		{
			Net.ToPlayer(player, "dayNight.refused", new MsgDnRefused
			{
				what = what,
				why = why
			});
		}
	}

	internal static void NoteRefused(string what)
	{
		Refusals++;
		ServerLastRefused = what ?? "";
	}

	private static void ClientDenied(string kind, string why)
	{
		DeniedMessages++;
		LastRefusedWhat = kind ?? "";
		LastRefusedWhy = why ?? "";
		Log.Info("dayNight: the server's gate refused " + kind + ": " + why);
		if (why == "The run has ended. Everyone can vote to start a new run.")
		{
			try
			{
				DayNight.Refused?.Invoke(LastRefusedWhat, LastRefusedWhy);
			}
			catch (Exception ex)
			{
				Log.Error("DayNight.Refused handler: " + ex);
			}
		}
	}

	private static void ClientRefused(MsgDnRefused m)
	{
		RefusedMessages++;
		LastRefusedWhat = m.what ?? "";
		LastRefusedWhy = m.why ?? "";
		if (m.what == "ready")
		{
			pendingReady = false;
			RefreshTablet();
		}
		if (m.what == "newRun")
		{
			DayNightRun.ClearPendingVote();
		}
		Log.Info("dayNight: the server refused " + m.what + ": " + m.why);
		Notice(m.why);
		try
		{
			DayNight.Refused?.Invoke(m.what, m.why);
		}
		catch (Exception ex)
		{
			Log.Error("DayNight.Refused handler: " + ex);
		}
	}

	private static string CanOpen(PlayerStore ps)
	{
		if (DayNightRun.RunOver)
		{
			return "The run has ended.";
		}
		if (ps == null || !Game.ShopLoaded || (Object)(object)StoreShutterButton.Instance == (Object)null)
		{
			return "The shop is not ready.";
		}
		if (NightRunning)
		{
			return "The night is running.";
		}
		if ((int)ps.storeState != 2)
		{
			if ((int)ps.storeState != 1)
			{
				return "The day is over: end it on the tablet by the bedroll.";
			}
			return "The shop is already open.";
		}
		if (ps.lockShutterButtonFlag > 0)
		{
			return "The shutter is still moving. Try again in a moment.";
		}
		if (ps.isClientBeingArrested || ps.isHandlingShootout)
		{
			return "Not now: there is trouble at the counter.";
		}
		if (!ps.CanStartDay())
		{
			return "The shop cannot open yet.";
		}
		return null;
	}

	private static void ServerOpen(MsgDnOpen m, int from)
	{
		PlayerStore store = Game.Store;
		string text = CanOpen(store);
		if (text != null)
		{
			Refuse(from, "open", text);
			return;
		}
		MsgDnShutter msgDnShutter = new MsgDnShutter
		{
			day = Game.Day,
			by = from,
			paper = m.paper
		};
		Exception ex = null;
		int num = 0;
		TradeVisits.ServerBeforeOpen();
		msgDnShutter.picks = SupplierPicks.ForOpening();
		using (Replay.Server(out msgDnShutter.seed))
		{
			ex = RunOpen(m.paper);
			if (ex == null)
			{
				msgDnShutter.xc = CrewCustomers.Decide();
				num = CrewCustomers.Add(msgDnShutter.xc);
			}
		}
		if (msgDnShutter.xc > 0)
		{
			Log.Info("dayNight: " + num + " of " + msgDnShutter.xc + " extra customer(s) joined today's queue (now " + (store.storeClientManager?.clientStack?.Count).GetValueOrDefault() + ")");
		}
		if (ex != null && (int)store.storeState != 1)
		{
			Log.Error("dayNight: opening the shutter failed: " + ex);
			return;
		}
		if (ex != null)
		{
			Log.Error("dayNight: opening the shutter threw part way (broadcast anyway, clients replay the same): " + ex);
		}
		msgDnShutter.rd = Replay.Digest();
		msgDnShutter.parts = Parts();
		Net.ToAll("dayNight.opened", msgDnShutter);
		Log.Info("dayNight: " + Net.PlayerName(from) + " opened the shop (day " + msgDnShutter.day + "); " + Digest.Compute().Describe());
		Raise(DayNight.Opened, msgDnShutter.day, from);
	}

	private static Exception RunOpen(bool paper)
	{
		EmporiumOption instance = EmporiumOption.Instance;
		bool newspaperToTrash = (Object)(object)instance != (Object)null && instance.newspaperToTrash;
		try
		{
			if ((Object)(object)instance != (Object)null)
			{
				instance.newspaperToTrash = paper;
			}
			StoreShutterButton.Instance.OpenShutter();
			return null;
		}
		catch (Exception result)
		{
			return result;
		}
		finally
		{
			if ((Object)(object)instance != (Object)null)
			{
				instance.newspaperToTrash = newspaperToTrash;
			}
		}
	}

	private static void ClientOpened(MsgDnShutter m)
	{
		PlayerStore store = Game.Store;
		if (store == null || (Object)(object)StoreShutterButton.Instance == (Object)null)
		{
			Log.Warn("dayNight: opened arrived with no shop");
			return;
		}
		if ((int)store.storeState != 2)
		{
			Mismatches++;
			Log.Warn("dayNight: the server opened the shop but ours is " + store.storeState.ToString() + (DayLog.Replaying ? "" : "; asking for a resync"));
			if (!DayLog.Replaying)
			{
				Snapshot.RequestResync("dayNight: the server opened the shop, ours is " + store.storeState.ToString());
			}
			return;
		}
		int num = 0;
		Exception ex;
		SupplierPicks.Adopt(m.picks);
		using (Replay.Client(m.seed))
		{
			ex = RunOpen(m.paper);
			if (m.xc > 0)
			{
				num = CrewCustomers.Add(m.xc);
			}
		}
		if (ex != null)
		{
			Log.Error("dayNight: replaying the shutter opening threw: " + ex);
		}
		if (m.xc > 0)
		{
			Log.Info("dayNight: " + num + " of " + m.xc + " extra customer(s) for a bigger crew joined today's queue");
		}
		CheckAfter("the shutter opening", m.rd, m.parts);
		if (m.by != Net.LocalPlayer && m.by > 0)
		{
			Notice(Net.PlayerName(m.by) + " opened the shop.");
		}
		Raise(DayNight.Opened, m.day, m.by);
	}

	private static string CanClose(PlayerStore ps, int from)
	{
		if (DayNightRun.RunOver)
		{
			return "The run has ended.";
		}
		if (ps == null || !Game.ShopLoaded || (Object)(object)StoreShutterButton.Instance == (Object)null)
		{
			return "The shop is not ready.";
		}
		if ((int)ps.storeState != 1)
		{
			return "The shop is not open.";
		}
		if (ps.lockShutterButtonFlag > 0)
		{
			return "The shutter is still moving. Try again in a moment.";
		}
		if (ps.isClientBeingArrested || ps.isHandlingShootout)
		{
			return "Not now: there is trouble at the counter.";
		}
		if (!ps.CanEndDay())
		{
			return "Someone who will not be sent away is still waiting.";
		}
		int num = Locks.Holder("counter");
		if (num >= 0 && num != from && ps.currentClientInstance != null)
		{
			return Net.PlayerName(num) + " is in the middle of a deal at the counter. Close when they are done.";
		}
		return null;
	}

	private static void ServerClose(MsgDnEmpty m, int from)
	{
		PlayerStore store = Game.Store;
		string text = CanClose(store, from);
		if (text != null)
		{
			Refuse(from, "close", text);
			return;
		}
		MsgDnShutter msgDnShutter = new MsgDnShutter
		{
			day = Game.Day,
			by = from
		};
		Exception ex = null;
		using (Replay.Server(out msgDnShutter.seed))
		{
			try
			{
				StoreShutterButton.Instance.EndAndClose();
			}
			catch (Exception ex2)
			{
				ex = ex2;
			}
		}
		if (ex != null && (int)store.storeState != 0)
		{
			Log.Error("dayNight: closing the shutter failed: " + ex);
			return;
		}
		if (ex != null)
		{
			Log.Error("dayNight: closing the shutter threw part way (broadcast anyway): " + ex);
		}
		msgDnShutter.visits = Outings.NightBudget();
		if (msgDnShutter.visits > 0)
		{
			using (Net.Apply())
			{
				store.visitLeftTonight = msgDnShutter.visits;
			}
		}
		msgDnShutter.rd = Replay.Digest();
		msgDnShutter.parts = Parts();
		Net.ToAll("dayNight.closed", msgDnShutter);
		Log.Info("dayNight: " + Net.PlayerName(from) + " closed the shop (day " + msgDnShutter.day + "); night trips " + store.visitLeftTonight + "; " + Digest.Compute().Describe());
		Raise(DayNight.Closed, msgDnShutter.day, from);
	}

	private static void ClientClosed(MsgDnShutter m)
	{
		PlayerStore store = Game.Store;
		if (store == null || (Object)(object)StoreShutterButton.Instance == (Object)null)
		{
			Log.Warn("dayNight: closed arrived with no shop");
			return;
		}
		if ((int)store.storeState != 1)
		{
			Mismatches++;
			Log.Warn("dayNight: the server closed the shop but ours is " + store.storeState.ToString() + (DayLog.Replaying ? "" : "; asking for a resync"));
			if (!DayLog.Replaying)
			{
				Snapshot.RequestResync("dayNight: the server closed the shop, ours is " + store.storeState.ToString());
			}
			return;
		}
		using (Replay.Client(m.seed))
		{
			try
			{
				StoreShutterButton.Instance.EndAndClose();
			}
			catch (Exception ex)
			{
				Log.Error("dayNight: replaying the shutter closing threw: " + ex);
			}
		}
		if (m.visits > 0)
		{
			using (Net.Apply())
			{
				store.visitLeftTonight = m.visits;
			}
		}
		CheckAfter("the shutter closing", m.rd, m.parts);
		if (m.by != Net.LocalPlayer && m.by > 0)
		{
			Notice(Net.PlayerName(m.by) + " closed the shop.");
		}
		Raise(DayNight.Closed, m.day, m.by);
	}

	internal static void ApplyTabletServiceWrites()
	{
		PlayerStore store = Game.Store;
		if (store?.storeService == null)
		{
			return;
		}
		try
		{
			StoreService debrisRemovalService = StoreService.GetDebrisRemovalService();
			if (debrisRemovalService != null)
			{
				debrisRemovalService.UpdateCost();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("dayNight: debris cost update threw: " + ex.Message);
		}
		foreach (StoreService item in store.storeService)
		{
			if (item != null && item.unlocked && (item.id == null || !item.id.Equals("POWER_SERVICE") || !NetworkUpgrade.IsPowerHijackUnlocked()))
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

	internal static int ServicesTotal()
	{
		PlayerStore store = Game.Store;
		int num = 0;
		if (store?.storeService != null)
		{
			foreach (StoreService item in store.storeService)
			{
				if (item != null && item.unlocked && item.isUsing)
				{
					num += item.cost;
				}
			}
		}
		return num;
	}

	internal static int ServicesTotalAsTablet()
	{
		PlayerStore store = Game.Store;
		int num = 0;
		if (store?.storeService == null)
		{
			return 0;
		}
		bool flag = false;
		try
		{
			flag = NetworkUpgrade.IsPowerHijackUnlocked();
		}
		catch
		{
		}
		foreach (StoreService item in store.storeService)
		{
			if (item == null || !item.unlocked)
			{
				continue;
			}
			bool flag2 = item.isUsing;
			if (!flag || item.id == null || !item.id.Equals("POWER_SERVICE"))
			{
				if (item.isMandatory)
				{
					flag2 = true;
				}
				try
				{
					if (item.IsOnCooldown())
					{
						flag2 = false;
					}
				}
				catch
				{
				}
			}
			if (flag2)
			{
				num += item.cost;
			}
		}
		return num;
	}

	internal static string CantPayReason()
	{
		PlayerStore store = Game.Store;
		if (store == null)
		{
			return null;
		}
		int num = ServicesTotalAsTablet();
		if (num > 0 && num > store.playerCash)
		{
			return "Not enough cash for tonight's services (" + num + " needed, " + store.playerCash + " in the till): turn some off on the tablet.";
		}
		return null;
	}

	private static string CanReady()
	{
		PlayerStore store = Game.Store;
		if (DayNightRun.RunOver)
		{
			return "The run has ended.";
		}
		if (store == null || !Game.ShopLoaded)
		{
			return "The shop is not ready.";
		}
		if (NightRunning)
		{
			return "The night is already running.";
		}
		if ((int)store.storeState != 0)
		{
			if ((int)store.storeState != 1)
			{
				return "The day has not started yet.";
			}
			return "Close the shop first.";
		}
		if (Outings.AnyOut)
		{
			return Outings.WhoIsOut() + " is still out.";
		}
		if (DayNightOuting.IsOut)
		{
			return "Come back from tonight's trip first.";
		}
		if (DayNightOuting.MapOpen)
		{
			return "Someone has the map open: close it or go out first.";
		}
		return CantPayReason();
	}

	private static void ServerReady(MsgDnReady m, int from)
	{
		if (m == null)
		{
			return;
		}
		if (m.ready)
		{
			string text = CanReady();
			if (text != null)
			{
				Refuse(from, "ready", text);
				SendReadyTo(from, "refused");
				return;
			}
			if (!ready.Contains(from))
			{
				ready.Add(from);
				if (deadlineMs <= 0)
				{
					deadlineMs = Clock.Ms + (long)ReadyTimeoutSeconds * 1000L;
				}
			}
		}
		else
		{
			if (!ready.Remove(from))
			{
				SendReadyTo(from, "resend");
				return;
			}
			if (ready.Count == 0)
			{
				deadlineMs = 0L;
			}
		}
		BroadcastReady(from, m.ready ? "ready" : "unready");
		EvaluateBarrier();
	}

	internal static List<int> LivePlayers()
	{
		List<int> list = new List<int>();
		foreach (Server.Peer value in Server.ByPlayer.Values)
		{
			if (value.State == Server.PeerState.Ready && value.Info != null)
			{
				list.Add(value.Info.Id);
			}
		}
		list.Sort();
		return list;
	}

	private static MsgDnReadyState ReadyState(int by, string why)
	{
		List<int> source = LivePlayers();
		return new MsgDnReadyState
		{
			ready = ready.OrderBy((int x) => x).ToArray(),
			waiting = source.Where((int x) => !ready.Contains(x)).ToArray(),
			secondsLeft = ReadySecondsLeft,
			by = by,
			why = why
		};
	}

	private static void BroadcastReady(int by, string why)
	{
		MsgDnReadyState msgDnReadyState = ReadyState(by, why);
		waiting.Clear();
		waiting.AddRange(msgDnReadyState.waiting);
		Net.ToAll("dayNight.readyChanged", msgDnReadyState);
		Log.Info("dayNight: ready " + Ids(msgDnReadyState.ready) + ", waiting for " + Ids(msgDnReadyState.waiting) + ((msgDnReadyState.secondsLeft >= 0) ? (", night in at most " + msgDnReadyState.secondsLeft + " s") : "") + " (" + why + ((by > 0) ? (" by " + Net.PlayerName(by)) : "") + ")");
		RaiseReady();
	}

	private static void SendReadyTo(int player, string why)
	{
		if (player > 0)
		{
			Net.ToPlayer(player, "dayNight.readyChanged", ReadyState(-1, why));
		}
	}

	private static string Ids(int[] a)
	{
		if (a != null && a.Length != 0)
		{
			return string.Join(", ", a.Select(Net.PlayerName));
		}
		return "nobody";
	}

	internal static void EvaluateBarrier()
	{
		if (!Net.IsServer || NightRunning || ready.Count == 0)
		{
			return;
		}
		if (ready.RemoveAll((int id) => !Server.ByPlayer.ContainsKey(id)) > 0 && ready.Count == 0)
		{
			deadlineMs = 0L;
			BroadcastReady(-1, "left");
			return;
		}
		List<int> list = LivePlayers();
		bool flag = list.Count > 0 && list.All(ready.Contains);
		bool flag2 = deadlineMs > 0 && Clock.Ms >= deadlineMs;
		if (!flag && !flag2)
		{
			return;
		}
		string text = CanReady();
		if (text != null)
		{
			Log.Info("dayNight: the night cannot run now (" + text + "); everyone is un-readied");
			foreach (int item in ready)
			{
				if (Server.ByPlayer.ContainsKey(item))
				{
					Net.ToPlayer(item, "dayNight.refused", new MsgDnRefused
					{
						what = "ready",
						why = text
					});
				}
			}
			ready.Clear();
			deadlineMs = 0L;
			BroadcastReady(-1, "refused");
		}
		else
		{
			RunNight(flag ? ("everyone ready (" + ready.Count + ")") : ("ready timeout (" + ready.Count(list.Contains) + " of " + list.Count + " ready after " + ReadyTimeoutSeconds + " s)"));
		}
	}

	internal static void ClearReady(string why)
	{
		if (ready.Count != 0 || deadlineMs > 0)
		{
			ready.Clear();
			deadlineMs = 0L;
			if (Net.IsServer)
			{
				BroadcastReady(-1, why);
			}
		}
	}

	private static void OnPlayerLeft(PlayerInfo p)
	{
		if (!Net.IsServer || p == null)
		{
			return;
		}
		if (ready.Remove(p.Id))
		{
			if (ready.Count == 0)
			{
				deadlineMs = 0L;
			}
			BroadcastReady(-1, "left");
		}
		else if (ready.Count > 0)
		{
			BroadcastReady(-1, "left");
		}
		Log.Info("dayNight: " + p.Name + " left" + ((ready.Count > 0) ? ("; the barrier is checked again (" + ready.Count + " ready)") : ""));
		EvaluateBarrier();
		DayNightRun.OnPlayerLeft(p.Id);
	}

	private static void OnClientReady(int player)
	{
		if (Net.IsServer)
		{
			Net.ToPlayer(player, "dayNight.state", new MsgDnState
			{
				ready = ReadyState(-1, "resend"),
				runOver = DayNightRun.RunOver,
				ending = DayNightRun.Ending,
				votes = (DayNightRun.RunOver ? DayNightRun.VotesState(-1, "resend") : null),
				mapOpen = DayNightOuting.MapOpen
			});
			EvaluateBarrier();
			DayNightRun.OnClientReady(player);
		}
	}

	private static void ClientReadyChanged(MsgDnReadyState m)
	{
		ApplyReadyView(m);
		if (m.why == "ready" && m.by > 0 && m.by != Net.LocalPlayer)
		{
			Notice(Net.PlayerName(m.by) + " is ready to end the day.");
		}
	}

	private static void ApplyReadyView(MsgDnReadyState m)
	{
		if (m != null)
		{
			ready.Clear();
			if (m.ready != null)
			{
				ready.AddRange(m.ready);
			}
			waiting.Clear();
			if (m.waiting != null)
			{
				waiting.AddRange(m.waiting);
			}
			deadlineMs = ((m.secondsLeft >= 0 && !DayLog.Replaying) ? (Clock.Ms + (long)m.secondsLeft * 1000L) : 0);
			if (ready.Contains(Net.LocalPlayer) || m.by == Net.LocalPlayer)
			{
				pendingReady = false;
			}
			RefreshTablet();
			RaiseReady();
		}
	}

	private static void ClientState(MsgDnState m)
	{
		if (m != null)
		{
			ApplyReadyView(m.ready);
			DayNightOuting.SetMapView(m.mapOpen);
			DayNightRun.ApplyState(m);
		}
	}

	internal static ReadyBar.View ReadyView()
	{
		ReadyBar.View view = new ReadyBar.View();
		PlayerStore store = Game.Store;
		if (!Net.IsClient || store == null || !Game.ShopLoaded || Snapshot.IsLoading || blackedOut || DayNightRun.RunOver || (int)store.storeState != 0)
		{
			return view;
		}
		view.Active = true;
		List<int> list = new List<int>(ready.Count);
		int num = 0;
		foreach (int item in ready)
		{
			list.Add(item);
			if (Net.Players.ContainsKey(item))
			{
				num++;
			}
		}
		foreach (int item2 in waiting)
		{
			if (Net.Players.ContainsKey(item2))
			{
				num++;
			}
		}
		view.Ready = list;
		view.Needed = num;
		view.SecondsLeft = ReadySecondsLeft;
		view.LocalReady = ready.Contains(Net.LocalPlayer) || pendingReady;
		string text = LocalReadyBlock(store);
		view.CanPress = text == null;
		view.Note = text;
		return view;
	}

	private static string LocalReadyBlock(PlayerStore ps)
	{
		if (Outings.AnyOut)
		{
			return Outings.WhoIsOut() + " is still out.";
		}
		if (DayNightOuting.IsOut)
		{
			return "Come back from tonight's trip first.";
		}
		if (DayNightOuting.MapOpen)
		{
			return "The map is open: go out or close it first.";
		}
		return CantPayReason();
	}

	internal static void RefreshTablet()
	{
		if (!Net.IsClient || DayLog.Replaying)
		{
			return;
		}
		EndOfDayUIManager instance = EndOfDayUIManager.Instance;
		if ((Object)(object)instance == (Object)null || !instance.IsOpen)
		{
			return;
		}
		try
		{
			instance.RefreshUI();
			if (ready.Contains(Net.LocalPlayer) || pendingReady)
			{
				object? obj = endDayButtonField?.GetValue(instance);
				object? obj2 = ((obj is TabletButton) ? obj : null);
				if (obj2 != null)
				{
					((TabletButton)obj2).SetInteractable(false);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("dayNight: tablet refresh: " + ex.Message);
		}
	}

	private static void RunNight(string why)
	{
		EndOfDayUIManager instance = EndOfDayUIManager.Instance;
		if ((Object)(object)instance == (Object)null || (Object)(object)StoreUIManager.Instance == (Object)null)
		{
			Log.Error("dayNight: no end-of-day manager in the scene; the night cannot run");
			return;
		}
		// A call still going on ends with the day; otherwise the server stays in it while every morning has none.
		DialogueSystem.ServerEndCall("the night");
		int[] array = ready.OrderBy((int x) => x).ToArray();
		ready.Clear();
		deadlineMs = 0L;
		NightRunning = true;
		nightDay = Game.Day;
		LastDayEndedWhy = why;
		// Every player compares the shop as the night starts (NightCheck).
		Machines.PushNow();
		NightCheck.SendBaseline(nightDay);
		Net.ToAll("dayNight.dayEnded", new MsgDnDayEnded
		{
			day = nightDay,
			why = why,
			ready = array
		});
		Log.Info("dayNight: day " + nightDay + " ends: " + why + "; the night runs now");
		if (Net.IsHost)
		{
			waiting.Clear();
			pendingReady = false;
			DayNightOuting.SetMapView(open: false);
		}
		Raise(DayNight.DayEnded, nightDay, why);
		ApplyTabletServiceWrites();
		bool consumed;
		using (DayNightBlackscreen dayNightBlackscreen = DayNightBlackscreen.Wrap((Action orig) => delegate
		{
			try
			{
				using (Net.Apply())
				{
					orig?.Invoke();
				}
			}
			finally
			{
				NightDone();
			}
		}))
		{
			try
			{
				using (Net.Apply())
				{
					instance.OnEndDayButtonClick();
				}
			}
			catch (Exception ex)
			{
				Log.Error("dayNight: the end-of-day button threw: " + ex);
			}
			consumed = dayNightBlackscreen.Consumed;
		}
		if (!consumed)
		{
			NightRunning = false;
			Log.Error("dayNight: the night did not start (no blackscreen sequence)");
		}
	}

	private static void NightDone()
	{
		NightRunning = false;
		PlayerStore store = Game.Store;
		Log.Info("dayNight: the night of day " + nightDay + " is over: now day " + Game.Day + ", " + (store?.storeState).ToString() + (DayNightRun.RunOver ? ("; the run ended (" + DayNightRun.Ending + ")") : "; the game saved, the core sends everyone the morning"));
		DayNightRun.AfterNight();
	}

	private static void ClientDayEnded(MsgDnDayEnded m)
	{
		ready.Clear();
		waiting.Clear();
		deadlineMs = 0L;
		pendingReady = false;
		LastDayEndedWhy = m.why ?? "";
		DayNightOuting.SetMapView(open: false);
		Raise(DayNight.DayEnded, m.day, m.why);
		if (DayLog.Replaying)
		{
			return;
		}
		Log.Info("dayNight: day " + m.day + " ends (" + m.why + "); waiting for the morning");
		SuppressUnready = true;
		try
		{
			if ((Object)(object)EndOfDayUIManager.Instance != (Object)null && EndOfDayUIManager.Instance.IsOpen)
			{
				EndOfDayUIManager.Instance.CloseUI(true);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("dayNight: closing the tablet: " + ex.Message);
		}
		finally
		{
			SuppressUnready = false;
		}
		try
		{
			StoreUIManager instance = StoreUIManager.Instance;
			if ((Object)(object)instance != (Object)null)
			{
				if ((Object)(object)instance.savingTMP != (Object)null)
				{
					((Component)instance.savingTMP).gameObject.SetActive(true);
				}
				instance.StartBlackscreenSequence(false, (Action)null, (Action)null, 0.25f, 0.2f);
				blackedOut = true;
				nightBlackSinceMs = Clock.Ms;
			}
		}
		catch (Exception ex2)
		{
			Log.Warn("dayNight: night blackout: " + ex2.Message);
		}
	}

	internal static void ClearBlackout()
	{
		if (!blackedOut)
		{
			return;
		}
		blackedOut = false;
		try
		{
			StoreUIManager instance = StoreUIManager.Instance;
			if (!((Object)(object)instance == (Object)null))
			{
				if ((Object)(object)instance.savingTMP != (Object)null)
				{
					((Component)instance.savingTMP).gameObject.SetActive(false);
				}
				if ((Object)(object)instance.blackscreen != (Object)null)
				{
					((Component)instance.blackscreen).gameObject.SetActive(false);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("dayNight: clearing the blackout: " + ex.Message);
		}
	}

	private static void ResetClientView()
	{
		ready.Clear();
		waiting.Clear();
		deadlineMs = 0L;
		pendingReady = false;
		blackedOut = false;
		DayNightOuting.SetMapView(open: false);
		DayNightRun.ResetClient();
	}

	internal static void ResetForNewShop()
	{
		ready.Clear();
		waiting.Clear();
		deadlineMs = 0L;
		NightRunning = false;
		DayNightOuting.SetMapView(open: false);
	}

	[SystemTick]
	private static void Tick()
	{
		if (!Net.IsOnline)
		{
			return;
		}
		if (Net.IsServer)
		{
			if (deadlineMs > 0)
			{
				EvaluateBarrier();
			}
			DayNightRun.ServerTick();
		}
		else if (Net.IsClient)
		{
			if (blackedOut && Clock.Ms - nightBlackSinceMs > 60000)
			{
				Log.Warn("dayNight: no morning 60 s after the night started; asking for a resync");
				ClearBlackout();
				Snapshot.RequestResync("dayNight: no morning 60 s after the night started");
			}
			if (blackedOut && Snapshot.IsLoading)
			{
				blackedOut = false;
			}
			DayNightRun.ClientTick();
		}
	}

	private static void ServerBot(MsgDnBot m, int from)
	{
		string text = m.op ?? "";
		PlayerStore store = Game.Store;
		Log.Info("dayNight: lab op '" + text + "' from " + Net.PlayerName(from));
		if (text.StartsWith("gameover:", StringComparison.Ordinal) && store != null)
		{
			using (Net.Apply())
			{
				store.ExecuteGameOver(text.Substring(9));
				return;
			}
		}
		int result;
		int result2;
		if (text.StartsWith("gameoverstep:", StringComparison.Ordinal) && store != null)
		{
			string text2 = text.Substring(13);
			using (Net.Apply())
			{
				store.ExecuteGameOver(text2);
			}
			Net.ToAll("dayNight.bot.do", new MsgDnBot
			{
				op = "gameover:" + text2
			});
		}
		else if (text == "bomb" && (Object)(object)StoreUIManager.Instance != (Object)null)
		{
			using (Net.Apply())
			{
				StoreUIManager.Instance.TriggerBombing("bombed");
			}
			Net.ToAll("dayNight.bot.do", new MsgDnBot
			{
				op = text
			});
		}
		else if (text.StartsWith("cash:", StringComparison.Ordinal) && store != null && int.TryParse(text.Substring(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
		{
			store.playerCash = result;
		}
		else if (text.StartsWith("timeout:", StringComparison.Ordinal) && int.TryParse(text.Substring(8), NumberStyles.Integer, CultureInfo.InvariantCulture, out result2))
		{
			SetLabTimeout(result2);
		}
		else if (text == "newrun")
		{
			DayNightRun.BeginNewRun(from, "lab bot (the panel's path)");
		}
		else if (text.StartsWith("nightend:", StringComparison.Ordinal))
		{
			LabNightEnding = text.Substring(9);
			Log.Info("dayNight: lab: the next night ends the run (" + LabNightEnding + ") right after BeginDay");
		}
		else if (text == "clearqueue" || text == "stamp")
		{
			using (Net.Apply())
			{
				LabDo(text);
			}
			Net.ToAll("dayNight.bot.do", new MsgDnBot
			{
				op = text
			});
		}
	}

	private static void LabDo(string op)
	{
		PlayerStore store = Game.Store;
		if (store == null)
		{
			return;
		}
		switch (op)
		{
		case "clearqueue":
		{
			int valueOrDefault = (store.storeClientManager?.clientStack?.Count).GetValueOrDefault();
			store.storeClientManager?.clientStack?.Clear();
			Log.Info("dayNight: lab cleared the customer queue (" + valueOrDefault + ")");
			return;
		}
		case "stamp":
			store.isStampIntroduced = true;
			Log.Info("dayNight: lab: food stamps introduced (the commissary is on the map)");
			return;
		case "bomb":
			if (Net.IsClient && (Object)(object)StoreUIManager.Instance != (Object)null)
			{
				StoreUIManager.Instance.TriggerBombing("bombed");
				Log.Info("dayNight: lab: the bomb timer runs here too (its ending must be dropped)");
				return;
			}
			break;
		}
		if (op.StartsWith("gameover:", StringComparison.Ordinal) && Net.IsClient)
		{
			store.ExecuteGameOver(op.Substring(9));
		}
	}

	public static void SetLabTimeout(int seconds)
	{
		timeoutOverride = Math.Max(0, seconds);
		Log.Info("dayNight: ready timeout now " + ReadyTimeoutSeconds + " s (lab)");
	}

	private static void RaiseReady()
	{
		try
		{
			DayNight.ReadyChanged?.Invoke();
		}
		catch (Exception ex)
		{
			Log.Error("DayNight.ReadyChanged handler: " + ex);
		}
	}

	internal static void Raise<A>(Action<A> ev, A a)
	{
		try
		{
			ev?.Invoke(a);
		}
		catch (Exception ex)
		{
			Log.Error("DayNight event handler: " + ex);
		}
	}

	internal static void Raise<A, B>(Action<A, B> ev, A a, B b)
	{
		try
		{
			ev?.Invoke(a, b);
		}
		catch (Exception ex)
		{
			Log.Error("DayNight event handler: " + ex);
		}
	}

	internal static void Raise<A, B, C>(Action<A, B, C> ev, A a, B b, C c)
	{
		try
		{
			ev?.Invoke(a, b, c);
		}
		catch (Exception ex)
		{
			Log.Error("DayNight event handler: " + ex);
		}
	}
}
