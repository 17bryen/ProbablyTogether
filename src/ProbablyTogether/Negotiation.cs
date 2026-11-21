using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace ProbablyTogether;

public static class Negotiation
{
	public const string CounterLock = "counter";

	private static bool inited;

	public static int TabletSkipped;

	public static string LastTabletGate = "";

	internal const string AskConfirm = "\u0001confirm";

	internal static bool confirmFromServer;

	public static int ConfirmsAsked;

	private static long lastResyncAskMs = -100000L;

	private static readonly MethodInfo cycleBarter = AccessTools.Method(typeof(NegociationUIManager), "CycleBarterOfferSet", (Type[])null, (Type[])null);

	private static readonly int[] PricePercents = new int[4] { 10, 25, 50, 75 };

	private static readonly string[] bargainFields = new string[14]
	{
		"discountOffered", "discountPercent", "markupOffered", "markupPercent", "markupApplied", "blackmailOffered", "blackmailApplied", "buyingMarkupOffered", "buyingMarkupPercent", "buyingDiscountOffered",
		"buyingDiscountPercent", "buyingDiscountApplied", "threatenOffered", "threatenApplied"
	};

	private static FieldInfo[] bargainInfo;

	private static readonly FieldInfo bargainTracked = AccessTools.Field(typeof(BargainUIManager), "trackedClient");

	public const int MaxEstimate = 100000000;

	private static readonly MethodInfo setAllowed = AccessTools.Method(typeof(AppraisalItemsWindow), "SetAllowed", (Type[])null, (Type[])null);

	private static string lastCareer;

	private static string pendingCareer;

	private static readonly string[] CigReasons = new string[5] { "CIGARETTE_LETTERING_ISSUE", "CIGARETTE_COLOR_ISSUE", "CIGARETTE_SEAL_ISSUE", "CIGARETTE_LOGO_ISSUE", "CIGARETTE_BOX_ISSUE" };

	private static readonly string[] StampReasons = new string[5] { "STAMP_LETTERING_ISSUE", "STAMP_LOGO_ISSUE", "STAMP_SEAL_ISSUE", "STAMP_AMOUNT_ISSUE", "STAMP_CHIP_ISSUE" };

	private static readonly string[] InjectorReasons = new string[5] { "ISSUE_INDICATOR", "ISSUE_LABEL", "ISSUE_SERIAL", "ISSUE_BODY", "ISSUE_COLOR" };

	public const int MaxNameLength = 64;

	public static int LabelerClicks;

	private static readonly FieldInfo labelerCurItem = AccessTools.Field(typeof(LabelerUIManager), "curItem");

	private static readonly FieldInfo blueCurItem = AccessTools.Field(typeof(LabelerBlueUIManager), "curItem");

	private static readonly FieldInfo elementFeature = AccessTools.Field(typeof(LabelerElement), "thisFeature");

	public static int Actor => Net.Actor;

	public static bool LocalActs
	{
		get
		{
			if (Net.IsOnline)
			{
				if (Net.IsClient)
				{
					return Actor == Net.LocalPlayer;
				}
				return false;
			}
			return true;
		}
	}

	internal static bool Intercept
	{
		get
		{
			if (Net.IsClient)
			{
				return !Net.Applying;
			}
			return false;
		}
	}

	public static bool LocalHoldsCounter
	{
		get
		{
			if (Net.IsOnline)
			{
				return Locks.HeldBy(Customers.CounterLockNow) == Net.LocalPlayer;
			}
			return true;
		}
	}

	internal static bool ShopUp
	{
		get
		{
			if (PlayerStore.HasInstance)
			{
				return (Object)(object)EmporiumEntry.Instance != (Object)null;
			}
			return false;
		}
	}

	public static event Action<string, string> Refused;

	internal static bool TabletForMe(out string why)
	{
		if (DayLog.Replaying)
		{
			why = "rebuilding from the day log";
			return false;
		}
		int actor = Net.Actor;
		if (actor >= 0)
		{
			why = ((ItemsMove.ApplyingActor >= 0) ? "items action by " : "action by ") + actor;
			return actor == Net.LocalPlayer;
		}
		if (DialogueSync.UiForOthers)
		{
			why = "another player's dialogue step";
			return false;
		}
		why = "counter holder " + Locks.HeldBy(Customers.CounterLockNow);
		return LocalHoldsCounter;
	}

	[SystemInit]
	private static void Init()
	{
		if (inited)
		{
			return;
		}
		inited = true;
		try
		{
			RegisterDeals();
			RegisterLabels();
			RegisterAppraisal();
			RegisterConfront();
			Digest.AddPart("nego", () => Hash().ToString("x16"));
		}
		catch (Exception ex)
		{
			Log.Error("negotiation: registering message kinds failed: " + ex);
		}
	}

	private static void RegisterDeals()
	{
		Net.On<MsgNegoDeal>("nego.buy", ServerBuy, ClientBuy);
		Net.On<MsgNegoDeal>("nego.sell", ServerSell, ClientSell);
		Net.On<MsgNegoAccept>("nego.wholesale", ServerWholesale, ClientWholesale);
		Net.On<MsgNegoAccept>("nego.accept", ServerAccept, ClientAccept);
		Net.On<MsgNegoBarterSet>("nego.barterSet", ServerBarterSet, ClientBarterSet);
		Net.On<MsgNegoTalk>("nego.talk", ServerTalk, ClientTalk);
		Net.On<MsgBargain>("bargain.offer", ServerBargain, ClientBargain);
		Net.On<MsgNegoReply>("nego.reply", null, ClientReply);
	}

	internal static StoreClient CurrentCustomer()
	{
		if (!PlayerStore.HasInstance)
		{
			return null;
		}
		StoreClientInstance currentClientInstance = PlayerStore.Instance.currentClientInstance;
		if (currentClientInstance == null)
		{
			return null;
		}
		return currentClientInstance.GetClientBlueprint();
	}

	internal static NegoVisit LocalVisit()
	{
		return new NegoVisit
		{
			seq = Hooks.VisitSeq(),
			who = CurrentCustomer()?.identifier
		};
	}

	internal static bool MayHaggle(string what)
	{
		string text = Customers.LocalCounterRefusal();
		if (text == null)
		{
			return true;
		}
		Log.Info("negotiation: " + what + " ignored: " + text);
		Negotiation.Refused?.Invoke(what, text);
		return false;
	}

	internal static long[] IdsOf(IEnumerable<GameItem> items)
	{
		List<long> list = new List<long>();
		if (items != null)
		{
			foreach (GameItem item in items)
			{
				list.Add(Ids.Of(item));
			}
		}
		return list.ToArray();
	}

	internal static List<GameItem> Resolve(long[] ids, out string why)
	{
		why = null;
		List<GameItem> list = new List<GameItem>();
		if (ids == null || ids.Length == 0)
		{
			why = "no items named";
			return null;
		}
		if (ids.Length > 256)
		{
			why = "too many items";
			return null;
		}
		HashSet<long> hashSet = new HashSet<long>();
		for (int i = 0; i < ids.Length; i++)
		{
			long num = ids[i];
			if (num == 0L || !hashSet.Add(num))
			{
				why = "item id " + num + " is missing or repeated";
				return null;
			}
			GameItem val = Find(num);
			if (val == null)
			{
				why = "no item with id " + num;
				return null;
			}
			list.Add(val);
		}
		return list;
	}

	internal static GameItem Find(long id)
	{
		return Ids.Find(id);
	}

	private static string CheckVisit(NegoVisit v)
	{
		StoreClient val = CurrentCustomer();
		if (val == null)
		{
			return "nobody is at the counter";
		}
		if (v == null || v.who != val.identifier)
		{
			return "that customer already left";
		}
		int num = Hooks.VisitSeq();
		if (v.seq != 0 && num != 0 && v.seq != num)
		{
			return "that customer already left";
		}
		return null;
	}

	internal static bool Serve<T>(string kind, T m, int from, bool counter, Func<string> validate, Action act, Action<T> fill = null) where T : NegoMsg
	{
		if (!Net.IsServer || m == null)
		{
			return false;
		}
		string text = null;
		bool took = false;
		if (!ShopUp)
		{
			text = "the shop is not loaded";
		}
		else if (counter)
		{
			text = CheckVisit(m.v);
			if (text == null)
			{
				text = TakeCounter(from, out took);
			}
		}
		if (text == null)
		{
			try
			{
				text = validate?.Invoke();
			}
			catch (Exception ex)
			{
				text = "the server could not check it";
				Log.Warn("negotiation: " + kind + " check threw: " + ex.Message);
			}
		}
		if (text != null)
		{
			Customers.GiveBack(from, took);
		}
		if (text == "\u0001confirm")
		{
			Net.ToPlayer(from, "nego.reply", new MsgNegoReply
			{
				kind = kind,
				what = "confirm"
			});
			return false;
		}
		if (text != null)
		{
			Refuse(from, kind, text);
			return false;
		}
		Seed seed;
		// A counter deal files purchases by the asking player's setting, never into a trip's bag.
		int filing = (!counter) ? (-1) : ((m.inv >= 0) ? m.inv : MyFiling());
		try
		{
			using (Replay.Server(out seed, from))
			{
				using (Filing(filing))
				{
					act();
				}
			}
		}
		catch (Exception ex2)
		{
			Log.Error("negotiation: server " + kind + " from " + Net.PlayerName(from) + " threw mid-deal (clients will resync on the digest): " + ex2);
			Refuse(from, kind, "the server could not finish that deal");
			return false;
		}
		m.player = from;
		m.seed = seed;
		m.inv = filing;
		if (counter && m.v == null)
		{
			m.v = LocalVisit();
		}
		fill?.Invoke(m);
		m.after = CaptureAfter();
		Net.ToAll(kind, m);
		if (counter)
		{
			Locks.Touch(Customers.CounterLockNow, from);
		}
		Log.Info("negotiation: " + kind + " by " + Net.PlayerName(from) + " done; cash " + m.after.cash);
		return true;
	}

	internal static string TakeCounter(int from, out bool took)
	{
		string text = Systems.Regulars.Refusal(from);
		if (text != null)
		{
			took = false;
			return text;
		}
		if (!Customers.TryCounter(from, out var refusal, out took))
		{
			return refusal;
		}
		return null;
	}

	internal static void Mirror<T>(string kind, T m, Func<string> check, Action act) where T : NegoMsg
	{
		if (!Net.IsClient || m == null)
		{
			return;
		}
		if (!ShopUp)
		{
			Mismatch(kind + ": the shop is not loaded here");
			return;
		}
		if (m.v != null)
		{
			string text = CurrentCustomer()?.identifier;
			if (text != m.v.who)
			{
				Mismatch(kind + ": the counter customer here is " + (text ?? "nobody") + ", the server's is " + (m.v.who ?? "nobody"));
				return;
			}
		}
		string text2 = null;
		try
		{
			text2 = check?.Invoke();
		}
		catch (Exception ex)
		{
			text2 = "check threw " + ex.Message;
		}
		if (text2 != null)
		{
			Mismatch(kind + ": " + text2);
			return;
		}
		try
		{
			using (Replay.Client(m.seed, m.player))
			{
				using (Filing(m.inv))
				{
					act();
				}
			}
		}
		catch (Exception ex2)
		{
			Log.Error("negotiation: client " + kind + " threw: " + ex2);
			Mismatch(kind + " threw here");
		}
		ApplyAfter(kind, m.after);
	}

	/// This PC's "move bought items to inventory" setting as a filing (1 or 0).
	internal static int MyFiling()
	{
		try
		{
			return EmporiumOption.MoveBoughtItemsToInventory ? 1 : 0;
		}
		catch
		{
			return -1;
		}
	}

	/// Filing for the deal being applied; -1 lets the game decide.
	internal static int FilingNow = -1;

	internal static int Filed;

	private sealed class FilingScope : IDisposable
	{
		private readonly int was;

		internal FilingScope(int filing)
		{
			was = FilingNow;
			FilingNow = filing;
		}

		public void Dispose()
		{
			FilingNow = was;
		}
	}

	internal static IDisposable Filing(int filing)
	{
		return new FilingScope(filing);
	}

	internal static void Refuse(int player, string kind, string reason)
	{
		Log.Info("negotiation: refused " + kind + " from " + Net.PlayerName(player) + ": " + reason);
		if (player > 0)
		{
			Net.ToPlayer(player, "nego.reply", new MsgNegoReply
			{
				kind = kind,
				what = "refused",
				reason = reason
			});
		}
	}

	private static void ClientReply(MsgNegoReply r)
	{
		if (r == null)
		{
			return;
		}
		if (r.what == "confirm")
		{
			ConfirmsAsked++;
			if ((Object)(object)NegociationUIManager.Instance != (Object)null)
			{
				confirmFromServer = true;
				try
				{
					NegociationUIManager.Instance.OpenConfirmPanel();
				}
				finally
				{
					confirmFromServer = false;
				}
			}
		}
		else
		{
			Log.Info("negotiation: the server refused " + r.kind + ": " + r.reason);
			Negotiation.Refused?.Invoke(r.kind, r.reason);
			if (r.kind != null && r.kind.StartsWith("label.", StringComparison.Ordinal))
			{
				RefreshLabelerAfterRefusal();
			}
		}
	}

	internal static NegoAfter CaptureAfter()
	{
		NegoAfter negoAfter = new NegoAfter
		{
			rep = new Dictionary<string, double>()
		};
		if (!PlayerStore.HasInstance)
		{
			return negoAfter;
		}
		PlayerStore instance = PlayerStore.Instance;
		negoAfter.cash = instance.playerCash;
		negoAfter.favor = instance.wildFavor;
		if (instance.storeReputations != null)
		{
			foreach (StoreReputation storeReputation in instance.storeReputations)
			{
				if (storeReputation != null && storeReputation.factionId != null)
				{
					negoAfter.rep[storeReputation.factionId] = storeReputation.GetReputationExact();
				}
			}
		}
		StoreClient val = CurrentCustomer();
		if (val != null && val.useClientBudget)
		{
			negoAfter.budget = val.GetBudget();
		}
		try
		{
			negoAfter.dg = Replay.Digest();
		}
		catch
		{
			negoAfter.dg = 0L;
		}
		return negoAfter;
	}

	internal static void ApplyAfter(string kind, NegoAfter a)
	{
		if (a == null || !PlayerStore.HasInstance)
		{
			return;
		}
		PlayerStore instance = PlayerStore.Instance;
		long num = 0L;
		try
		{
			num = Replay.Digest();
		}
		catch
		{
		}
		bool flag = a.dg == 0L || num == 0L || a.dg == num;
		List<string> list = new List<string>();
		if (instance.playerCash != a.cash)
		{
			list.Add("cash " + instance.playerCash + "->" + a.cash);
			instance.playerCash = a.cash;
		}
		if (instance.wildFavor != a.favor)
		{
			list.Add("favor " + instance.wildFavor + "->" + a.favor);
			instance.wildFavor = a.favor;
		}
		if (a.rep != null && instance.storeReputations != null)
		{
			foreach (StoreReputation storeReputation in instance.storeReputations)
			{
				if (storeReputation == null || storeReputation.factionId == null || !a.rep.TryGetValue(storeReputation.factionId, out var value))
				{
					continue;
				}
				double reputationExact = storeReputation.GetReputationExact();
				if (reputationExact != value)
				{
					if (Math.Abs(reputationExact - value) > 1E-09)
					{
						list.Add("rep " + storeReputation.factionId + " " + reputationExact.ToString("0.###", CultureInfo.InvariantCulture) + "->" + value.ToString("0.###", CultureInfo.InvariantCulture));
					}
					storeReputation.SetReputation(value);
				}
			}
		}
		StoreClient val = CurrentCustomer();
		if (val != null && val.useClientBudget && a.budget.HasValue && val.GetBudget() != a.budget.Value)
		{
			list.Add("budget " + val.GetBudget() + "->" + a.budget.Value);
			val.OverrideBudget(a.budget.Value);
		}
		if (list.Count > 0)
		{
			Log.Info("negotiation: " + kind + " absolutes from the server: " + string.Join(", ", list));
		}
		if (!flag)
		{
			Mismatch(kind + ": state after the deal differs from the server's (digest " + num.ToString("x") + " vs " + a.dg.ToString("x") + ")");
		}
	}

	internal static void Mismatch(string what)
	{
		Log.Warn("negotiation: out of step with the server: " + what);
		if (Net.IsClient && !DayLog.Replaying && !Locks.LocalHoldsAny())
		{
			long num = Environment.TickCount;
			if (num - lastResyncAskMs >= 30000)
			{
				lastResyncAskMs = num;
				Snapshot.RequestResync("negotiation: " + what);
			}
		}
	}

	public static bool RequestDeal(string kind, IList<GameItem> items, bool single, bool ignoreBudget)
	{
		if (!MayHaggle(kind))
		{
			return false;
		}
		long[] array = IdsOf(items);
		if (array.Length == 0 || array.Contains(0L))
		{
			Log.Warn("negotiation: " + kind + " not sent: an item has no id from the server");
			return false;
		}
		Net.ToServer(kind, new MsgNegoDeal
		{
			v = LocalVisit(),
			ids = array,
			single = single,
			ignoreBudget = ignoreBudget,
			inv = MyFiling()
		});
		return true;
	}

	public static bool RequestAccept(string kind, string intent, GameItem item = null)
	{
		if (!MayHaggle(kind))
		{
			return false;
		}
		long itemId = ((item != null) ? Ids.Of(item) : 0);
		Net.ToServer(kind, new MsgNegoAccept
		{
			v = LocalVisit(),
			intent = intent,
			itemId = itemId,
			inv = MyFiling()
		});
		return true;
	}

	public static bool RequestBarterSet(int offset)
	{
		if (!MayHaggle("nego.barterSet"))
		{
			return false;
		}
		Net.ToServer("nego.barterSet", new MsgNegoBarterSet
		{
			v = LocalVisit(),
			offset = offset,
			inv = MyFiling()
		});
		return true;
	}

	public static bool RequestTalk(string which)
	{
		if (!MayHaggle("nego.talk"))
		{
			return false;
		}
		Net.ToServer("nego.talk", new MsgNegoTalk
		{
			v = LocalVisit(),
			which = which
		});
		return true;
	}

	public unsafe static bool RequestBargain(BargainType type, int percent)
	{
		if (!MayHaggle("bargain.offer"))
		{
			return false;
		}
		Net.ToServer("bargain.offer", new MsgBargain
		{
			v = LocalVisit(),
			type = type.ToString(),
			percent = percent
		});
		return true;
	}

	private static string TradeOpen(StoreClient c)
	{
		if (c == null)
		{
			return "nobody is at the counter";
		}
		if (NegociationUIManager.IsTradeLocked())
		{
			return "trading is locked right now";
		}
		if (c.isBusinessComplete)
		{
			return "this customer's business is done";
		}
		return null;
	}

	private static bool OnBackCounter(GameItem it)
	{
		if (it != null)
		{
			return (object)it.parentInventory == EmporiumEntry.Instance.backInvinvElement;
		}
		return false;
	}

	private static int Sum(List<GameItem> items)
	{
		int num = 0;
		foreach (GameItem item in items)
		{
			num += (int)item.GetNegociatedValue();
		}
		return num;
	}

	private static void ServerBuy(MsgNegoDeal m, int from)
	{
		List<GameItem> items = null;
		Serve("nego.buy", m, from, counter: true, delegate
		{
			StoreClient val = CurrentCustomer();
			string why = TradeOpen(val);
			if (why != null)
			{
				return why;
			}
			if (val.isRegularSellDisable)
			{
				return "this customer does not sell that way";
			}
			items = Resolve(m.ids, out why);
			if (why != null)
			{
				return why;
			}
			foreach (GameItem item in items)
			{
				if (!OnBackCounter(item))
				{
					return item.GetDisplayName(false) + " is not on the counter";
				}
				if (GeneralHelper.IsItemOwned(item))
				{
					return item.GetDisplayName(false) + " is already ours";
				}
			}
			if (m.single)
			{
				if (items.Count != 1)
				{
					return "one item at a time";
				}
				StoreClient.ClientIntent clientIntent = val.clientIntent;
				if ((int)clientIntent != 2 && (int)clientIntent != 3)
				{
					return "this customer is not selling";
				}
			}
			else if (val.isMultiBuyDisabled && items.Count > 1)
			{
				return "this customer sells one item at a time";
			}
			int num = Sum(items);
			return (num > PlayerStore.Instance.playerCash) ? ("not enough cash (" + num + " needed)") : null;
		}, delegate
		{
			RunBuy(m, items);
		});
	}

	private static void ClientBuy(MsgNegoDeal m)
	{
		List<GameItem> items = null;
		Mirror("nego.buy", m, delegate
		{
			items = Resolve(m.ids, out var why);
			return why;
		}, delegate
		{
			RunBuy(m, items);
		});
	}

	private static void RunBuy(MsgNegoDeal m, List<GameItem> items)
	{
		NegociationUIManager instance = NegociationUIManager.Instance;
		if (m.single)
		{
			PlayerStore.Instance.currentNegociatedItem = items[0];
			instance.BuyItem();
		}
		else
		{
			instance.BuyMultipleItem(items);
		}
	}

	private static void ServerSell(MsgNegoDeal m, int from)
	{
		List<GameItem> items = null;
		Serve("nego.sell", m, from, counter: true, delegate
		{
			StoreClient val = CurrentCustomer();
			string why = TradeOpen(val);
			if (why != null)
			{
				return why;
			}
			items = Resolve(m.ids, out why);
			if (why != null)
			{
				return why;
			}
			foreach (GameItem item in items)
			{
				if (!OnBackCounter(item))
				{
					return item.GetDisplayName(false) + " is not on the counter";
				}
				if (!GeneralHelper.IsItemOwned(item))
				{
					return item.GetDisplayName(false) + " is not ours to sell";
				}
			}
			PlayerStore instance = PlayerStore.Instance;
			if (m.single)
			{
				if (items.Count != 1)
				{
					return "one item at a time";
				}
				GameItem val2 = items[0];
				if (!val.CanClientExposeAnyFeatureDeep(val2) && !instance.CanSellThisItem(val2, false) && instance.CanSellThisItem(val2, true))
				{
					return "\u0001confirm";
				}
				if (!val.CanClientExposeAnyFeatureDeep(val2) && !instance.CanSellThisItem(val2, false))
				{
					return "the customer will not buy " + val2.GetDisplayName(false);
				}
			}
			else
			{
				bool flag = false;
				foreach (GameItem item2 in items)
				{
					if (!val.IsClientBuyingThisItem(item2))
					{
						return "the customer is not buying " + item2.GetDisplayName(false);
					}
					if (val.CanClientExposeAnyFeatureDeep(item2) || instance.CanSellThisItem(item2, m.ignoreBudget))
					{
						flag = true;
					}
				}
				if (!flag)
				{
					return "the customer will not buy any of these";
				}
			}
			return (string)null;
		}, delegate
		{
			RunSell(m, items);
		});
	}

	private static void ClientSell(MsgNegoDeal m)
	{
		List<GameItem> items = null;
		Mirror("nego.sell", m, delegate
		{
			items = Resolve(m.ids, out var why);
			return why;
		}, delegate
		{
			RunSell(m, items);
		});
	}

	private static void RunSell(MsgNegoDeal m, List<GameItem> items)
	{
		NegociationUIManager instance = NegociationUIManager.Instance;
		if (m.single)
		{
			PlayerStore.Instance.currentNegociatedItem = items[0];
			instance.SellItem();
		}
		else
		{
			instance.SellMultiple(items, m.ignoreBudget);
		}
	}

	private static void ServerWholesale(MsgNegoAccept m, int from)
	{
		Serve("nego.wholesale", m, from, counter: true, delegate
		{
			StoreClient val = CurrentCustomer();
			string text = TradeOpen(val);
			if (text != null)
			{
				return text;
			}
			if (!val.isOfferWholesale)
			{
				return "no wholesale offer";
			}
			if (EmporiumEntry.Instance.GetAllNonOwendItemCount() == 0)
			{
				return "nothing to buy";
			}
			int wholesalePrice = PlayerStore.Instance.GetWholesalePrice();
			return (wholesalePrice > PlayerStore.Instance.playerCash) ? ("not enough cash (" + wholesalePrice + " needed)") : null;
		}, delegate
		{
			NegociationUIManager.Instance.OnWholesaleClick();
		});
	}

	private static void ClientWholesale(MsgNegoAccept m)
	{
		Mirror("nego.wholesale", m, null, delegate
		{
			NegociationUIManager.Instance.OnWholesaleClick();
		});
	}

	private static bool AcceptIntent(string s, out StoreClient.ClientIntent i)
	{
		i = (StoreClient.ClientIntent)0;
		switch (s)
		{
		case "BARTER":
			i = (StoreClient.ClientIntent)6;
			return true;
		case "INFORMATION_DEALER":
			i = (StoreClient.ClientIntent)10;
			return true;
		case "GUNSMITH":
			i = (StoreClient.ClientIntent)15;
			return true;
		default:
			return false;
		}
	}

	private static void ServerAccept(MsgNegoAccept m, int from)
	{
		GameItem gun = null;
		Serve("nego.accept", m, from, counter: true, delegate
		{
			StoreClient val = CurrentCustomer();
			string text = TradeOpen(val);
			if (text != null)
			{
				return text;
			}
			if (!AcceptIntent(m.intent, out var i) || val.clientIntent != i)
			{
				return "this customer has no such offer";
			}
			if ((int)i == 15 && (text = PickGun(val, m.itemId, out gun)) != null)
			{
				return text;
			}
			if ((int)i != 6)
			{
				if ((int)i != 10)
				{
					if ((int)i == 15 && (val.gunOrderData == null || !val.gunOrderData.IsAccepted()))
					{
						return "the gun does not meet the order";
					}
				}
				else if (!PlayerStore.Instance.CanPay(val.serviceCost) || !val.isIntroduced)
				{
					return "not enough cash for the information";
				}
			}
			else
			{
				if (val.barterOffer == null || !val.barterOffer.IsBarterAcceptable() || !val.isIntroduced)
				{
					return "the barter is not ready";
				}
				DialogUIManager instance = DialogUIManager.Instance;
				if (((instance == null) ? ((bool?)null) : instance.GetCurrentDialog()?.isMainDialog) == true)
				{
					return "wait for the customer to finish talking";
				}
			}
			return (string)null;
		}, delegate
		{
			RunAccept(gun);
		}, delegate(MsgNegoAccept x)
		{
			if (gun != null)
			{
				x.itemId = Ids.Of(gun);
			}
		});
	}

	private static void ClientAccept(MsgNegoAccept m)
	{
		GameItem gun = null;
		Mirror("nego.accept", m, delegate
		{
			if (m.intent != "GUNSMITH" || m.itemId == 0L)
			{
				return (string)null;
			}
			gun = Find(m.itemId);
			return (gun != null) ? null : ("the delivered gun " + m.itemId + " is missing here");
		}, delegate
		{
			RunAccept(gun);
		});
	}

	private static string PickGun(StoreClient c, long id, out GameItem gun)
	{
		gun = null;
		if (c.gunOrderData == null)
		{
			return "this customer ordered no gun";
		}
		if (c.gunOrderData.isServiceOrder)
		{
			gun = c.gunOrderData.GetPlacedServiceGun();
			if (gun != null)
			{
				return null;
			}
			return "the customer's gun is not on the counter";
		}
		gun = ((id != 0L) ? Find(id) : GunOrderData.ResolveOrderGun());
		if (gun == null)
		{
			return "no gun on the counter";
		}
		if (!gun.IsTag("ADV_GUN_TAG") || !OnBackCounter(gun))
		{
			return gun.GetDisplayName(false) + " is not a gun on the counter";
		}
		PlayerStore.Instance.currentNegociatedItem = gun;
		return null;
	}

	private static void RunAccept(GameItem gun)
	{
		if (gun != null && CurrentCustomer()?.gunOrderData?.isServiceOrder != true)
		{
			PlayerStore.Instance.currentNegociatedItem = gun;
		}
		NegociationUIManager.Instance.OnOfferAcceptClick();
	}

	private static void ServerBarterSet(MsgNegoBarterSet m, int from)
	{
		Serve("nego.barterSet", m, from, counter: true, delegate
		{
			StoreClient val = CurrentCustomer();
			if (val == null)
			{
				return "nobody is at the counter";
			}
			if (m.offset != 1 && m.offset != -1)
			{
				return "bad offset";
			}
			if (val.barterOffer == null || !val.barterOffer.UseOfferSets() || val.barterOffer.OfferSetCount() < 2 || !val.isIntroduced)
			{
				return "this customer has one offer";
			}
			return (cycleBarter == null) ? "the game changed (no CycleBarterOfferSet)" : null;
		}, delegate
		{
			cycleBarter.Invoke(NegociationUIManager.Instance, new object[1] { m.offset });
		}, delegate(MsgNegoBarterSet x)
		{
			x.setIndex = CurrentCustomer()?.barterOffer?.currentSetIndex ?? (-1);
		});
	}

	private static void ClientBarterSet(MsgNegoBarterSet m)
	{
		Mirror("nego.barterSet", m, () => (!(cycleBarter == null)) ? null : "the game changed (no CycleBarterOfferSet)", delegate
		{
			cycleBarter.Invoke(NegociationUIManager.Instance, new object[1] { m.offset });
		});
		int num = CurrentCustomer()?.barterOffer?.currentSetIndex ?? (-1);
		if (num != m.setIndex)
		{
			Mismatch("nego.barterSet: offer set " + num + " here, " + m.setIndex + " on the server");
		}
	}

	private static void ServerTalk(MsgNegoTalk m, int from)
	{
		Serve("nego.talk", m, from, counter: true, delegate
		{
			StoreClient val = CurrentCustomer();
			if (val == null)
			{
				return "nobody is at the counter";
			}
			if (m.which == "interrogation" && val.interogationDialogue != null)
			{
				return (string)null;
			}
			return (m.which == "glasses" && val.glasseDialogue != null) ? null : "nothing to ask";
		}, delegate
		{
			RunTalk(m.which);
		});
	}

	private static void ClientTalk(MsgNegoTalk m)
	{
		Mirror("nego.talk", m, null, delegate
		{
			RunTalk(m.which);
		});
	}

	private static void RunTalk(string which)
	{
		if (which == "interrogation")
		{
			NegociationUIManager.Instance.OnInterogationButtonClick();
		}
		else
		{
			NegociationUIManager.Instance.OnGlasseButtonClick();
		}
	}

	private static bool ParseBargain(string s, out BargainType t)
	{
		t = (BargainType)0;
		switch (s)
		{
		case "Discount":
			t = (BargainType)0;
			return true;
		case "Markup":
			t = (BargainType)1;
			return true;
		case "BuyingMarkup":
			t = (BargainType)2;
			return true;
		case "BuyingDiscount":
			t = (BargainType)3;
			return true;
		case "Blackmail":
			t = (BargainType)4;
			return true;
		case "Threaten":
			t = (BargainType)5;
			return true;
		default:
			return false;
		}
	}

	internal static bool BargainAvailable(StoreClient c)
	{
		if (c == null || !c.isBargainAvailable || !PlayerStore.Instance.isBargainIntroduced || StartingPerk.IsAwfulNegotiator())
		{
			return false;
		}
		MapUIManager instance = MapUIManager.Instance;
		if (!((Object)(object)instance == (Object)null))
		{
			return !instance.isOutside;
		}
		return true;
	}

	private static void ServerBargain(MsgBargain m, int from)
	{
		BargainType type = (BargainType)0;
		string before = null;
		Serve("bargain.offer", m, from, counter: true, delegate
		{
			StoreClient val = CurrentCustomer();
			string text = TradeOpen(val);
			if (text != null)
			{
				return text;
			}
			if (!ParseBargain(m.type, out type))
			{
				return "unknown bargain";
			}
			if ((int)type != 4 && (int)type != 5 && Array.IndexOf(PricePercents, m.percent) < 0)
			{
				return "bad percent";
			}
			if (!BargainAvailable(val))
			{
				return "bargaining is not possible with this customer";
			}
			if ((int)type == 5 && !PlayerStore.Instance.isAugIntroduced)
			{
				return "that offer is not possible now";
			}
			BargainUIManager instance = BargainUIManager.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				return "no bargain panel on the server";
			}
			BargainType val2 = type;
			if ((int)val2 switch
			{
				0 => instance.CanOfferDiscount() ? 1 : 0, 
				1 => instance.CanOfferMarkup() ? 1 : 0, 
				2 => instance.CanOfferBuyingMarkup() ? 1 : 0, 
				3 => instance.CanDemandBuyingDiscount() ? 1 : 0, 
				4 => instance.CanBlackmail() ? 1 : 0, 
				_ => instance.CanThreatenAug() ? 1 : 0, 
			} == 0)
			{
				return "that offer is not possible now";
			}
			before = val.clientIntent.ToString();
			return (string)null;
		}, delegate
		{
			RunBargain(type, m.percent);
		}, delegate(MsgBargain x)
		{
			x.outcome = BargainOutcome(type, before);
		});
	}

	private static void ClientBargain(MsgBargain m)
	{
		BargainType type = (BargainType)0;
		Mirror("bargain.offer", m, delegate
		{
			if (!ParseBargain(m.type, out type))
			{
				return "unknown bargain";
			}
			return (!((Object)(object)BargainUIManager.Instance == (Object)null)) ? null : "no bargain panel";
		}, delegate
		{
			RunBargain(type, m.percent);
		});
	}

	private static void RunBargain(BargainType type, int percent)
	{
		BargainUIManager instance = BargainUIManager.Instance;
		switch ((int)type)
		{
		case 0:
			instance.OfferDiscount(percent);
			break;
		case 1:
			instance.OfferMarkup(percent);
			break;
		case 2:
			instance.OfferBuyingMarkup(percent);
			break;
		case 3:
			instance.DemandBuyingDiscount(percent);
			break;
		case 4:
			instance.Blackmail();
			break;
		case 5:
			instance.ThreatenAug();
			break;
		}
	}

	private static string BargainOutcome(BargainType type, string intentBefore)
	{
		StoreClient val = CurrentCustomer();
		BargainUIManager instance = BargainUIManager.Instance;
		if (val != null && intentBefore != ((object)(StoreClient.ClientIntent)5).ToString() && (int)val.clientIntent == 5)
		{
			return "left";
		}
		if ((int)type switch
		{
			0 => instance.DiscountOffered ? 1 : 0, 
			2 => instance.BuyingMarkupOffered ? 1 : 0, 
			1 => HasFeatureOnCounter("bargainMarkup") ? 1 : 0, 
			3 => HasFeatureOnCounter("bargainBuyingDiscount") ? 1 : 0, 
			4 => (HasFeatureOnCounter("blackmailDiscount") || HasFeatureOnCounter("blackmailMarkup")) ? 1 : 0, 
			_ => (HasFeatureOnCounter("threatenDiscount") || HasFeatureOnCounter("threatenMarkup")) ? 1 : 0, 
		} == 0)
		{
			return "refused";
		}
		return "accepted";
	}

	internal static string ReplayGuid()
	{
		byte[] array = new byte[16];
		for (int i = 0; i < 4; i++)
		{
			int num = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
			array[i * 4] = (byte)num;
			array[i * 4 + 1] = (byte)(num >> 8);
			array[i * 4 + 2] = (byte)(num >> 16);
			array[i * 4 + 3] = (byte)(num >> 24);
		}
		array[7] = (byte)((array[7] & 0xF) | 0x40);
		array[8] = (byte)((array[8] & 0x3F) | 0x80);
		return new Guid(array).ToString();
	}

	internal static string Explain()
	{
		if (!ShopUp)
		{
			return "";
		}
		PlayerStore instance = PlayerStore.Instance;
		StoreClient val = CurrentCustomer();
		List<string> list = new List<string>();
		EmporiumEntry instance2 = EmporiumEntry.Instance;
		GraphNodeStorage[] array = new GraphNodeStorage[2] { Systems.HostTrip.Back(instance2), Systems.HostTrip.Front(instance2) };
		foreach (GraphNodeStorage val2 in array)
		{
			if (val2 == null)
			{
				continue;
			}
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(val2, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				if (item?.itemFeatures == null)
				{
					continue;
				}
				foreach (ItemFeature itemFeature in item.itemFeatures)
				{
					if (itemFeature != null)
					{
						list.Add(item.uniqueId + ":" + itemFeature.identifier + ":" + itemFeature.category + ":" + (itemFeature.IsExposed() ? 1 : 0) + ":" + itemFeature.fakeCondition?.identifier);
					}
				}
			}
		}
		list.Sort(StringComparer.Ordinal);
		string text = "-";
		BargainUIManager instance3 = BargainUIManager.Instance;
		if ((Object)(object)instance3 != (Object)null && bargainTracked != null)
		{
			if (bargainInfo == null)
			{
				bargainInfo = Array.ConvertAll(bargainFields, (string n) => AccessTools.Field(typeof(BargainUIManager), n));
			}
			object value = bargainTracked.GetValue(instance3);
			text = ((value == null) ? "none" : ((value == val) ? "current" : "other")) + ":" + string.Join(",", Array.ConvertAll(bargainInfo, (FieldInfo f) => (f == null) ? "?" : Convert.ToString(f.GetValue(instance3))));
		}
		ulong h = 14695981039346656037uL;
		foreach (string item2 in list)
		{
			MixS(ref h, item2);
		}
		ulong h2 = 14695981039346656037uL;
		MixS(ref h2, Signature(Career()));
		return "career=" + (h2 & 0xFFFFFFFFu).ToString("x8") + " cust=" + (val?.identifier ?? "-") + " deal=" + (instance.dealMakerActivatedThisCustomer ? 1 : 0) + "/" + instance.dealMakerBargainBonus + " bargain=" + text + " feats=" + list.Count + ":" + ((list.Count <= 8) ? string.Join(" ", list) : (h & 0xFFFFFFFFu).ToString("x8"));
	}

	internal static ulong Hash()
	{
		ulong h = 14695981039346656037uL;
		if (!ShopUp)
		{
			return 0uL;
		}
		PlayerStore instance = PlayerStore.Instance;
		MixS(ref h, Signature(Career()));
		StoreClient val = CurrentCustomer();
		MixS(ref h, val?.identifier);
		if (val != null)
		{
			MixL(ref h, val.useClientBudget ? val.GetBudget() : (-1));
			MixL(ref h, val.clientImpatience);
			MixL(ref h, val.isBusinessComplete ? 1 : 0);
			MixL(ref h, val.isIntroduced ? 1 : 0);
			MixS(ref h, val.clientIntent.ToString());
			MixL(ref h, val.barterOffer?.currentSetIndex ?? (-1));
		}
		MixL(ref h, instance.dealMakerActivatedThisCustomer ? 1 : 0);
		MixL(ref h, instance.dealMakerBargainBonus);
		BargainUIManager instance2 = BargainUIManager.Instance;
		if ((Object)(object)instance2 != (Object)null && bargainTracked != null)
		{
			if (bargainInfo == null)
			{
				bargainInfo = Array.ConvertAll(bargainFields, (string n) => AccessTools.Field(typeof(BargainUIManager), n));
			}
			// No customer at the counter: the bargain panel's last state is not shop state.
			bool flag = val == null || bargainTracked.GetValue(instance2) != val;
			FieldInfo[] array = bargainInfo;
			foreach (FieldInfo fieldInfo in array)
			{
				object obj = ((flag || fieldInfo == null) ? null : fieldInfo.GetValue(instance2));
				MixL(ref h, (obj is bool flag2) ? (flag2 ? 1 : 0) : ((obj is int num2) ? num2 : 0));
			}
		}
		EmporiumEntry instance3 = EmporiumEntry.Instance;
		List<string> list = new List<string>();
		GraphNodeStorage[] array2 = (GraphNodeStorage[])(object)new GraphNodeStorage[2]
		{
			Systems.HostTrip.Back(instance3),
			Systems.HostTrip.Front(instance3)
		};
		foreach (GraphNodeStorage val2 in array2)
		{
			if (val2 == null)
			{
				continue;
			}
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(val2, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				if (item?.itemFeatures == null)
				{
					continue;
				}
				foreach (ItemFeature itemFeature in item.itemFeatures)
				{
					if (itemFeature != null)
					{
						list.Add(item.uniqueId + ":" + itemFeature.identifier + ":" + itemFeature.category + ":" + (itemFeature.IsExposed() ? 1 : 0) + ":" + itemFeature.fakeCondition?.identifier);
					}
				}
			}
		}
		list.Sort(StringComparer.Ordinal);
		foreach (string item2 in list)
		{
			MixS(ref h, item2);
		}
		return h;
	}

	private static void MixL(ref ulong h, long v)
	{
		for (int i = 0; i < 8; i++)
		{
			h ^= (byte)(v >> i * 8);
			h *= 1099511628211uL;
		}
	}

	private static void MixS(ref ulong h, string s)
	{
		if (s == null)
		{
			MixL(ref h, -7L);
			return;
		}
		foreach (char c in s)
		{
			h ^= c;
			h *= 1099511628211uL;
		}
		MixL(ref h, s.Length);
	}

	internal static bool HasFeatureOnCounter(string featureId)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return false;
		}
		GraphNodeStorage[] array = (GraphNodeStorage[])(object)new GraphNodeStorage[2]
		{
			Systems.HostTrip.Back(instance),
			Systems.HostTrip.Front(instance)
		};
		foreach (GraphNodeStorage val in array)
		{
			if (val == null)
			{
				continue;
			}
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				if (item.FindItemFeatureByID(featureId) != null)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static void RegisterAppraisal()
	{
		Net.On<MsgAppraisalEstimate>("appraisal.estimate", ServerEstimate, ClientEstimate);
		Net.On<MsgAppraisalBlock>("appraisal.block", ServerBlock, ClientBlock);
		Net.On<MsgCareer>("appraisal.career", null, ClientCareer);
	}

	public static bool RequestEstimate(GameItem item, int value)
	{
		if (!MayHaggle("appraisal.estimate"))
		{
			return false;
		}
		long num = Ids.Of(item);
		if (num == 0L)
		{
			Log.Warn("negotiation: estimate not sent: the item has no id from the server");
			return false;
		}
		Net.ToServer("appraisal.estimate", new MsgAppraisalEstimate
		{
			v = LocalVisit(),
			itemId = num,
			value = value
		});
		return true;
	}

	internal static bool OnCounters(GameItem it)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if (it != null && (Object)(object)instance != (Object)null)
		{
			if ((object)it.parentInventory != instance.backInvinvElement)
			{
				return (object)it.parentInventory == instance.frontInvinvElement;
			}
			return true;
		}
		return false;
	}

	private static void ServerEstimate(MsgAppraisalEstimate m, int from)
	{
		GameItem item = null;
		int uidBefore = 0;
		Serve("appraisal.estimate", m, from, counter: true, delegate
		{
			StoreClient val = CurrentCustomer();
			if (val == null)
			{
				return "nobody is at the counter";
			}
			if ((int)val.clientIntent != 14)
			{
				return "this customer wants no appraisal";
			}
			item = Find(m.itemId);
			if (item == null)
			{
				return "no such item";
			}
			if (!item.IsTag("APPRAISAL_ITEM") || GeneralHelper.IsItemOwned(item) || !OnCounters(item))
			{
				return "that is not one of the customer's items";
			}
			if (m.value < 0 || m.value > 100000000)
			{
				return "that estimate is out of range";
			}
			uidBefore = PlayerStore.Instance.currentUniqueId;
			return (string)null;
		}, delegate
		{
			AppraisalServiceHelper.OnEstimate(item, m.value);
		}, delegate(MsgAppraisalEstimate x)
		{
			StoreClient val = CurrentCustomer();
			bool flag = item == null || Ids.Find(m.itemId) == null || !OnCounters(item);
			x.result = ((!flag) ? ((val != null && val.clientImpatience >= 100) ? 2 : ((m.value >= AppraisalHelper.GetCorrectBaseValue(item)) ? 1 : (-1))) : 0);
			x.slipId = (flag ? NewestOnCounters(uidBefore) : 0);
			x.career = Career();
			lastCareer = Signature(x.career);
		});
	}

	private static void ClientEstimate(MsgAppraisalEstimate m)
	{
		GameItem item = null;
		Mirror("appraisal.estimate", m, () => ((item = Find(m.itemId)) != null) ? null : ("item " + m.itemId + " is missing here"), delegate
		{
			AppraisalServiceHelper.OnEstimate(item, m.value);
		});
		if (m.slipId != 0L && Find(m.slipId) == null)
		{
			Mismatch("appraisal.estimate: the fee slip " + m.slipId + " is missing here");
		}
		if (m.career != null)
		{
			ApplyCareer(m.career);
		}
	}

	private static long NewestOnCounters(int uidBefore)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		GameItem val = null;
		GraphNodeStorage[] array = (GraphNodeStorage[])(object)new GraphNodeStorage[2]
		{
			Systems.HostTrip.Back(instance),
			Systems.HostTrip.Front(instance)
		};
		foreach (GraphNodeStorage val2 in array)
		{
			if (val2 == null)
			{
				continue;
			}
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(val2, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				if (item.uniqueId > uidBefore && (val == null || item.uniqueId > val.uniqueId))
				{
					val = item;
				}
			}
		}
		if (val == null)
		{
			return 0L;
		}
		return Ids.Of(val);
	}

	public static bool RequestBlock(AppraisalItemType type, bool allowed)
	{
		if (type == null || type.id == null)
		{
			return false;
		}
		Net.ToServer("appraisal.block", new MsgAppraisalBlock
		{
			typeId = type.id,
			allowed = allowed
		});
		return true;
	}

	private static void ServerBlock(MsgAppraisalBlock m, int from)
	{
		if (!Net.IsServer || m == null)
		{
			return;
		}
		AppraisalItemType val = ((m.typeId != null) ? AppraisalItemRegistry.Get(m.typeId) : null);
		string text = null;
		if (!ShopUp)
		{
			text = "the shop is not loaded";
		}
		else if (val == null)
		{
			text = "unknown item type";
		}
		else if (!AppraisalItemRegistry.CanBlock(val, AppraisalRankHelper.GetRank()))
		{
			text = "that type cannot be turned away at this rank";
		}
		else if (setAllowed == null)
		{
			text = "the game changed (no SetAllowed)";
		}
		if (text != null)
		{
			Refuse(from, "appraisal.block", text);
			return;
		}
		using (Net.Apply())
		{
			setAllowed.Invoke(null, new object[2] { val, m.allowed });
		}
		m.player = from;
		m.blacklist = new List<string>(PlayerStore.Instance.appraisalBlacklist ?? new List<string>());
		Net.ToAll("appraisal.block", m);
		lastCareer = Signature(Career());
	}

	private static void ClientBlock(MsgAppraisalBlock m)
	{
		if (Net.IsClient && m != null && PlayerStore.HasInstance)
		{
			PlayerStore.Instance.appraisalBlacklist = new List<string>(m.blacklist ?? new List<string>());
			if (m.player != Net.LocalPlayer)
			{
				RefreshItemsWindow();
			}
		}
	}

	private static void RefreshItemsWindow()
	{
		try
		{
			CustomUIManager instance = CustomUIManager.Instance;
			CustomUIWindow val = ((instance != null) ? instance.GetWindow("appraisal_items") : null);
			if (val != null && val.IsAlive)
			{
				AppraisalItemsWindow.Open();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("negotiation: appraisal items window refresh: " + ex.Message);
		}
	}

	internal static MsgCareer Career()
	{
		PlayerStore instance = PlayerStore.Instance;
		return new MsgCareer
		{
			introduced = instance.isAppraisalIntroduced,
			introducedDay = instance.appraisalIntroducedDay,
			level = instance.appraiserLevel,
			progress = instance.appraisalProgress,
			declined = instance.appraisalPromotionDeclined,
			promotionDay = instance.appraisalPromotionDay,
			blacklist = new List<string>(instance.appraisalBlacklist ?? new List<string>())
		};
	}

	private static string Signature(MsgCareer c)
	{
		if (c != null)
		{
			return c.introduced + "|" + c.introducedDay + "|" + c.level + "|" + c.progress.ToString("R", CultureInfo.InvariantCulture) + "|" + c.declined + "|" + c.promotionDay + "|" + string.Join(",", c.blacklist);
		}
		return "";
	}

	[SystemTick]
	private static void Tick()
	{
		if (Net.IsServer)
		{
			ServerCareerTick();
		}
	}

	internal static void ServerCareerTick()
	{
		if (!Net.IsServer || !ShopUp || !Net.IsLive)
		{
			lastCareer = null;
			return;
		}
		MsgCareer msgCareer = Career();
		string text = Signature(msgCareer);
		if (lastCareer == null)
		{
			lastCareer = text;
			return;
		}
		if (text == lastCareer)
		{
			pendingCareer = null;
			return;
		}
		if (pendingCareer == null)
		{
			pendingCareer = text;
			return;
		}
		lastCareer = text;
		pendingCareer = null;
		Net.ToAll("appraisal.career", msgCareer);
	}

	private static void ClientCareer(MsgCareer c)
	{
		if (Net.IsClient && c != null && PlayerStore.HasInstance)
		{
			ApplyCareer(c);
		}
	}

	private static void ApplyCareer(MsgCareer c)
	{
		PlayerStore instance = PlayerStore.Instance;
		List<string> list = new List<string>();
		if (instance.isAppraisalIntroduced != c.introduced)
		{
			list.Add("introduced");
			instance.isAppraisalIntroduced = c.introduced;
		}
		if (instance.appraisalIntroducedDay != c.introducedDay)
		{
			list.Add("introducedDay");
			instance.appraisalIntroducedDay = c.introducedDay;
		}
		if (instance.appraiserLevel != c.level)
		{
			list.Add("level " + instance.appraiserLevel + "->" + c.level);
			instance.appraiserLevel = c.level;
		}
		if (instance.appraisalProgress != c.progress)
		{
			if (Math.Abs(instance.appraisalProgress - c.progress) > 1E-09)
			{
				list.Add("progress");
			}
			instance.appraisalProgress = c.progress;
		}
		if (instance.appraisalPromotionDeclined != c.declined)
		{
			list.Add("declined");
			instance.appraisalPromotionDeclined = c.declined;
		}
		if (instance.appraisalPromotionDay != c.promotionDay)
		{
			list.Add("promotionDay");
			instance.appraisalPromotionDay = c.promotionDay;
		}
		List<string> list2 = c.blacklist ?? new List<string>();
		if (instance.appraisalBlacklist == null || string.Join(",", instance.appraisalBlacklist) != string.Join(",", list2))
		{
			list.Add("blacklist");
			instance.appraisalBlacklist = new List<string>(list2);
		}
		if (list.Count > 0)
		{
			Log.Info("negotiation: appraisal career from the server: " + string.Join(", ", list));
		}
	}

	private static void RegisterConfront()
	{
		Net.On<MsgNegoConfront>("nego.confront", ServerConfront, ClientConfront);
	}

	public static bool RequestConfront(GameItem item, string via, string reason = null)
	{
		if (item == null || !MayHaggle("nego.confront"))
		{
			return false;
		}
		long num = Ids.Of(item);
		if (num == 0L)
		{
			Log.Warn("negotiation: confront not sent: the item has no id from the server");
			return false;
		}
		Net.ToServer("nego.confront", new MsgNegoConfront
		{
			v = LocalVisit(),
			itemId = num,
			via = via,
			reason = reason
		});
		return true;
	}

	internal static string InspectReason(InspectionUIManager ui, GameItem item)
	{
		if ((Object)(object)ui == (Object)null || item == null)
		{
			return null;
		}
		string[] array = (item.IsTag("CIGARETTE_TAG") ? CigReasons : (item.IsTag("FOOD_STAMP_TAG") ? StampReasons : (item.IsTag("INS_INJECTOR_TAG") ? InjectorReasons : null)));
		if (array == null)
		{
			return null;
		}
		Toggle[] array2 = (Toggle[])(object)new Toggle[5] { ui.toggle1, ui.toggle2, ui.toggle3, ui.toggle4, ui.toggle5 };
		string result = null;
		for (int i = 0; i < array2.Length; i++)
		{
			if ((Object)(object)array2[i] != (Object)null && array2[i].isOn)
			{
				result = array[i];
			}
		}
		return result;
	}

	private static bool Genuine(GameItem item, string category, string genuine)
	{
		ItemFeature val = item.FindItemFeatureByCategory(category);
		if (val != null)
		{
			return val.GetCurrentItemCondition()?.identifier == genuine;
		}
		return false;
	}

	private static Dialogue InspectDialogue(GameItem item, string reason)
	{
		if (item.IsTag("CIGARETTE_TAG"))
		{
			if (!Genuine(item, "CATEGORY_GENUINE_CIGARETTE", "genuineCigarette"))
			{
				return InsCigaretteHelper.CreateTalkDialogue(item);
			}
			return InsCigaretteHelper.CreateConfrontDialogue(reason, item);
		}
		if (item.IsTag("FOOD_STAMP_TAG"))
		{
			if (!Genuine(item, "CATEGORY_GENUINE_STAMP", "genuineStamp"))
			{
				return InsStampHelper.CreateTalkDialogue(item);
			}
			return InsStampHelper.CreateConfrontDialogue(reason, item);
		}
		if (item.IsTag("INS_INJECTOR_TAG") && Genuine(item, "CATEGORY_GENUINE_INJECTOR", "genuineInjector"))
		{
			return InsInjectorHelper.CreateConfrontDialogue(reason, item);
		}
		return null;
	}

	internal static bool InspectActs(GameItem item)
	{
		if (item != null)
		{
			if ((!item.IsTag("CIGARETTE_TAG") || item.FindItemFeatureByCategory("CATEGORY_GENUINE_CIGARETTE")?.realCondition == null) && (!item.IsTag("FOOD_STAMP_TAG") || item.FindItemFeatureByCategory("CATEGORY_GENUINE_STAMP")?.realCondition == null))
			{
				if (item.IsTag("INS_INJECTOR_TAG"))
				{
					return Genuine(item, "CATEGORY_GENUINE_INJECTOR", "genuineInjector");
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private static bool ValidReason(GameItem item, string reason)
	{
		string[] array = (item.IsTag("CIGARETTE_TAG") ? CigReasons : (item.IsTag("FOOD_STAMP_TAG") ? StampReasons : (item.IsTag("INS_INJECTOR_TAG") ? InjectorReasons : null)));
		if (array != null && reason != null)
		{
			return Array.IndexOf(array, reason) >= 0;
		}
		return false;
	}

	private static bool WaterConfrontable(GameItem item, string via)
	{
		if (via == "strip" && (Object)(object)WaterStripUIManager.Instance != (Object)null)
		{
			return WaterStripUIManager.Instance.CanConfront(item);
		}
		return WaterScannerUIManager.CanConfront(item);
	}

	private static void ServerConfront(MsgNegoConfront m, int from)
	{
		GameItem item = null;
		bool ok = false;
		Serve("nego.confront", m, from, counter: true, delegate
		{
			if (m.via != "strip" && m.via != "scanner" && m.via != "inspect")
			{
				return "unknown confront";
			}
			item = Find(m.itemId);
			if (item == null)
			{
				return "no such item";
			}
			if (GeneralHelper.IsItemOwned(item) || !OnCounters(item))
			{
				return "that is not the customer's item on the counter";
			}
			if (InspectionUIManager.IsConfrontLocked())
			{
				return "not now";
			}
			if (m.via == "inspect")
			{
				if (item.IsTag("ALREADY_CONFRONTED_TAG"))
				{
					return "that item was already confronted";
				}
				if (!ValidReason(item, m.reason) || !InspectActs(item))
				{
					return "pick what is wrong with it first";
				}
				return (string)null;
			}
			if (WaterFeatureHelper.GetWaterFeature(item) == null)
			{
				return "that is not water";
			}
			return (!WaterConfrontable(item, m.via)) ? "nothing to confront" : null;
		}, delegate
		{
			ok = RunConfront(item, m.via, m.reason);
		}, delegate(MsgNegoConfront x)
		{
			x.ok = ok;
		});
	}

	private static void ClientConfront(MsgNegoConfront m)
	{
		GameItem item = null;
		bool ok = false;
		Mirror("nego.confront", m, () => ((item = Find(m.itemId)) != null) ? null : ("item " + m.itemId + " is missing here"), delegate
		{
			ok = RunConfront(item, m.via, m.reason);
		});
		if (item != null && ok != m.ok)
		{
			Mismatch("nego.confront: the dialogue " + (m.ok ? "started" : "did not start") + " on the server, " + (ok ? "started" : "did not start") + " here");
		}
		if (item != null && m.player == Net.LocalPlayer)
		{
			AfterConfrontUi(item, m.via);
		}
	}

	private static bool RunConfront(GameItem item, string via, string reason)
	{
		if (via == "inspect")
		{
			Dialogue val = InspectDialogue(item, reason);
			if (val != null)
			{
				return DialogUIManager.Instance.StartDialogue(val, false);
			}
			return false;
		}
		ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(item);
		if (waterFeature == null || !DialogUIManager.Instance.StartDialogue(waterFeature.GetExposeDialog(), false))
		{
			return false;
		}
		InspectableHelper.OnConfrontSuccess(item);
		return true;
	}

	private static void AfterConfrontUi(GameItem item, string via)
	{
		try
		{
			WaterStripUIManager instance = WaterStripUIManager.Instance;
			if ((Object)(object)instance != (Object)null && instance.currentWaterContainer == item && (Object)(object)instance.confrontButton != (Object)null && !instance.CanConfront(item))
			{
				((Component)instance.confrontButton).gameObject.SetActive(false);
			}
			WaterScannerUIManager instance2 = WaterScannerUIManager.Instance;
			if ((Object)(object)instance2 != (Object)null && instance2.currentWaterContainer == item && (Object)(object)instance2.panel != (Object)null && instance2.panel.activeSelf)
			{
				instance2.RefreshUI();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("negotiation: confront panel refresh: " + ex.Message);
		}
	}

	private static void RegisterLabels()
	{
		Net.On<MsgLabelSet>("label.set", ServerLabelSet, ClientLabelSet);
		Net.On<MsgLabelName>("label.name", ServerLabelName, ClientLabelName);
	}

	internal static ItemFeature LabelFeature(GameItem item, string category)
	{
		if (item?.itemFeatures == null || category == null)
		{
			return null;
		}
		foreach (ItemFeature itemFeature in item.itemFeatures)
		{
			if (itemFeature != null && itemFeature.category == category && LabelerHelper.IsEditableLabelFeature(itemFeature))
			{
				return itemFeature;
			}
		}
		return null;
	}

	internal static int ConditionIndex(string category, string cond)
	{
		if (category == null || cond == null || !ItemConditionList.conditionLookup.TryGetValue(category, out var value) || value == null)
		{
			return -1;
		}
		for (int i = 0; i < value.Count; i++)
		{
			if (value[i] != null && value[i].identifier == cond)
			{
				return i;
			}
		}
		return -1;
	}

	private static bool IsNoOp(ItemFeature f, string cond)
	{
		if (f != null && !f.IsExposed() && f.fakeCondition != null)
		{
			return f.fakeCondition.identifier == cond;
		}
		return false;
	}

	public static bool RequestLabel(GameItem item, string category, string cond, bool repeat)
	{
		ItemFeature val = LabelFeature(item, category);
		if (val == null || IsNoOp(val, cond))
		{
			return false;
		}
		long num = Ids.Of(item);
		if (num == 0L)
		{
			Log.Warn("negotiation: label not sent: the item has no id from the server");
			return false;
		}
		Net.ToServer("label.set", new MsgLabelSet
		{
			itemId = num,
			category = category,
			cond = cond,
			repeat = repeat
		});
		return true;
	}

	private static void ServerLabelSet(MsgLabelSet m, int from)
	{
		if (!Net.IsServer || m == null)
		{
			return;
		}
		GameItem val = Find(m.itemId);
		string text = null;
		ItemFeature f = null;
		int index = -1;
		if (!ShopUp)
		{
			text = "the shop is not loaded";
		}
		else if (val == null)
		{
			text = "no such item";
		}
		else if (!GeneralHelper.IsItemOwned(val))
		{
			text = "only our own items can be labeled";
		}
		else if ((f = LabelFeature(val, m.category)) == null)
		{
			text = "that label cannot be changed";
		}
		else if ((index = ConditionIndex(m.category, m.cond)) < 0)
		{
			text = "unknown label";
		}
		else if (IsNoOp(f, m.cond))
		{
			return;
		}
		if (text != null)
		{
			Refuse(from, "label.set", text);
			return;
		}
		Seed seed;
		using (Replay.Server(out seed, from))
		{
			ApplyLabel(f, m.category, index, m.repeat);
		}
		m.player = from;
		m.seed = seed;
		Net.ToAll("label.set", m);
	}

	private static void ClientLabelSet(MsgLabelSet m)
	{
		if (!Net.IsClient || m == null)
		{
			return;
		}
		GameItem item = Find(m.itemId);
		ItemFeature val = LabelFeature(item, m.category);
		int num = ConditionIndex(m.category, m.cond);
		if (val == null || num < 0)
		{
			Mismatch("label.set: item " + m.itemId + " or its " + m.category + " label is missing here");
		}
		else
		{
			using (Replay.Client(m.seed, m.player))
			{
				ApplyLabel(val, m.category, num, m.repeat);
			}
			if (m.player != Net.LocalPlayer || m.repeat)
			{
				RefreshLabelerFor(item);
			}
		}
	}

	private static void ApplyLabel(ItemFeature f, string category, int index, bool repeat)
	{
		List<ItemCondition> list = ItemConditionList.conditionLookup[category];
		f.fakeCondition = list[index];
		f.SetExposed(false);
		if (!repeat)
		{
			WaterFeatureHelper.OnWaterLabelUpdated(f, index);
		}
		if (!LocalActs || !Net.IsClient || DayLog.Replaying)
		{
			return;
		}
		try
		{
			EmporiumAudioPlayer instance = EmporiumAudioPlayer.Instance;
			if ((Object)(object)instance != (Object)null && (Object)(object)instance.audioLabeler != (Object)null)
			{
				instance.audioLabeler.Play();
				LabelerClicks++;
			}
		}
		catch
		{
		}
		if (repeat)
		{
			return;
		}
		try
		{
			AchievementTracker instance2 = AchievementTracker.Instance;
			if (instance2 != null)
			{
				instance2.CheckLowerLevelsSpecial();
			}
		}
		catch
		{
		}
	}

	internal static GameItem LabelerItem()
	{
		if (!((Object)(object)LabelerUIManager.Instance != (Object)null))
		{
			return null;
		}
		object? obj = labelerCurItem?.GetValue(LabelerUIManager.Instance);
		return (GameItem)((obj is GameItem) ? obj : null);
	}

	private static void RefreshLabelerFor(GameItem item)
	{
		LabelerUIManager instance = LabelerUIManager.Instance;
		if ((Object)(object)instance == (Object)null || item == null || (Object)(object)instance.panel == (Object)null || !instance.panel.activeSelf || LabelerItem() != item)
		{
			return;
		}
		try
		{
			instance.OpenUI(item);
		}
		catch (Exception ex)
		{
			Log.Warn("negotiation: labeler refresh: " + ex.Message);
		}
	}

	internal static void RefreshLabelerAfterRefusal()
	{
		GameItem val = LabelerItem();
		if (val != null)
		{
			RefreshLabelerFor(val);
		}
	}

	internal static ItemFeature ElementFeature(LabelerElement e)
	{
		if (!((Object)(object)e != (Object)null))
		{
			return null;
		}
		object? obj = elementFeature?.GetValue(e);
		return (ItemFeature)((obj is ItemFeature) ? obj : null);
	}

	internal static GameItem BlueLabelerItem()
	{
		if (!((Object)(object)LabelerBlueUIManager.Instance != (Object)null))
		{
			return null;
		}
		object? obj = blueCurItem?.GetValue(LabelerBlueUIManager.Instance);
		return (GameItem)((obj is GameItem) ? obj : null);
	}

	public static bool RequestName(GameItem item, string text)
	{
		long num = Ids.Of(item);
		if (num == 0L)
		{
			Log.Warn("negotiation: name not sent: the item has no id from the server");
			return false;
		}
		if (text != null && text.Length > 64)
		{
			text = text.Substring(0, 64);
		}
		Net.ToServer("label.name", new MsgLabelName
		{
			itemId = num,
			text = text
		});
		return true;
	}

	private static void ServerLabelName(MsgLabelName m, int from)
	{
		if (!Net.IsServer || m == null)
		{
			return;
		}
		GameItem val = Find(m.itemId);
		string text = null;
		if (!ShopUp)
		{
			text = "the shop is not loaded";
		}
		else if (val == null)
		{
			text = "no such item";
		}
		else if (!GeneralHelper.IsItemOwned(val))
		{
			text = "only our own items can be named";
		}
		else if (val.identifier == "dossier")
		{
			text = "the dossier cannot be renamed";
		}
		else if (m.text != null && m.text.Length > 64)
		{
			text = "that name is too long";
		}
		if (text != null)
		{
			Refuse(from, "label.name", text);
			return;
		}
		using (Net.Apply())
		{
			ApplyName(val, m.text);
		}
		m.player = from;
		Net.ToAll("label.name", m);
	}

	private static void ClientLabelName(MsgLabelName m)
	{
		if (!Net.IsClient || m == null)
		{
			return;
		}
		GameItem val = Find(m.itemId);
		if (val == null)
		{
			Mismatch("label.name: item " + m.itemId + " is missing here");
			return;
		}
		using (Net.Apply())
		{
			ApplyName(val, m.text);
		}
	}

	private static void ApplyName(GameItem item, string text)
	{
		if (text == null)
		{
			GeneralHelper.RemoveCustomName(item);
		}
		else
		{
			GeneralHelper.SetCustomName(item, text);
		}
		if (item.contentWindow != null && item.contentWindow.IsVisible())
		{
			item.contentWindow.SetTitle(item.GetDisplayName(true));
		}
	}
}
