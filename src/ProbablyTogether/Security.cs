using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

public static class Security
{
	private sealed class Walk
	{
		public GenericClientMono O;

		public bool Arriving;

		public float Since;
	}

	public const string CounterLock = "counter";

	private const float ArriveTimeout = 12f;

	private const float LeaveTimeout = 12f;

	private const float ShootoutTimeout = 15f;

	private const float PendingSeconds = 6f;

	private static bool inited;

	private static int followSeq;

	private static float reportPendingUntil;

	private static float shotPendingUntil;

	private static bool aimSent;

	private static string stolenArrestVisit = "";

	internal static bool ClientOfficersHere;

	private static int serverAiming = -1;

	private static float shootoutDueAt = -1f;

	internal static GenericClientMono CurrentOfficer;

	private static readonly FieldInfo walkingInField = AccessTools.Field(typeof(GenericClientMono), "walkingIn");

	private static readonly MethodInfo createEleanorArrest = AccessTools.Method(typeof(StoreClientUniqueList), "CreateEleanorArrest", (Type[])null, (Type[])null);

	private static readonly List<Walk> walks = new List<Walk>();

	private static int walkLogs;

	public static int AimingPlayer { get; private set; } = -1;

	public static string LastRefusal { get; private set; } = "";

	public static int RefusalCount { get; private set; }

	public static event Action<string, string> Refused;

	public static event Action<int, bool> AimChanged;

	[SystemInit]
	internal static void Init()
	{
		if (!inited)
		{
			inited = true;
			Net.On<MsgSecAct>("sec.report", ServerReport, null);
			Net.On<MsgSecDone>("sec.reported", null, ClientReported);
			Net.On<MsgSecAct>("sec.shoot", ServerShoot, null);
			Net.On<MsgSecDone>("sec.shot", null, ClientShot);
			Net.On<MsgSecPoster>("sec.poster", ServerPoster, ClientPoster);
			Net.On<MsgSecAim>("sec.aim", ServerAim, ClientAim);
			Net.On<MsgSecRefused>("sec.refused", null, ClientRefused);
			Net.On("sec.arrived", null, delegate(MsgSecFollow m)
			{
				ClientFollow("arrived", m);
			});
			Net.On("sec.left", null, delegate(MsgSecFollow m)
			{
				ClientFollow("left", m);
			});
			Net.On("sec.arrestDone", null, delegate(MsgSecFollow m)
			{
				ClientFollow("arrestDone", m);
			});
			Net.On("sec.shootoutStart", null, delegate(MsgSecFollow m)
			{
				ClientFollow("shootoutStart", m);
			});
			Net.On("sec.arrestStolen", null, delegate(MsgSecFollow m)
			{
				ClientFollow("arrestStolen", m);
			});
			Net.On<MsgSecState>("sec.state", null, SecurityState.ClientReceive);
			Net.On<MsgSecAct>("sec.holster", ServerHolster, null);
			Net.PlayerLeft += OnPlayerLeft;
			Snapshot.ClientReady += SecurityState.SendTo;
			Snapshot.Loaded += OnSnapshotLoaded;
			Digest.AddPart("security", DigestPart);
		}
	}

	public static string DigestPart()
	{
		if (!Game.ShopLoaded || Game.Store == null)
		{
			return "";
		}
		MsgSecState msgSecState = SecurityState.Capture();
		return msgSecState.fp + "|" + (msgSecState.arresting ? 'A' : 'a') + (msgSecState.shootout ? 'S' : 's') + (msgSecState.shot ? 'X' : 'x');
	}

	private static void OnSnapshotLoaded()
	{
		ClientOfficersHere = false;
		reportPendingUntil = 0f;
		shotPendingUntil = 0f;
		aimSent = false;
		stolenArrestVisit = "";
		SetAiming(-1, on: false);
	}

	private static void OnPlayerLeft(PlayerInfo p)
	{
		if (Net.IsServer && serverAiming == p.Id)
		{
			serverAiming = -1;
			SendAimToOthers(p.Id, on: false);
		}
		if (Net.IsClient && AimingPlayer == p.Id)
		{
			SetAiming(-1, on: false);
		}
	}

	public static string VisitKey()
	{
		if (!Customers.SomeoneAtCounter)
		{
			return "";
		}
		return Customers.VisitKey();
	}

	public static string CustomerId()
	{
		StoreClientInstance val = Game.Store?.currentClientInstance;
		if (val == null)
		{
			return "";
		}
		try
		{
			return val.GetClientBlueprint()?.identifier ?? "";
		}
		catch
		{
			return "?";
		}
	}

	private static StoreClient Customer()
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
				result = ((currentClientInstance != null) ? currentClientInstance.GetClientBlueprint() : null);
			}
			return (StoreClient)result;
		}
		catch
		{
			return null;
		}
	}

	internal static GenericClientMono Officer(int who)
	{
		StoreUIManager instance = StoreUIManager.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return null;
		}
		return (GenericClientMono)(who switch
		{
			2 => instance.secGameObject2, 
			1 => instance.secGameObject, 
			_ => null, 
		});
	}

	internal static int OfficerIndex(GenericClientMono o)
	{
		StoreUIManager instance = StoreUIManager.Instance;
		if ((Object)(object)o == (Object)null || (Object)(object)instance == (Object)null)
		{
			return 0;
		}
		if ((Object)(object)o == (Object)(object)instance.secGameObject)
		{
			return 1;
		}
		if ((Object)(object)o == (Object)(object)instance.secGameObject2)
		{
			return 2;
		}
		return 0;
	}

	internal static bool OfficerActive(int who)
	{
		GenericClientMono val = Officer(who);
		if ((Object)(object)val != (Object)null)
		{
			return ((Component)val).gameObject.activeSelf;
		}
		return false;
	}

	private static void Notify(string text)
	{
		try
		{
			if ((Object)(object)StoreUIManager.Instance != (Object)null)
			{
				StoreUIManager.Instance.Notify(Kit.Safe(Lang.Tr(text)), "default");
			}
		}
		catch
		{
		}
	}

	private static string F(float s)
	{
		return s.ToString("F1", CultureInfo.InvariantCulture);
	}

	internal static void RequestReport()
	{
		if (!Net.IsLive)
		{
			Log.Info("client: security button ignored (the shop is still loading)");
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (!(realtimeSinceStartup < reportPendingUntil) && CounterFreeForMe("report"))
		{
			reportPendingUntil = realtimeSinceStartup + 6f;
			Net.ToServer("sec.report", new MsgSecAct
			{
				visit = VisitKey(),
				id = CustomerId()
			});
		}
	}

	internal static void RequestShoot()
	{
		if (Net.IsLive)
		{
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (!(realtimeSinceStartup < shotPendingUntil) && CounterFreeForMe("shoot"))
			{
				shotPendingUntil = realtimeSinceStartup + 6f;
				Net.ToServer("sec.shoot", new MsgSecAct
				{
					visit = VisitKey(),
					id = CustomerId()
				});
			}
		}
	}

	internal static void RequestPoster(bool pulled)
	{
		if (Net.IsLive)
		{
			Net.ToServer("sec.poster", new MsgSecPoster
			{
				pulled = pulled
			});
		}
	}

	internal static bool AimAllowed()
	{
		return CounterFreeForMe("aim");
	}

	internal static void SendAim(bool on)
	{
		if (Net.IsLive && aimSent != on)
		{
			aimSent = on;
			Net.ToServer("sec.aim", new MsgSecAim
			{
				on = on
			});
		}
	}

	internal static void RequestHolster()
	{
		if (Net.IsLive && aimSent)
		{
			Net.ToServer("sec.holster", new MsgSecAct
			{
				visit = VisitKey(),
				id = CustomerId()
			});
			Log.Info("client: shotgun put away without a shot: asking the server to send '" + CustomerId() + "' off");
		}
	}

	internal static bool LocalStolenArrestResult()
	{
		if (stolenArrestVisit.Length > 0)
		{
			return stolenArrestVisit == VisitKey();
		}
		return false;
	}

	private static bool CounterFreeForMe(string what)
	{
		string text = Customers.LocalCounterRefusal();
		if (text == null)
		{
			return true;
		}
		LocalRefuse(what, text);
		return false;
	}

	private static void LocalRefuse(string what, string reason)
	{
		LastRefusal = reason;
		RefusalCount++;
		Log.Info("client: " + what + " refused: " + reason);
		Notify(reason);
		try
		{
			Security.Refused?.Invoke(what, reason);
		}
		catch (Exception ex)
		{
			Log.Error("Security.Refused handler: " + ex);
		}
	}

	private static string CounterCheck(int from, string visit)
	{
		if (!Game.ShopLoaded || (Object)(object)StoreUIManager.Instance == (Object)null)
		{
			return "the shop is not open yet";
		}
		if (Game.Store.currentClientInstance == null)
		{
			return "nobody is at the counter";
		}
		string text = VisitKey();
		if (visit != text)
		{
			return "that customer is no longer at the counter";
		}
		if (DayNightRun.RunOver)
		{
			return "the run has ended";
		}
		if (!Customers.TryCounter(from, out var refusal))
		{
			return refusal;
		}
		return null;
	}

	private static void Refuse(int player, string what, string why)
	{
		Log.Info("server: refused " + what + " from " + Net.PlayerName(player) + ": " + why);
		Net.ToPlayer(player, "sec.refused", new MsgSecRefused
		{
			what = what,
			reason = why
		});
	}

	private static void ServerReport(MsgSecAct m, int from)
	{
		string text = CounterCheck(from, m.visit);
		if (text == null)
		{
			StoreClient val = Customer();
			if (StoreUIManager.Instance.isShot)
			{
				text = "the customer was shot";
			}
			else if (val == null)
			{
				text = "nobody is at the counter";
			}
			else if (!val.CanReport())
			{
				text = (val.reported ? "security is already on the way" : "security will not come for this customer");
			}
			else if (StoreReputation.IsPerkUnlocked("SEC_ALARM_IGNORED"))
			{
				text = "security ignores your alarm";
			}
		}
		if (text != null)
		{
			Refuse(from, "report", text);
			return;
		}
		PlayerStore store = Game.Store;
		string text2 = VisitKey();
		Seed seed;
		using (Replay.Server(out seed))
		{
			Net.ToAll("sec.reported", new MsgSecDone
			{
				by = from,
				visit = text2,
				id = CustomerId(),
				seed = seed
			});
			store.OnCustomerReported();
		}
		StoreUIManager instance = StoreUIManager.Instance;
		string text3 = ((!store.isClientBeingArrested) ? "intercepted (no arrest)" : (instance.isReportedAug ? "augment: dismissed, officers in 2.5 s" : ((store.onSecAlarmLeftAction != null) ? "officers called" : "officers called (no reward pending)")));
		Log.Info("server: " + Net.PlayerName(from) + " called security on '" + m.id + "' (visit " + text2 + "): " + text3);
	}

	private static void ServerShoot(MsgSecAct m, int from)
	{
		string text = CounterCheck(from, m.visit);
		PlayerStore store = Game.Store;
		StoreUIManager instance = StoreUIManager.Instance;
		if (text == null)
		{
			Shotgun val = (((Object)(object)instance.shotgun != (Object)null) ? instance.shotgun.GetComponent<Shotgun>() : null);
			if (!store.isShootingUnlocked)
			{
				text = "the shotgun is not unlocked";
			}
			else if (!instance.CanShoot())
			{
				text = "the shot was already fired";
			}
			else if ((Object)(object)val != (Object)null && !val.CanPickup())
			{
				text = "you cannot shoot this customer";
			}
		}
		if (text != null)
		{
			Refuse(from, "shoot", text);
			return;
		}
		string text2 = VisitKey();
		bool flag = Customer()?.isAug ?? false;
		Seed seed;
		using (Replay.Server(out seed))
		{
			Net.ToAll("sec.shot", new MsgSecDone
			{
				by = from,
				visit = text2,
				id = CustomerId(),
				isAug = flag,
				seed = seed
			});
			instance.Shoot();
		}
		shootoutDueAt = Time.realtimeSinceStartup + 15f;
		Log.Info("server: " + Net.PlayerName(from) + " shot '" + m.id + "' (visit " + text2 + ", " + (flag ? "an augment" : "not an augment") + ")");
	}

	private static void ServerHolster(MsgSecAct m, int from)
	{
		string text = CounterCheck(from, m.visit);
		PlayerStore store = Game.Store;
		StoreUIManager instance = StoreUIManager.Instance;
		if (text == null)
		{
			if (serverAiming != from)
			{
				text = "you are not aiming";
			}
			else if (instance.isShot)
			{
				text = "the shot was already fired";
			}
			else if (store.isClientBeingArrested || store.isHandlingShootout)
			{
				text = "security is dealing with this customer";
			}
		}
		serverAiming = ((serverAiming == from) ? (-1) : serverAiming);
		if (text != null)
		{
			Refuse(from, "holster", text);
			return;
		}
		string text2 = CustomerId();
		bool flag = Customer()?.isAug ?? false;
		int augPresence = store.augPresence;
		store.DismissCurrentClient(true, true, true);
		Log.Info("server: " + Net.PlayerName(from) + " put the shotgun away: '" + text2 + "' sent off (visit " + m.visit + (flag ? (", an augment left alive: presence " + augPresence + " -> " + store.augPresence) : "") + ")");
	}

	private static void ServerPoster(MsgSecPoster m, int from)
	{
		StorePoster instance = StorePoster.Instance;
		if (!Game.ShopLoaded || (Object)(object)instance == (Object)null)
		{
			Refuse(from, "poster", "the shop is not open yet");
			return;
		}
		if (DayNightRun.RunOver)
		{
			Refuse(from, "poster", "the run has ended");
			return;
		}
		if (instance.isPulled == m.pulled)
		{
			Net.ToPlayer(from, "sec.poster", new MsgSecPoster
			{
				pulled = instance.isPulled,
				by = -1
			});
			return;
		}
		using (Net.Apply())
		{
			if (m.pulled)
			{
				instance.PullUp(true);
			}
			else
			{
				instance.PullDown(true);
			}
		}
		Net.ToAll("sec.poster", new MsgSecPoster
		{
			pulled = m.pulled,
			by = from
		});
		Log.Info("server: " + Net.PlayerName(from) + (m.pulled ? " pulled up the poster (inspections search the hidden compartment)" : " put the poster down"));
	}

	private static void ServerAim(MsgSecAim m, int from)
	{
		if (m.on && !Customers.TryCounter(from, out var refusal))
		{
			Refuse(from, "aim", refusal);
			return;
		}
		Locks.Touch(Customers.CounterLockNow, from);
		if (m.on)
		{
			serverAiming = from;
		}
		else if (serverAiming == from)
		{
			serverAiming = -1;
		}
		SendAimToOthers(from, m.on);
	}

	private static void SendAimToOthers(int player, bool on)
	{
		foreach (int item in new List<int>(Net.Players.Keys))
		{
			if (item != player)
			{
				Net.ToPlayer(item, "sec.aim", new MsgSecAim
				{
					on = on,
					by = player
				});
			}
		}
	}

	internal static IDisposable BeginFollow(string kind, int who, long itemId = 0L)
	{
		if (!Net.IsServer || Net.Applying || !Net.IsLive)
		{
			return null;
		}
		Seed seed;
		IDisposable result = Replay.Server(out seed);
		MsgSecFollow msgSecFollow = new MsgSecFollow
		{
			n = ++followSeq,
			who = who,
			visit = VisitKey(),
			id = CustomerId(),
			itemId = itemId,
			seed = seed
		};
		try
		{
			Net.ToAll(kind, msgSecFollow);
		}
		catch (Exception ex)
		{
			Log.Error("server: " + kind + " broadcast failed: " + ex.Message);
		}
		Log.Info("server: " + kind + " #" + msgSecFollow.n + " (officer " + who + ", customer '" + msgSecFollow.id + "' visit " + msgSecFollow.visit + ((itemId != 0L) ? (", item " + itemId) : "") + ")");
		return result;
	}

	internal static void OnShootoutStarted()
	{
		shootoutDueAt = -1f;
	}

	internal static long FindUnownedCrate()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return 0L;
		}
		GameGridInventory[] array = (GameGridInventory[])(object)new GameGridInventory[2] { instance.backInvinvElement, instance.frontInvinvElement };
		foreach (GameGridInventory val in array)
		{
			if (val == null)
			{
				continue;
			}
			foreach (GameItem childItem in ((GameInventory)val).childItems)
			{
				if (childItem != null && childItem.identifier == "luxury_supply_crate" && !GeneralHelper.IsItemOwned(childItem))
				{
					return Ids.Of(childItem);
				}
			}
		}
		return 0L;
	}

	private static void ClientReported(MsgSecDone m)
	{
		if (m.by == Net.LocalPlayer)
		{
			reportPendingUntil = 0f;
		}
		if (!Game.ShopLoaded)
		{
			return;
		}
		string text = VisitKey();
		if (m.visit != text)
		{
			Log.Warn("client: security was called on visit " + m.visit + " but our counter shows visit '" + text + "'");
		}
		ClientOfficersHere = false;
		using (Replay.Client(m.seed))
		{
			Game.Store.OnCustomerReported();
		}
	}

	private static void ClientShot(MsgSecDone m)
	{
		if (m.by == Net.LocalPlayer)
		{
			shotPendingUntil = 0f;
		}
		if (!Game.ShopLoaded || (Object)(object)StoreUIManager.Instance == (Object)null)
		{
			return;
		}
		string text = VisitKey();
		if (m.visit != text)
		{
			Log.Warn("client: shot at visit " + m.visit + " but our counter shows visit '" + text + "'");
		}
		StoreClient val = Customer();
		if (val != null && val.isAug != m.isAug)
		{
			Log.Warn("client: the server's customer is " + (m.isAug ? "" : "not ") + "an augment, ours is " + (val.isAug ? "" : "not ") + "(the shootout result follows the server)");
		}
		using (Replay.Client(m.seed))
		{
			StoreUIManager.Instance.Shoot();
		}
	}

	private static void ClientPoster(MsgSecPoster m)
	{
		StorePoster instance = StorePoster.Instance;
		if (!Game.ShopLoaded || (Object)(object)instance == (Object)null)
		{
			return;
		}
		bool flag = m.by != Net.LocalPlayer || DayLog.Replaying;
		using (Net.Apply())
		{
			if (m.pulled)
			{
				instance.PullUp(flag);
			}
			else
			{
				instance.PullDown(flag);
			}
		}
	}

	private static void ClientAim(MsgSecAim m)
	{
		SetAiming(m.on ? m.by : ((AimingPlayer == m.by) ? (-1) : AimingPlayer), m.on);
	}

	private static void SetAiming(int player, bool on)
	{
		int aimingPlayer = AimingPlayer;
		AimingPlayer = player;
		if (aimingPlayer == player)
		{
			return;
		}
		try
		{
			Security.AimChanged?.Invoke(on ? player : aimingPlayer, on);
		}
		catch (Exception ex)
		{
			Log.Error("Security.AimChanged handler: " + ex);
		}
	}

	private static void ClientRefused(MsgSecRefused m)
	{
		if (m.what == "report")
		{
			reportPendingUntil = 0f;
		}
		if (m.what == "shoot")
		{
			shotPendingUntil = 0f;
		}
		if (m.what == "aim" || m.what == "holster")
		{
			aimSent = false;
		}
		LocalRefuse(m.what, m.reason);
	}

	private static void ClientFollow(string what, MsgSecFollow m)
	{
		StoreUIManager instance = StoreUIManager.Instance;
		PlayerStore store = Game.Store;
		if (!Game.ShopLoaded || (Object)(object)instance == (Object)null)
		{
			return;
		}
		string text = VisitKey();
		if (m.visit.Length > 0 && text.Length > 0 && m.visit != text)
		{
			Log.Warn("client: " + what + " for visit " + m.visit + " ('" + m.id + "') but our counter shows visit " + text);
		}
		switch (what)
		{
		case "arrived":
		{
			GenericClientMono val5 = Officer(m.who);
			if ((Object)(object)val5 != (Object)null)
			{
				val5.SetImmediatlyArrive();
			}
			ClientOfficersHere = true;
			using (Replay.Client(m.seed))
			{
				instance.OnGenericArrived("SEC");
				break;
			}
		}
		case "left":
		{
			using (Replay.Client(m.seed))
			{
				instance.OnGenericLeft("SEC");
			}
			GenericClientMono val3 = Officer(m.who);
			if ((Object)(object)val3 != (Object)null && ((Component)val3).gameObject.activeSelf)
			{
				((Component)val3).gameObject.SetActive(false);
			}
			if (DayLog.Replaying)
			{
				for (int i = 1; i <= 2; i++)
				{
					GenericClientMono val4 = Officer(i);
					if ((Object)(object)val4 != (Object)null && ((Component)val4).gameObject.activeSelf)
					{
						((Component)val4).gameObject.SetActive(false);
					}
				}
			}
			ClientOfficersHere = false;
			break;
		}
		case "arrestDone":
			using (Replay.Client(m.seed))
			{
				instance.OnArrestDialoguePlayed();
				break;
			}
		case "shootoutStart":
			ClientOfficersHere = false;
			using (Replay.Client(m.seed))
			{
				instance.StartShootoutSequence();
				break;
			}
		case "arrestStolen":
		{
			GameItem val = ((m.itemId != 0L) ? Ids.Find(m.itemId) : null);
			StoreClient val2 = Customer();
			if (val == null)
			{
				Log.Warn("client: the Eleanor crate " + m.itemId + " is not in our shop");
			}
			using (Replay.Client(m.seed))
			{
				Dialogue customOnArrestDialogue = val2?.customOnArrestDialogue;
				if (val != null)
				{
					GeneralHelper.SetItemOwned(val, false);
				}
				if (val2 != null && val != null && createEleanorArrest != null)
				{
					try
					{
						val2.customOnArrestDialogue = (Dialogue)createEleanorArrest.Invoke(null, new object[1] { val });
					}
					catch (Exception ex)
					{
						Log.Warn("client: Eleanor arrest dialogue: " + ex.Message);
					}
				}
				stolenArrestVisit = text;
				if (store.ArrestCurrentClientForStolenGoods())
				{
					store.storeClientData.eleanorArrested = true;
					break;
				}
				stolenArrestVisit = "";
				if (val != null)
				{
					GeneralHelper.SetItemOwned(val, true);
				}
				if (val2 != null)
				{
					val2.customOnArrestDialogue = customOnArrestDialogue;
				}
				break;
			}
		}
		}
	}

	internal static void OfficerWalkStarted(GenericClientMono o, bool arriving)
	{
		if (Net.IsServer && !((Object)(object)o == (Object)null))
		{
			walks.RemoveAll((Walk w) => (Object)(object)w.O == (Object)(object)o);
			walks.Add(new Walk
			{
				O = o,
				Arriving = arriving,
				Since = Time.realtimeSinceStartup
			});
		}
	}

	internal static void OfficerWalkEnded(GenericClientMono o, bool arrived)
	{
		if (!Net.IsServer || (Object)(object)o == (Object)null)
		{
			return;
		}
		Walk walk = walks.Find((Walk x) => (Object)(object)x.O == (Object)(object)o && x.Arriving == arrived);
		if (walk != null)
		{
			walks.Remove(walk);
			if (walkLogs++ < 40)
			{
				Log.Info("server: officer " + OfficerIndex(o) + (arrived ? " reached the counter" : " left the shop") + " after " + F(Time.realtimeSinceStartup - walk.Since) + " s (walk)");
			}
		}
	}

	[SystemTick]
	private static void SystemTick()
	{
		if (Net.IsOnline)
		{
			Tick();
		}
	}

	internal static void Tick()
	{
		if (!Net.IsServer)
		{
			if (Net.IsClient)
			{
				SecurityState.ClientTick();
			}
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (DayNightRun.RunOver)
		{
			walks.Clear();
			shootoutDueAt = -1f;
			return;
		}
		for (int num = walks.Count - 1; num >= 0; num--)
		{
			Walk walk = walks[num];
			if ((Object)(object)walk.O == (Object)null)
			{
				walks.RemoveAt(num);
			}
			else
			{
				float num2 = (walk.Arriving ? 12f : 12f);
				if (!(realtimeSinceStartup - walk.Since < num2))
				{
					walks.RemoveAt(num);
					int num3 = OfficerIndex(walk.O);
					try
					{
						if (walk.Arriving)
						{
							if ((walkingInField == null || (bool)walkingInField.GetValue(walk.O)) && ((Component)walk.O).gameObject.activeSelf)
							{
								Log.Warn("server: officer " + num3 + " did not reach the counter in " + F(num2) + " s; arriving by timer");
								walk.O.OnArrived();
							}
						}
						else if (((Component)walk.O).gameObject.activeSelf)
						{
							Log.Warn("server: officer " + num3 + " did not leave in " + F(num2) + " s; leaving by timer");
							walk.O.OnLeft();
						}
					}
					catch (Exception ex)
					{
						Log.Error("server: officer timer: " + ex);
					}
				}
			}
		}
		if (shootoutDueAt > 0f && realtimeSinceStartup > shootoutDueAt)
		{
			shootoutDueAt = -1f;
			PlayerStore store = Game.Store;
			StoreUIManager instance = StoreUIManager.Instance;
			if (store != null && (Object)(object)instance != (Object)null && instance.isShot && !store.isHandlingShootout)
			{
				Log.Warn("server: the shot's animation never called the officer in " + F(15f) + " s; calling it by timer");
				try
				{
					if ((Object)(object)instance.body != (Object)null)
					{
						instance.body.SetActive(true);
					}
					instance.StartShootoutSequence();
				}
				catch (Exception ex2)
				{
					Log.Error("server: shootout timer: " + ex2);
				}
			}
		}
		SecurityState.ServerTick(realtimeSinceStartup);
	}
}
