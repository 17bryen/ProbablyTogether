using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DialogueLiveA : DialogueLiveBase
{
	private int cashBefore;

	private int seqAtChoose;

	private int choices;

	private float lastTouch;

	private float typingSeenAt;

	public override string Name => "dialogue-live-a";

	protected override void Build()
	{
		Join();
		QuietOptions();
		WaitMark("b-ready", "live-b", 300f);
		Command("wait-lou", "wait-lou", (string r) => r.StartsWith("ok", StringComparison.Ordinal));
		WaitFor("lou-talking", 20f, () => DialogueBotBase.Cur() == "wanted5morning" && DialogueSystem.Counter.Current != null, DialogueBotBase.Live);
		Check("lou-timer-skipped-here", () => DialogueSystem.MorningSkipped >= 1, () => "morning timers skipped " + DialogueSystem.MorningSkipped);
		SameAsServerSoon("same-lou", 10f, "dlg.counter.at", "dlg.digest", "cust.current", "story.hash");
		WaitMark("b-saw-lou", "saw-lou", 40f);
		Command("lou-leaves", "timer-dismiss", (string r) => r.StartsWith("ok dismissed", StringComparison.Ordinal));
		WaitFor("lou-gone", 15f, () => DialogueBotBase.Cur() == "none" && DialogueSystem.Counter.Current == null, DialogueBotBase.Live);
		SameAsServerSoon("same-after-lou", 15f, "dlg.counter.at", "dlg.digest", "cust.current", "story.hash", "lock.counter");
		WaitFor("counter-idle", 30f, () => DialogueSystem.Counter.Current == null && Locks.HeldBy("counter") < 0 && DialogueBotBase.Cur() == "none", DialogueBotBase.Live);
		Do("note-cash", delegate
		{
			cashBefore = Game.Store.playerCash;
			return "cash " + cashBefore + ", offers seen " + DialogueBotNet.Offers;
		});
		Command("offer-bt", "offer-bt", (string r) => r.StartsWith("ok", StringComparison.Ordinal));
		WaitFor("offer-claimed", 10f, () => DialogueBotNet.Offers >= 1 && DialogueSystem.Counter.Current != null && DialogueSystem.Counter.Current.startAction != null && DialogueSystem.Counter.Current.plainText == "line one", () => "the line here is this client's own copy (closures), offers " + DialogueBotNet.Offers + "; " + DialogueBotBase.Live());
		SameAsServerSoon("offer-seeded-draw", 8f, "dlg.bt.value", "dlg.counter.at");
		Drive("offer-to-choice", 20f, (int n) => DialogueSystem.Counter.IsChoiceNow && !DialogueSystem.Counter.LineUnfinished);
		WaitFor("offer-choices-up", 6f, () => DialogueBotBase.ChoiceBubbles() == 2, () => "choice bubbles " + DialogueBotBase.ChoiceBubbles());
		RequestThen("offer-choose-A", 10f, () => DialogueSync.RequestChoose("counter", 0), () => Game.Store.playerCash == cashBefore + 3, DialogueBotBase.Live);
		Drive("offer-to-end", 25f, (int n) => DialogueBotNet.BtEnds >= 1 && DialogueSystem.Counter.Current == null);
		Check("offer-outcome", () => Game.Store.playerCash == cashBefore + 8, () => "cash " + cashBefore + " -> " + Game.Store.playerCash + " (choice +3, end action +5)");
		WaitFor("offer-counter-free", 10f, () => Locks.HeldBy("counter") < 0, DialogueBotBase.Live);
		SameAsServerSoon("same-after-offer", 15f, "cash", "dlg.counter.at", "dlg.digest", "lock.counter");
		Command("start-bf", "start-bf", (string r) => r.StartsWith("ok", StringComparison.Ordinal));
		WaitFor("bf-waiting", 15f, () => DialogueSystem.Counter.Current == null && DialogueBotBase.BfScheduled() && DialogueBotNet.BfEnds == 0, DialogueBotBase.Live);
		SameAsServerSoon("same-scheduled", 10f, "dlg.digest", "dlg.bf.ends", "cash");
		Do("note-cash-bf", delegate
		{
			cashBefore = Game.Store.playerCash;
			return "cash " + cashBefore + ", scheduled " + DialogueSystem.Counter.Scheduled.Count;
		});
		Command("flush-embedded", "flush-embedded", (string r) => r.StartsWith("ok", StringComparison.Ordinal));
		WaitFor("flushed-here", 10f, () => DialogueBotNet.BfEnds == 1 && !DialogueBotBase.BfScheduled(), DialogueBotBase.Live);
		Check("flush-outcome", () => Game.Store.playerCash == cashBefore + 7, () => "cash " + cashBefore + " -> " + Game.Store.playerCash + " (end action +7, once)");
		SameAsServerSoon("same-after-flush", 10f, "cash", "dlg.bf.ends", "dlg.digest", "dlg.counter.at");
		Do("open-shutter", delegate
		{
			if ((int)Game.Store.storeState != 2)
			{
				return "already " + Game.Store.storeState.ToString();
			}
			StoreShutterButton.Instance.OnClick();
			return "shutter clicked (dayNight.open)";
		});
		WaitFor("open", 30f, () => (int)Game.Store.storeState == 1, () => "state " + Game.Store.storeState.ToString());
		WaitFor("counter-empty", 30f, () => DialogueBotBase.Cur() == "none" && DialogueSystem.Counter.Current == null, DialogueBotBase.Live);
		Command("queue-board", "queue:appraisalBoardIntro", (string r) => r.StartsWith("ok", StringComparison.Ordinal));
		WaitFor("queued-here", 15f, delegate
		{
			StoreClientManager storeClientManager = Game.Store.storeClientManager;
			return storeClientManager != null && storeClientManager.clientStack?.Count > 0 && Game.Store.storeClientManager.clientStack[0]?.identifier == "appraisalBoardIntro";
		}, () => "queue head " + (Game.Store.storeClientManager?.clientStack?.FirstOrDefault()?.identifier ?? "none"));
		Do("call", delegate
		{
			Customers.RequestCall();
			return "customers.call sent";
		});
		WaitFor("called", 20f, () => DialogueBotBase.Cur() == "appraisalBoardIntro" && Locks.HeldBy("counter") == Net.LocalPlayer, DialogueBotBase.Live);
		WaitFor("main-started", 45f, () => DialogueSystem.Counter.Current != null && BotProbes.Read("dlg.main") == "appraisalBoardIntro/1", DialogueBotBase.Live);
		SameAsServerSoon("same-main", 10f, "dlg.counter.at", "dlg.main", "cust.current", "cust.holder");
		Drive("intro-3-lines", 30f, (int n) => n >= 3 && !DialogueSystem.Counter.LineUnfinished && DialogueSystem.Counter.WaitingManual);
		SameAsServerSoon("same-mid", 10f, "dlg.counter.at", "dlg.digest");
		Command("mark-mid", "mark:mid", (string r) => r == "ok");
		int id = -1;
		float lastAsk = -10f;
		Step("b-rejoined", 170f, delegate(BotStep st)
		{
			if (st.Elapsed - lastTouch > 20f)
			{
				lastTouch = st.Elapsed;
				Customers.RequestTakeCounter();
			}
			if (id >= 0)
			{
				string text = DialogueBotNet.Take(id);
				if (text == null)
				{
					return StepStatus.Running;
				}
				id = -1;
				if (text == "yes")
				{
					st.Detail = "B is back after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s; " + DialogueBotBase.Live();
					return StepStatus.Pass;
				}
			}
			if (st.Elapsed - lastAsk >= 1f)
			{
				lastAsk = st.Elapsed;
				id = DialogueBotNet.Send("has:rejoined");
			}
			return StepStatus.Running;
		}, delegate
		{
			lastTouch = 0f;
		}).TimeoutDetail = () => "B never came back; " + DialogueBotBase.Live();
		Check("still-holder", () => Locks.HeldBy("counter") == Net.LocalPlayer, DialogueBotBase.Live);
		Drive("to-choice", 40f, (int n) => DialogueSystem.Counter.IsChoiceNow && !DialogueSystem.Counter.LineUnfinished);
		WaitFor("choices-up", 8f, () => DialogueBotBase.ChoiceBubbles() == DialogueSystem.Counter.Current.choices.Count, () => "choice bubbles " + DialogueBotBase.ChoiceBubbles() + " of " + DialogueSystem.Counter.Current?.choices?.Count);
		Do("note-choices", delegate
		{
			choices = DialogueSystem.Counter.Current.choices.Count;
			seqAtChoose = DialogueSync.Seq("counter");
			return choices + " choices (join shown when cash >= " + Game.Store.GetAppraisalJoinCost() + "), seq " + seqAtChoose;
		});
		SameAsServerSoon("same-choice-line", 10f, "dlg.counter.at", "dlg.digest", "cash");
		Command("mark-choice", "mark:choice", (string r) => r == "ok");
		WaitMark("b-forged", "forged", 40f);
		RequestThen("choose-later", 10f, () => DialogueSync.RequestChoose("counter", 1), () => DialogueSync.Seq("counter") >= seqAtChoose + 1 && (DialogueSystem.Counter.Current == null || !DialogueSystem.Counter.IsChoiceNow), DialogueBotBase.Live);
		Drive("to-end", 40f, (int n) => DialogueLiveBase.FutureHasBoard2() && DialogueSystem.Counter.Current == null);
		WaitFor("customer-left", 20f, () => DialogueBotBase.Cur() == "none", DialogueBotBase.Live);
		Check("later-queued", () => DialogueLiveBase.FutureHasBoard2(), () => "future " + BotProbes.Read("dlg.future"));
		SameAsServerSoon("same-after-visit", 15f, "cash", "dlg.future", "cust.current", "dlg.counter.at", "dlg.digest", "lock.counter", "story.hash", "events.hash", "requests.hash");
		Check("visit-no-mismatch", () => DialogueSync.MismatchCount == 0, () => "stepDone mismatches " + DialogueSync.MismatchCount);
		Command("phone-open", "phone-open:" + 8376L, (string r) => r.StartsWith("ok", StringComparison.Ordinal));
		Do("quick-dial", delegate
		{
			PhoneUIManager.Instance.AutoCall(8376L);
			return "AutoCall(" + 8376L + "), quick dials sent " + DialogueSystem.QuickDials;
		});
		WaitFor("talking", 15f, () => PhoneChannel.Talking && PhoneChannel.Caller?.dialogFuncId == "Wild" && Locks.HeldBy("phone") == Net.LocalPlayer && DialogueSystem.QuickDials == 1, DialogueBotBase.Ph);
		SameAsServerSoon("same-call", 10f, "dlg.phone.at", "dlg.phone.caller", "dlg.phone.talking", "dlg.digest", "lock.phone");
		Command("mark-call", "mark:call", (string r) => r == "ok");
		WaitMark("b-saw-call", "saw-call", 40f);
		Do("close-panel", delegate
		{
			PhoneUIManager.Instance.CloseUI();
			return "phone panel closed while talking: " + DialogueBotBase.Ph();
		});
		WaitFor("hung-up", 15f, () => !PhoneChannel.Talking && DialogueSystem.Phone.Current == null && Locks.HeldBy("phone") < 0, DialogueBotBase.Ph);
		SameAsServerSoon("same-after-hangup", 15f, "dlg.phone.at", "dlg.phone.talking", "dlg.phone.caller", "dlg.digest", "lock.phone", "cash");
		Check("no-mismatch", () => DialogueSync.MismatchCount == 0, () => "stepDone mismatches " + DialogueSync.MismatchCount);
		Command("queue-seller", "queue:scavGeneral", (string r) => r.StartsWith("ok", StringComparison.Ordinal));
		WaitFor("seller-queued", 15f, () => Game.Store.storeClientManager?.clientStack?.FirstOrDefault()?.identifier == "scavGeneral", () => "queue head " + (Game.Store.storeClientManager?.clientStack?.FirstOrDefault()?.identifier ?? "none"));
		Do("call-seller", delegate
		{
			Customers.RequestCall();
			return "customers.call sent";
		});
		WaitFor("seller-talking", 45f, () => DialogueBotBase.Cur() == "scavGeneral" && DialogueSystem.Counter.Current != null && BotProbes.Read("dlg.main") == "scavGeneral/1", DialogueBotBase.Live);
		Do("note-typing", delegate
		{
			typingSeenAt = Time.realtimeSinceStartup;
			return "line unfinished " + DialogueSystem.Counter.LineUnfinished;
		});
		AskServer("ask-timeouts");
		string value;
		Do("note-timeouts", delegate
		{
			Vars["timeouts"] = (base.Server.TryGetValue("dlg.timeouts", out value) ? value : "?");
			return "server timeouts so far " + Vars["timeouts"];
		});
		SameAsServerSoon("same-seller", 10f, "dlg.counter.at", "dlg.digest", "dlg.main", "cust.current");
		Step("pacer-typewriter", 20f, delegate(BotStep st)
		{
			if (DialogueSystem.Counter.LineUnfinished)
			{
				return StepStatus.Running;
			}
			st.Detail = "the seller's line finished typing " + (Time.realtimeSinceStartup - typingSeenAt).ToString("F1", CultureInfo.InvariantCulture) + " s after it started here; " + DialogueBotBase.Live();
			return StepStatus.Pass;
		}).TimeoutDetail = () => "still unfinished; " + DialogueBotBase.Live();
		AskServer("ask-timeouts-after");
		Check("shown-by-holder", () => base.Server.TryGetValue("dlg.timeouts", out value) && value == Vars["timeouts"], () => "server timeouts " + Vars["timeouts"] + " -> " + (base.Server.TryGetValue("dlg.timeouts", out value) ? value : "?") + " (unchanged = the holder's typewriter event became dialogue.shown)");
		SameAsServerSoon("same-after-shown", 10f, "dlg.counter.at", "dlg.digest");
		Command("mark-seller", "mark:seller", (string r) => r == "ok");
		WaitMark("b-saw-seller", "saw-seller", 40f);
		Do("close-shutter", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "dayNight.close sent with " + DialogueBotBase.Cur() + " at the counter; " + DialogueBotBase.Live();
		});
		WaitFor("closed", 30f, () => (int)Game.Store.storeState == 0 && DialogueBotBase.Cur() == "none", DialogueBotBase.Live);
		Delay("close-timers", 4f);
		SameAsServerSoon("same-after-close", 15f, "cash", "cust.current", "dlg.counter.at", "dlg.digest", "lock.counter", "story.hash", "events.hash");
		Check("close-no-mismatch", () => DialogueSync.MismatchCount == 0, () => "stepDone mismatches " + DialogueSync.MismatchCount);
		Command("mark-done", "mark:a-done", (string r) => r == "ok");
	}
}
