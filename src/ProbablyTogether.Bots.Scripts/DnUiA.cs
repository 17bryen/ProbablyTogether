using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DnUiA : DayNight3Base
{
	private int denied0;

	private int refusedMsgs0;

	private int deniedShown0;

	public override string Name => "dnui-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		JoinSteps();
		WaitServer("both-live", 420f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "dn.live") >= 2, "dn.live", "players");
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return "day " + day0 + ", " + DayNightBotBase.State;
		});
		Check("clean-here", () => Chat.Clean("B\u200bob\u202e <i>x</i> \ud83d\ude00 \ud800end\ufeff") == "Bob ‹i>x‹/i> \ud83d\ude00 end" && Chat.Clean(new string('a', 199) + "\ud83d\ude00").Length == 199 && Chat.Clean(new string('a', 198) + "\ud83d\ude00").Length == 200, () => "Clean(...) = '" + Chat.Clean("B\u200bob\u202e <i>x</i> \ud83d\ude00 \ud800end\ufeff") + "'; 199 a + emoji -> " + Chat.Clean(new string('a', 199) + "\ud83d\ude00").Length + " chars, 198 a + emoji -> " + Chat.Clean(new string('a', 198) + "\ud83d\ude00").Length + " chars");
		Do("chat-send", () => (!Chat.Send("B\u200bob\u202e <i>x</i> \ud83d\ude00 end\ufeff")) ? "not sent" : "sent a line with U+200B, U+202E, <i>, an emoji and U+FEFF");
		WaitFor("chat-clean", 20f, () => Chat.Lines.Any((Chat.Line l) => l.Player == Net.LocalPlayer && l.Text.Contains("end")), () => "lines " + Chat.Lines.Count);
		Check("chat-cleaned", () => Chat.Lines.Last((Chat.Line l) => l.Player == Net.LocalPlayer).Text == "Bob ‹i>x‹/i> \ud83d\ude00 end", () => "came back '" + Chat.Lines.Last((Chat.Line l) => l.Player == Net.LocalPlayer).Text + "' (" + Chat.Lines.Last((Chat.Line l) => l.Player == Net.LocalPlayer).Text.Length + " chars)");
		WaitFor("owner-line", 60f, () => Chat.Lines.Any((Chat.Line l) => l.Owner), () => "lines " + string.Join(" | ", Chat.Lines.Select((Chat.Line l) => (l.System ? "[sys] " : "") + l.Text)));
		Check("owner-drawn", delegate
		{
			Chat.Line line = Chat.Lines.Last((Chat.Line x) => x.Owner);
			return line.System && line.Player == 0 && line.Text == "Owner line ‹b>lab‹/b>" && (Chat.OwnerLine(line) ?? "").Contains("<b>Server owner:</b>");
		}, delegate
		{
			Chat.Line line = Chat.Lines.Last((Chat.Line x) => x.Owner);
			return "owner line text '" + line.Text + "', drawn '" + Chat.OwnerLine(line) + "', label '" + (((Object)(object)line.Label != (Object)null) ? ((TMP_Text)line.Label).text : "(headless: no label)") + "'";
		});
		Do("notice-escaped", delegate
		{
			Notices.Show("<b>lab</b> notice", "lab");
			return "Notices.Show('<b>lab</b> notice')";
		});
		Check("notice-text", () => Notices.Last == "‹b>lab‹/b> notice", () => "notice '" + Notices.Last + "'");
		Do("open", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "dayNight.open sent " + DayNightBotBase.SentOf("dayNight.open");
		});
		WaitFor("opened", 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		WaitFor("shutter-settled", 15f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0, () => "flag " + DayNightBotBase.Ps.lockShutterButtonFlag);
		WaitFor("b-at-counter", 90f, () => Locks.HeldBy("counter") > 0 && Locks.HeldBy("counter") != Net.LocalPlayer && DayNightBotBase.Ps.currentClientInstance != null, delegate
		{
			string text = Locks.HeldBy("counter").ToString();
			StoreClientInstance currentClientInstance = DayNightBotBase.Ps.currentClientInstance;
			return "counter " + text + ", customer " + (((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.identifier) ?? "none");
		});
		Do("close-mid-deal", delegate
		{
			DayNight.LastRefusedWhat = "";
			StoreShutterButton.Instance.EndAndClose();
			return "dayNight.close sent with " + DayNightBotBase.Ps.currentClientInstance.GetClientBlueprint().identifier + " at the counter, held by player " + Locks.HeldBy("counter");
		});
		WaitFor("close-refused", 15f, () => DayNight.LastRefusedWhat == "close", () => "refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Check("refused-mid-deal", () => DayNight.LastRefusedWhy.Contains("middle of a deal") && DayNightBotBase.State == "OPEN", () => "'" + DayNight.LastRefusedWhy + "', state " + DayNightBotBase.State);
		WaitFor("counter-free", 60f, () => Locks.HeldBy("counter") < 0 || Locks.HeldBy("counter") == Net.LocalPlayer, () => "counter " + Locks.HeldBy("counter"));
		Do("queue", delegate
		{
			if (DayNightBotBase.Ps.CanEndDay())
			{
				return "every waiting customer can be sent away";
			}
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "clearqueue"
			});
			return "a story customer is waiting: lab op clearqueue";
		});
		WaitFor("closable", 15f, () => DayNightBotBase.Ps.CanEndDay(), () => "queue " + DayNightBotBase.Ps.storeClientManager.clientStack.Count);
		Do("close", delegate
		{
			DayNight.LastRefusedWhat = "";
			StoreShutterButton.Instance.EndAndClose();
			StoreClientInstance currentClientInstance = DayNightBotBase.Ps.currentClientInstance;
			return "dayNight.close sent, customer " + (((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.identifier) ?? "none") + ", counter " + Locks.HeldBy("counter");
		});
		WaitFor("closed", 30f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State + ", last refusal " + DayNight.LastRefusedWhy);
		WaitFor("customer-gone", 20f, () => DayNightBotBase.Ps.currentClientInstance == null, delegate
		{
			StoreClientInstance currentClientInstance = DayNightBotBase.Ps.currentClientInstance;
			return "customer " + (((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.identifier) ?? "none");
		});
		Delay("past-close-timers", 2f);
		Agree("same-close", 20f, "dn.state", "dn.visit", "dn.queue", "dn.client", "dn.front", "dn.digest", "dn.counter");
		MarkEnding("before-ending");
		Do("rent-ending", delegate
		{
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "gameover:rent"
			});
			return "lab op gameover:rent (a losing ending)";
		});
		WaitFor("ended", 30f, () => DayNightRun.RunOver && DayNightRun.Ending == "rent" && DayNight3Base.PanelUp, () => "run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + "), panel " + DayNight3Base.PanelUp);
		WaitFor("plate", 15f, () => NewRun.Visible && NewRun.Text.StartsWith("Start a new run: 0/2 voted", StringComparison.Ordinal) && NewRun.Text.EndsWith("| Start a new run", StringComparison.Ordinal), () => "plate " + NewRun.Visible + " '" + NewRun.Text + "'");
		Check("ending-shown-once", () => DayNightRun.ShownCount == shown0 + 1 && DayNight3Base.EndedLineShown, () => "shown " + (DayNightRun.ShownCount - shown0) + ", ended line " + DayNight3Base.EndedLineShown);
		Do("gated", delegate
		{
			denied0 = DayNight.DeniedMessages;
			refusedMsgs0 = DayNight.RefusedMessages;
			deniedShown0 = Notices.DeniedShown;
			DayNight.LastRefusedWhat = "";
			Net.ToServer("test.gated", new MsgDnEmpty());
			return "sent kind test.gated (denied " + denied0 + ", dayNight.refused " + refusedMsgs0 + ", denied notices " + deniedShown0 + " so far)";
		});
		WaitFor("gated-refused", 15f, () => DayNight.LastRefusedWhat == "test.gated", () => "refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Delay("any-second-refusal", 3f);
		Check("told-once", () => DayNight.DeniedMessages == denied0 + 1 && DayNight.RefusedMessages == refusedMsgs0 && Notices.DeniedShown == deniedShown0 + 1 && Notices.Last == "The run has ended. Everyone can vote to start a new run." && Net.LastDeniedKind == "test.gated", () => "sys.denied " + (DayNight.DeniedMessages - denied0) + ", dayNight.refused " + (DayNight.RefusedMessages - refusedMsgs0) + ", notices " + (Notices.DeniedShown - deniedShown0) + " ('" + Notices.Last + "' from " + Notices.LastSource + ")");
		Do("press", delegate
		{
			NewRun.Press();
			return "pressed the plate's button";
		});
		WaitFor("vote-counted", 15f, () => DayNightRun.NewRunVotes.Contains(Net.LocalPlayer) && NewRun.Text.StartsWith("Start a new run: 1/2 voted", StringComparison.Ordinal) && NewRun.Text.EndsWith("| Take my vote back", StringComparison.Ordinal), () => "votes " + string.Join(",", DayNightRun.NewRunVotes) + ", plate '" + NewRun.Text + "'");
		NewRunUp("new-run", 120f);
		Check("by-everyone", () => DayNightRun.LastNewRunWhy.StartsWith("everyone voted (2)", StringComparison.Ordinal), () => "why: " + DayNightRun.LastNewRunWhy);
		Check("plate-gone", () => !NewRun.Visible && !DayNight3Base.PanelUp, () => "plate " + NewRun.Visible + ", panel " + DayNight3Base.PanelUp);
		Agree("same-new-run", 40f, "day", "cash", "dn.state", "dn.digest", "dn.runover", "dn.votes", "dn.outing");
		MarkEnding("before-force");
		Do("cue-voted", () => (!Chat.Send("voted run agreed")) ? "chat not sent" : "said 'voted run agreed'");
		WaitFor("forced-run", 240f, () => DayNightBotBase.Live && DayNightProbes.Loads > loads0 && DayNightBotBase.Day == 1 && DayNightRun.LastNewRunWhy == "panel: newrun force" && !DayNightRun.RunOver, () => "loads " + DayNightProbes.Loads + " (" + loads0 + "), live " + DayNightBotBase.Live + ", day " + DayNightBotBase.Day + ", why '" + DayNightRun.LastNewRunWhy + "'");
		Agree("same-forced-run", 40f, "day", "cash", "dn.state", "dn.digest", "dn.runover", "dn.outing");
		Do("cue-force", () => (!Chat.Send("force run agreed")) ? "chat not sent" : "said 'force run agreed'");
		Do("open-n", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "dayNight.open sent";
		});
		WaitFor("opened-n", 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		WaitFor("shutter-settled-n", 15f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0, () => "flag " + DayNightBotBase.Ps.lockShutterButtonFlag);
		Do("queue-n", delegate
		{
			if (DayNightBotBase.Ps.CanEndDay())
			{
				return "every waiting customer can be sent away";
			}
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "clearqueue"
			});
			return "lab op clearqueue";
		});
		WaitFor("closable-n", 15f, () => DayNightBotBase.Ps.CanEndDay(), () => "queue " + DayNightBotBase.Ps.storeClientManager.clientStack.Count);
		Do("close-n", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "dayNight.close sent";
		});
		WaitFor("closed-n", 30f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State + ", last refusal " + DayNight.LastRefusedWhy);
		Do("arm-night-ending", delegate
		{
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "nightend:aug"
			});
			return "lab op nightend:aug";
		});
		Delay("armed", 1f);
		MarkEnding("before-night-ending");
		Ready("ready-n");
		WaitFor("night-ending", 150f, () => DayNightBotBase.Live && DayNightRun.RunOver && DayNightRun.Ending == "aug" && DayNight3Base.PanelUp && DayNightBotBase.Day == 2, () => "run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + "), panel " + DayNight3Base.PanelUp + ", day " + DayNightBotBase.Day + ", live " + DayNightBotBase.Live);
		Check("night-ending-plate", () => DayNightRun.ShownCount == shown0 + 1 && DayNight3Base.EndedLineShown, () => "shown " + (DayNightRun.ShownCount - shown0) + ", ended line " + DayNight3Base.EndedLineShown + ", plate '" + NewRun.Text + "'");
		WaitFor("night-plate", 15f, () => NewRun.Visible && NewRun.Text.StartsWith("Start a new run:", StringComparison.Ordinal), () => "plate " + NewRun.Visible + " '" + NewRun.Text + "'");
		Agree("same-night-ending", 40f, "day", "cash", "dn.state", "dn.runover");
		Do("cue-night", () => (!Chat.Send("night ending agreed")) ? "chat not sent" : "said 'night ending agreed'");
		Check("no-mismatch", () => DayNight.Mismatches == 0, () => "day-night mismatches " + DayNight.Mismatches + ", applied " + DayNight.Applied).Soft();
		Delay("let-b-leave", 20f);
	}
}
