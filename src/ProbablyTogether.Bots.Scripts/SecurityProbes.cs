using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class SecurityProbes : BotProbeSet
{
	internal static int Phase;

	internal static int Done;

	internal static int Loads;

	private static bool wired;

	private static readonly string[] smallItems = new string[8] { "fanta_pill", "caffeine_pill", "birth_control_pill", "pink_injector", "dream_dust", "bandage_item", "phagimycin_pill", "smelling_salt" };

	private static readonly string[] plainItems = new string[5] { "caffeine_pill", "birth_control_pill", "bandage_item", "phagimycin_pill", "smelling_salt" };

	private static int serverPicks;

	public override void Register()
	{
		BotProbes.Add("sec.visit", () => Security.VisitKey());
		BotProbes.Add("sec.cust", () => (!Game.ShopLoaded) ? "" : Security.CustomerId());
		BotProbes.Add("sec.counter", delegate
		{
			if (!Game.ShopLoaded)
			{
				return "";
			}
			string text = Security.CustomerId();
			return (text.Length <= 0) ? "nobody" : text;
		});
		BotProbes.Add("sec.arrived", () => PS((PlayerStore p) => (p.currentClientInstance == null || !p.isClientArrived) ? "0" : "1"));
		BotProbes.Add("sec.dismissable", () => PS((PlayerStore p) => (p.currentClientInstance != null) ? ((!p.currentClientInstance.GetClientBlueprint().dismissable) ? "0" : "1") : "-"));
		BotProbes.Add("sec.fp", () => (!Game.ShopLoaded) ? "" : SecurityState.Capture().fp);
		BotProbes.Add("sec.part", () => Security.DigestPart());
		BotProbes.Add("sec.wrongful", () => PS((PlayerStore p) => BotProbeSet.Str(p.wrongfulReportCount)));
		BotProbes.Add("sec.crimes", () => PS((PlayerStore p) => (p.secData != null) ? (string.Join(",", p.secData.crimeTypes) + ":" + string.Join(",", p.secData.crimeAmount)) : ""));
		BotProbes.Add("sec.bribe", () => PS((PlayerStore p) => BotProbeSet.Str(p.secData?.bribeTracker)));
		BotProbes.Add("sec.aug", () => PS((PlayerStore p) => BotProbeSet.Str(p.augPresence)));
		BotProbes.Add("sec.augRise", () => BotProbeSet.Str(Difficulty.Rules?.augLeftAlivePresenceRise));
		BotProbes.Add("sec.unlocked", () => PS((PlayerStore p) => (!p.isShootingUnlocked) ? "0" : "1"));
		BotProbes.Add("sec.wanted8", () => PS((PlayerStore p) => (p.wantedStatus == null || !p.wantedStatus.TryGetValue("wanted8", out var value)) ? "0" : BotProbeSet.Str(value)));
		BotProbes.Add("sec.arresting", () => PS((PlayerStore p) => (!p.isClientBeingArrested) ? "0" : "1"));
		BotProbes.Add("sec.shootout", () => PS((PlayerStore p) => (!p.isHandlingShootout) ? "0" : "1"));
		BotProbes.Add("sec.shot", () => (!((Object)(object)StoreUIManager.Instance == (Object)null)) ? ((!StoreUIManager.Instance.isShot) ? "0" : "1") : "");
		BotProbes.Add("sec.poster", () => (!((Object)(object)StorePoster.Instance == (Object)null)) ? ((!StorePoster.Instance.isPulled) ? "0" : "1") : "");
		BotProbes.Add("sec.officers", () => BotProbeSet.Str((Security.OfficerActive(1) ? 1 : 0) + (Security.OfficerActive(2) ? 1 : 0)));
		BotProbes.Add("sec.slips", () => BotProbeSet.Str(CountItems("sec_slip")));
		BotProbes.Add("sec.rep", () => PS((PlayerStore p) => BotProbeSet.Str(StoreReputation.GetSecReputation())));
		BotProbes.Add("sec.lock", () => BotProbeSet.Str(Locks.HeldBy("counter")));
		BotProbes.Add("sec.aimer", () => BotProbeSet.Str(Security.AimingPlayer));
		BotProbes.Add("sec.state", () => SecurityState.Counters);
		BotProbes.Add("sec.items", () => (!Game.ShopLoaded) ? "" : (Digest.Compute().items + ":" + Digest.Compute().hash));
		BotProbes.Add("sec.contraband", () => BotProbeSet.Str(CountTagged("CONTRABAND_ITEM_TAG", hiddenOnly: false)));
		BotProbes.Add("sec.hidden", () => BotProbeSet.Str(CountTagged("CONTRABAND_ITEM_TAG", hiddenOnly: true)));
		BotProbes.Add("sec.stolen", () => StolenText());
		BotProbes.Add("sec.inspLoss", () => PS((PlayerStore p) => p.lastInspectionLossDay + "/" + p.lastInspectionLossValue));
		BotProbes.Add("sec.eleanor", () => PS((PlayerStore p) => (p.storeClientData == null || !p.storeClientData.eleanorArrested) ? "0" : "1"));
		BotProbes.Add("sec.crate", () => CrateText());
		BotProbes.Add("sec.dlg", delegate
		{
			string text = DialogueSync.CurrentFp("counter");
			return string.IsNullOrEmpty(text) ? "none" : text;
		});
		BotProbes.Add("sec.line", delegate
		{
			Dialogue current = DialogueSystem.Counter.Current;
			if (current == null)
			{
				return "none";
			}
			string text = current.plainText ?? "";
			return (text.Length <= 48) ? text : text.Substring(0, 48);
		});
		BotProbes.Add("sec.ended", () => (!DayNightRun.RunOver) ? "0" : ("1:" + DayNightRun.Ending));
		BotProbes.Add("secbot.phase", () => BotProbeSet.Str(Phase));
		BotProbes.Add("secbot.done", () => BotProbeSet.Str(Done));
		BotProbes.Add("sec.loads", () => BotProbeSet.Str(Loads));
		if (wired)
		{
			return;
		}
		wired = true;
		Snapshot.Loaded += delegate
		{
			Loads++;
		};
		Net.On<SecBotCmd>("bot.sec", ServerCmd, null);
		Net.On("secbot.seeded", null, delegate(SecBotSeeded m)
		{
			using (Replay.Client(m.seed))
			{
				Seeded(m.what, m.arg, m.n, m.pick);
			}
		});
	}

	private static string PS(Func<PlayerStore, string> f)
	{
		PlayerStore store = Game.Store;
		if (!Game.ShopLoaded || store == null)
		{
			return "";
		}
		return f(store);
	}

	internal static IEnumerable<GameItem> AllItems()
	{
		if (!Game.ShopLoaded)
		{
			yield break;
		}
		foreach (GraphNodeStorage item in Game.Roots())
		{
			if (item == null)
			{
				continue;
			}
			foreach (GameItem item2 in GraphUtils.FindAllChildrenType<GameItem>(item, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				if (item2 != null)
				{
					yield return item2;
				}
			}
		}
	}

	internal static int CountItems(string identifier)
	{
		if (!Game.ShopLoaded)
		{
			return -1;
		}
		return AllItems().Count((GameItem it) => it.identifier == identifier);
	}

	internal static int CountTagged(string tag, bool hiddenOnly)
	{
		if (!Game.ShopLoaded)
		{
			return -1;
		}
		if (!hiddenOnly)
		{
			return AllItems().Count((GameItem it) => it.IsTag(tag));
		}
		return GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)EmporiumEntry.Instance.hiddenElement, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).Count((GameItem it) => it != null && it.IsTag(tag));
	}

	internal static string StolenText()
	{
		if (!Game.ShopLoaded)
		{
			return "";
		}
		EmporiumEntry e = EmporiumEntry.Instance;
		IEnumerable<string> values = from it in AllItems()
			where it.IsTag("STOLEN_TAG")
			orderby it.uniqueId
			select it.uniqueId + "=" + StolenHelper.GetFoundChance(it) + "@" + (((object)it.parentInventory == e.invElement) ? "m" : (((object)it.parentInventory == e.frontInvinvElement) ? "f" : (((object)it.parentInventory == e.backInvinvElement) ? "b" : "o")));
		string text = string.Join(",", values);
		if (text.Length <= 0)
		{
			return "none";
		}
		return text;
	}

	internal static string CrateText()
	{
		if (!Game.ShopLoaded)
		{
			return "";
		}
		EmporiumEntry e = EmporiumEntry.Instance;
		IEnumerable<string> values = from it in AllItems()
			where it.identifier == "luxury_supply_crate"
			orderby it.uniqueId
			select it.uniqueId + (GeneralHelper.IsItemOwned(it) ? ":owned" : ":not") + "@" + (((object)it.parentInventory == e.invElement) ? "m" : (((object)it.parentInventory == e.frontInvinvElement) ? "f" : (((object)it.parentInventory == e.backInvinvElement) ? "b" : "o")));
		string text = string.Join(",", values);
		if (text.Length <= 0)
		{
			return "none";
		}
		return text;
	}

	internal static bool RealItem(string id)
	{
		return SecurityLabItems.Real(id);
	}

	private static StoreClient Factory(string kind)
	{
		return (StoreClient)(kind switch
		{
			"wanted8" => StoreClientListWanted.CreateWanted8(), 
			"aug" => StoreClientList.CreateAugMed(-1), 
			"inspector0" => StoreClientList.CreateInspectionClient(0, false), 
			"inspector1" => StoreClientList.CreateInspectionClient(1, false), 
			"eleanor4" => StoreClientUniqueList.Eleanor4(), 
			_ => StoreClientList.CreateScavGeneral(), 
		});
	}

	internal static void Seeded(string what, string arg, int n, int pick)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		PlayerStore store = Game.Store;
		if (!Game.ShopLoaded || (Object)(object)instance == (Object)null || store == null)
		{
			return;
		}
		string text = (DayLog.Replaying ? " (replayed)" : "");
		switch (what)
		{
		case "queue":
		{
			List<StoreClient> clientStack = store.storeClientManager.clientStack;
			int count = clientStack.Count;
			clientStack.Clear();
			StoreClient val4 = Factory(arg);
			clientStack.Insert(0, val4);
			BotLog.Info("security", "queue: '" + val4.identifier + "' waits first (" + count + " generated customer(s) sent home)" + text);
			break;
		}
		case "spawn":
		{
			if (arg == "crate")
			{
				GameItem val = DirectoryMaster.Item("luxury_supply_crate", true);
				GeneralHelper.SetItemOwned(val, true);
				int num = GraphUtils.TryAcceptAll((GraphNodeStorage)(object)instance.invElement, val, -1);
				BotLog.Info("security", "spawned an owned crate (uid " + val.uniqueId + ")" + ((num == 0) ? "" : " that did not fit") + text);
				break;
			}
			GraphNodeStorage obj;
			if (!(arg == "hidden"))
			{
				GraphNodeStorage invElement = (GraphNodeStorage)(object)instance.invElement;
				obj = invElement;
			}
			else
			{
				GraphNodeStorage invElement = (GraphNodeStorage)(object)instance.hiddenElement;
				obj = invElement;
			}
			GraphNodeStorage val2 = obj;
			string[] array = ((arg == "stolen") ? plainItems : smallItems);
			for (int i = 0; i < array.Length; i++)
			{
				string text2 = array[(pick + i) % array.Length];
				if (!RealItem(text2))
				{
					BotLog.Info("security", "skipped '" + text2 + "': not an item in this game version");
					continue;
				}
				GameItem val3 = DirectoryMaster.Item(text2, true);
				if (val3 == null || val3.uniqueId == 0)
				{
					continue;
				}
				if (arg == "stolen" && val3.IsTag("CONTRABAND_ITEM_TAG"))
				{
					val3.Destroy();
					continue;
				}
				if (arg == "stolen")
				{
					StolenHelper.InitStolenItem(val3, n);
				}
				else
				{
					ContrabandHelper.InitContrabandItem(val3, n);
				}
				GeneralHelper.SetItemOwned(val3, true);
				if (GraphUtils.TryAcceptAll(val2, val3, -1) == 0)
				{
					BotLog.Info("security", "spawned '" + text2 + "' (" + ((arg == "stolen") ? "stolen, heat " : "contraband level ") + n + ", uid " + val3.uniqueId + ") in " + arg + text);
					return;
				}
			}
			BotLog.Info("security", "spawn in " + arg + ": nothing fitted");
			break;
		}
		case "cash":
			store.playerCash = n;
			BotLog.Info("security", "cash set to " + n + text);
			break;
		}
	}

	private static void ServerCmd(SecBotCmd m, int from)
	{
		PlayerStore store = Game.Store;
		BotLog.Info("security", "cmd " + m.cmd + " " + m.arg + " " + m.n + " from " + Net.PlayerName(from));
		string cmd = m.cmd;
		if (cmd == null)
		{
			return;
		}
		int pick;
		Seed seed;
		switch (cmd.Length)
		{
		case 5:
			switch (cmd[0])
			{
			case 'q':
				if (!(cmd == "queue"))
				{
					return;
				}
				break;
			case 's':
				if (!(cmd == "spawn"))
				{
					return;
				}
				break;
			case 'c':
				if (cmd == "clear")
				{
					BotLog.Info("security", "clear: " + Customers.BotTimerDismiss());
				}
				return;
			case 'p':
				if (cmd == "phase")
				{
					Phase = Math.Max(Phase, m.n);
				}
				return;
			default:
				return;
			}
			goto IL_0137;
		case 4:
			switch (cmd[0])
			{
			default:
				return;
			case 'c':
				break;
			case 'd':
				if (cmd == "done")
				{
					Done++;
				}
				return;
			}
			if (!(cmd == "cash"))
			{
				break;
			}
			goto IL_0137;
		case 9:
			{
				if (cmd == "unlockGun")
				{
					Shotgun.Unlock();
				}
				break;
			}
			IL_0137:
			if (m.cmd == "queue" && store?.currentClientInstance != null)
			{
				BotLog.Info("security", "queue refused: '" + Security.CustomerId() + "' is at the counter");
				break;
			}
			pick = ((m.cmd == "spawn") ? serverPicks++ : 0);
			using (Replay.Server(out seed))
			{
				Net.ToAll("secbot.seeded", new SecBotSeeded
				{
					what = m.cmd,
					arg = m.arg,
					n = m.n,
					pick = pick,
					seed = seed
				});
				Seeded(m.cmd, m.arg, m.n, pick);
				break;
			}
		}
	}
}
