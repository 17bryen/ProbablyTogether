using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public static class EconomyLab
{
	public static bool FixturesDone;

	public static string RaceId;

	public static int Loads;

	public static readonly HashSet<string> Marks = new HashSet<string>();

	public const string TestRequest = "br_test";

	public const string BusinessPermitId = "businessPermitOfficial";

	private static bool wired;

	private static readonly MethodInfo useClicked = AccessTools.Method(typeof(WildNetworkUI), "OnUseClicked", Type.EmptyTypes, (Type[])null);

	private static readonly MethodInfo onToggled = AccessTools.Method(typeof(EndOfDayUIManager), "OnToggled", new Type[2]
	{
		typeof(StoreService),
		typeof(bool)
	}, (Type[])null);

	public static bool CommissaryOpen => Economy.CommissaryWindowOpen;

	internal static void Wire()
	{
		if (wired)
		{
			return;
		}
		wired = true;
		Net.On("econ.bot", delegate(MsgEconLab m, int from)
		{
			ServerOp(m.op, m.a, m.b, m.c, from);
		}, null);
		Net.On("econ.bot.done", null, delegate(MsgEconLabDone m)
		{
			using (Replay.Client(m.seed))
			{
				Run(m.op, m.a, m.b, m.c);
			}
		});
		Snapshot.Loaded += delegate
		{
			Loads++;
		};
	}

	public static void Op(string op, string a = null, string b = null, string c = null)
	{
		if (Net.IsClient)
		{
			Net.ToServer("econ.bot", new MsgEconLab
			{
				op = op,
				a = a,
				b = b,
				c = c
			});
		}
		else
		{
			ServerOp(op, a, b, c, 0);
		}
	}

	private static void ServerOp(string op, string a, string b, string c, int from)
	{
		if (!Net.IsServer)
		{
			return;
		}
		switch (op)
		{
		case "mark":
			Marks.Add(a ?? "");
			Log.Info("economy lab: mark " + a + " from " + Net.PlayerName(from));
			return;
		case "lock":
		{
			bool flag = Locks.TryTake(a, I(b));
			Log.Info("economy lab: lock " + a + " for " + Net.PlayerName(I(b)) + ": " + flag);
			return;
		}
		case "unlock":
			Locks.Release(a, -1);
			Log.Info("economy lab: lock " + a + " released");
			return;
		}
		Seed seed;
		string text;
		using (Replay.Server(out seed))
		{
			text = Run(op, a, b, c);
		}
		Net.ToAll("econ.bot.done", new MsgEconLabDone
		{
			op = op,
			a = a,
			b = b,
			c = c,
			seed = seed
		});
		Log.Info("economy lab: " + op + " " + a + " " + b + " " + c + " -> " + text);
	}

	private static int I(string s)
	{
		if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return 0;
		}
		return result;
	}

	private static string Run(string op, string a, string b, string c)
	{
		PlayerStore store = Game.Store;
		if (store == null)
		{
			return "no store";
		}
		switch (op)
		{
		case "cash":
			store.playerCash = I(a);
			return "cash " + store.playerCash;
		case "favor":
			store.wildFavor = I(a);
			return "favor " + store.wildFavor;
		case "credit":
			store.isStampIntroduced = true;
			store.commissaryData.redeemedCredit = I(a);
			return "tokens " + I(a);
		case "stock":
		{
			CommissaryItemData itemData = store.commissaryData.GetItemData(a);
			if (itemData == null)
			{
				return "no item " + a;
			}
			itemData.StockCount = I(b);
			return a + " stock " + itemData.StockCount;
		}
		case "days":
			store.daySinceLastBrowseUpgrade = I(a);
			return "daySinceLastBrowseUpgrade " + I(a);
		case "spawn":
		{
			int num2 = Math.Max(1, I(b));
			for (int i = 0; i < num2; i++)
			{
				GameItem val = ((a == "food_stamp") ? PreBuiltItemHelper.CreateRealStamp(40) : ItemSpawner.Spawn(a));
				if (val == null)
				{
					return "no item " + a;
				}
				GeneralHelper.SetItemOwned(val, true);
				GraphUtils.TryAccept((GraphNodeStorage)(object)EmporiumEntry.Instance.invElement, val);
			}
			return num2 + " x " + a;
		}
		case "request":
		{
			store.storeRequests.RemoveAll((StoreRequest r) => r != null && r.id == "br_test");
			string factionId = ((store.storeReputations != null && store.storeReputations.Count > 0) ? store.storeReputations[0].factionId : "");
			store.storeRequests.Add(new StoreRequest
			{
				id = "br_test",
				title = "ProbablyTogether lab",
				description = "lab request",
				factionId = factionId,
				requestedItem = a,
				requiredItem = a,
				requiredItemCount = Math.Max(1, I(b)),
				creditReward = I(c),
				reputationReward = 0,
				requesterName = "Lab",
				duration = 5
			});
			return "request " + a + " x" + b + " for " + c;
		}
		case "service":
		{
			StoreService val3 = StoreService.FindServiceByID(a);
			if (val3 == null)
			{
				return "no service " + a;
			}
			val3.unlocked = b == "1";
			val3.currentCooldown = I(c);
			return a + " unlocked " + val3.unlocked + " cooldown " + val3.currentCooldown;
		}
		case "use":
		{
			StoreService val2 = StoreService.FindServiceByID(a);
			if (val2 == null)
			{
				return "no service " + a;
			}
			val2.isUsing = b == "1";
			return a + " isUsing " + val2.isUsing;
		}
		case "evening":
			store.UpdateServiceCost();
			return "UpdateServiceCost ran";
		case "lottery":
		{
			GameItem val4 = ItemSpawner.Spawn("lottery_ticket");
			if (val4 == null)
			{
				return "no lottery_ticket";
			}
			val4.EnableTag("LOTTERY_IS_WINNING_TAG", false);
			val4.SetValue((long)I(a));
			GeneralHelper.SetItemOwned(val4, true);
			GraphUtils.TryAccept((GraphNodeStorage)(object)EmporiumEntry.Instance.invElement, val4);
			return "winning ticket worth " + I(a);
		}
		case "visitor":
		{
			StoreClientManager storeClientManager = store.storeClientManager;
			if (storeClientManager == null)
			{
				return "no customer manager";
			}
			storeClientManager.AddNextClient((a == "businessPermitOfficial") ? StoreClientList.CreateBusinessPermit() : StoreClientListDict.CreateStoreClient(a));
			return "visitor " + a + " first in the queue (" + (storeClientManager.clientStack?.Count ?? 0) + " waiting)";
		}
		case "phone":
		{
			long num = long.Parse(a, CultureInfo.InvariantCulture);
			if (!StorePhoneClient.IsNumberExist(num))
			{
				return "no number " + a;
			}
			store.PhoneClientDict[num].phoneState = (StorePhoneClient.PhoneState)Enum.Parse(typeof(StorePhoneClient.PhoneState), b);
			return "phone " + a + " " + store.PhoneClientDict[num].phoneState.ToString();
		}
		case "arm":
			RaceId = a;
			return "race " + a;
		case "fixtures":
			FixturesDone = true;
			return "fixtures done";
		default:
			return "unknown op " + op;
		}
	}

	public static StorePhoneClient.PhoneState WildPhone()
	{
		return (StorePhoneClient.PhoneState)((StorePhoneClient.GetWildPhoneClient()?.phoneState) ?? 0);
	}

	public static Func<BotStep, StepStatus> PhoneBrowsePoll()
	{
		bool called = false;
		float last = 0f;
		int shown = 0;
		int advanced = 0;
		int chose = 0;
		return delegate(BotStep st)
		{
			if (WildNetworkUI.IsOpen && !PhoneChannel.Talking)
			{
				st.Detail = "network window open after the phone call to Wild (shown " + shown + ", advance " + advanced + ", chose browse " + chose + "; phone lock " + Locks.HeldBy("phone") + ", dialogue refusals " + DialogueSync.RefusedCount + ") after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
				return StepStatus.Pass;
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (realtimeSinceStartup - last < 1f)
			{
				return StepStatus.Running;
			}
			last = realtimeSinceStartup;
			if (!called)
			{
				if (Locks.HeldBy("phone") >= 0)
				{
					st.Detail = "the phone is busy (" + Locks.HeldBy("phone") + ")";
					return StepStatus.Running;
				}
				called = DialogueSync.RequestCall(8376L);
				st.Detail = "called Wild: " + called;
				return StepStatus.Running;
			}
			PhoneChannel phone = DialogueSystem.Phone;
			if (phone.Current == null)
			{
				st.Detail = "waiting for the call (talking " + PhoneChannel.Talking + ", lock " + Locks.HeldBy("phone") + ")";
				return StepStatus.Running;
			}
			if (phone.LineUnfinished)
			{
				if (DialogueSync.RequestShown("phone"))
				{
					shown++;
				}
			}
			else if (phone.IsChoiceNow)
			{
				if (DialogueSync.RequestChoose("phone", 0))
				{
					chose++;
				}
			}
			else if (DialogueSync.RequestAdvance("phone"))
			{
				advanced++;
			}
			st.Detail = "pacing the call: shown " + shown + ", advance " + advanced + ", chose " + chose + ", window " + WildNetworkUI.IsOpen + ", refusals " + DialogueSync.RefusedCount + " (" + DialogueSystem.LastRefused + ")";
			return StepStatus.Running;
		};
	}

	public static string ClickBuy(string id)
	{
		Economy.SelectUpgrade(id);
		useClicked.Invoke(null, null);
		string[] obj = new string[5] { "pressed Buy on ", id, " (cost ", null, null };
		NetworkUpgrade obj2 = Up(id);
		obj[3] = ((obj2 != null) ? new int?(obj2.GetCost()) : ((int?)null)).ToString();
		obj[4] = ")";
		return string.Concat(obj);
	}

	public static string ClickService(string id, bool on)
	{
		StoreService val = StoreService.FindServiceByID(id);
		if (val == null)
		{
			return "no service " + id;
		}
		if ((Object)(object)EndOfDayUIManager.Instance != (Object)null)
		{
			onToggled.Invoke(EndOfDayUIManager.Instance, new object[2] { val, on });
		}
		else
		{
			Economy.RequestToggle(id, on);
		}
		return "clicked " + id + (on ? " on" : " off");
	}

	public static string CounterId()
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
			obj = "none";
		}
		return (string)obj;
	}

	public static string QueueHead()
	{
		List<StoreClient> list = Game.Store?.storeClientManager?.clientStack;
		object obj;
		if (list != null)
		{
			if (list.Count == 0)
			{
				return "none";
			}
			obj = list[0]?.identifier;
			if (obj == null)
			{
				return "null";
			}
		}
		else
		{
			obj = "";
		}
		return (string)obj;
	}

	public static NetworkUpgrade Up(string id)
	{
		if (Game.Store?.networkUpgrade == null || !Game.Store.networkUpgrade.TryGetValue(id, out var value))
		{
			return null;
		}
		return value;
	}

	public static StoreService Svc(string id)
	{
		return StoreService.FindServiceByID(id);
	}

	public static StoreRequest Req()
	{
		return Game.Store?.storeRequests?.Find((StoreRequest r) => r != null && r.id == "br_test");
	}

	public static string OwnedList(params string[] ids)
	{
		return string.Join(",", ids.Select((string i) => i + ":" + Owned(i)));
	}

	public static int Owned(string identifier)
	{
		int num = 0;
		foreach (GameItem allOwnedItem in EmporiumEntry.Instance.GetAllOwnedItems(false, true))
		{
			if (allOwnedItem != null && allOwnedItem.identifier == identifier)
			{
				num++;
			}
		}
		return num;
	}

	public static string FreshUpgrade(params string[] skip)
	{
		PlayerStore store = Game.Store;
		if (store?.networkUpgrade == null)
		{
			return null;
		}
		List<string> list = new List<string>(store.networkUpgrade.Keys);
		list.Sort(StringComparer.Ordinal);
		foreach (string item in list)
		{
			if (Array.IndexOf(skip, item) < 0)
			{
				NetworkUpgrade val = store.networkUpgrade[item];
				if (val != null && !val.isRepeatable && val.cooldownDuration == 0 && val.IsReady() && !val.IsUnlocked() && !NetworkUpgrade.IsHiddenForRun(item) && (val.prerequisite == null || val.prerequisite.Count <= 0) && (!NetworkUpgradeList.UnlockActions.ContainsKey(item) || !(item != "RENOVATION1") || !(item != "RENOVATION2") || !(item != "DOSSIER_UPGRADE") || !(item != "TRASHCAN_UPGRADE")))
				{
					return item;
				}
			}
		}
		return null;
	}
}
