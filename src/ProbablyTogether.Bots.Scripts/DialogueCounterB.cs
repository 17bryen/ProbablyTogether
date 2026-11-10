using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DialogueCounterB : DialogueBotBase
{
	private int refusedBefore;

	private int eventsBefore;

	private int holder;

	private DialogUIManager oldManager;

	private int mismatchesBefore;

	private string calBefore;

	public override string Name => "dialogue-counter-b";

	public override float TimeoutSeconds => 420f;

	protected override void Build()
	{
		Join();
		Command("ready", "mark:counter-b", (string r) => r == "ok");
		WaitFor("someone-at-counter", 180f, delegate
		{
			int num = Locks.HeldBy("counter");
			return num >= 0 && num != Net.LocalPlayer && DialogueBotBase.Cur() != "none" && DialogueSystem.Counter.Current != null;
		}, DialogueBotBase.Co);
		Do("note-refused", delegate
		{
			holder = Locks.HeldBy("counter");
			refusedBefore = DialogueSync.RefusedCount;
			eventsBefore = DialogueBotBase.RefusedEventCount;
			return "holder " + Net.PlayerName(holder) + ", customer " + DialogueBotBase.Cur() + ", refused " + refusedBefore;
		});
		Check("local-gate", () => !DialogueSync.RequestChoose("counter", 0) && !DialogueSync.RequestAdvance("counter"), () => "requests on someone else's counter are not sent");
		Check("local-gate-notice", () => DialogueSync.RefusedCount == refusedBefore + 2 && DialogueBotBase.RefusedEventCount == eventsBefore + 2 && DialogueSync.LastRefused == "advance: " + Net.PlayerName(holder) + " is at the counter." && DialogueBotBase.LastRefusedEvent == "counter|advance|" + Net.PlayerName(holder) + " is at the counter.", () => "refused " + refusedBefore + " -> " + DialogueSync.RefusedCount + ", events " + eventsBefore + " -> " + DialogueBotBase.RefusedEventCount + ", last '" + DialogueSync.LastRefused + "' / '" + DialogueBotBase.LastRefusedEvent + "'");
		Do("forge-advance", delegate
		{
			refusedBefore = DialogueSync.RefusedCount;
			Net.ToServer("dialogue.advance", new MsgDlgReq
			{
				ch = "counter",
				fp = DialogueSync.CurrentFp("counter")
			});
			return "sent dialogue.advance around the client gate on " + DialogueRefs.Short(DialogueSystem.Counter.Current);
		});
		WaitFor("advance-refused", 10f, () => DialogueSync.RefusedCount > refusedBefore, DialogueBotBase.Co);
		Check("refusal-names-holder", () => DialogueSync.LastRefused == "advance: " + Net.PlayerName(holder) + " is at the counter." && DialogueBotBase.LastRefusedEvent == "counter|advance|" + Net.PlayerName(holder) + " is at the counter.", () => "last '" + DialogueSync.LastRefused + "', event '" + DialogueBotBase.LastRefusedEvent + "'");
		WaitMark("a-at-choice", "bt-choice", 150f);
		Do("forge-choose", delegate
		{
			refusedBefore = DialogueSync.RefusedCount;
			Dialogue current = DialogueSystem.Counter.Current;
			Net.ToServer("dialogue.choose", new MsgDlgReq
			{
				ch = "counter",
				fp = DialogueRefs.Fp(current),
				index = 0,
				cfp = ((current != null && current.choices.Count > 0) ? DialogueRefs.Fp(current.choices[0].Item2) : "-")
			});
			return "sent dialogue.choose around the client gate on " + DialogueRefs.Short(current);
		});
		WaitFor("choose-refused", 10f, () => DialogueSync.RefusedCount > refusedBefore, DialogueBotBase.Co);
		Check("choose-names-holder", () => DialogueSync.LastRefused == "choose: " + Net.PlayerName(holder) + " is at the counter.", () => "last '" + DialogueSync.LastRefused + "'");
		Command("forged", "mark:bt-forged", (string r) => r == "ok");
		WaitFor("bt-over", 60f, () => DialogueBotNet.BtEnds == 1 && DialogueSystem.Counter.Current == null, DialogueBotBase.Co);
		SameAsServerSoon("same-after-bt", 15f, "cash", "dlg.bt.ends", "dlg.counter.at", "lock.counter");
		WaitFor("report-flushed-here", 90f, () => DialogueBotNet.BfEnds == 1 && !DialogueBotBase.BfScheduled(), DialogueBotBase.Live);
		SameAsServerSoon("same-after-report", 15f, "cash", "dlg.bf.ends", "dlg.digest");
		WaitMark("visit-over", "visit-over", 150f);
		WaitFor("counter-free", 15f, () => DialogueBotBase.Cur() == "none" && Locks.HeldBy("counter") < 0, DialogueBotBase.Live);
		WaitMark("a-done", "counter-a-done", 90f);
		Check("flush-once", () => DialogueBotNet.BfEnds == 1 && DialogueBotNet.BtEnds == 2, () => "bf ends " + DialogueBotNet.BfEnds + ", bt ends " + DialogueBotNet.BtEnds + " (expected 1, 2)");
		SameAsServerSoon("same-after", 15f, "cash", "dlg.bt.ends", "dlg.bf.ends", "dlg.counter.at", "dlg.digest", "lock.counter");
		Check("no-mismatch", () => DialogueSync.MismatchCount == 0, () => "stepDone mismatches " + DialogueSync.MismatchCount);
		Do("newspaper", delegate
		{
			StoreEventManager storeEventManager = StoreStation.Instance.storeEventManager;
			int count = storeEventManager.GetActiveEvents().FindAll((StoreEvent e) => !e.isHiddenFromNewsPaper).Count;
			string text = DialogueEvents.EventsHash();
			NewsUIManager.Instance.PopulateUI();
			string text2 = DialogueEvents.EventsHash();
			bool flag = storeEventManager.IsEventActive("allQuiet");
			if (text2 != text || flag)
			{
				throw new Exception("events changed: " + text + " -> " + text2 + (flag ? " (allQuiet active)" : ""));
			}
			return "events unchanged " + text2 + "; visible events " + count + ((count == 0) ? " (the allQuiet path ran and was skipped)" : " (allQuiet path not reached)");
		});
		Do("calendar-open", delegate
		{
			calBefore = DialogueEvents.EventsHash();
			AdvCalendarUIManager.Instance.HandleInnateEvent();
			return "events before " + calBefore;
		});
		WaitFor("calendar-applied", 10f, () => DialogueEvents.EventsHash() != calBefore, () => "events " + DialogueEvents.EventsHash()).Soft();
		SameAsServerSoon("same-events", 10f, "events.hash", "story.hash", "requests.hash");
		Do("resync", delegate
		{
			oldManager = DialogUIManager.Instance;
			mismatchesBefore = DialogueSync.MismatchCount;
			Snapshot.RequestResync();
			return "asked; " + DialogueBotBase.Co();
		});
		WaitFor("reloaded", 60f, () => (Object)(object)DialogUIManager.Instance != (Object)null && DialogUIManager.Instance != oldManager && Net.IsLive && !Snapshot.IsLoading && !DayLog.Replaying, DialogueBotBase.Co);
		SameAsServerSoon("same-after-replay", 15f, "cash", "dlg.counter.at", "dlg.phone.at", "dlg.counter.seq", "dlg.phone.seq", "dlg.digest", "lock.counter", "events.hash", "story.hash");
		Check("replay-no-mismatch", () => DialogueSync.MismatchCount == mismatchesBefore, () => "stepDone mismatches during the replay " + (DialogueSync.MismatchCount - mismatchesBefore));
	}
}
