using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

public static class Customers
{
	private sealed class VisitBox
	{
		public int N;
	}

	internal sealed class Scope
	{
		public IDisposable D;

		public Seed Seed;

		public bool Done;

		public StoreClientInstance Before;

		public int V;

		public string Id;

		public int By;

		public long[] Gone;

		public bool Had;

		public bool Top;

		public void Close()
		{
			if (Done)
			{
				return;
			}
			Done = true;
			try
			{
				D?.Dispose();
			}
			catch (Exception ex)
			{
				Log.Error("customers: closing a replay scope: " + ex);
			}
		}
	}

	public const string Counter = "counter";

	private static readonly ConditionalWeakTable<StoreClientManager, VisitBox> visits = new ConditionalWeakTable<StoreClientManager, VisitBox>();

	public const string JacksonLock = "outing.jackson";

	internal static bool InBell;

	internal static bool LeavePending;

	internal static StoreClient ArrivedFor;

	internal static long[] PendingGone;

	private static readonly Dictionary<int, bool> autoPref = new Dictionary<int, bool>();

	private static long autoCallUntilMs;

	private static int lastHolder = -1;

	private static long lastTickMs;

	public static int Refusals;

	public static int Mismatches;

	public static int Applied;

	private static bool inited;

	private static readonly FieldInfo walkingInField = AccessTools.Field(typeof(StoreClientMono), "walkingIn");

	internal static readonly FieldInfo ignoreOnLeftField = AccessTools.Field(typeof(StoreClientMono), "ignoreOnLeftFlag");

	private static float lastWaitSent = -100f;

	public static int BotDone;

	public static int BotFailed;

	public static int BotMarks;

	public static readonly List<string> BotMarkNames = new List<string>();

	public static int Visit
	{
		get
		{
			StoreClientManager val = Game.Store?.storeClientManager;
			if (val != null)
			{
				return visits.GetOrCreateValue(val).N;
			}
			return 0;
		}
		internal set
		{
			StoreClientManager val = Game.Store?.storeClientManager;
			if (val != null)
			{
				visits.GetOrCreateValue(val).N = value;
			}
		}
	}

	public static int CounterHolder => Locks.HeldBy("counter");

	public static string CounterLockNow
	{
		get
		{
			if (!DayNightOuting.IsOut)
			{
				return "counter";
			}
			return "outing.jackson";
		}
	}

	public static bool SomeoneAtCounter
	{
		get
		{
			try
			{
				return Game.Store?.currentClientInstance != null;
			}
			catch
			{
				return false;
			}
		}
	}

	internal static bool CounterTalkOn
	{
		get
		{
			try
			{
				return DialogueSystem.Counter.Current != null;
			}
			catch
			{
				return false;
			}
		}
	}

	private static bool LocalAuto
	{
		get
		{
			try
			{
				return (Object)(object)EmporiumOption.Instance != (Object)null && EmporiumOption.Instance.autoCallNext;
			}
			catch
			{
				return false;
			}
		}
	}

	public static event Action<string, string> Refused;

	public static string VisitKey()
	{
		return Game.Day + ":" + Visit;
	}

	public static int VisitSeq()
	{
		int visit = Visit;
		if (visit > 0)
		{
			return Game.Day * 1000 + visit;
		}
		return 0;
	}

	public static string HeldText(int h, string lockName)
	{
		return Net.PlayerName(h) + ((lockName == "outing.jackson") ? " is haggling with Dr. Jackson." : " is at the counter.");
	}

	public static bool TryCounter(int player, out string refusal, out bool took)
	{
		refusal = null;
		took = false;
		if (!Net.IsServer || player <= 0 || !SomeoneAtCounter)
		{
			return true;
		}
		string counterLockNow = CounterLockNow;
		int num = Locks.Holder(counterLockNow);
		if (num == player)
		{
			Locks.Touch(counterLockNow, player);
			return true;
		}
		if (num >= 0)
		{
			if (Locks.TakeOver(counterLockNow, player))
			{
				took = true;
				return true;
			}
			refusal = HeldText(num, counterLockNow);
			Locks.Wanted(counterLockNow, player);
			return false;
		}
		if (!Locks.TryTake(counterLockNow, player))
		{
			refusal = "Someone else is at the counter.";
			return false;
		}
		took = true;
		Log.Info("customers: " + Net.PlayerName(player) + " took the free " + ((counterLockNow == "outing.jackson") ? "Dr. Jackson counter" : "counter") + " (visit " + Visit + ", " + (CurrentId() ?? "?") + ")");
		return true;
	}

	public static bool TryCounter(int player, out string refusal)
	{
		bool took;
		return TryCounter(player, out refusal, out took);
	}

	public static void GiveBack(int player, bool took)
	{
		if (took && Net.IsServer)
		{
			string counterLockNow = CounterLockNow;
			if (Locks.Holder(counterLockNow) == player)
			{
				Locks.Release(counterLockNow, player);
				Log.Info("customers: gave the counter back (" + Net.PlayerName(player) + "'s action was refused)");
			}
		}
	}

	public static string CounterHeldByOther(int player)
	{
		if (!Net.IsOnline || !SomeoneAtCounter)
		{
			return null;
		}
		int num = Locks.HeldBy(CounterLockNow);
		if (num < 0 || num == player || Locks.CanTakeOver(CounterLockNow, player))
		{
			return null;
		}
		return Net.PlayerName(num);
	}

	/// Who else holds the counter right now, takeover or not (for the helper check on the customer's item).
	public static string CounterHolderOther(int player)
	{
		if (!Net.IsOnline || !SomeoneAtCounter)
		{
			return null;
		}
		int num = Locks.HeldBy(CounterLockNow);
		if (num < 0 || num == player)
		{
			return null;
		}
		return Net.PlayerName(num);
	}

	public static string LocalCounterRefusal()
	{
		if (!Net.IsOnline || !SomeoneAtCounter)
		{
			return null;
		}
		string counterLockNow = CounterLockNow;
		int num = Locks.HeldBy(counterLockNow);
		if (num < 0 || num == Net.LocalPlayer || Locks.CanTakeOver(counterLockNow, Net.LocalPlayer))
		{
			return null;
		}
		return HeldText(num, counterLockNow);
	}

	public static bool CounterOk(int player)
	{
		string refusal;
		return TryCounter(player, out refusal);
	}

	[SystemInit]
	internal static void Init()
	{
		if (inited)
		{
			return;
		}
		inited = true;
		Net.On<MsgCustCall>("customers.call", OnCallRequest, null);
		Net.On<MsgCustDismiss>("customers.dismiss", OnDismissRequest, null);
		Net.On<MsgCustCounter>("customers.take", OnTakeRequest, null);
		Net.On<MsgCustCounter>("customers.release", OnReleaseRequest, null);
		Net.On<MsgCustWait>("customers.wait", OnWaitRequest, null);
		Net.On<MsgCustCalled>("customers.called", null, OnCalled);
		Net.On<MsgCustArrived>("customers.arrived", null, OnArrived);
		Net.On<MsgCustDismissed>("customers.dismissed", null, OnDismissed);
		Net.On<MsgCustLeft>("customers.left", null, OnLeft);
		Net.On<MsgCustRefused>("customers.refused", null, OnRefused);
		if (!string.IsNullOrEmpty(Args.Bot))
		{
			Net.On<MsgCustBot>("customers.bot.do", null, OnBotDo);
			Net.On<MsgCustBot>("customers.bot.done", OnBotDone, null);
			Net.On("customers.bot.mark", delegate(MsgCustBot m, int from)
			{
				BotMarks++;
				if (!string.IsNullOrEmpty(m.name) && m.name.Length <= 40 && BotMarkNames.Count < 200)
				{
					BotMarkNames.Add(m.name);
				}
				Log.Info("customers: bot " + Net.PlayerName(from) + " mark " + BotMarks + " (" + m.name + ")");
			}, null);
		}
		Digest.AddPart("customers", () => (Game.Store?.storeClientManager != null) ? Hash().ToString("x16") : "");
		DayNight.AddDigestPart("customers", Hash);
		ItemsUse.RegisterValidator("customers", CounterUseCheck);
		Locks.Changed += delegate(string name, int holder)
		{
			if (name == "counter" && holder >= 0)
			{
				lastHolder = holder;
			}
		};
		Net.PlayerLeft += delegate(PlayerInfo p)
		{
			autoPref.Remove(p.Id);
		};
	}

	[SystemTick]
	internal static void Tick()
	{
		if (Net.IsServer)
		{
			ServerTick();
		}
	}

	private static string CounterUseCheck(UseContext ctx)
	{
		if (ctx == null || ctx.Player <= 0 || !ctx.TouchesCounter)
		{
			return null;
		}
		if (CounterHelp.IsPartnerCheck(ctx))
		{
			return CounterHelp.Admit(ctx);
		}
		if (!TryCounter(ctx.Player, out var refusal))
		{
			return refusal;
		}
		return null;
	}

	public static void RequestCall()
	{
		if (!Net.IsOnline)
		{
			PlayerStore.Instance.TryCallNextClient();
		}
		else if (Net.IsClient)
		{
			if (Net.IsLive)
			{
				Net.ToServer("customers.call", new MsgCustCall
				{
					auto = LocalAuto
				});
			}
		}
		else
		{
			OnCallRequest(new MsgCustCall
			{
				auto = LocalAuto
			}, Net.LocalPlayer);
		}
	}

	public static void RequestDismiss(bool callNext)
	{
		if (!Net.IsOnline)
		{
			PlayerStore.Instance.TryPlayerDismissCurrentClient(callNext);
			return;
		}
		MsgCustDismiss msgCustDismiss = new MsgCustDismiss
		{
			v = Visit,
			id = CurrentId(),
			callNext = callNext,
			auto = LocalAuto
		};
		if (Net.IsClient)
		{
			if (Net.IsLive)
			{
				Net.ToServer("customers.dismiss", msgCustDismiss);
			}
		}
		else
		{
			OnDismissRequest(msgCustDismiss, Net.LocalPlayer);
		}
	}

	public static void RequestTakeCounter()
	{
		if (Net.IsClient && Net.IsLive)
		{
			Net.ToServer("customers.take", new MsgCustCounter
			{
				v = Visit,
				auto = LocalAuto
			});
		}
	}

	public static void RequestReleaseCounter()
	{
		if (Net.IsClient && Net.IsLive)
		{
			Net.ToServer("customers.release", new MsgCustCounter
			{
				v = Visit,
				auto = LocalAuto
			});
		}
	}

	private static void Refuse(int player, string what, string why)
	{
		Refusals++;
		Log.Info("customers: refused " + what + " from " + Net.PlayerName(player) + ": " + why);
		if (Net.IsServer && player > 0)
		{
			Net.ToPlayer(player, "customers.refused", new MsgCustRefused
			{
				what = what,
				why = why
			});
		}
	}

	private static void SetAuto(int player, bool auto)
	{
		autoPref[player] = auto;
	}

	internal static void ApplyAuto()
	{
		if (!Net.IsServer)
		{
			return;
		}
		try
		{
			EmporiumOption instance = EmporiumOption.Instance;
			if (!((Object)(object)instance == (Object)null))
			{
				int num = Locks.Holder("counter");
				bool value = default(bool);
				instance.autoCallNext = num >= 0 && autoPref.TryGetValue(num, out value) && value;
			}
		}
		catch (Exception ex)
		{
			Log.Warn("customers: autoCallNext: " + ex.Message);
		}
	}

	private static void OnCallRequest(MsgCustCall m, int from)
	{
		if (!Game.ShopLoaded)
		{
			Refuse(from, "call", "The shop is not loaded.");
			return;
		}
		SetAuto(from, m.auto);
		PlayerStore store = Game.Store;
		StoreClientManager storeClientManager = store.storeClientManager;
		int num = Locks.Holder("counter");
		if (num >= 0 && num != from)
		{
			Refuse(from, "call", Net.PlayerName(num) + " is at the counter.");
			return;
		}
		if ((int)store.storeState != 1)
		{
			Refuse(from, "call", "The shop is not open.");
			return;
		}
		if (store.currentClientInstance != null)
		{
			Refuse(from, "call", "A customer is already at the counter.");
			return;
		}
		if (storeClientManager == null || storeClientManager.clientStack == null || storeClientManager.clientStack.Count == 0)
		{
			Refuse(from, "call", "Nobody is waiting.");
			return;
		}
		if (((Object)(object)StoreUIManager.Instance != (Object)null && StoreUIManager.Instance.isShot) || store.isHandlingShootout || store.isClientBeingArrested)
		{
			Refuse(from, "call", "Not now.");
			return;
		}
		if (store.lockNextBellFlag > 0)
		{
			if (LeavePending && Locks.TryTake("counter", from))
			{
				store.callNextWhenPossible = true;
				autoCallUntilMs = Clock.Ms + 15000;
				Log.Info("customers: " + Net.PlayerName(from) + " calls the next customer once the last one has left");
			}
			else
			{
				Refuse(from, "call", "A customer is still walking.");
			}
			return;
		}
		if (!Locks.TryTake("counter", from))
		{
			Refuse(from, "call", "Someone else is at the counter.");
			return;
		}
		Locks.Touch("counter", from);
		store.TryCallNextClient();
		if (store.currentClientInstance == null)
		{
			Locks.Release("counter", from);
			Refuse(from, "call", "The next customer could not be called.");
		}
	}

	private static void OnDismissRequest(MsgCustDismiss m, int from)
	{
		if (!Game.ShopLoaded)
		{
			Refuse(from, "dismiss", "The shop is not loaded.");
			return;
		}
		SetAuto(from, m.auto);
		PlayerStore store = Game.Store;
		StoreClientInstance currentClientInstance = store.currentClientInstance;
		if (currentClientInstance == null)
		{
			Refuse(from, "dismiss", "Nobody is at the counter.");
			return;
		}
		StoreClient clientBlueprint = currentClientInstance.GetClientBlueprint();
		string refusal;
		if (m.v != Visit || (m.id != null && m.id != clientBlueprint?.identifier))
		{
			Refuse(from, "dismiss", "That customer has already left.");
		}
		else if (CounterHeldByOther(from) != null)
		{
			Refuse(from, "dismiss", HeldText(Locks.Holder(CounterLockNow), CounterLockNow));
		}
		else if (!store.IsClientDimissable())
		{
			Refuse(from, "dismiss", "This customer will not leave yet.");
		}
		else if (store.lockNextBellFlag > 0)
		{
			Refuse(from, "dismiss", "Wait until the customer reaches the counter.");
		}
		else if (!TryCounter(from, out refusal))
		{
			Refuse(from, "dismiss", refusal);
		}
		else
		{
			store.TryPlayerDismissCurrentClient(m.callNext);
		}
	}

	private static void OnTakeRequest(MsgCustCounter m, int from)
	{
		if (Game.ShopLoaded)
		{
			SetAuto(from, m.auto);
			string refusal;
			bool took;
			if (Game.Store.currentClientInstance == null)
			{
				Refuse(from, "take", "Nobody is at the counter.");
			}
			else if (!TryCounter(from, out refusal, out took))
			{
				Refuse(from, "take", refusal);
			}
			else if (took)
			{
				Log.Info("customers: " + Net.PlayerName(from) + " took the counter (visit " + Visit + ")");
			}
		}
	}

	private static void OnWaitRequest(MsgCustWait m, int from)
	{
		if (!SomeoneAtCounter)
		{
			return;
		}
		string counterLockNow = CounterLockNow;
		int num = Locks.Holder(counterLockNow);
		if (num >= 0 && num != from && Locks.TakeOver(counterLockNow, from))
		{
			return;
		}
		if (num >= 0 && num != from)
		{
			Locks.Wanted(counterLockNow, from);
			if (Log.Allow("cwait" + from, 15000L, out var skipped))
			{
				Log.Info("customers: " + Net.PlayerName(from) + " is waiting for the counter (" + Net.PlayerName(num) + " has it, frees in " + Locks.SecondsLeft(counterLockNow) + " s if idle)" + Log.More(skipped));
			}
		}
	}

	internal static void NoteWaiting()
	{
		if (Net.IsClient && Net.IsLive && !Snapshot.IsLoading && !DayLog.Replaying && CrewCustomers.AtLeastMin(Client.ServerMod))
		{
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (!(realtimeSinceStartup - lastWaitSent < 4f))
			{
				lastWaitSent = realtimeSinceStartup;
				Net.ToServer("customers.wait", new MsgCustWait
				{
					v = Visit
				});
			}
		}
	}

	private static void OnReleaseRequest(MsgCustCounter m, int from)
	{
		string counterLockNow = CounterLockNow;
		if (Locks.Holder(counterLockNow) != from)
		{
			Refuse(from, "release", "You are not at the counter.");
			return;
		}
		Locks.Release(counterLockNow, from);
		Log.Info("customers: " + Net.PlayerName(from) + " stepped away from the counter (visit " + Visit + ")");
	}

	internal static Scope Open()
	{
		Scope scope = new Scope();
		scope.D = Replay.Server(out scope.Seed);
		return scope;
	}

	private static string Dt()
	{
		if (!string.IsNullOrEmpty(Args.Bot))
		{
			return DigestText();
		}
		return null;
	}

	internal static void AfterServerCall(Scope s)
	{
		StoreClientInstance currentClientInstance = Game.Store.currentClientInstance;
		if (currentClientInstance == null || currentClientInstance == s.Before)
		{
			if (autoCallUntilMs > 0)
			{
				autoCallUntilMs = 0L;
				if (currentClientInstance == null)
				{
					ReleaseCounter("nobody else is waiting");
				}
			}
			return;
		}
		autoCallUntilMs = 0L;
		StoreClient clientBlueprint = currentClientInstance.GetClientBlueprint();
		MsgCustCalled msgCustCalled = new MsgCustCalled
		{
			v = Visit,
			id = clientBlueprint.identifier,
			sprite = (clientBlueprint.spriteName ?? ""),
			tell = (int)clientBlueprint.augIdleTell,
			by = Locks.Holder("counter"),
			seed = s.Seed,
			d = Hash(),
			rd = Replay.Digest(),
			dt = Dt()
		};
		Net.ToAll("customers.called", msgCustCalled);
		Log.Info("customers: called " + clientBlueprint.identifier + " (visit " + msgCustCalled.v + ", face " + msgCustCalled.sprite + ", tell " + clientBlueprint.augIdleTell.ToString() + ") for " + ((msgCustCalled.by >= 0) ? Net.PlayerName(msgCustCalled.by) : "nobody") + "; " + mgrCount() + " waiting");
	}

	internal static void AfterServerArrived(Scope s)
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
			obj = ((currentClientInstance != null) ? currentClientInstance.GetClientBlueprint() : null);
		}
		StoreClient val = (StoreClient)obj;
		if (val != null)
		{
			Net.ToAll("customers.arrived", new MsgCustArrived
			{
				v = Visit,
				id = val.identifier,
				seed = s.Seed,
				d = Hash(),
				rd = Replay.Digest(),
				dt = Dt()
			});
			Log.Info("customers: " + val.identifier + " reached the counter (visit " + Visit + ")");
		}
	}

	internal static void AfterServerPlayerDismiss(Scope s, bool callNext)
	{
		bool flag = Game.Store?.currentClientInstance != s.Before;
		MsgCustDismissed msgCustDismissed = new MsgCustDismissed
		{
			v = s.V,
			id = s.Id,
			callNext = callNext,
			by = s.By,
			ended = flag,
			gone = (flag ? (PendingGone ?? new long[0]) : new long[0]),
			seed = s.Seed,
			d = Hash(),
			rd = Replay.Digest(),
			dt = Dt()
		};
		PendingGone = null;
		Net.ToAll("customers.dismissed", msgCustDismissed);
		Log.Info("customers: " + (flag ? "dismissed " : "dismiss intercepted by ") + s.Id + " (visit " + s.V + ", by " + Net.PlayerName(s.By) + ", callNext " + callNext + ", " + msgCustDismissed.gone.Length + " item(s) gone)");
		ForgetGone(msgCustDismissed.gone);
	}

	internal static void AfterServerDismiss(Scope s, bool callNextArg, bool right, bool interrupt)
	{
		_ = Game.Store;
		PendingGone = s.Gone;
		ReleaseCounter("the visit ended");
		if (s.Top)
		{
			MsgCustLeft msgCustLeft = new MsgCustLeft
			{
				v = s.V,
				id = s.Id,
				callNext = callNextArg,
				right = right,
				interrupt = interrupt,
				gone = (s.Gone ?? new long[0]),
				seed = s.Seed,
				d = Hash(),
				rd = Replay.Digest(),
				dt = Dt()
			};
			Net.ToAll("customers.left", msgCustLeft);
			Log.Info("customers: " + s.Id + " left (visit " + s.V + ", by itself; " + msgCustLeft.gone.Length + " item(s) gone)");
			ForgetGone(msgCustLeft.gone);
			PendingGone = null;
		}
	}

	private static void ReleaseCounter(string why)
	{
		int num = Locks.Holder("counter");
		if (num >= 0)
		{
			Locks.Release("counter", -1);
			Log.Info("customers: counter released (" + Net.PlayerName(num) + "): " + why);
		}
	}

	internal static long[] CaptureGone()
	{
		List<long> list = new List<long>();
		try
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				return list.ToArray();
			}
			GameGridInventory[] array = (GameGridInventory[])(object)new GameGridInventory[2] { instance.frontInvinvElement, instance.backInvinvElement };
			foreach (GameGridInventory val in array)
			{
				if (((val != null) ? ((GameInventory)val).childItems : null) == null)
				{
					continue;
				}
				foreach (GameItem childItem in ((GameInventory)val).childItems)
				{
					if (childItem != null && !GeneralHelper.IsItemOwned(childItem))
					{
						long num = Ids.Of(childItem);
						if (num > 0)
						{
							list.Add(num);
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("customers: listing counter items: " + ex.Message);
		}
		return list.ToArray();
	}

	private static void ForgetGone(long[] gone)
	{
		if (gone == null)
		{
			return;
		}
		for (int i = 0; i < gone.Length; i++)
		{
			GameItem val = Ids.Find(gone[i]);
			if (val != null && val.parentInventory == null)
			{
				Ids.Forget(val);
			}
		}
	}

	internal static void BumpVisit()
	{
		Visit++;
	}

	private static int mgrCount()
	{
		return (Game.Store?.storeClientManager?.clientStack?.Count).GetValueOrDefault();
	}

	internal static void ServerTick()
	{
		if (!Net.IsServer || !Game.ShopLoaded)
		{
			return;
		}
		long ms = Clock.Ms;
		if (ms - lastTickMs < 500)
		{
			return;
		}
		lastTickMs = ms;
		PlayerStore store = Game.Store;
		StoreClientInstance currentClientInstance = store.currentClientInstance;
		int num = Locks.Holder("counter");
		if (currentClientInstance == null)
		{
			if (num >= 0 && ms > autoCallUntilMs && (!store.callNextWhenPossible || !LeavePending) && !CounterTalkOn)
			{
				ReleaseCounter("nobody is at the counter");
			}
		}
		else if (num < 0)
		{
			PassCounterIfNeeded();
		}
	}

	internal static void PassCounterIfNeeded()
	{
		if (!Net.IsServer || !Game.ShopLoaded)
		{
			return;
		}
		StoreClientInstance currentClientInstance = Game.Store.currentClientInstance;
		if (currentClientInstance == null || Locks.Holder("counter") >= 0)
		{
			return;
		}
		StoreClient clientBlueprint = currentClientInstance.GetClientBlueprint();
		if (clientBlueprint != null && !clientBlueprint.dismissable)
		{
			int num = NextPlayer(lastHolder);
			if (num > 0 && Locks.TryTake("counter", num))
			{
				Log.Info("customers: " + clientBlueprint.identifier + " cannot be dismissed; the counter passes to " + Net.PlayerName(num));
			}
		}
	}

	private static int NextPlayer(int after)
	{
		List<int> list = new List<int>();
		foreach (int key in Net.Players.Keys)
		{
			if (key > 0 && Net.IsPlayerLive(key))
			{
				list.Add(key);
			}
		}
		if (list.Count == 0)
		{
			return -1;
		}
		list.Sort();
		foreach (int item in list)
		{
			if (item > after)
			{
				return item;
			}
		}
		return list[0];
	}

	private static string CurrentId()
	{
		try
		{
			PlayerStore store = Game.Store;
			object result;
			if (store == null)
			{
				result = null;
			}
			else
			{
				StoreClientInstance currentClientInstance = store.currentClientInstance;
				result = ((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.identifier);
			}
			return (string)result;
		}
		catch
		{
			return null;
		}
	}

	private static bool Ready(string kind)
	{
		if (Game.ShopLoaded)
		{
			return true;
		}
		Log.Warn("customers: " + kind + " while the shop is not loaded: ignored");
		return false;
	}

	private static void Broken(string kind, string why)
	{
		Mismatches++;
		Log.Warn("customers: cannot apply " + kind + ": " + why + "; asking for a resync");
		Snapshot.RequestResync("customers: cannot apply " + kind + ": " + why);
	}

	private static void CheckDigest(string kind, long d, long rd, string dt)
	{
		long num = Hash();
		long num2 = Replay.Digest();
		if (num != d || num2 != rd)
		{
			Mismatches++;
			Log.Warn("customers: after " + kind + " the client differs from the server: customers " + ((num == d) ? "same" : "DIFFERENT") + ", action digest " + ((num2 == rd) ? "same" : "DIFFERENT") + ((dt != null) ? ("; server " + dt + " / mine " + DigestText()) : ""));
		}
	}

	private static void FinishPendingLeave()
	{
		if (LeavePending && !((Object)(object)StoreClientMono.Instance == (Object)null))
		{
			PlayerStore.Instance.callNextWhenPossible = false;
			try
			{
				StoreClientMono.Instance.OnLeft();
			}
			catch (Exception ex)
			{
				Log.Warn("customers: finishing the last walk-out: " + ex.Message);
			}
			LeavePending = false;
		}
	}

	private static void OnCalled(MsgCustCalled m)
	{
		if (!Ready("customers.called"))
		{
			return;
		}
		PlayerStore instance = PlayerStore.Instance;
		List<StoreClient> list = instance.storeClientManager?.clientStack;
		if (instance.currentClientInstance != null)
		{
			Broken("customers.called", "a customer (" + CurrentId() + ") is still at the counter here");
			return;
		}
		if (list == null || list.Count == 0 || list[0] == null || list[0].identifier != m.id)
		{
			Broken("customers.called", "the queue head here is " + ((list == null || list.Count == 0) ? "empty" : list[0]?.identifier) + ", the server called " + m.id);
			return;
		}
		FinishPendingLeave();
		StoreClient val = list[0];
		if (!string.IsNullOrEmpty(m.sprite))
		{
			val.spriteName = m.sprite;
		}
		val.augIdleTell = (StoreClient.AugIdleTell)m.tell;
		using (Replay.Client(m.seed))
		{
			instance.GetNextClient();
			StoreUIManager.Instance.QueueClientArriveAnimation();
		}
		Applied++;
		if (Visit != m.v)
		{
			Log.Info("customers: visit number here " + Visit + ", server " + m.v + ": taking the server's");
			Visit = m.v;
		}
		CheckDigest("customers.called " + m.id, m.d, m.rd, m.dt);
	}

	private static void OnArrived(MsgCustArrived m)
	{
		if (!Ready("customers.arrived"))
		{
			return;
		}
		PlayerStore instance = PlayerStore.Instance;
		StoreClientInstance currentClientInstance = instance.currentClientInstance;
		StoreClient val = ((currentClientInstance != null) ? currentClientInstance.GetClientBlueprint() : null);
		if (val == null || val.identifier != m.id)
		{
			Broken("customers.arrived", "the customer here is " + (val?.identifier ?? "nobody") + ", the server's is " + m.id);
			return;
		}
		if (ArrivedFor == val)
		{
			Log.Info("customers: " + m.id + " already arrived here");
			return;
		}
		SnapArrival(instance);
		using (Replay.Client(m.seed))
		{
			StoreUIManager.Instance.OnNextClientArrived();
		}
		Applied++;
		CheckDigest("customers.arrived " + m.id, m.d, m.rd, m.dt);
	}

	private static void SnapArrival(PlayerStore ps)
	{
		StoreClientMono instance = StoreClientMono.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			ps.isClientArrived = true;
			return;
		}
		if (walkingInField != null && (bool)walkingInField.GetValue(instance))
		{
			try
			{
				StoreUIManager.Instance.clientGameObject.SetImmediatlyArrive();
			}
			catch (Exception ex)
			{
				Log.Warn("customers: snapping the walk-in: " + ex.Message);
			}
			if (walkingInField != null && (bool)walkingInField.GetValue(instance))
			{
				walkingInField.SetValue(instance, false);
				ps.isClientArrived = true;
				try
				{
					StoreSecButton instance2 = StoreSecButton.Instance;
					if (instance2 != null)
					{
						instance2.ResetLight();
					}
				}
				catch
				{
				}
				ps.ClientFlagDown();
			}
		}
		ps.isClientArrived = true;
	}

	private static void OnDismissed(MsgCustDismissed m)
	{
		if (!Ready("customers.dismissed"))
		{
			return;
		}
		PlayerStore instance = PlayerStore.Instance;
		string text = CurrentId();
		if (m.ended && AlreadyEnded(m.v, m.id))
		{
			Log.Info("customers: dismiss of " + m.id + " (visit " + m.v + "); the visit already ended here");
			Leftovers("customers.dismissed", m.gone);
			CheckDigest("customers.dismissed " + m.id + " (already ended)", m.d, m.rd, m.dt);
			return;
		}
		if (text == null || text != m.id)
		{
			Broken("customers.dismissed", "the customer here is " + (text ?? "nobody") + ", the server dismissed " + m.id);
			return;
		}
		if (Visit != m.v)
		{
			Log.Info("customers: dismiss for visit " + m.v + " while this client counts " + Visit);
		}
		using (Replay.Client(m.seed))
		{
			instance.TryPlayerDismissCurrentClient(m.callNext);
		}
		Applied++;
		if (instance.currentClientInstance == null != m.ended)
		{
			Log.Warn("customers: dismiss of " + m.id + (m.ended ? " ended the visit on the server but not here" : " was intercepted on the server but not here"));
		}
		Leftovers("customers.dismissed", m.gone);
		CheckDigest("customers.dismissed " + m.id, m.d, m.rd, m.dt);
	}

	private static bool AlreadyEnded(int v, string id)
	{
		string text = CurrentId();
		int visit = Visit;
		if (text == null)
		{
			return visit >= v;
		}
		return visit > v;
	}

	private static void OnLeft(MsgCustLeft m)
	{
		if (!Ready("customers.left"))
		{
			return;
		}
		PlayerStore instance = PlayerStore.Instance;
		string text = CurrentId();
		if (AlreadyEnded(m.v, m.id))
		{
			Log.Info("customers: " + m.id + " left the server (visit " + m.v + "); the visit already ended here (visit " + Visit + ", at the counter " + (text ?? "nobody") + ")");
			Leftovers("customers.left", m.gone);
			CheckDigest("customers.left " + m.id + " (already ended)", m.d, m.rd, m.dt);
		}
		else if (text == null || text != m.id)
		{
			Broken("customers.left", "the customer here is " + (text ?? "nobody") + ", " + m.id + " left the server");
		}
		else
		{
			using (Replay.Client(m.seed))
			{
				instance.DismissCurrentClient(m.callNext, m.right, m.interrupt);
			}
			Applied++;
			Leftovers("customers.left", m.gone);
			CheckDigest("customers.left " + m.id, m.d, m.rd, m.dt);
		}
	}

	private static void Leftovers(string kind, long[] gone)
	{
		if (gone == null)
		{
			return;
		}
		int num = 0;
		for (int i = 0; i < gone.Length; i++)
		{
			long id = gone[i];
			GameItem val = Ids.Find(id);
			if (val == null)
			{
				continue;
			}
			if (val.parentInventory != null)
			{
				num++;
				try
				{
					using (Net.Apply())
					{
						val.Destroy();
					}
				}
				catch (Exception ex)
				{
					Log.Warn("customers: removing leftover item " + id + ": " + ex.Message);
				}
			}
			Ids.Forget(val);
		}
		if (num > 0)
		{
			Mismatches++;
			Log.Warn("customers: " + kind + ": " + num + " customer item(s) were still on the counter here; removed");
		}
	}

	private static void OnRefused(MsgCustRefused m)
	{
		Log.Info("customers: the server refused " + m.what + ": " + m.why);
		try
		{
			Customers.Refused?.Invoke(m.what, m.why);
		}
		catch (Exception ex)
		{
			Log.Error("Customers.Refused handler: " + ex);
		}
	}

	private static void Entry(StringBuilder sb, StoreClient c, bool current)
	{
		if (c == null)
		{
			sb.Append('-');
			return;
		}
		sb.Append(c.identifier).Append('|').Append(c.spriteName)
			.Append('|')
			.Append((int)c.clientIntent)
			.Append('|')
			.Append(c.GetBudget())
			.Append('|')
			.Append(c.isAug ? 1 : 0)
			.Append('|')
			.Append(c.augTier)
			.Append('|')
			.Append(c.attractedItemId)
			.Append('|')
			.Append(c.dismissable ? 1 : 0);
		if (current)
		{
			sb.Append('|').Append((int)c.augIdleTell);
		}
	}

	private static void Dict(StringBuilder sb, Dictionary<string, int> d)
	{
		if (d == null)
		{
			sb.Append('-');
			return;
		}
		List<string> list = new List<string>(d.Keys);
		list.Sort(StringComparer.Ordinal);
		ulong h = 14695981039346656037uL;
		foreach (string item in list)
		{
			Mix(ref h, item);
			Mix(ref h, d[item].ToString(CultureInfo.InvariantCulture));
		}
		sb.Append(list.Count).Append(':').Append(h.ToString("x16"));
	}

	public static string DigestText()
	{
		PlayerStore store = Game.Store;
		StoreClientManager val = store?.storeClientManager;
		if (val == null)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder();
		try
		{
			stringBuilder.Append("cur=");
			StoreClientInstance currentClientInstance = store.currentClientInstance;
			Entry(stringBuilder, (currentClientInstance != null) ? currentClientInstance.GetClientBlueprint() : null, current: true);
			stringBuilder.Append(";q=");
			if (val.clientStack != null)
			{
				foreach (StoreClient item in val.clientStack)
				{
					Entry(stringBuilder, item, current: false);
					stringBuilder.Append(',');
				}
			}
			stringBuilder.Append(";fut=");
			if (store.futurStoreClientIdQueue != null)
			{
				for (int i = 0; i < store.futurStoreClientIdQueue.Count; i++)
				{
					stringBuilder.Append(store.futurStoreClientIdQueue[i]).Append(':').Append((store.futurStoreClientDayQueue != null && i < store.futurStoreClientDayQueue.Count) ? store.futurStoreClientDayQueue[i] : (-1))
						.Append(',');
				}
			}
			stringBuilder.Append(";cd=");
			Dict(stringBuilder, val.clientTracker);
			stringBuilder.Append(";seen=");
			Dict(stringBuilder, val.permanentClientTracker);
			stringBuilder.Append(";dm=").Append(store.dealMakerBargainBonus).Append(store.dealMakerActivatedThisCustomer ? "+" : "-");
		}
		catch (Exception ex)
		{
			stringBuilder.Append("!" + ex.GetType().Name);
		}
		return stringBuilder.ToString();
	}

	public static long Hash()
	{
		ulong h = 14695981039346656037uL;
		Mix(ref h, DigestText());
		return (long)h;
	}

	private static void Mix(ref ulong h, string s)
	{
		if (s == null)
		{
			h ^= 255uL;
			h *= 1099511628211uL;
			return;
		}
		foreach (char c in s)
		{
			h ^= c;
			h *= 1099511628211uL;
		}
		h ^= (ulong)s.Length;
		h *= 1099511628211uL;
	}

	public static void BotMark(string what)
	{
		if (Net.IsClient && Net.IsLive)
		{
			Net.ToServer("customers.bot.mark", new MsgCustBot
			{
				name = what
			});
		}
	}

	public static string BotDo(string op, params string[] args)
	{
		if (!Net.IsServer)
		{
			return "not the server";
		}
		if (Game.Store?.storeClientManager == null)
		{
			return "no shop";
		}
		Seed seed;
		string text;
		using (Replay.Server(out seed))
		{
			text = RunBotOp(op, args);
		}
		Net.ToAll("customers.bot.do", new MsgCustBot
		{
			name = op,
			ids = args,
			seed = seed,
			d = Hash(),
			dt = DigestText()
		});
		return text + ": " + mgrCount() + " waiting, at the counter " + (CurrentId() ?? "nobody");
	}

	private static string RunBotOp(string op, string[] args)
	{
		PlayerStore instance = PlayerStore.Instance;
		StoreClientManager storeClientManager = instance.storeClientManager;
		args = args ?? new string[0];
		switch (op)
		{
		case "add":
		{
			string[] array = args;
			foreach (string text in array)
			{
				storeClientManager.AddNextClient(StoreClientListDict.CreateStoreClient(text));
			}
			return "added " + string.Join(",", args);
		}
		case "aug":
			storeClientManager.AddNextClient(StoreClientList.CreateAugMed(1));
			return "added aug_med";
		case "wait":
			instance.SetupWaitingClient((args.Length != 0) ? args[0] : "lou");
			return "waiting visitor " + ((args.Length != 0) ? args[0] : "lou");
		case "owned":
		{
			GameItem val3 = DirectoryMaster.Item((args.Length != 0) ? args[0] : "gene_scanner", true);
			if (val3 == null)
			{
				return "no such item";
			}
			instance.AddDirectSellingItemToTable(val3, true, false, false, 0);
			string[] obj = new string[5]
			{
				"ours on the counter: ",
				val3.identifier,
				" #",
				Ids.Of(val3).ToString(),
				null
			};
			GameInventory parentInventory = val3.parentInventory;
			EmporiumEntry instance2 = EmporiumEntry.Instance;
			obj[4] = (((object)parentInventory == ((instance2 != null) ? instance2.frontInvinvElement : null)) ? "" : " (NOT placed)");
			return string.Concat(obj);
		}
		case "sell":
		{
			string text2 = ((args.Length != 0) ? args[0] : "fake_cigarette");
			GameItem val = ((text2 == "fake_cigarette") ? PreBuiltItemHelper.FakeCigarette() : DirectoryMaster.Item(text2, true));
			if (val == null)
			{
				return "no such item";
			}
			instance.AddDirectSellingItemToTable(val, false, false, false, 0);
			return "the customer's " + val.identifier + " #" + Ids.Of(val) + " on the counter";
		}
		case "talk":
		{
			StoreClientInstance currentClientInstance = instance.currentClientInstance;
			StoreClient val2 = ((currentClientInstance != null) ? currentClientInstance.GetClientBlueprint() : null);
			if (val2 == null)
			{
				return "nobody at the counter";
			}
			val2.interogationDialogue = new Dialogue().SetText("CLIENT", "Lab: an interrogation answer.");
			val2.glasseDialogue = new Dialogue().SetText("CLIENT", "Lab: an answer about the glasses.");
			return val2.identifier + " can be interrogated and asked about the glasses";
		}
		case "stock":
		{
			int result;
			int num = ((args.Length != 0 && int.TryParse(args[0], out result)) ? Math.Min(result, 5) : 2);
			for (int i = 0; i < num; i++)
			{
				instance.AddDirectSellingItemToTable(ItemSpawner.SpawnFromTable("packedFoodTable"), false, false, false, 0);
			}
			return "stock x" + num;
		}
		default:
			return "unknown op " + op;
		}
	}

	public static string BotTimerDismiss()
	{
		if (!Net.IsServer || Game.Store?.currentClientInstance == null)
		{
			return "nobody at the counter";
		}
		string text = CurrentId();
		Game.Store.DismissCurrentClient(false, true, true);
		return "dismissed " + text + " by a timer";
	}

	private static void OnBotDo(MsgCustBot m)
	{
		if (Ready("customers.bot.do"))
		{
			using (Replay.Client(m.seed))
			{
				RunBotOp(m.name, m.ids);
			}
			Applied++;
			CheckDigest("customers.bot.do " + m.name, m.d, Replay.Digest(), m.dt);
		}
	}

	public static int CounterItems()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return -1;
		}
		int num = 0;
		GameGridInventory[] array = (GameGridInventory[])(object)new GameGridInventory[2] { instance.frontInvinvElement, instance.backInvinvElement };
		foreach (GameGridInventory val in array)
		{
			if (((val != null) ? ((GameInventory)val).childItems : null) == null)
			{
				continue;
			}
			foreach (GameItem childItem in ((GameInventory)val).childItems)
			{
				if (childItem != null && !GeneralHelper.IsItemOwned(childItem))
				{
					num++;
				}
			}
		}
		return num;
	}

	private static void OnBotDone(MsgCustBot m, int from)
	{
		if (m.ok)
		{
			BotDone++;
		}
		else
		{
			BotFailed++;
		}
		Log.Info("customers: bot " + Net.PlayerName(from) + " (" + m.name + ") finished " + (m.ok ? "PASS" : "FAIL"));
	}

	public static void BotReportDone(string name, bool ok)
	{
		if (Net.IsClient && Net.IsLive)
		{
			Net.ToServer("customers.bot.done", new MsgCustBot
			{
				name = name,
				ok = ok
			});
		}
	}
}
