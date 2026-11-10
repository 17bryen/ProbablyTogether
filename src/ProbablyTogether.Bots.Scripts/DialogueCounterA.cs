using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DialogueCounterA : DialogueBotBase
{
	private const string Seller = "scavGeneral";

	private int seqAtChoose;

	private int cashBefore;

	private float typingSeenAt;

	private float speedBefore;

	private bool lockSeenFree;

	public override string Name => "dialogue-counter-a";

	public override float TimeoutSeconds => 420f;

	protected override void Build()
	{
		Join();
		QuietOptions();
		WaitFor("counter-idle", 60f, () => DialogueSystem.Counter.Current == null && Locks.HeldBy("phone") < 0 && DialogueBotBase.Cur() == "none", DialogueBotBase.Co);
		WaitMark("watcher-ready", "counter-b", 120f);
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
		Command("queue-seller", "queue:scavGeneral", (string r) => r.StartsWith("ok", StringComparison.Ordinal));
		WaitFor("queued-here", 15f, delegate
		{
			StoreClientManager storeClientManager = Game.Store.storeClientManager;
			return storeClientManager != null && storeClientManager.clientStack?.Count > 0 && Game.Store.storeClientManager.clientStack[0]?.identifier == "scavGeneral";
		}, delegate
		{
			StoreClientManager storeClientManager = Game.Store.storeClientManager;
			return "queue head " + ((storeClientManager == null || !(storeClientManager.clientStack?.Count > 0)) ? "none" : Game.Store.storeClientManager.clientStack[0]?.identifier);
		});
		AskServer("ask-timeouts");
		string value;
		Do("note-timeouts", delegate
		{
			Vars["timeouts"] = (base.Server.TryGetValue("dlg.timeouts", out value) ? value : "?");
			return "server timeouts so far " + Vars["timeouts"];
		});
		Do("slow-typewriter", delegate
		{
			StoreUIManager instance = StoreUIManager.Instance;
			speedBefore = instance.dialogSpeed;
			instance.dialogSpeed = 0.02f;
			instance.SetTypewriterSpeed();
			return "typewriter speed " + speedBefore.ToString("F2", CultureInfo.InvariantCulture) + " -> 0.02 on this copy only";
		});
		Do("call", delegate
		{
			Customers.RequestCall();
			return "customers.call sent";
		});
		WaitFor("called", 20f, () => DialogueBotBase.Cur() == "scavGeneral" && Locks.HeldBy("counter") == Net.LocalPlayer, DialogueBotBase.Live);
		Step("line-typing", 45f, delegate(BotStep st)
		{
			if (DialogueSystem.Counter.Current == null || !DialogueSystem.Counter.LineUnfinished)
			{
				return StepStatus.Running;
			}
			typingSeenAt = Time.realtimeSinceStartup;
			st.Detail = "the seller's first line is typing here (this client paces: holder " + Locks.HeldBy("counter") + "); " + DialogueBotBase.Live();
			return StepStatus.Pass;
		}).TimeoutDetail = () => "no unfinished line; " + DialogueBotBase.Live();
		Step("shown-by-timeout", 25f, delegate(BotStep st)
		{
			if (DialogueSystem.Counter.LineUnfinished)
			{
				return StepStatus.Running;
			}
			float num = Time.realtimeSinceStartup - typingSeenAt;
			st.Detail = "the server showed the line " + num.ToString("F1", CultureInfo.InvariantCulture) + " s after it started typing here (timeout " + 8f.ToString("F0", CultureInfo.InvariantCulture) + " s, minus the trip here); " + DialogueBotBase.Live();
			return (num >= 6.5f && num <= 14f) ? StepStatus.Pass : StepStatus.Fail;
		}).TimeoutDetail = () => "still unfinished; " + DialogueBotBase.Live();
		AskServer("ask-timeouts-after");
		Check("timeout-counted", () => base.Server.TryGetValue("dlg.timeouts", out value) && int.TryParse(value, out var result) && int.TryParse(Vars["timeouts"], out var result2) && result == result2 + 1, () => "server timeouts " + Vars["timeouts"] + " -> " + (base.Server.TryGetValue("dlg.timeouts", out value) ? value : "?"));
		Do("normal-typewriter", delegate
		{
			StoreUIManager.Instance.dialogSpeed = speedBefore;
			StoreUIManager.Instance.SetTypewriterSpeed();
			return "typewriter speed back to " + speedBefore.ToString("F2", CultureInfo.InvariantCulture);
		});
		SameAsServerSoon("same-after-timeout", 10f, "dlg.counter.at", "dlg.digest", "cust.holder");
		Drive("main-to-end", 40f, (int n) => DialogueSystem.Counter.Current == null && DialogueBotBase.Cur() == "scavGeneral");
		Check("still-holder", () => Locks.HeldBy("counter") == Net.LocalPlayer && DialogueBotBase.Cur() == "scavGeneral", DialogueBotBase.Live);
		SameAsServerSoon("same-after-main", 15f, "dlg.counter.at", "dlg.digest", "cust.current", "cust.holder", "cash");
		Command("mark-customer", "mark:customer", (string r) => r == "ok");
		Do("note-cash", delegate
		{
			cashBefore = PlayerStore.Instance.playerCash;
			return "cash " + cashBefore;
		});
		Command("start-bt", "start-bt", (string r) => r.StartsWith("ok", StringComparison.Ordinal));
		WaitFor("line-one", 10f, () => DialogueSystem.Counter.Current != null && DialogueSystem.Counter.Current.plainText == "line one" && !DialogueSystem.Counter.LineUnfinished, DialogueBotBase.Co);
		SameAsServerSoon("same-line-one", 10f, "dlg.counter.at");
		SameAsServerSoon("seeded-draw", 6f, "dlg.bt.value").Soft();
		RequestThen("advance", 10f, () => DialogueSync.RequestAdvance("counter"), () => DialogueSystem.Counter.Current != null && DialogueSystem.Counter.Current.choices.Count == 2, DialogueBotBase.Co);
		Check("holds-counter", () => Locks.HeldBy("counter") == Net.LocalPlayer, DialogueBotBase.Co);
		RequestThen("shown-choices", 12f, () => DialogueSync.RequestShown("counter"), () => !DialogueSystem.Counter.LineUnfinished, DialogueBotBase.Co);
		Delay("choices-up", 1.5f);
		Do("note-seq", delegate
		{
			seqAtChoose = DialogueSync.Seq("counter");
			return "seq " + seqAtChoose;
		});
		Command("mark-choice", "mark:bt-choice", (string r) => r == "ok");
		WaitMark("b-forged", "bt-forged", 30f);
		RequestThen("choose-A", 10f, () => DialogueSync.RequestChoose("counter", 0), () => DialogueSync.Seq("counter") >= seqAtChoose + 2 && DialogueSystem.Counter.Current != null && DialogueSystem.Counter.Current.plainText == "picked A", DialogueBotBase.Co);
		Check("choice-action-once", () => PlayerStore.Instance.playerCash == cashBefore + 3, () => "cash " + cashBefore + " -> " + PlayerStore.Instance.playerCash + " (expected +3)");
		RequestThen("end-action", 12f, () => DialogueSync.RequestShown("counter"), () => DialogueBotNet.BtEnds == 1 && DialogueSystem.Counter.Current == null, DialogueBotBase.Co);
		Check("end-action-once", () => PlayerStore.Instance.playerCash == cashBefore + 8 && DialogueBotNet.BtEnds == 1, () => "cash +" + (PlayerStore.Instance.playerCash - cashBefore) + " ends " + DialogueBotNet.BtEnds + " (expected +8, 1)");
		Check("counter-kept", () => Locks.HeldBy("counter") == Net.LocalPlayer && DialogueBotBase.Cur() == "scavGeneral", () => "the real customer keeps the counter with A after the conversation; " + DialogueBotBase.Live());
		SameAsServerSoon("same-after-bt", 15f, "cash", "dlg.bt.ends", "dlg.counter.at", "dlg.digest", "lock.counter");
		Command("start-bf", "start-bf", (string r) => r.StartsWith("ok", StringComparison.Ordinal));
		WaitFor("bf-waiting", 20f, () => DialogueSystem.Counter.Current == null && DialogueBotBase.BfScheduled() && DialogueBotNet.BfEnds == 0, DialogueBotBase.Live);
		SameAsServerSoon("same-scheduled", 10f, "dlg.digest", "dlg.bf.ends", "cash");
		Do("note-cash-bf", delegate
		{
			cashBefore = Game.Store.playerCash;
			return "cash " + cashBefore + ", end actions waiting " + DialogueSystem.Counter.Scheduled.Count;
		});
		Do("press-security", delegate
		{
			StoreSecButton.Instance.OnClick();
			return "security button pressed on this client (sec.report sent), customer " + DialogueBotBase.Cur();
		});
		Step("report-flushed-here", 15f, delegate(BotStep st)
		{
			if (DialogueBotNet.BfEnds < 1 || DialogueBotBase.BfScheduled())
			{
				return StepStatus.Running;
			}
			st.Detail = "the waiting end action ran by the report's flush: ends " + DialogueBotNet.BfEnds + ", cash " + cashBefore + " -> " + Game.Store.playerCash + "; " + DialogueBotBase.Live();
			return (DialogueBotNet.BfEnds == 1) ? StepStatus.Pass : StepStatus.Fail;
		}).TimeoutDetail = () => "bf ends " + DialogueBotNet.BfEnds + ", scheduled " + DialogueBotBase.BfScheduled() + ", security refusal '" + Security.LastRefusal + "'; " + DialogueBotBase.Live();
		Check("report-flush-cash", () => Game.Store.playerCash == cashBefore + 7, () => "cash " + cashBefore + " -> " + Game.Store.playerCash + " (end action +7)").Soft();
		SameAsServerSoon("same-after-report", 10f, "cash", "dlg.bf.ends", "dlg.digest");
		Drive("arrest-to-end", 120f, (int n) => DialogueBotBase.Cur() == "none", (Dialogue d) => 0);
		WaitFor("counter-released", 15f, () => Locks.HeldBy("counter") < 0, DialogueBotBase.Live);
		Delay("bf-timer-passes", 3f);
		Check("flush-once", () => DialogueBotNet.BfEnds == 1, () => "bf end actions " + DialogueBotNet.BfEnds + " (the 25 s timer must not run it again)");
		SameAsServerSoon("same-after-visit", 20f, "cash", "dlg.bf.ends", "dlg.counter.at", "dlg.digest", "lock.counter", "cust.current");
		Command("mark-visit", "mark:visit-over", (string r) => r == "ok");
		Do("note-cash-free", delegate
		{
			cashBefore = Game.Store.playerCash;
			lockSeenFree = true;
			return "cash " + cashBefore + ", nobody at the counter, lock " + Locks.HeldBy("counter");
		});
		Command("start-bt-free", "start-bt", (string r) => r.StartsWith("ok", StringComparison.Ordinal));
		Step("free-line-one", 10f, delegate(BotStep st)
		{
			if (Locks.HeldBy("counter") >= 0)
			{
				lockSeenFree = false;
			}
			Dialogue current = DialogueSystem.Counter.Current;
			if (current == null || current.plainText != "line one" || DialogueSystem.Counter.LineUnfinished)
			{
				return StepStatus.Running;
			}
			st.Detail = "line one shown by the server (nobody paces); " + DialogueBotBase.Live();
			return StepStatus.Pass;
		}).TimeoutDetail = () => DialogueBotBase.Live();
		RequestThen("free-advance", 10f, () => DialogueSync.RequestAdvance("counter"), delegate
		{
			if (Locks.HeldBy("counter") >= 0)
			{
				lockSeenFree = false;
			}
			return DialogueSystem.Counter.Current != null && DialogueSystem.Counter.Current.choices.Count == 2 && !DialogueSystem.Counter.LineUnfinished;
		}, DialogueBotBase.Co);
		Delay("free-choices-up", 1.5f);
		RequestThen("free-choose-B", 15f, () => DialogueSync.RequestChoose("counter", 1), delegate
		{
			if (Locks.HeldBy("counter") >= 0)
			{
				lockSeenFree = false;
			}
			return DialogueBotNet.BtEnds == 2 && DialogueSystem.Counter.Current == null;
		}, DialogueBotBase.Co);
		Check("never-locked", () => lockSeenFree && Locks.HeldBy("counter") < 0 && Game.Store.playerCash == cashBefore, () => "counter lock stayed free the whole conversation: " + lockSeenFree + ", cash " + cashBefore + " -> " + Game.Store.playerCash + " (choice B: +0)");
		SameAsServerSoon("same-after-free", 15f, "cash", "dlg.bt.ends", "dlg.counter.at", "dlg.digest", "lock.counter");
		Check("no-mismatch", () => DialogueSync.MismatchCount == 0, () => "stepDone mismatches " + DialogueSync.MismatchCount);
		Command("mark-done", "mark:counter-a-done", (string r) => r == "ok");
	}
}
