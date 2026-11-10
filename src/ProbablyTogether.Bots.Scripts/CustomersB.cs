using System;
using System.Collections.Generic;
using System.Globalization;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CustomersB : CustomersBot
{
	private int a;

	private int v;

	private int refusals0;

	private int cashOpen;

	public override string Name => "customers-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 1000f;

	protected override void Build()
	{
		Joined();
		WaitMark("a-open", 240f, "a:open");
		PollServer("sellers-queued", 60f, (Dictionary<string, string> s) => s["cust.state"] == "OPEN" && s["cust.head"] == "scavGeneral" && CustomersBot.Int(s["cust.queue"]) >= 5);
		Converge("same-queue", 20f, "cust.state", "cust.queue", "cust.head", "cust.current", "cust.digest", "cust.action");
		Check("shutter-digest-ok", () => DayNight.Mismatches == 0, () => "dayNight mismatches " + DayNight.Mismatches + " (customers part included)");
		Do("note-cash", delegate
		{
			cashOpen = CustomersBot.Int(CustomersBot.Mine("cash"));
			return "cash " + cashOpen;
		});
		Mark("b:open");
		for (int num = 1; num <= 3; num++)
		{
			int kk = num;
			string text = num.ToString(CultureInfo.InvariantCulture);
			Func<bool> notRun = () => !Has("a:talked-" + kk);
			string why = "A made no visit " + text;
			WaitMark("a-talked-" + text, 180f, "a:talked-" + text, "a:e2e");
			CustomersBot.Unless(notRun, why, Converge("same-talk-" + text, 15f, CustomersBot.TalkProbes));
			CustomersBot.Unless(notRun, why, Step("a-holds-" + text, 0f, delegate(BotStep st)
			{
				a = CustomersBot.Int(Theirs("cust.holder"));
				st.Detail = "holder here " + CustomersBot.Mine("cust.holder") + ", server " + a + ", me " + Net.LocalPlayer;
				return (a > 0 && a != Net.LocalPlayer && Locks.HeldBy("counter") == a) ? StepStatus.Pass : StepStatus.Fail;
			}));
			if (num == 1)
			{
				CustomersBot.Unless(notRun, why, Do("try-call", delegate
				{
					refusals0 = CustomersBot.Int(Theirs("cust.refusals"));
					v = CustomersBot.Int(Theirs("cust.visit"));
					Customers.RequestCall();
					Customers.RequestDismiss(callNext: false);
					return "sent call + dismiss (server refusals " + refusals0 + ")";
				}));
				CustomersBot.Unless(notRun, why, PollServer("refused", 15f, (Dictionary<string, string> s) => CustomersBot.Int(s["cust.refusals"]) >= refusals0 + 2));
				CustomersBot.Unless(notRun, why, Check("nothing-changed", () => CustomersBot.Int(Theirs("cust.visit")) == v && Theirs("cust.current") == "scavGeneral" && CustomersBot.Int(Theirs("cust.holder")) == a, () => "server " + CustomersBot.Show(Srv)));
				int ref0 = 0;
				string tool0 = "";
				CustomersBot.Unless(notRun, why, Do("use-on-counter", delegate
				{
					ref0 = ItemsUse.RefusedCount;
					tool0 = CustomersBot.ToolStateText();
					GameItem val = CustomersBot.CounterTool();
					if (val == null)
					{
						return "no scanner of ours on the counter";
					}
					val.ToggleSlotItem();
					return "toggled our gene scanner on the counter (" + tool0 + ")";
				}));
				CustomersBot.Unless(notRun, why, WaitFor("use-refused", 15f, () => ItemsUse.RefusedCount > ref0 && (ItemsUse.LastRefusal ?? "").Contains("is at the counter"), () => "refused " + ref0 + " -> " + ItemsUse.RefusedCount + ": " + ItemsUse.LastRefusal + "; scanner " + tool0 + " -> " + CustomersBot.ToolStateText()));
				CustomersBot.Unless(notRun, why, Converge("use-unchanged", 10f, "cust.tool", "cust.action", "cust.digest"));
			}
			CustomersBot.Unless(notRun, why, Mark("b:talked-" + text));
			CustomersBot.Unless(notRun, why, WaitMark("a-dealt-or-gone-" + text, 90f, "a:dealt-" + text, "a:gone-" + text));
			CustomersBot.Unless(() => notRun() || !Has("a:dealt-" + kk), "no deal on visit " + text, Converge("same-deal-" + text, 15f, "cash", "cust.stock", "cust.dm", "cust.action", "cust.digest"));
			CustomersBot.Unless(() => notRun() || !Has("a:dealt-" + kk), "no deal on visit " + text, Check("cash-paid-" + text, () => CustomersBot.Int(CustomersBot.Mine("cash")) < cashOpen, () => "cash at open " + cashOpen + ", now " + CustomersBot.Mine("cash")));
			CustomersBot.Unless(() => notRun() || !Has("a:dealt-" + kk), "no deal on visit " + text, Mark("b:dealt-" + text));
			CustomersBot.Unless(notRun, why, WaitMark("a-gone-" + text, 90f, "a:gone-" + text));
			CustomersBot.Unless(notRun, why, Converge("same-gone-" + text, 15f, CustomersBot.AfterProbes));
			CustomersBot.Unless(notRun, why, Mark("b:gone-" + text));
		}
		WaitMark("a-e2e", 120f, "a:e2e");
		Converge("same-after-visits", 15f, CustomersBot.AfterProbes);
		Mark("b:e2e");
		WaitMark("a-idle-out", 240f, "a:idle-out");
		PollServer("counter-free", 30f, (Dictionary<string, string> s) => s["cust.holder"] == "-1" && s["cust.current"] != "none");
		Do("take", delegate
		{
			v = CustomersBot.Int(Theirs("cust.visit"));
			Customers.RequestTakeCounter();
			return "visit " + v + ", customer " + Theirs("cust.current");
		});
		WaitFor("took", 10f, () => Locks.HeldBy("counter") == Net.LocalPlayer, CustomersBot.ShowMine);
		Do("dismiss", delegate
		{
			Customers.RequestDismiss(callNext: false);
			return "visit " + Customers.Visit;
		});
		WaitFor("gone-here", 20f, () => CustomersBot.Mine("cust.current") == "none", CustomersBot.ShowMine);
		Converge("same-after-idle", 15f, CustomersBot.AfterProbes);
		Mark("b:idle");
		WaitMark("a-leaving", 180f, "a:leaving");
		PollServer("a-left", 60f, (Dictionary<string, string> s) => s["cust.current"] != "none" && s["cust.holder"] == "-1" && s["players"] == "1" && s["cust.arrived"] == "1");
		Do("take-2", delegate
		{
			Customers.RequestTakeCounter();
			return "visit " + Theirs("cust.visit") + ", customer " + Theirs("cust.current");
		});
		WaitFor("took-2", 10f, () => Locks.HeldBy("counter") == Net.LocalPlayer, CustomersBot.ShowMine);
		Do("dismiss-2", delegate
		{
			Customers.RequestDismiss(callNext: false);
			return "visit " + Customers.Visit;
		});
		WaitFor("gone-2", 20f, () => CustomersBot.Mine("cust.current") == "none", CustomersBot.ShowMine);
		Converge("same-end", 15f, CustomersBot.AfterProbes);
		Do("resync", delegate
		{
			Snapshot.RequestResync();
			return "asked for the morning snapshot + today's log (applied so far " + Customers.Applied + ")";
		});
		WaitFor("reloading", 30f, () => Net.ClientState != ClientState.Ready, () => "state " + Net.ClientState);
		WaitFor("reloaded", 150f, () => Net.IsLive && BotProbes.InShop, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
		Converge("same-after-replay", 30f, "cust.state", "cust.current", "cust.queue", "cust.head", "cust.visit", "cust.seq", "cust.holder", "cust.last", "cust.flag", "cust.stock", "cash", "cust.action", "cust.digest");
		Mark("b:lou-ready");
		WaitFor("lou-here", 30f, () => CustomersBot.Mine("cust.current") == "wanted5morning", CustomersBot.ShowMine);
		Converge("same-lou", 10f, "cust.current", "cust.visit", "cust.seq", "cust.dismissable", "cust.digest");
		WaitFor("counter-passed", 10f, () => Locks.HeldBy("counter") == Net.LocalPlayer, () => "holder " + CustomersBot.Mine("cust.holder") + ", me " + Net.LocalPlayer);
		Check("lou-not-dismissable", () => CustomersBot.Mine("cust.dismissable") == "0", CustomersBot.ShowMine);
		WaitFor("lou-talks", 20f, () => CustomersBot.Mine("cust.dlgon") == "1", () => "morning dialogue: steps " + CustomersBot.Mine("cust.dlgseq") + ", line " + CustomersBot.Mine("cust.dlgfp"));
		Converge("same-lou-dialogue", 10f, "cust.current", "cust.dlgseq", "cust.dlgfp", "cust.dlgon", "cust.holder");
		Do("try-dismiss-lou", delegate
		{
			refusals0 = CustomersBot.Int(Theirs("cust.refusals"));
			Customers.RequestDismiss(callNext: false);
			return "visit " + Customers.Visit + " (server refusals " + refusals0 + ")";
		});
		PollServer("lou-refused", 15f, (Dictionary<string, string> s) => CustomersBot.Int(s["cust.refusals"]) > refusals0 && s["cust.current"] == "wanted5morning");
		Mark("b:lou-refused");
		WaitFor("lou-left", 20f, () => CustomersBot.Mine("cust.current") == "none", CustomersBot.ShowMine);
		Converge("same-after-timer", 10f, "cust.current", "cust.last", "cust.holder", "cust.visit", "cust.dlgseq", "cust.dlgon", "cust.digest", "cust.action");
		Check("no-mismatch", () => Customers.Mismatches == 0, () => "customers mismatches " + Customers.Mismatches + ", applied " + Customers.Applied + "; replay checks ok " + Replay.ChecksOk + " bad " + Replay.ChecksBad).Soft();
		Check("no-shutter-mismatch", () => DayNight.Mismatches == 0, () => "dayNight mismatches " + DayNight.Mismatches).Soft();
		ReportDone();
	}
}
