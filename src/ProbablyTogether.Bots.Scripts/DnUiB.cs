using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DnUiB : DayNight3Base
{
	public override string Name => "dnui-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		JoinSteps();
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return "day " + day0;
		});
		Do("chat", () => (!Chat.Send("hello from b")) ? "not sent" : "said hello (the rate table has B)");
		WaitFor("owner-line", 60f, () => Chat.Lines.Any((Chat.Line l) => l.Owner && l.Text.StartsWith("Owner line", StringComparison.Ordinal)), () => "lines " + Chat.Lines.Count);
		WaitFor("opened-by-a", 300f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		WaitFor("shutter-settled", 15f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0, () => "flag " + DayNightBotBase.Ps.lockShutterButtonFlag);
		Do("call", delegate
		{
			Customers.RequestCall();
			return "customers.call sent (queue " + DayNightBotBase.Ps.storeClientManager.clientStack.Count + ")";
		});
		WaitFor("at-counter", 30f, () => Locks.HeldBy("counter") == Net.LocalPlayer && DayNightBotBase.Ps.currentClientInstance != null, delegate
		{
			string text = Locks.HeldBy("counter").ToString();
			StoreClientInstance currentClientInstance = DayNightBotBase.Ps.currentClientInstance;
			return "counter " + text + ", customer " + (((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.identifier) ?? "none");
		});
		Agree("same-customer", 20f, "dn.client", "dn.counter");
		WaitServer("a-close-refused", 60f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "dn.lastref") == "close", "dn.lastref", "dn.state");
		Check("still-open", () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		Do("step-away", delegate
		{
			Customers.RequestReleaseCounter();
			return "customers.release sent";
		});
		WaitFor("counter-released", 15f, () => Locks.HeldBy("counter") != Net.LocalPlayer, () => "counter " + Locks.HeldBy("counter"));
		WaitFor("closed", 60f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		WaitFor("customer-gone", 20f, () => DayNightBotBase.Ps.currentClientInstance == null);
		Delay("past-close-timers", 2f);
		Agree("same-close", 20f, "dn.state", "dn.visit", "dn.queue", "dn.client", "dn.front", "dn.digest", "dn.counter");
		MarkEnding("before-ending");
		WaitFor("ended", 120f, () => DayNightRun.RunOver && DayNightRun.Ending == "rent" && DayNight3Base.PanelUp, () => "run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + "), panel " + DayNight3Base.PanelUp);
		WaitFor("plate", 15f, () => NewRun.Visible && NewRun.Text.StartsWith("Start a new run:", StringComparison.Ordinal), () => "plate " + NewRun.Visible + " '" + NewRun.Text + "'");
		Check("ending-shown-once", () => DayNightRun.ShownCount == shown0 + 1 && DayNight3Base.EndedLineShown, () => "shown " + (DayNightRun.ShownCount - shown0) + ", ended line " + DayNight3Base.EndedLineShown);
		WaitFor("a-voted", 120f, () => DayNightRun.NewRunVotes.Any((int p) => p != Net.LocalPlayer) && NewRun.Text.StartsWith("Start a new run: 1/2 voted", StringComparison.Ordinal), () => "votes " + string.Join(",", DayNightRun.NewRunVotes) + ", plate '" + NewRun.Text + "'");
		Check("plate-shows-a", () => NewRun.Text.Contains(" voted") && NewRun.Text.Contains("not yet") && NewRun.Text.EndsWith("| Start a new run", StringComparison.Ordinal), () => "plate '" + NewRun.Text + "'");
		Do("press", delegate
		{
			NewRun.Press();
			return "pressed the plate's button";
		});
		NewRunUp("new-run", 120f);
		Check("plate-gone", () => !NewRun.Visible && !DayNight3Base.PanelUp, () => "plate " + NewRun.Visible + ", panel " + DayNight3Base.PanelUp);
		Agree("same-new-run", 40f, "day", "cash", "dn.state", "dn.digest", "dn.runover", "dn.votes", "dn.outing");
		MarkEnding("before-force");
		Do("cue-voted", () => (!Chat.Send("b agreed on the voted run")) ? "chat not sent" : "said 'b agreed on the voted run'");
		WaitFor("forced-run", 240f, () => DayNightBotBase.Live && DayNightProbes.Loads > loads0 && DayNightBotBase.Day == 1 && DayNightRun.LastNewRunWhy == "panel: newrun force" && !DayNightRun.RunOver, () => "loads " + DayNightProbes.Loads + " (" + loads0 + "), live " + DayNightBotBase.Live + ", day " + DayNightBotBase.Day + ", why '" + DayNightRun.LastNewRunWhy + "'");
		Agree("same-forced-run", 40f, "day", "cash", "dn.state", "dn.digest", "dn.runover", "dn.outing");
		WaitFor("closed-n", 150f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		WaitFor("a-ready-n", 60f, () => DayNight.ReadyPlayers.Any((int p) => p != Net.LocalPlayer), () => "ready " + string.Join(",", DayNight.ReadyPlayers));
		MarkEnding("before-night-ending");
		Ready("ready-n");
		WaitFor("night-ending", 150f, () => DayNightBotBase.Live && DayNightRun.RunOver && DayNightRun.Ending == "aug" && DayNight3Base.PanelUp && DayNightBotBase.Day == 2, () => "run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + "), panel " + DayNight3Base.PanelUp + ", day " + DayNightBotBase.Day + ", live " + DayNightBotBase.Live);
		Check("night-ending-shown", () => DayNightRun.ShownCount == shown0 + 1 && DayNight3Base.EndedLineShown, () => "shown " + (DayNightRun.ShownCount - shown0) + ", ended line " + DayNight3Base.EndedLineShown);
		Agree("same-night-ending", 40f, "day", "cash", "dn.state", "dn.runover");
		Check("no-mismatch", () => DayNight.Mismatches == 0, () => "day-night mismatches " + DayNight.Mismatches + ", applied " + DayNight.Applied).Soft();
		WaitFor("told-to-leave", 300f, () => Chat.Lines.Any((Chat.Line l) => l.Owner && l.Text == "Owner line: bot2 may leave now"), () => "lines " + Chat.Lines.Count);
		Do("leave", delegate
		{
			Net.Disconnect();
			return "left the server";
		});
	}
}
