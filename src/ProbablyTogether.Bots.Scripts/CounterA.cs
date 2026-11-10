using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CounterA : CounterBot
{
	private int b;

	private string bName = "";

	private long freeId;

	private string lastNegoRefusal = "";

	public override string Name => "counter-a";

	public override BotRole Role => BotRole.Client;

	protected override void Build()
	{
		Joined();
		Do("listen", delegate
		{
			Negotiation.Refused += delegate(string k, string r)
			{
				lastNegoRefusal = k + ": " + r;
			};
			return "negotiation refusals are logged";
		});
		PollServer("both-in", 900f, (Dictionary<string, string> s) => CustomersBot.Int(s["cust.ready"]) >= 2);
		Do("options", delegate
		{
			EmporiumOption instance = EmporiumOption.Instance;
			if ((Object)(object)instance != (Object)null)
			{
				instance.autoCallNext = false;
				instance.autoNextDialog = false;
			}
			return "autoCallNext off, autoNextDialog off (the bot paces)";
		});
		Do("open-shutter", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "clicked the shutter (state " + CustomersBot.Mine("cust.state") + ")";
		});
		WaitFor("open-here", 30f, () => CustomersBot.Mine("cust.state") == "OPEN", CustomersBot.ShowMine);
		Mark("a:open");
		PollServer("queued", 60f, (Dictionary<string, string> s) => s["cust.head"] == "scavGeneral" && CustomersBot.Int(s["cust.queue"]) >= 3);
		NegoOp("bargain-flag", "unlock");
		NegoOp("aug-flag", "augintro");
		WaitFor("flags", 20f, () => CustomersBot.Mine("ctr.flags") == "ba", () => "flags " + CustomersBot.Mine("ctr.flags"));
		Converge("same-start", 20f, "cust.state", "cust.queue", "cust.head", "cust.current", "ctr.flags", "cust.digest", "cust.action");
		Do("call-1", delegate
		{
			v1 = Customers.Visit;
			Customers.RequestCall();
			return "visit " + v1 + ", head " + CustomersBot.Mine("cust.head");
		});
		WaitFor("a-takes", 15f, () => CustomersBot.Mine("cust.current") == "scavGeneral" && CounterBot.Holder() == BotScript.Str(CounterBot.Me), () => "customer " + CustomersBot.Mine("cust.current") + ", holder " + CounterBot.Holder() + ", me " + CounterBot.Me);
		WaitFor("arrived-1", 30f, () => CustomersBot.Mine("cust.arrived") == "1", CustomersBot.ShowMine);
		PaceTalk("talk-1", 60f, needStart: true);
		Converge("same-talk-1", 15f, CounterBot.Same);
		Mark("a:holding");
		WaitMark("b-refused", 120f, "b:refused");
		Mark("a:want-cig");
		WaitMark("cig-placed", 30f, "s:cig");
		WaitFor("cig-here", 15f, () => CounterBot.Cig() != null, () => "front counter " + CustomersBot.Mine("cust.stock"));
		Do("confront", delegate
		{
			GameItem it = CounterBot.Cig();
			string text2 = new string[5] { "CIGARETTE_LETTERING_ISSUE", "CIGARETTE_COLOR_ISSUE", "CIGARETTE_SEAL_ISSUE", "CIGARETTE_LOGO_ISSUE", "CIGARETTE_BOX_ISSUE" }.FirstOrDefault((string r) => it.IsTag(r)) ?? "CIGARETTE_SEAL_ISSUE";
			Vars["dlg0"] = BotScript.Str(DialogueSync.Seq("counter"));
			bool flag = Negotiation.RequestConfront(it, "inspect", text2);
			ItemFeature val = it.FindItemFeatureByCategory("CATEGORY_GENUINE_CIGARETTE");
			return (flag ? "nego.confront via inspect sent" : "NOT sent") + " on " + it.identifier + " #" + Ids.Of(it) + ", reason " + text2 + " (pack " + (val?.realCondition?.identifier ?? "?") + ", tagged " + it.IsTag(text2) + ")";
		});
		WaitFor("confronted", 15f, () => DialogueSystem.Counter.Current != null && DialogueSync.Seq("counter") > CustomersBot.Int(Vars["dlg0"]), () => "dialogue steps " + Vars["dlg0"] + " -> " + DialogueSync.Seq("counter") + ", line " + DialogueSync.CurrentFp("counter") + ", last nego refusal " + lastNegoRefusal);
		Converge("same-confront", 20f, "cust.current", "cust.holder", "cust.dlgfp", "ctr.nego", "ctr.rep", "cust.stock", "cust.digest", "cust.action");
		PaceTalk("talk-confront", 40f, needStart: false);
		Converge("same-after-confront", 15f, CounterBot.Same);
		Mark("a:want-talk");
		WaitMark("talk-ready", 30f, "s:talk");
		WaitFor("talk-here", 10f, () => Negotiation.CurrentCustomer()?.interogationDialogue != null, () => "interrogation dialogue here " + (Negotiation.CurrentCustomer()?.interogationDialogue != null));
		string[] array = new string[2] { "interrogation", "glasses" };
		foreach (string text in array)
		{
			string w = text;
			Do("ask-" + w, delegate
			{
				Vars["t0"] = BotScript.Str(DialogueSync.Seq("counter"));
				return (Negotiation.RequestTalk(w) ? ("nego.talk " + w + " sent") : "NOT sent") + " (dialogue steps " + Vars["t0"] + ")";
			});
			WaitFor("asked-" + w, 15f, () => DialogueSystem.Counter.Current != null && DialogueSync.Seq("counter") > CustomersBot.Int(Vars["t0"]), () => "steps " + Vars["t0"] + " -> " + DialogueSync.Seq("counter") + ", line " + DialogueSync.CurrentFp("counter") + ", last nego refusal " + lastNegoRefusal);
			Converge("same-" + w, 20f, "cust.current", "cust.holder", "cust.dlgfp", "cust.dlgseq", "ctr.nego", "cust.digest", "cust.action");
			PaceTalk("talk-" + w, 30f, needStart: false);
		}
		Converge("same-after-talk", 15f, CounterBot.Same);
		Do("bell", delegate
		{
			Customers.RequestDismiss(callNext: true);
			return "the bell: dismiss " + CustomersBot.Mine("cust.current") + " and call the next (visit " + Customers.Visit + ")";
		});
		WaitFor("gone-1", 20f, () => Customers.Visit > v1 + 1 || CustomersBot.Mine("cust.current") == "none", CustomersBot.ShowMine);
		Mark("a:finished");
		WaitMark("b-took-2", 120f, "b:took-2");
		Do("note-b", delegate
		{
			b = CustomersBot.Int(CounterBot.Holder());
			bName = Net.PlayerName(b);
			return "holder " + b + " (" + bName + "), customer " + CustomersBot.Mine("cust.current");
		});
		Check("b-holds", () => b > 0 && b != CounterBot.Me, () => "holder " + CounterBot.Holder() + ", me " + CounterBot.Me);
		MoveRefusedHere("a-move-refused-here", () => CounterBot.OwnedFitting((GameInventory)(object)CounterBot.E.backInvinvElement), () => (GameInventory)(object)CounterBot.E.backInvinvElement, () => bName + " is at the counter.");
		HaggleRefusedHere("a-haggle-refused-here", (BargainType)3, 10, () => bName);
		Mark("a:refused");
		WaitMark("b-bell-2", 180f, "b:bell-2");
		WaitFor("aug-here", 40f, () => CustomersBot.Mine("cust.current") == "aug_med" && CustomersBot.Mine("cust.arrived") == "1", CustomersBot.ShowMine);
		Converge("same-aug", 15f, "cust.current", "cust.visit", "cust.holder", "ctr.lock", "cust.digest");
		Check("aug-came-free", () => CounterBot.Holder() == "-1", () => "holder " + CounterBot.Holder() + " (nobody should hold it: the auto-called customer is free)");
		Do("threaten", delegate
		{
			bool flag = Negotiation.RequestBargain((BargainType)5, 20);
			return (flag ? "bargain.offer Threaten sent" : "NOT sent") + " (intent " + CustomersBot.Mine("ctr.intent") + ", flags " + CustomersBot.Mine("ctr.flags") + ")";
		});
		WaitFor("a-took-3", 15f, () => CounterBot.Holder() == BotScript.Str(CounterBot.Me), () => "holder " + CounterBot.Holder() + ", me " + CounterBot.Me + ", last nego refusal " + lastNegoRefusal);
		Converge("same-threaten", 20f, CounterBot.Same);
		Do("threaten-outcome", () => "features " + CustomersBot.Mine("ctr.feat") + ", intent " + CustomersBot.Mine("ctr.intent") + ", rep " + CustomersBot.Mine("ctr.rep") + ", line " + DialogueSync.CurrentFp("counter"));
		Mark("a:took-3");
		WaitMark("b-refused-3", 120f, "b:refused-3");
		PaceTalk("talk-3", 60f, needStart: false);
		Do("dismiss-3", delegate
		{
			Customers.RequestDismiss(callNext: false);
			return "dismiss button (no call): visit " + Customers.Visit;
		});
		WaitFor("gone-3", 20f, () => CustomersBot.Mine("cust.current") == "none", CustomersBot.ShowMine);
		Converge("same-gone-3", 15f, CounterBot.Same);
		Check("counter-free-3", () => CounterBot.Holder() == "-1", () => "holder " + CounterBot.Holder());
		Mark("a:empty");
		Step("a-free-move", 20f, delegate(BotStep st)
		{
			if (freeId == 0L)
			{
				GameItem val = CounterBot.OwnedFitting((GameInventory)(object)CounterBot.E.backInvinvElement);
				if (val == null)
				{
					st.Detail = "nothing fits the back counter";
					return StepStatus.Fail;
				}
				freeId = Ids.Of(val);
				ItemsMove.QuickMove(val, (GraphNodeStorage)(object)CounterBot.E.backInvinvElement, all: false, -1, "bot");
				return StepStatus.Running;
			}
			GameItem val2 = Ids.Find(freeId);
			if (val2 == null || (object)val2.parentInventory != CounterBot.E.backInvinvElement)
			{
				return StepStatus.Running;
			}
			st.Detail = val2.identifier + "#" + freeId + " -> back counter with nobody at the counter; holder after " + CounterBot.Holder() + " (no lock taken)";
			return (CounterBot.Holder() == "-1" && CustomersBot.Mine("cust.current") == "none") ? StepStatus.Pass : StepStatus.Fail;
		});
		Step("a-free-transfer", 20f, delegate(BotStep st)
		{
			if (st.Elapsed < 0.05f)
			{
				ItemsMove.TransferButton(frontVisible: false, "bot");
			}
			GameItem val = Ids.Find(freeId);
			if (val == null || (object)val.parentInventory != CounterBot.E.invElement)
			{
				return StepStatus.Running;
			}
			st.Detail = "transfer button: #" + freeId + " back to the main storage; holder " + CounterBot.Holder();
			return (CounterBot.Holder() == "-1") ? StepStatus.Pass : StepStatus.Fail;
		});
		Converge("same-free", 15f, CounterBot.Same);
		WaitMark("b-free", 120f, "b:free");
		Do("clear-queue", delegate
		{
			if (Game.Store.CanEndDay())
			{
				return "every waiting customer can be sent away";
			}
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "clearqueue"
			});
			return "a story customer waits (cannot be sent away): day-night's lab op clearqueue";
		});
		WaitFor("closable", 20f, () => Game.Store.CanEndDay(), () => "queue " + CustomersBot.Mine("cust.queue"));
		Do("close", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "dayNight.close sent";
		});
		WaitFor("closed", 30f, () => CustomersBot.Mine("cust.state") == "CLOSED", CustomersBot.ShowMine);
		Delay("past-close-timers", 5f);
		Do("door", delegate
		{
			CounterBot.E.selectedBackpack = null;
			MapUIManager.Instance.OpenUI();
			return "dayNight.goOut sent";
		});
		WaitFor("map-open", 20f, () => DayNightOuting.MapOpen, () => "map open " + DayNightOuting.MapOpen + ", refused " + DayNight.LastRefusedWhy);
		Do("travel-jackson", delegate
		{
			AfterhourLocation val = AfterhourLocation.Get("dr_jackson");
			MapUIManager.Instance.Travel(val);
			return "discovered " + (val != null && val.IsDiscovered()) + ", available " + (val != null && val.IsAvailable());
		});
		WaitFor("at-jackson", 30f, () => DayNightOuting.LocationId == "dr_jackson" && CustomersBot.Mine("cust.current") == "inventor", () => "at " + CustomersBot.Mine("ctr.loc") + ", customer " + CustomersBot.Mine("cust.current") + "; refused " + DayNight.LastRefusedWhy);
		Converge("same-jackson", 20f, "ctr.loc", "cust.current", "ctr.jlock", "cust.holder", "cust.action");
		Check("jackson-free", () => CustomersBot.Mine("ctr.jlock") == "-1" && CustomersBot.Mine("cust.holder") == "-1", () => "outing.jackson " + CustomersBot.Mine("ctr.jlock") + ", counter " + CustomersBot.Mine("cust.holder"));
		Mark("a:jackson");
		WaitMark("b-jackson-took", 120f, "b:jackson-took");
		MoveRefusedHere("a-jackson-refused-here", () => CounterBot.CheapestGoods(), () => (GameInventory)(object)CounterBot.E.backInvinvElement, () => bName + " is haggling with Dr. Jackson.");
		HaggleRefusedHere("a-jackson-haggle-refused-here", (BargainType)3, 10, () => bName);
		Mark("a:jackson-refused");
		WaitMark("b-jackson-done", 120f, "b:jackson-done");
		Do("return-early", delegate
		{
			DayNight.LastRefusedWhat = "";
			MapUIManager.Instance.ReturnNow();
			return "dayNight.return sent while " + bName + " holds Dr. Jackson's counter";
		});
		WaitFor("return-refused", 15f, () => DayNight.LastRefusedWhat == "return", () => "refused '" + DayNight.LastRefusedWhat + "': " + DayNight.LastRefusedWhy + "; at " + CustomersBot.Mine("ctr.loc"));
		Check("still-at-jackson", () => CustomersBot.Mine("ctr.loc") == "dr_jackson", () => "at " + CustomersBot.Mine("ctr.loc") + "; refusal: " + DayNight.LastRefusedWhy);
		Mark("a:return-refused");
		WaitMark("b-stepped-away", 60f, "b:stepped-away");
		Do("return", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "dayNight.return sent";
		});
		WaitFor("home", 30f, () => DayNightOuting.LocationId == "", () => "at " + CustomersBot.Mine("ctr.loc"));
		Converge("same-home", 20f, "ctr.loc", "cust.current", "ctr.jlock", "cust.holder", "cash", "cust.action");
		Check("jackson-lock-freed", () => CustomersBot.Mine("ctr.jlock") == "-1", () => "outing.jackson holder " + CustomersBot.Mine("ctr.jlock"));
		Check("no-mismatch", () => Customers.Mismatches == 0 && ItemsMove.ApplyMismatches == 0, () => "customers mismatches " + Customers.Mismatches + ", items apply mismatches " + ItemsMove.ApplyMismatches + "; replay checks ok " + Replay.ChecksOk + " bad " + Replay.ChecksBad).Soft();
		Mark("a:home");
		ReportDone();
	}
}
