using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DayNight3B : DayNight3Base
{
	public override string Name => "daynight3-b";

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
		WaitFor("opened-by-a", 120f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
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
		WaitFor("map-open", 90f, () => DayNightOuting.MapOpen && DayNightBotBase.Ps.isStampIntroduced, () => "map " + DayNightOuting.MapOpen + ", stamp " + DayNightBotBase.Ps.isStampIntroduced);
		Do("travel-commissary", delegate
		{
			AfterhourLocation val = AfterhourLocation.Get("commissary");
			MapUIManager.Instance.Travel(val);
			return "dayNight.travel sent; discovered " + (val != null && val.IsDiscovered()) + ", available " + (val != null && val.IsAvailable());
		});
		WaitFor("at-commissary", 20f, () => DayNightOuting.LocationId == "commissary", () => "at '" + DayNightOuting.LocationId + "', last refusal " + DayNight.LastRefusedWhy);
		Agree("same-commissary", 20f, "dn.loc", "dn.stamp", "dn.outing", "dn.bag", "dn.digest");
		WaitFor("home", 90f, () => DayNightOuting.LocationId == "" && DayNightBotBase.Ps.visitLeftTonight == 0, () => "at '" + DayNightOuting.LocationId + "', visits " + DayNightBotBase.Ps.visitLeftTonight);
		Agree("same-return", 20f, "dn.loc", "dn.visit", "dn.outing", "dn.bag", "dn.digest");
		WaitFor("a-ready", 120f, () => DayNight.ReadyPlayers.Any((int p) => p != Net.LocalPlayer), () => "ready " + string.Join(",", DayNight.ReadyPlayers));
		Check("waiting-for-me", () => DayNightBotBase.Day == day0 && DayNight.WaitingPlayers.Contains(Net.LocalPlayer), () => "day " + DayNightBotBase.Day + ", waiting " + string.Join(",", DayNight.WaitingPlayers));
		Delay("let-a-see-the-barrier-hold", 5f);
		Check("still-day-1", () => DayNightBotBase.Day == day0 && DayNightBotBase.State == "CLOSED", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Do("leave", delegate
		{
			Net.Disconnect();
			return "left the server in the middle of the barrier (never ready)";
		});
		WaitFor("at-menu", 60f, () => !Net.IsClient && BotProbes.Scene == "EmporiumMenu" && (Object)(object)NewGameData.Instance != (Object)null, () => "role " + Net.Role.ToString() + ", scene " + BotProbes.Scene);
		Delay("away", 3f);
		Do("rejoin", delegate
		{
			Net.Connect(Args.Join, Args.Password ?? "", Args.Name);
			return "connecting to " + Args.Join + " again";
		});
		WaitFor("rejoined", 120f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "player " + Net.LocalPlayer + ", live " + DayNightBotBase.Live + ", day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Check("b-never-ready", () => DayNightBotBase.SentOf("dayNight.ready") == 0, () => "dayNight.ready sent " + DayNightBotBase.SentOf("dayNight.ready"));
		Agree("same-after-rejoin", 30f, "day", "cash", "dn.state", "dn.digest", "dn.outing", "dn.stamp");
		MarkEnding("before-bomb");
		WaitFor("bombed", 150f, () => DayNightRun.RunOver && DayNightRun.Ending == "bombed" && DayNight3Base.PanelUp, () => "run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + "), panel " + DayNight3Base.PanelUp + ", shown " + DayNightRun.LastShown);
		Delay("both-timers-done", 6f);
		Check("bomb-shown-once", () => DayNightRun.ShownCount == shown0 + 1 && DayNightRun.DroppedLocal == dropped0 + 1, () => "shown " + (DayNightRun.ShownCount - shown0) + " time(s), own timer's ending dropped " + (DayNightRun.DroppedLocal - dropped0) + " time(s)");
		Check("ended-line", () => DayNight3Base.EndedLineShown, () => "new-run plate '" + NewRun.Text + "'");
		WaitFor("a-voted", 120f, () => DayNightRun.NewRunVotes.Any((int p) => p != Net.LocalPlayer), () => "votes " + string.Join(",", DayNightRun.NewRunVotes));
		Check("vote-waits-for-me", () => DayNightRun.RunOver && DayNightRun.NewRunNeeded == 2 && DayNightRun.NewRunWaiting.Contains(Net.LocalPlayer), () => "run over " + DayNightRun.RunOver + ", needed " + DayNightRun.NewRunNeeded + ", waiting " + string.Join(",", DayNightRun.NewRunWaiting) + ", " + DayNightRun.NewRunSecondsLeft + " s left");
		Delay("let-a-see", 3f);
		Do("vote", delegate
		{
			DayNightRun.RequestNewRun();
			return "voted for a new run";
		});
		NewRunUp("new-run", 90f);
		Agree("same-new-run", 30f, "day", "cash", "dn.state", "dn.digest", "dn.runover", "dn.votes");
		MarkEnding("before-arrest");
		WaitFor("arrested", 120f, () => DayNightRun.RunOver && DayNightRun.Ending == "arrest" && DayNight3Base.PanelUp, () => "run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + ")");
		Delay("settle", 2f);
		Check("arrest-shown-once", () => DayNightRun.ShownCount == shown0 + 1, () => "shown " + (DayNightRun.ShownCount - shown0) + " time(s)");
		NewRunUp("new-run-by-timeout", 120f);
		Check("b-voted-once", () => DayNightBotBase.SentOf("dayNight.newRun") == 1, () => "dayNight.newRun sent " + DayNightBotBase.SentOf("dayNight.newRun"));
		Agree("same-new-run-2", 30f, "day", "cash", "dn.state", "dn.digest", "dn.runover");
		Check("no-mismatch", () => DayNight.Mismatches == 0, () => "day-night mismatches " + DayNight.Mismatches + ", applied " + DayNight.Applied).Soft();
	}
}
