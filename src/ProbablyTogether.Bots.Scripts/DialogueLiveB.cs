using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DialogueLiveB : DialogueLiveBase
{
	private int refusedBefore;

	private int mismatchBefore;

	private int mainBlockedBefore;

	private int blockedBefore;

	private string address;

	private static readonly Type consoleType = AccessTools.TypeByName("QFSW.QC.QuantumConsole");

	private static readonly MethodInfo consoleActivate = AccessTools.Method("QFSW.QC.QuantumConsole:Activate", Type.EmptyTypes, (Type[])null);

	private static readonly MethodInfo consoleIsActive = AccessTools.PropertyGetter("QFSW.QC.QuantumConsole:IsActive");

	public override string Name => "dialogue-live-b";

	protected override void Build()
	{
		Join();
		Check("dev-txt-present", () => File.Exists(DevTools.MarkerPath("dev.txt")), () => DevTools.MarkerPath("dev.txt")).Soft();
		Check("devtools-off", () => !DevTools.IsEnabled, () => "DevTools.IsEnabled " + DevTools.IsEnabled + " while connected").Soft();
		Do("f9-blocked", delegate
		{
			blockedBefore = Suppress.BlockedDevTools;
			CustomUIDebugPanel.Toggle();
			CustomUIDebugPanel.ToggleLegacyPanel();
			if (Suppress.BlockedDevTools != blockedBefore + 2)
			{
				throw new Exception("F9 panels not blocked (" + blockedBefore + " -> " + Suppress.BlockedDevTools + ")");
			}
			return "Toggle and ToggleLegacyPanel blocked (" + Suppress.BlockedDevTools + ")";
		}).Soft();
		Do("f8-blocked", delegate
		{
			if (consoleType == null || consoleActivate == null)
			{
				return "no Quantum Console type in this build";
			}
			Object[] array = Object.FindObjectsOfType(consoleType);
			if (array.Length == 0)
			{
				return "no console object on this copy (the game destroyed it): nothing to open";
			}
			int blockedDevTools = Suppress.BlockedDevTools;
			Object[] array2 = array;
			foreach (Object obj in array2)
			{
				consoleActivate.Invoke(obj, null);
			}
			bool flag = array.Any(delegate(Object c)
			{
				object obj2 = consoleIsActive?.Invoke(c, null);
				bool flag2 = default(bool);
				int num;
				if (obj2 is bool)
				{
					flag2 = (bool)obj2;
					num = 1;
				}
				else
				{
					num = 0;
				}
				return (byte)((uint)num & (flag2 ? 1u : 0u)) != 0;
			});
			if (flag || Suppress.BlockedDevTools < blockedDevTools + array.Length)
			{
				throw new Exception("console opened (active " + flag + ", blocked " + (Suppress.BlockedDevTools - blockedDevTools) + ")");
			}
			return array.Length + " console(s): Activate() blocked, IsActive False (blocked " + Suppress.BlockedDevTools + ")";
		}).Soft();
		Command("ready", "mark:live-b", (string r) => r == "ok");
		WaitFor("lou-talking", 300f, () => DialogueBotBase.Cur() == "wanted5morning" && DialogueSystem.Counter.Current != null, DialogueBotBase.Live);
		Check("lou-timer-skipped-here", () => DialogueSystem.MorningSkipped >= 1, () => "morning timers skipped " + DialogueSystem.MorningSkipped);
		SameAsServerSoon("same-lou", 10f, "dlg.counter.at", "dlg.digest", "cust.current", "story.hash");
		Command("saw-lou", "mark:saw-lou", (string r) => r == "ok");
		WaitFor("lou-gone", 40f, () => DialogueBotBase.Cur() == "none" && DialogueSystem.Counter.Current == null, DialogueBotBase.Live);
		SameAsServerSoon("same-after-lou", 15f, "dlg.counter.at", "dlg.digest", "cust.current", "story.hash", "lock.counter");
		WaitFor("offer-done", 90f, () => DialogueBotNet.Offers >= 1 && DialogueBotNet.BtEnds >= 1 && DialogueSystem.Counter.Current == null, () => "offers " + DialogueBotNet.Offers + ", end actions " + DialogueBotNet.BtEnds + "; " + DialogueBotBase.Live());
		SameAsServerSoon("same-after-offer", 15f, "cash", "dlg.bt.value", "dlg.bt.ends", "dlg.counter.at", "dlg.digest");
		WaitFor("flushed-here", 60f, () => DialogueBotNet.BfEnds == 1 && !DialogueBotBase.BfScheduled(), DialogueBotBase.Live);
		SameAsServerSoon("same-after-flush", 10f, "cash", "dlg.bf.ends", "dlg.digest", "dlg.counter.at");
		WaitFor("customer-talking", 120f, () => DialogueBotBase.Cur() == "appraisalBoardIntro" && DialogueSystem.Counter.Current != null && Locks.HeldBy("counter") >= 0 && Locks.HeldBy("counter") != Net.LocalPlayer, DialogueBotBase.Live);
		Check("main-started-here", () => BotProbes.Read("dlg.main") == "appraisalBoardIntro/1", DialogueBotBase.Live);
		WaitFor("bubble-shown", 15f, () => (Object)(object)DialogUIManager.Instance.clientBubble != (Object)null && ((Component)DialogUIManager.Instance.clientBubble).gameObject.activeSelf, () => "client bubble on screen for " + DialogueRefs.Short(DialogueSystem.Counter.Current));
		Check("local-gate", () => !DialogueSync.RequestAdvance("counter") && !DialogueSync.RequestChoose("counter", 0), () => "requests on someone else's counter are not sent");
		SameAsServerSoon("same-line", 10f, "dlg.counter.at", "dlg.digest", "dlg.main", "cust.current", "cust.holder");
		WaitMark("a-mid", "mid", 90f);
		Do("leave", delegate
		{
			address = (string.IsNullOrEmpty(Args.Join) ? Net.ServerAddress : Args.Join);
			mismatchBefore = DialogueSync.MismatchCount;
			Net.Disconnect();
			return "left " + address + " during the visit";
		});
		WaitFor("menu", 60f, () => Net.ClientState == ClientState.Idle && BotProbes.Scene == "EmporiumMenu", () => "scene " + BotProbes.Scene);
		Delay("away", 2f);
		Do("rejoin", delegate
		{
			mainBlockedBefore = DialogueSystem.MainBlocked;
			Net.Connect(address, Args.Password ?? "", Args.Name);
			return "connecting to " + address;
		});
		WaitFor("live-again", 150f, () => Net.IsLive && BotProbes.InShop && DialogueSystem.Counter.Ready && !Snapshot.IsLoading && !DayLog.Replaying && DayLog.Last != null, () => "scene " + BotProbes.Scene + " state " + Net.ClientState);
		Check("log-replayed", () => DayLog.Last.Replayed == DayLog.Last.Expected && DayLog.Last.Replayed > 0 && DayLog.Last.ChecksBad == 0, () => DayLog.Last?.ToString() ?? "");
		SameAsServerSoon("same-after-join", 15f, "cash", "cust.current", "cust.holder", "dlg.counter.at", "dlg.digest", "dlg.main", "dlg.bt.value", "story.hash", "events.hash");
		Check("join-no-mismatch", () => DialogueSync.MismatchCount == mismatchBefore, () => "stepDone mismatches while joining " + (DialogueSync.MismatchCount - mismatchBefore) + "; " + DialogueBotBase.Live());
		WaitFor("old-arrival-timer-dropped", 6f, () => DialogueSystem.MainBlocked > mainBlockedBefore, () => "the replayed arrival's 0.25 s StartMainDialogue blocked " + (DialogueSystem.MainBlocked - mainBlockedBefore) + " time(s); " + DialogueBotBase.Live()).Soft();
		Command("rejoined", "mark:rejoined", (string r) => r == "ok");
		WaitFor("choice-up", 90f, () => DialogueSystem.Counter.IsChoiceNow && !DialogueSystem.Counter.LineUnfinished && DialogueBotBase.ChoiceBubbles() == DialogueSystem.Counter.Current.choices.Count, () => "choice bubbles " + DialogueBotBase.ChoiceBubbles() + "; " + DialogueBotBase.Live());
		SameAsServerSoon("same-choice-line", 10f, "dlg.counter.at", "dlg.digest", "cash");
		WaitMark("a-at-choice", "choice", 30f);
		Do("forge-choose", delegate
		{
			refusedBefore = DialogueSync.RefusedCount;
			Dialogue current = DialogueSystem.Counter.Current;
			Net.ToServer("dialogue.choose", new MsgDlgReq
			{
				ch = "counter",
				fp = DialogueRefs.Fp(current),
				index = 1,
				cfp = DialogueRefs.Fp(current.choices[1].Item2)
			});
			return "sent dialogue.choose around the client gate on " + DialogueRefs.Short(current);
		});
		WaitFor("choose-refused", 10f, () => DialogueSync.RefusedCount > refusedBefore, DialogueBotBase.Live);
		Check("still-choosing", () => DialogueSystem.Counter.IsChoiceNow, DialogueBotBase.Live);
		Command("forged", "mark:forged", (string r) => r == "ok");
		WaitFor("visit-over", 90f, () => DialogueBotBase.Cur() == "none" && DialogueLiveBase.FutureHasBoard2() && DialogueSystem.Counter.Current == null, DialogueBotBase.Live);
		SameAsServerSoon("same-after-visit", 15f, "cash", "dlg.future", "cust.current", "dlg.counter.at", "dlg.digest", "lock.counter", "story.hash", "events.hash", "requests.hash");
		WaitFor("someone-calls", 90f, () => PhoneChannel.Talking && Locks.HeldBy("phone") >= 0 && Locks.HeldBy("phone") != Net.LocalPlayer, DialogueBotBase.Ph);
		SameAsServerSoon("same-call", 10f, "dlg.phone.at", "dlg.phone.caller", "dlg.phone.talking", "dlg.digest", "lock.phone");
		Do("close-own-panel", delegate
		{
			PhoneUIManager.Instance.CloseUI();
			return "closed this copy's phone panel: " + DialogueBotBase.Ph();
		});
		Check("still-mirroring", () => PhoneChannel.Talking && DialogueSystem.Phone.Current != null, DialogueBotBase.Ph);
		Command("saw-call", "mark:saw-call", (string r) => r == "ok");
		WaitFor("call-over", 60f, () => !PhoneChannel.Talking && Locks.HeldBy("phone") < 0 && DialogueSystem.Phone.Current == null, DialogueBotBase.Ph);
		SameAsServerSoon("same-after-call", 15f, "dlg.phone.at", "dlg.phone.talking", "dlg.phone.caller", "dlg.digest", "lock.phone", "cash");
		Check("no-mismatch", () => DialogueSync.MismatchCount == mismatchBefore, () => "stepDone mismatches " + (DialogueSync.MismatchCount - mismatchBefore));
		SameAsServerSoon("same-digest-parts", 15f, "dlg.digest", "story.hash", "events.hash", "requests.hash", "cash");
		WaitFor("seller-talking", 90f, () => DialogueBotBase.Cur() == "scavGeneral" && DialogueSystem.Counter.Current != null, DialogueBotBase.Live);
		SameAsServerSoon("same-seller", 10f, "dlg.counter.at", "dlg.digest", "dlg.main", "cust.current");
		Command("saw-seller", "mark:saw-seller", (string r) => r == "ok");
		WaitFor("closed", 60f, () => (int)Game.Store.storeState == 0 && DialogueBotBase.Cur() == "none", DialogueBotBase.Live);
		Delay("close-timers", 4f);
		SameAsServerSoon("same-after-close", 15f, "cash", "cust.current", "dlg.counter.at", "dlg.digest", "lock.counter", "story.hash", "events.hash");
		Check("close-no-mismatch", () => DialogueSync.MismatchCount == mismatchBefore, () => "stepDone mismatches " + (DialogueSync.MismatchCount - mismatchBefore));
	}
}
