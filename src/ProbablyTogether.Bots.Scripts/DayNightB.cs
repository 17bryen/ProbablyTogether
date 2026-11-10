using System;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DayNightB : DayNightBotBase
{
	private int flagAtClick;

	private int other;

	private int loadsAtEnd;

	public override string Name => "daynight-b";

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
		WaitFor("opened-by-a", 240f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		Check("b-sent-no-open", () => DayNightBotBase.SentOf("dayNight.open") == 0, () => "dayNight.open sent " + DayNightBotBase.SentOf("dayNight.open"));
		Do("click-while-moving", delegate
		{
			flagAtClick = DayNightBotBase.Ps.lockShutterButtonFlag;
			if (flagAtClick > 0)
			{
				StoreShutterButton.Instance.OnClick();
			}
			return "shutter flag " + flagAtClick + ((flagAtClick > 0) ? ": clicked" : ": already settled, not clicked");
		});
		Delay("after-click", 0.5f);
		Check("click-ignored", () => DayNightBotBase.State == "OPEN" && DayNightBotBase.SentOf("dayNight.close") == 0, () => "state " + DayNightBotBase.State + ", dayNight.close sent " + DayNightBotBase.SentOf("dayNight.close"));
		WaitFor("shutter-settled", 15f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0, () => "flag " + DayNightBotBase.Ps.lockShutterButtonFlag);
		Delay("let-a-compare", 6f);
		Do("b-close", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "dayNight.close sent " + DayNightBotBase.SentOf("dayNight.close");
		});
		WaitFor("closed", 20f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		Delay("past-close-timers", 5f);
		Agree("same-close", 20f, "dn.state", "dn.visit", "dn.queue", "dn.client", "dn.front", "dn.digest");
		WaitFor("map-open", 120f, () => DayNightOuting.MapOpen, () => "map " + DayNightOuting.MapOpen);
		Agree("same-map", 20f, "dn.map", "dn.front", "dn.digest");
		Do("bad-travel", delegate
		{
			DayNight.LastRefusedWhat = "";
			Net.ToServer("dayNight.travel", new MsgDnTravel
			{
				locationId = "no_such_place"
			});
			return "asked to travel to no_such_place";
		});
		WaitFor("travel-refused", 15f, () => DayNight.LastRefusedWhat == "travel", () => "refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		WaitFor("at-dumping-ground", 90f, () => DayNightOuting.LocationId == "dumping_ground", () => "at " + DayNightOuting.LocationId);
		Agree("same-travel", 30f, "dn.loc", "dn.outing", "dn.bag", "dn.digest");
		Do("resync-out", delegate
		{
			loadsAtEnd = DayNightProbes.Loads;
			Snapshot.RequestResync();
			return "asked for a resync at the dumping ground (loads " + loadsAtEnd + ")";
		});
		WaitFor("resynced-out", 60f, () => DayNightProbes.Loads > loadsAtEnd && DayNightBotBase.Live, () => "loads " + DayNightProbes.Loads + ", live " + DayNightBotBase.Live);
		Do("where-after-replay", () => "at " + ((DayNightOuting.LocationId.Length > 0) ? DayNightOuting.LocationId : "home") + ", visits left " + DayNightBotBase.Ps.visitLeftTonight + ", searches " + DayNightBotBase.Ps.scavengingAttempts + " (A keeps going while this copy reloads)");
		Agree("same-after-replay", 30f, "dn.state", "dn.loc", "dn.visit", "dn.scav", "dn.wound", "dn.outing", "dn.bag", "dn.digest");
		WaitFor("home", 240f, () => DayNightOuting.LocationId == "" && DayNightBotBase.Ps.visitLeftTonight == 0, () => "at " + DayNightOuting.LocationId + ", visits left " + DayNightBotBase.Ps.visitLeftTonight);
		Agree("same-return", 30f, "dn.loc", "dn.visit", "dn.scav", "dn.wound", "dn.outing", "dn.bag", "dn.digest");
		WaitFor("a-ready", 120f, () => DayNight.ReadyPlayers.Any((int p) => p != Net.LocalPlayer), () => "ready " + string.Join(",", DayNight.ReadyPlayers));
		Do("other", delegate
		{
			other = DayNight.ReadyPlayers.First((int p) => p != Net.LocalPlayer);
			return "player " + other + " is ready, " + DayNight.ReadySecondsLeft + " s left";
		});
		Delay("barrier-holds", 2f);
		Check("waiting-for-me", () => DayNightBotBase.Day == day0 && DayNight.WaitingPlayers.Contains(Net.LocalPlayer), () => "day " + DayNightBotBase.Day + ", waiting " + string.Join(",", DayNight.WaitingPlayers));
		Do("b-ready", delegate
		{
			ReadyBar.View view = DayNight.ReadyView();
			Vars["rv"] = "active " + view.Active + ", ready " + string.Join(",", view.Ready) + ", needed " + view.Needed + ", " + view.SecondsLeft + " s, can press " + view.CanPress;
			ReadyBar.Press(ready: true);
			return "the ready indicator's button pressed (view: " + Vars["rv"] + "); dayNight.ready sent " + DayNightBotBase.SentOf("dayNight.ready");
		});
		Check("indicator-view", () => Vars["rv"].StartsWith("active True, ready " + other + ", needed 2, ", StringComparison.Ordinal) && Vars["rv"].EndsWith("can press True", StringComparison.Ordinal), () => Vars["rv"]);
		WaitFor("night-1", 150f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State + ", live " + DayNightBotBase.Live);
		Agree("same-night-1", 30f, "day", "cash", "dn.state", "dn.digest", "dn.outing", "dn.ready");
		WaitFor("at-jackson", 200f, () => DayNightOuting.LocationId == "dr_jackson", () => "at " + DayNightOuting.LocationId);
		Agree("same-jackson", 30f, "dn.loc", "dn.front", "dn.client", "dn.outing", "dn.digest");
		WaitFor("night-2", 300f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 2 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Check("b-never-ready", () => DayNightBotBase.SentOf("dayNight.ready") == 1, () => "dayNight.ready sent " + DayNightBotBase.SentOf("dayNight.ready"));
		Agree("same-night-2", 30f, "day", "cash", "dn.state", "dn.digest");
		WaitFor("buyout-shown", 300f, () => DayNightRun.LastShown.StartsWith("buyout@", StringComparison.Ordinal), () => "shown " + DayNightRun.LastShown);
		WaitFor("ending-shown", 300f, () => DayNightRun.RunOver && (Object)(object)GameOverUIManager.Instance != (Object)null && GameOverUIManager.Instance.IsShowing && DayNightRun.LastShown.StartsWith("rent@", StringComparison.Ordinal), () => "run over " + DayNightRun.RunOver + ", shown " + DayNightRun.LastShown + ", panel " + ((Object)(object)GameOverUIManager.Instance != (Object)null && GameOverUIManager.Instance.IsShowing));
		Do("loads", delegate
		{
			loadsAtEnd = DayNightProbes.Loads;
			return "snapshot loads so far " + loadsAtEnd + ", ending " + DayNightRun.Ending;
		});
		Agree("same-ended", 20f, "dn.runover");
		WaitFor("new-run", 240f, () => DayNightBotBase.Live && DayNightProbes.Loads > loadsAtEnd && !DayNightRun.RunOver && DayNightBotBase.Day == 1 && DayNightBotBase.State == "MORNING", () => "loads " + DayNightProbes.Loads + ", run over " + DayNightRun.RunOver + ", day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Agree("same-new-run", 30f, "day", "cash", "dn.state", "dn.digest", "dn.runover");
		WaitFor("a-ready-again", 150f, () => DayNight.ReadyPlayers.Any((int p) => p != Net.LocalPlayer), () => "ready " + string.Join(",", DayNight.ReadyPlayers));
		Ready("b-ready-again");
		WaitFor("night-ending", 120f, () => DayNightBotBase.Live && DayNightRun.RunOver && DayNightRun.Ending == "aug" && GameOverUIManager.Instance.IsShowing && DayNightBotBase.Day == 2, () => "run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + "), panel " + GameOverUIManager.Instance.IsShowing + ", day " + DayNightBotBase.Day);
		Agree("same-night-ending", 30f, "day", "cash", "dn.state", "dn.digest", "dn.runover");
		Do("loads-after-ending", delegate
		{
			loadsAtEnd = DayNightProbes.Loads;
			return "snapshot loads " + loadsAtEnd;
		});
		Delay("no-resync-loop-wait", 25f);
		Check("no-resync-loop", () => DayNightProbes.Loads == loadsAtEnd && GameOverUIManager.Instance.IsShowing, () => "snapshot loads " + DayNightProbes.Loads + " (was " + loadsAtEnd + ")");
		Check("no-mismatch", () => DayNight.Mismatches == 0, () => "day-night mismatches " + DayNight.Mismatches + ", applied " + DayNight.Applied);
	}
}
