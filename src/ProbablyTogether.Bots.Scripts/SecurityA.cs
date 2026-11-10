using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class SecurityA : SecurityClientScript
{
	private int phase;

	private Dialogue choiceLine;

	private float choiceSince;

	public override string Name => "security-a";

	private void HandOver(string tag, float timeout = 200f)
	{
		phase += 2;
		Cmd(tag + "-phase", "phase", "", phase - 1);
		WaitPhase(tag + "-b-compared", phase, timeout);
	}

	protected override void Build()
	{
		Joined();
		WaitServer("both-players", 300f, (IReadOnlyDictionary<string, string> v) => v.TryGetValue("players", out var value) && value == "2", "players");
		Do("open-shutter", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "clicked (dayNight.open), state " + Game.Store.storeState.ToString();
		});
		WaitFor("shop-open", 30f, () => (int)Game.Store.storeState == 1, () => "state " + Game.Store.storeState.ToString());
		ClearCounter("open");
		phase = 0;
		HandOver("opened");
		Do("wrongful-before", delegate
		{
			Vars["w0"] = SecurityClientScript.Mine("sec.wrongful");
			return "wrongful " + Vars["w0"] + ", slips " + SecurityClientScript.Mine("sec.slips") + ", cash " + SecurityClientScript.Mine("cash");
		});
		Call("plain", "scavGeneral");
		Do("press-security", delegate
		{
			StoreSecButton.Instance.OnClick();
			return "pressed (request sent)";
		});
		WaitFor("report-replayed", 15f, () => SecurityClientScript.Mine("sec.arresting") == "1", () => "arresting " + SecurityClientScript.Mine("sec.arresting") + ", refusal '" + Security.LastRefusal + "'");
		WaitFor("officers-arrived", 30f, () => Security.ClientOfficersHere, () => "officers " + SecurityClientScript.Mine("sec.officers"));
		Pace("wrongful-arrest", 90f, () => SecurityClientScript.Mine("sec.visit") == "");
		WaitFor("wrongful-officers-left", 40f, () => SecurityClientScript.Mine("sec.officers") == "0", () => "officers " + SecurityClientScript.Mine("sec.officers"));
		WaitFor("wrongful-counted", 40f, () => SecurityClientScript.N("sec.wrongful") == SecurityClientScript.N(Vars["w0"]) + 1, () => "wrongful " + SecurityClientScript.Mine("sec.wrongful") + " (was " + Vars["w0"] + ")");
		Settle("wrongful");
		SameAll("wrongful", "sec.wrongful", "cash", "sec.slips", "sec.rep", "sec.fp", "sec.part", "sec.arresting", "sec.officers", "sec.lock");
		SameSoft("wrongful", "sec.items");
		HandOver("wrongful");
		Do("wanted-before", () => "cash " + SecurityClientScript.Mine("cash") + ", wanted8 " + SecurityClientScript.Mine("sec.wanted8"));
		Call("wanted", "wanted8");
		HandOver("wanted-held");
		Do("press-security-2", delegate
		{
			StoreSecButton.Instance.OnClick();
			return "pressed";
		});
		WaitFor("wanted-report-replayed", 15f, () => SecurityClientScript.Mine("sec.arresting") == "1", () => "arresting " + SecurityClientScript.Mine("sec.arresting") + ", refusal '" + Security.LastRefusal + "'");
		WaitFor("wanted-officers", 30f, () => Security.ClientOfficersHere, () => "officers " + SecurityClientScript.Mine("sec.officers"));
		Pace("wanted-arrest", 90f, () => SecurityClientScript.Mine("sec.visit") == "");
		WaitFor("wanted-officers-left", 40f, () => SecurityClientScript.Mine("sec.officers") == "0", () => "officers " + SecurityClientScript.Mine("sec.officers"));
		WaitFor("wanted-paid", 40f, () => SecurityClientScript.Mine("sec.wanted8") == "1", () => "wanted8 " + SecurityClientScript.Mine("sec.wanted8") + ", cash " + SecurityClientScript.Mine("cash"));
		Settle("wanted");
		SameAll("wanted", "sec.wanted8", "cash", "sec.slips", "sec.rep", "sec.fp", "sec.part");
		SameSoft("wanted", "sec.items");
		HandOver("wanted");
		Cmd("unlock-gun", "unlockGun");
		WaitFor("gun-unlocked", 30f, () => SecurityClientScript.Mine("sec.unlocked") == "1", () => "unlocked " + SecurityClientScript.Mine("sec.unlocked") + ", state " + SecurityState.Counters);
		Call("holster", "scavGeneral");
		Do("holster-before", delegate
		{
			Vars["hrep"] = SecurityClientScript.Mine("sec.rep");
			Vars["hcash"] = SecurityClientScript.Mine("cash");
			return "rep " + Vars["hrep"] + ", cash " + Vars["hcash"];
		});
		Do("holster-aim", delegate
		{
			StoreUIManager.Instance.EnterShootingMode();
			return "entering the shooting view";
		});
		WaitFor("holster-aiming", 10f, () => StoreUIManager.Instance.isShootingMode, () => "isShootingMode " + StoreUIManager.Instance.isShootingMode);
		WaitServer("holster-server-sees-aim", 10f, (IReadOnlyDictionary<string, string> v) => v.TryGetValue("sec.lock", out var value) && value == Net.LocalPlayer.ToString(CultureInfo.InvariantCulture), "sec.lock");
		HandOver("holster-aiming");
		Do("holster-put-away", delegate
		{
			StoreUIManager.Instance.ExitShootingMode();
			return "left the shooting view without a shot";
		});
		WaitFor("holster-left-view", 10f, () => !StoreUIManager.Instance.isShootingMode, () => "isShootingMode " + StoreUIManager.Instance.isShootingMode);
		WaitFor("holster-customer-gone", 20f, () => SecurityClientScript.Mine("sec.visit") == "", () => "cust " + SecurityClientScript.Mine("sec.cust") + ", refusal '" + Security.LastRefusal + "'");
		Check("holster-no-rep-change", () => SecurityClientScript.Mine("sec.rep") == Vars["hrep"] && SecurityClientScript.Mine("cash") == Vars["hcash"], () => "rep " + Vars["hrep"] + " -> " + SecurityClientScript.Mine("sec.rep") + ", cash " + Vars["hcash"] + " -> " + SecurityClientScript.Mine("cash"));
		Settle("holster");
		SameAll("holster", "sec.counter", "sec.lock", "sec.aimer", "sec.rep", "cash", "sec.fp", "sec.part");
		SameSoft("holster", "sec.items");
		HandOver("holster");
		Call("aug-alive", "aug_med");
		Do("aug-alive-before", delegate
		{
			Vars["a0"] = SecurityClientScript.Mine("sec.aug");
			return "aug presence " + Vars["a0"] + ", rise " + SecurityClientScript.Mine("sec.augRise");
		});
		Do("aug-alive-dismiss", delegate
		{
			Customers.RequestDismiss(callNext: false);
			return "dismiss button (customers.dismiss)";
		});
		WaitFor("aug-alive-gone", 20f, () => SecurityClientScript.Mine("sec.visit") == "", () => "cust " + SecurityClientScript.Mine("sec.cust"));
		Check("aug-alive-rise-once", () => SecurityClientScript.N("sec.aug") == SecurityClientScript.N(Vars["a0"]) + SecurityClientScript.N("sec.augRise"), () => "aug presence " + Vars["a0"] + " -> " + SecurityClientScript.Mine("sec.aug") + " (rise " + SecurityClientScript.Mine("sec.augRise") + ")");
		Settle("aug-alive");
		SameAll("aug-alive", "sec.aug", "sec.fp", "sec.part", "cash");
		HandOver("aug-alive");
		Do("cc-before", delegate
		{
			Vars["s0"] = SecurityClientScript.Mine("sec.stolen");
			return "stolen " + Vars["s0"];
		});
		Cmd("cc-spawn-1", "spawn", "stolen", 99);
		Cmd("cc-spawn-2", "spawn", "stolen", 60);
		Cmd("cc-spawn-3", "spawn", "stolen", 5);
		WaitFor("cc-spawned", 15f, () => NewStolen().Count == 3, () => "stolen " + SecurityClientScript.Mine("sec.stolen") + " (was " + Vars["s0"] + ")");
		SameAll("cc-spawn", "sec.stolen", "sec.items");
		Call("cc", "securityInspector");
		Do("cc-line-before", () => "dialogue " + SecurityClientScript.Mine("sec.dlg") + " '" + SecurityClientScript.Mine("sec.line") + "', counter items " + SecurityClientScript.Mine("sec.stolen"));
		for (int num = 0; num < 3; num++)
		{
			int k = num;
			string t = "cc-move" + (k + 1);
			Do(t, delegate
			{
				GameItem val = NewStolen()[k];
				Vars[t + "q"] = BotScript.Str(ItemsMove.AppliedCount);
				int num2 = ItemsMove.QuickMove(val, (GraphNodeStorage)(object)EmporiumEntry.Instance.frontInvinvElement, all: true, -1, "bot");
				return "items.quickMove of uid " + val.uniqueId + " (heat " + StolenHelper.GetFoundChance(val) + ") onto the front counter: request " + num2;
			});
			WaitFor(t + "-applied", 15f, () => ItemsMove.AppliedCount > SecurityClientScript.N(Vars[t + "q"]) && NewStolen().Count((GameItem x) => (object)x.parentInventory == EmporiumEntry.Instance.frontInvinvElement) >= k + 1, () => "applied " + ItemsMove.AppliedCount + ", stolen " + SecurityClientScript.Mine("sec.stolen") + ", reject " + (ItemsMove.LastReject?.why ?? "-"));
			Delay(t + "-settle", 1f);
			Do(t + "-outcome", () => "stolen " + SecurityClientScript.Mine("sec.stolen") + ", dismissable " + SecurityClientScript.Mine("sec.dismissable") + ", dialogue " + SecurityClientScript.Mine("sec.dlg") + " '" + SecurityClientScript.Mine("sec.line") + "'");
			SameAll(t, "sec.stolen", "sec.dismissable", "sec.items");
		}
		Pace("cc-inspection", 150f, () => SecurityClientScript.Mine("sec.visit") == "", 120f);
		Settle("cc");
		SameAll("cc", "sec.stolen", "sec.rep", "sec.inspLoss", "cash", "sec.fp", "sec.part");
		SameSoft("cc", "sec.items");
		HandOver("cc");
		Do("insp-before", delegate
		{
			Vars["c0"] = SecurityClientScript.Mine("sec.contraband");
			Vars["h0"] = SecurityClientScript.Mine("sec.hidden");
			return "contraband " + Vars["c0"] + ", hidden " + Vars["h0"] + ", poster " + SecurityClientScript.Mine("sec.poster");
		});
		Cmd("insp-spawn-main", "spawn", "main", 2);
		Cmd("insp-spawn-hidden", "spawn", "hidden", 2);
		WaitFor("insp-spawned", 15f, () => SecurityClientScript.N("sec.contraband") == SecurityClientScript.N(Vars["c0"]) + 2 && SecurityClientScript.N("sec.hidden") == SecurityClientScript.N(Vars["h0"]) + 1, () => "contraband " + SecurityClientScript.Mine("sec.contraband") + ", hidden " + SecurityClientScript.Mine("sec.hidden"));
		Call("insp", "securityInspector");
		Do("insp-mark", delegate
		{
			Vars["c1"] = SecurityClientScript.Mine("sec.contraband");
			Vars["h1"] = SecurityClientScript.Mine("sec.hidden");
			return "contraband " + Vars["c1"] + ", hidden " + Vars["h1"];
		});
		Pace("insp-dialogue", 150f, () => SecurityClientScript.Mine("sec.visit") == "", 120f);
		Check("insp-seized", () => SecurityClientScript.N("sec.contraband") < SecurityClientScript.N(Vars["c1"]) && SecurityClientScript.N("sec.hidden") == SecurityClientScript.N(Vars["h1"]), () => "contraband " + Vars["c1"] + " -> " + SecurityClientScript.Mine("sec.contraband") + ", hidden " + Vars["h1"] + " -> " + SecurityClientScript.Mine("sec.hidden") + " (poster down: kept), loss " + SecurityClientScript.Mine("sec.inspLoss"));
		Settle("insp");
		SameAll("insp", "sec.contraband", "sec.hidden", "sec.rep", "sec.inspLoss", "cash", "sec.fp", "sec.part");
		SameSoft("insp", "sec.items");
		HandOver("insp");
		Cmd("bribe-cash", "cash", "", 900);
		WaitFor("bribe-cash-set", 15f, () => SecurityClientScript.Mine("cash") == "900", () => "cash " + SecurityClientScript.Mine("cash"));
		Do("bribe-before", delegate
		{
			Vars["b0"] = SecurityClientScript.Mine("sec.bribe");
			return "bribe tracker " + Vars["b0"] + ", contraband " + SecurityClientScript.Mine("sec.contraband");
		});
		Call("bribe", "securityInspector");
		Pace("bribe-dialogue", 150f, () => SecurityClientScript.Mine("sec.visit") == "", 120f, (Dialogue d) => (d.choices.Count >= 2) ? 1 : 0);
		Check("bribe-paid", () => SecurityClientScript.N("sec.bribe") > SecurityClientScript.N(Vars["b0"]) && SecurityClientScript.N("cash") < 900, () => "bribe tracker " + Vars["b0"] + " -> " + SecurityClientScript.Mine("sec.bribe") + ", cash 900 -> " + SecurityClientScript.Mine("cash"));
		Settle("bribe");
		SameAll("bribe", "cash", "sec.bribe", "sec.contraband", "sec.rep", "sec.fp", "sec.part");
		SameSoft("bribe", "sec.items");
		HandOver("bribe");
		Cmd("fine-cash", "cash", "", 3000);
		Cmd("fine-spawn", "spawn", "main", 3);
		WaitFor("fine-ready", 15f, () => SecurityClientScript.Mine("cash") == "3000", () => "cash " + SecurityClientScript.Mine("cash"));
		Call("fine", "securityInspector");
		Pace("fine-dialogue", 150f, () => SecurityClientScript.Mine("sec.visit") == "", 120f);
		Check("fine-paid", () => SecurityClientScript.N("cash") < 3000, () => "cash 3000 -> " + SecurityClientScript.Mine("cash") + ", contraband " + SecurityClientScript.Mine("sec.contraband") + ", rep " + SecurityClientScript.Mine("sec.rep"));
		Settle("fine");
		SameAll("fine", "cash", "sec.contraband", "sec.rep", "sec.inspLoss", "sec.fp", "sec.part");
		SameSoft("fine", "sec.items");
		HandOver("fine");
		Cmd("eleanor-spawn", "spawn", "crate");
		WaitFor("eleanor-crate", 15f, () => Crate() != null, () => "crates " + SecurityClientScript.Mine("sec.crate"));
		Call("eleanor", "eleanor4");
		Do("eleanor-move", delegate
		{
			GameItem val = Crate();
			Vars["eq"] = BotScript.Str(ItemsMove.AppliedCount);
			int num2 = ItemsMove.QuickMove(val, (GraphNodeStorage)(object)EmporiumEntry.Instance.frontInvinvElement, all: true, -1, "bot");
			return "items.quickMove of the crate uid " + val.uniqueId + " onto the front counter: request " + num2;
		});
		WaitFor("eleanor-moved", 15f, () => ItemsMove.AppliedCount > SecurityClientScript.N(Vars["eq"]), () => "crate " + SecurityClientScript.Mine("sec.crate") + ", reject " + (ItemsMove.LastReject?.why ?? "-"));
		WaitFor("eleanor-trap", 15f, () => SecurityClientScript.Mine("sec.arresting") == "1", () => "arresting " + SecurityClientScript.Mine("sec.arresting") + ", crate " + SecurityClientScript.Mine("sec.crate") + ", eleanor " + SecurityClientScript.Mine("sec.eleanor"));
		Check("eleanor-arrested-flag", () => SecurityClientScript.Mine("sec.eleanor") == "1" && SecurityClientScript.Mine("sec.crate").Contains(":not"), () => "eleanor " + SecurityClientScript.Mine("sec.eleanor") + ", crate " + SecurityClientScript.Mine("sec.crate"));
		WaitFor("eleanor-officers", 30f, () => Security.ClientOfficersHere, () => "officers " + SecurityClientScript.Mine("sec.officers"));
		Pace("eleanor-arrest", 90f, () => SecurityClientScript.Mine("sec.visit") == "");
		WaitFor("eleanor-officers-left", 40f, () => SecurityClientScript.Mine("sec.officers") == "0", () => "officers " + SecurityClientScript.Mine("sec.officers"));
		Settle("eleanor");
		SameAll("eleanor", "sec.eleanor", "sec.crate", "cash", "sec.slips", "sec.rep", "sec.fp", "sec.part");
		SameSoft("eleanor", "sec.items");
		HandOver("eleanor");
		Shootout("aug_med", "aug");
		HandOver("aug");
		Shootout("scavGeneral", "manslaughter");
		HandOver("manslaughter", 500f);
		Cmd("end-spawn", "spawn", "main", 4);
		WaitFor("end-spawned", 15f, () => SecurityClientScript.N("sec.contraband") > 0, () => "contraband " + SecurityClientScript.Mine("sec.contraband"));
		Call("end", "securityInspector");
		Pace("end-dialogue", 150f, () => SecurityClientScript.Mine("sec.ended") != "0", 140f);
		WaitFor("end-arrest", 20f, () => SecurityClientScript.Mine("sec.ended") == "1:arrest", () => "ended " + SecurityClientScript.Mine("sec.ended") + ", shown " + DayNightRun.LastShown);
		WaitServer("end-server", 20f, (IReadOnlyDictionary<string, string> v) => v.TryGetValue("sec.ended", out var value) && value == "1:arrest", "sec.ended");
		Do("end-report-after", delegate
		{
			Vars["dr"] = BotScript.Str(DayNight.Refusals);
			Net.ToServer("sec.report", new MsgSecAct
			{
				visit = Security.VisitKey()
			});
			return "sent sec.report after the ending";
		});
		WaitFor("end-report-refused", 10f, () => DayNight.LastRefusedWhat == "sec.report", () => "day-night refused '" + DayNight.LastRefusedWhat + "': " + DayNight.LastRefusedWhy);
		HandOver("end");
		Cmd("done", "done");
	}

	private List<GameItem> NewStolen()
	{
		string value;
		HashSet<string> before = new HashSet<string>(from x in (Vars.TryGetValue("s0", out value) ? value : "").Split(',')
			select x.Split('=')[0]);
		return (from it in SecurityProbes.AllItems()
			where it.IsTag("STOLEN_TAG") && !before.Contains(BotScript.Str(it.uniqueId))
			orderby it.uniqueId
			select it).ToList();
	}

	private static GameItem Crate()
	{
		return (from it in SecurityProbes.AllItems()
			where it.identifier == "luxury_supply_crate" && GeneralHelper.IsItemOwned(it) && (object)it.parentInventory == EmporiumEntry.Instance.invElement
			orderby it.uniqueId
			select it).LastOrDefault();
	}

	private void ClearCounter(string tag)
	{
		float sent = -100f;
		Step(tag + "-counter-clear", 60f, delegate(BotStep st)
		{
			if (SecurityClientScript.Mine("sec.visit") == "" && SecurityClientScript.Mine("sec.officers") == "0")
			{
				st.Detail = "nobody at the counter after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
				return StepStatus.Pass;
			}
			if (SecurityClientScript.Mine("sec.arrived") == "1" && Time.realtimeSinceStartup - sent > 10f)
			{
				sent = Time.realtimeSinceStartup;
				Net.ToServer("bot.sec", new SecBotCmd
				{
					cmd = "clear"
				});
			}
			return StepStatus.Running;
		}).TimeoutDetail = () => "cust " + SecurityClientScript.Mine("sec.cust") + ", officers " + SecurityClientScript.Mine("sec.officers");
	}

	private void Call(string tag, string id)
	{
		ClearCounter(tag);
		string arg = ((id == "scavGeneral") ? "plain" : ((id == "aug_med") ? "aug" : ((!(id == "securityInspector")) ? id : ((tag == "bribe") ? "inspector1" : "inspector0"))));
		Cmd(tag + "-queue", "queue", arg);
		WaitFor(tag + "-queued", 15f, () => Game.Store.storeClientManager.clientStack.Count == 1 && Game.Store.storeClientManager.clientStack[0].identifier == id, () => "queue " + string.Join(",", Game.Store.storeClientManager.clientStack.Select((StoreClient c) => c.identifier)));
		Do(tag + "-call", delegate
		{
			Customers.RequestCall();
			return "bell (customers.call)";
		});
		WaitFor(tag + "-called", 20f, () => SecurityClientScript.Mine("sec.cust") == id && SecurityClientScript.Mine("sec.lock") == BotScript.Str(Net.LocalPlayer), () => "cust " + SecurityClientScript.Mine("sec.cust") + ", counter " + SecurityClientScript.Mine("sec.lock"));
		WaitFor(tag + "-arrived", 30f, () => SecurityClientScript.Mine("sec.arrived") == "1", () => "arrived " + SecurityClientScript.Mine("sec.arrived"));
		Delay(tag + "-walk", 1.5f);
	}

	private void Settle(string tag)
	{
		WaitIdle(tag + "-idle", 40f);
		Delay(tag + "-settle", 2f);
	}

	private void Shootout(string id, string tag)
	{
		Call(tag, id);
		Do(tag + "-aim", delegate
		{
			StoreUIManager.Instance.EnterShootingMode();
			return "entering the shooting view";
		});
		WaitFor(tag + "-aiming", 10f, () => StoreUIManager.Instance.isShootingMode, () => "isShootingMode " + StoreUIManager.Instance.isShootingMode);
		Do(tag + "-shoot", delegate
		{
			StoreUIManager.Instance.Shoot();
			return "shot (request sent)";
		});
		WaitFor(tag + "-shot-replayed", 15f, () => SecurityClientScript.Mine("sec.shot") == "1", () => "shot " + SecurityClientScript.Mine("sec.shot") + ", refusal '" + Security.LastRefusal + "'");
		WaitFor(tag + "-shootout-officer", 30f, () => SecurityClientScript.Mine("sec.shootout") == "1", () => "shootout " + SecurityClientScript.Mine("sec.shootout"));
		WaitFor(tag + "-officer-arrived", 30f, () => Security.ClientOfficersHere, () => "officers " + SecurityClientScript.Mine("sec.officers"));
		Pace(tag + "-over", 120f, () => SecurityClientScript.Mine("sec.visit") == "" && SecurityClientScript.Mine("sec.shot") == "0");
		WaitIdle(tag + "-idle", 40f);
		Delay(tag + "-settle", 3f);
		SameAll(tag, "sec.aug", "cash", "sec.slips", "sec.rep", "sec.crimes", "sec.fp", "sec.part", "sec.shot", "sec.shootout");
		SameSoft(tag, "sec.items");
	}

	private BotStep Pace(string name, float timeout, Func<bool> done, float clearAfter = -1f, Func<Dialogue, int> choose = null)
	{
		float last = 0f;
		int requests = 0;
		bool cleared = false;
		List<string> chosen = new List<string>();
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (done())
			{
				st.Detail = "after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s, " + requests + " request(s)" + ((chosen.Count > 0) ? (", chose " + string.Join(" ", chosen)) : "") + (cleared ? " (ended by the bot's timer dismiss)" : "");
				return StepStatus.Pass;
			}
			if (!cleared && clearAfter > 0f && st.Elapsed > clearAfter)
			{
				cleared = true;
				Net.ToServer("bot.sec", new SecBotCmd
				{
					cmd = "clear"
				});
			}
			if (Time.realtimeSinceStartup - last < 0.8f)
			{
				return StepStatus.Running;
			}
			last = Time.realtimeSinceStartup;
			CounterChannel counter = DialogueSystem.Counter;
			Dialogue current = counter.Current;
			if (current == null)
			{
				return StepStatus.Running;
			}
			if (counter.LineUnfinished)
			{
				if (DialogueSync.RequestShown("counter"))
				{
					requests++;
				}
			}
			else if (current.choices.Count > 0)
			{
				if (current != choiceLine)
				{
					choiceLine = current;
					choiceSince = Time.realtimeSinceStartup;
					return StepStatus.Running;
				}
				if (Time.realtimeSinceStartup - choiceSince < 1.5f)
				{
					return StepStatus.Running;
				}
				int num = ((choose != null) ? choose(current) : 0);
				if (num < 0 || num >= current.choices.Count)
				{
					num = 0;
				}
				if (DialogueSync.RequestChoose("counter", num))
				{
					requests++;
					chosen.Add(num + "/" + current.choices.Count);
				}
			}
			else if (DialogueSync.RequestAdvance("counter"))
			{
				requests++;
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "cust=" + SecurityClientScript.Mine("sec.cust") + " officers=" + SecurityClientScript.Mine("sec.officers") + " arresting=" + SecurityClientScript.Mine("sec.arresting") + " shot=" + SecurityClientScript.Mine("sec.shot") + " dialogue=" + SecurityClientScript.Mine("sec.dlg") + " requests=" + requests + " refused '" + DialogueSync.LastRefused + "'";
		return botStep;
	}
}
