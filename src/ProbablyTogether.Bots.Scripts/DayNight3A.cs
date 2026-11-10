using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DayNight3A : DayNight3Base
{
	public override string Name => "daynight3-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		JoinSteps();
		WaitServer("both-live", 150f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "dn.live") >= 2, "dn.live", "players");
		Check("morning", () => DayNightBotBase.State == "MORNING", () => "state " + DayNightBotBase.State);
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return "day " + day0;
		});
		Do("vote-while-running", delegate
		{
			DayNight.LastRefusedWhat = "";
			DayNightRun.RequestNewRun();
			return "voted for a new run while the run goes on";
		});
		WaitFor("vote-refused", 15f, () => DayNight.LastRefusedWhat == "newRun" && !DayNightRun.LocalVoted, () => "refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
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
		Do("stamp", delegate
		{
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "stamp"
			});
			return "lab op stamp (the commissary on the map)";
		});
		WaitFor("stamp-here", 15f, () => DayNightBotBase.Ps.isStampIntroduced, () => "isStampIntroduced " + DayNightBotBase.Ps.isStampIntroduced);
		Do("door", delegate
		{
			EmporiumEntry.Instance.selectedBackpack = null;
			MapUIManager.Instance.OpenUI();
			return "dayNight.goOut sent " + DayNightBotBase.SentOf("dayNight.goOut");
		});
		WaitFor("map-open", 20f, () => DayNightOuting.MapOpen, () => "map " + DayNightOuting.MapOpen + ", last refusal " + DayNight.LastRefusedWhy);
		WaitFor("b-led-to-commissary", 60f, () => DayNightOuting.LocationId == "commissary", () => "at '" + DayNightOuting.LocationId + "'");
		Check("commissary-discovered", () => AfterhourLocation.Get("commissary").IsDiscovered() && !DayNightOuting.MapOpen, () => "map " + DayNightOuting.MapOpen);
		Agree("same-commissary", 20f, "dn.loc", "dn.stamp", "dn.outing", "dn.bag", "dn.digest");
		Do("search-here", delegate
		{
			DayNight.LastRefusedWhat = "";
			Net.ToServer("dayNight.scavenge", new MsgDnEmpty());
			return "asked to search at the commissary";
		});
		WaitFor("search-refused", 15f, () => DayNight.LastRefusedWhat == "scavenge", () => "refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Do("return", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "dayNight.return sent " + DayNightBotBase.SentOf("dayNight.return");
		});
		WaitFor("home", 20f, () => DayNightOuting.LocationId == "" && DayNightBotBase.Ps.visitLeftTonight == 0, () => "at '" + DayNightOuting.LocationId + "', visits " + DayNightBotBase.Ps.visitLeftTonight);
		Agree("same-return", 20f, "dn.loc", "dn.visit", "dn.outing", "dn.bag", "dn.digest");
		WaitServer("both-live-before-ready", 60f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "dn.live") >= 2, "dn.live");
		Ready("ready-1");
		WaitFor("ready-seen", 15f, () => DayNight.IsReady(Net.LocalPlayer), () => "ready " + string.Join(",", DayNight.ReadyPlayers) + ", waiting " + string.Join(",", DayNight.WaitingPlayers));
		Delay("barrier-holds", 2f);
		Check("no-night-yet", () => DayNightBotBase.Day == day0 && DayNightBotBase.State == "CLOSED", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		WaitFor("night-1", 120f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State + ", live " + DayNightBotBase.Live + ", last night '" + DayNight.LastDayEndedWhy + "'");
		Check("night-for-a-alone", () => DayNight.LastDayEndedWhy.StartsWith("everyone ready (1)", StringComparison.Ordinal), () => "night: " + DayNight.LastDayEndedWhy);
		Agree("same-night-1", 30f, "day", "cash", "dn.state", "dn.digest", "dn.outing", "dn.ready");
		WaitServer("b-back", 150f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "dn.live") >= 2, "dn.live", "players");
		Delay("let-b-settle", 8f);
		MarkEnding("before-bomb");
		Do("bomb", delegate
		{
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "bomb"
			});
			return "lab op bomb";
		});
		WaitFor("bombed", 30f, () => DayNightRun.RunOver && DayNightRun.Ending == "bombed" && DayNight3Base.PanelUp, () => "run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + "), panel " + DayNight3Base.PanelUp + ", shown " + DayNightRun.LastShown);
		Delay("both-timers-done", 3f);
		Check("bomb-shown-once", () => DayNightRun.ShownCount == shown0 + 1 && DayNightRun.DroppedLocal == dropped0 + 1 && DayNightRun.LastShown.StartsWith("bombed@", StringComparison.Ordinal), () => "shown " + (DayNightRun.ShownCount - shown0) + " time(s), own timer's ending dropped " + (DayNightRun.DroppedLocal - dropped0) + " time(s), last shown " + DayNightRun.LastShown);
		Check("ended-line", () => DayNight3Base.EndedLineShown, () => "new-run plate '" + NewRun.Text + "'");
		Agree("same-ended", 20f, "dn.runover");
		WaitServer("slot-moved-aside", 20f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "dn.slot").StartsWith("0/", StringComparison.Ordinal) && DayNightBotBase.V(v, "dn.slot") != "0/0", "dn.slot", "dn.runover");
		Do("gated", delegate
		{
			DayNight.LastRefusedWhat = "";
			Net.ToServer("test.gated", new MsgDnEmpty());
			return "sent kind test.gated";
		});
		WaitFor("gated-refused", 15f, () => DayNight.LastRefusedWhat == "test.gated", () => "refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Do("vote", delegate
		{
			DayNightRun.RequestNewRun();
			return "voted for a new run";
		});
		WaitFor("vote-counted", 15f, () => DayNightRun.NewRunVotes.Contains(Net.LocalPlayer) && DayNightRun.NewRunNeeded == 2 && DayNightRun.NewRunSecondsLeft >= 0, () => "votes " + string.Join(",", DayNightRun.NewRunVotes) + ", needed " + DayNightRun.NewRunNeeded + ", " + DayNightRun.NewRunSecondsLeft + " s left");
		Delay("vote-holds", 2f);
		Check("waits-for-b", () => DayNightRun.RunOver && DayNightRun.NewRunWaiting.Count == 1 && !DayNightRun.NewRunWaiting.Contains(Net.LocalPlayer), () => "run over " + DayNightRun.RunOver + ", waiting for " + string.Join(",", DayNightRun.NewRunWaiting));
		NewRunUp("new-run", 90f);
		Check("by-everyone", () => DayNightRun.LastNewRunWhy.StartsWith("everyone voted (2)", StringComparison.Ordinal), () => "why: " + DayNightRun.LastNewRunWhy);
		Check("panel-gone", () => !DayNight3Base.PanelUp);
		Agree("same-new-run", 30f, "day", "cash", "dn.state", "dn.digest", "dn.runover", "dn.votes");
		WaitServer("slot-saved-again", 20f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "dn.slot").StartsWith("1/", StringComparison.Ordinal), "dn.slot", "dn.runover");
		MarkEnding("before-arrest");
		Do("arrest", delegate
		{
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "gameoverstep:arrest"
			});
			return "lab op gameoverstep:arrest";
		});
		WaitFor("arrested", 30f, () => DayNightRun.RunOver && DayNightRun.Ending == "arrest" && DayNight3Base.PanelUp, () => "run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + "), panel " + DayNight3Base.PanelUp);
		Delay("settle", 2f);
		Check("arrest-shown-once", () => DayNightRun.ShownCount == shown0 + 1 && DayNight3Base.EndedLineShown, () => "shown " + (DayNightRun.ShownCount - shown0) + " time(s), line " + DayNight3Base.EndedLineShown);
		Do("vote-2", delegate
		{
			DayNightRun.RequestNewRun();
			return "voted (B will not)";
		});
		WaitFor("vote-2-counted", 15f, () => DayNightRun.NewRunVotes.Contains(Net.LocalPlayer), () => "votes " + string.Join(",", DayNightRun.NewRunVotes));
		Do("vote-2-time", delegate
		{
			Vars["t"] = Time.realtimeSinceStartup.ToString(CultureInfo.InvariantCulture);
			return DayNightRun.NewRunSecondsLeft + " s left";
		});
		NewRunUp("new-run-by-timeout", 90f);
		Check("by-timeout", () => DayNightRun.LastNewRunWhy.StartsWith("vote timeout (1 of 2", StringComparison.Ordinal) && Time.realtimeSinceStartup - float.Parse(Vars["t"], CultureInfo.InvariantCulture) >= 15f, () => "why: " + DayNightRun.LastNewRunWhy + ", " + (Time.realtimeSinceStartup - float.Parse(Vars["t"], CultureInfo.InvariantCulture)).ToString("F1", CultureInfo.InvariantCulture) + " s after the vote");
		Agree("same-new-run-2", 30f, "day", "cash", "dn.state", "dn.digest", "dn.runover");
		Check("no-mismatch", () => DayNight.Mismatches == 0, () => "day-night mismatches " + DayNight.Mismatches + ", applied " + DayNight.Applied).Soft();
	}
}
