using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DayNightA : DayNightBotBase
{
	private float readyAt;

	private int searches;

	private int cashBefore;

	private int loadsBefore;

	public override string Name => "daynight-a";

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
		Do("open-click", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "dayNight.open sent " + DayNightBotBase.SentOf("dayNight.open");
		});
		WaitFor("opened", 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		Agree("same-open", 20f, "dn.state", "day", "cash", "dn.queue", "dn.front", "dn.digest", "dn.outing");
		WaitFor("closed-by-b", 120f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		Delay("past-close-timers", 5f);
		Agree("same-close", 20f, "dn.state", "dn.visit", "dn.queue", "dn.client", "dn.front", "dn.digest");
		int asks = 0;
		int goOuts = 0;
		int uses = 0;
		Do("door", delegate
		{
			EmporiumEntry.Instance.selectedBackpack = null;
			asks = Snapshot.AskCount;
			goOuts = DayNightBotBase.SentOf("dayNight.goOut");
			uses = ItemsUse.SentCount;
			StoreResponse.ShowAfterhourResponse();
			return "the door's window opened: " + ((GameInventory)EmporiumEntry.Instance.responseInventoryClosable).childItems.Count + " choice tile(s)";
		});
		Do("door-choice", delegate
		{
			GameItem? obj = ((GameInventory)EmporiumEntry.Instance.responseInventoryClosable).childItems.OfType<GameItem>().FirstOrDefault() ?? throw new Exception("no choice tile in the door's window");
			if (!ItemsUse.IsChoiceTile(obj))
			{
				throw new Exception("the first tile is not seen as a choice tile");
			}
			obj.ActivateSlotItem();
			return "clicked the first tile (no backpack)";
		});
		Check("door-choice-sent", () => DayNightBotBase.SentOf("dayNight.goOut") == goOuts + 1 && ItemsUse.SentCount == uses && Snapshot.AskCount == asks, () => "dayNight.goOut sent " + (DayNightBotBase.SentOf("dayNight.goOut") - goOuts) + ", items requests " + (ItemsUse.SentCount - uses) + ", resyncs asked " + (Snapshot.AskCount - asks));
		WaitFor("map-open", 20f, () => DayNightOuting.MapOpen, () => "map " + DayNightOuting.MapOpen);
		Check("front-cleared", () => ((GameInventory)EmporiumEntry.Instance.frontInvinvElement).childItems.Count == 0, () => "front counter " + ((GameInventory)EmporiumEntry.Instance.frontInvinvElement).childItems.Count + " item(s)");
		Agree("same-map", 20f, "dn.map", "dn.front", "dn.digest");
		WaitServer("b-travel-refused", 90f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "dn.refusals") >= 1, "dn.refusals");
		Do("travel", delegate
		{
			MapUIManager.Instance.Travel(AfterhourLocation.Get("dumping_ground"));
			return "dayNight.travel sent " + DayNightBotBase.SentOf("dayNight.travel");
		});
		WaitFor("at-dumping-ground", 20f, () => DayNightOuting.LocationId == "dumping_ground", () => "at " + DayNightOuting.LocationId);
		Check("went-out", () => DayNightBotBase.Ps.healthData.wentToAfterhoursLastNight && DayNightBotBase.Ps.lastAfterhourOutingDay == DayNightBotBase.Day, () => "wentToAfterhoursLastNight " + DayNightBotBase.Ps.healthData.wentToAfterhoursLastNight + ", lastAfterhourOutingDay " + DayNightBotBase.Ps.lastAfterhourOutingDay + ", day " + DayNightBotBase.Day);
		Agree("same-travel", 20f, "dn.loc", "dn.outing", "dn.bag", "dn.digest");
		int waitingFor = -1;
		Step("search-all", 150f, delegate(BotStep st)
		{
			int scavengingAttempts = DayNightBotBase.Ps.scavengingAttempts;
			if (waitingFor >= 0 && scavengingAttempts < waitingFor)
			{
				return StepStatus.Running;
			}
			if (!ScavHelper.CanScavenge())
			{
				st.Detail = searches + " search(es); attempts " + scavengingAttempts + ", time left " + ScavHelper.GetScavTimeLeft() + ", wound " + DayNightBotBase.Ps.healthData.woundState + (DayNightBotBase.Ps.healthData.isWoundedFresh ? " (fresh)" : "") + ", bag " + ((GameInventory)EmporiumEntry.Instance.afterhourInventory).childItems.Count + " item(s)";
				return StepStatus.Pass;
			}
			ScavHelper.ScavengeDumpingGrounds();
			searches++;
			waitingFor = scavengingAttempts + 1;
			return StepStatus.Running;
		});
		Agree("same-searched", 20f, "dn.scav", "dn.wound", "dn.outing", "dn.bag", "dn.digest");
		Do("extra-search", delegate
		{
			DayNight.LastRefusedWhat = "";
			Net.ToServer("dayNight.scavenge", new MsgDnEmpty());
			return "sent a search the server must refuse";
		});
		WaitFor("extra-refused", 15f, () => DayNight.LastRefusedWhat == "scavenge", () => "refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Do("return", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "dayNight.return sent " + DayNightBotBase.SentOf("dayNight.return");
		});
		WaitFor("home", 20f, () => DayNightOuting.LocationId == "", () => "at " + DayNightOuting.LocationId);
		Check("visit-used", () => DayNightBotBase.Ps.visitLeftTonight == 0 && (int)DayNightBotBase.Ps.playerLocation == 0, () => "visitLeftTonight " + DayNightBotBase.Ps.visitLeftTonight + ", " + DayNightBotBase.Ps.playerLocation.ToString());
		Agree("same-return", 20f, "dn.loc", "dn.visit", "dn.outing", "dn.bag", "dn.digest");
		Do("door-again", delegate
		{
			DayNight.LastRefusedWhat = "";
			MapUIManager.Instance.OpenUI();
			return "dayNight.goOut sent " + DayNightBotBase.SentOf("dayNight.goOut");
		});
		WaitFor("door-refused", 15f, () => DayNight.LastRefusedWhat == "goOut", () => "refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		WaitServer("both-live-before-ready", 60f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "dn.live") >= 2, "dn.live");
		Ready("ready-1");
		WaitFor("ready-seen", 15f, () => DayNight.IsReady(Net.LocalPlayer), () => "ready " + string.Join(",", DayNight.ReadyPlayers) + ", waiting " + string.Join(",", DayNight.WaitingPlayers));
		Delay("barrier-holds", 3f);
		Check("no-night-without-b", () => DayNightBotBase.Day == day0 && DayNightBotBase.State == "CLOSED", () => "day " + DayNightBotBase.Day + " (" + day0 + "), " + DayNightBotBase.State);
		WaitFor("night-1", 150f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State + ", live " + DayNightBotBase.Live);
		Agree("same-night-1", 30f, "day", "cash", "dn.state", "dn.digest", "dn.outing", "dn.ready");
		Do("open-2", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened-2", 30f, () => DayNightBotBase.State == "OPEN");
		WaitFor("shutter-settled-2", 15f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0, () => "flag " + DayNightBotBase.Ps.lockShutterButtonFlag);
		Do("queue-2", delegate
		{
			if (DayNightBotBase.Ps.CanEndDay())
			{
				return "every waiting customer can be sent away";
			}
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "clearqueue"
			});
			return "a story customer is waiting (cannot be sent away): lab op clearqueue";
		});
		WaitFor("closable-2", 15f, () => DayNightBotBase.Ps.CanEndDay(), () => "queue " + DayNightBotBase.Ps.storeClientManager.clientStack.Count);
		Do("close-2", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "sent";
		});
		WaitFor("closed-2", 30f, () => DayNightBotBase.State == "CLOSED");
		Do("door-2", delegate
		{
			EmporiumEntry.Instance.selectedBackpack = null;
			MapUIManager.Instance.OpenUI();
			return "dayNight.goOut sent " + DayNightBotBase.SentOf("dayNight.goOut");
		});
		WaitFor("map-open-2", 20f, () => DayNightOuting.MapOpen);
		Do("travel-jackson", delegate
		{
			AfterhourLocation val = AfterhourLocation.Get("dr_jackson");
			MapUIManager.Instance.Travel(val);
			return "discovered " + (val != null && val.IsDiscovered()) + ", available " + (val != null && val.IsAvailable());
		});
		WaitFor("at-jackson", 20f, () => DayNightOuting.LocationId == "dr_jackson", () => "at " + DayNightOuting.LocationId + "; last refusal " + DayNight.LastRefusedWhy);
		Check("jackson-stock", () => ((GameInventory)EmporiumEntry.Instance.frontInvinvElement).childItems.Count > 0, delegate
		{
			string text = ((GameInventory)EmporiumEntry.Instance.frontInvinvElement).childItems.Count.ToString();
			StoreClientInstance currentClientInstance = DayNightBotBase.Ps.currentClientInstance;
			return "front counter " + text + " item(s), at the counter " + (((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.identifier) ?? "nobody");
		});
		Agree("same-jackson", 20f, "dn.loc", "dn.front", "dn.client", "dn.outing", "dn.digest");
		Do("return-jackson", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "dayNight.return sent " + DayNightBotBase.SentOf("dayNight.return");
		});
		WaitFor("home-2", 20f, () => DayNightOuting.LocationId == "" && DayNightBotBase.Ps.visitLeftTonight == 0);
		Agree("same-after-jackson", 20f, "dn.loc", "dn.front", "dn.client", "dn.visit", "dn.outing", "dn.digest");
		Ready("ready-2");
		Do("ready-2-time", delegate
		{
			readyAt = Time.realtimeSinceStartup;
			return "t " + readyAt.ToString("F1", CultureInfo.InvariantCulture);
		});
		WaitFor("countdown-seen", 15f, () => DayNight.IsReady(Net.LocalPlayer) && DayNight.ReadySecondsLeft >= 0, () => "seconds left " + DayNight.ReadySecondsLeft);
		WaitFor("night-2-by-timeout", 120f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 2 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Check("waited-for-timeout", () => Time.realtimeSinceStartup - readyAt >= 15f, () => "night came " + (Time.realtimeSinceStartup - readyAt).ToString("F1", CultureInfo.InvariantCulture) + " s after ready (timeout 20 s, B never ready)");
		Agree("same-night-2", 30f, "day", "cash", "dn.state", "dn.digest");
		Do("open-3", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened-3", 30f, () => DayNightBotBase.State == "OPEN");
		WaitFor("shutter-settled-3", 15f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0);
		Do("queue-3", delegate
		{
			if (DayNightBotBase.Ps.CanEndDay())
			{
				return "every waiting customer can be sent away";
			}
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "clearqueue"
			});
			return "a story customer is waiting (cannot be sent away): lab op clearqueue";
		});
		WaitFor("closable-3", 15f, () => DayNightBotBase.Ps.CanEndDay(), () => "queue " + DayNightBotBase.Ps.storeClientManager.clientStack.Count);
		Do("close-3", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "sent";
		});
		WaitFor("closed-3", 30f, () => DayNightBotBase.State == "CLOSED");
		Do("cant-pay", delegate
		{
			DayNight.ApplyTabletServiceWrites();
			int num = DayNight.ServicesTotal();
			cashBefore = DayNightBotBase.Ps.playerCash;
			if (num <= 0)
			{
				Vars["cantpay"] = "skip";
				return "tonight's services cost 0: nothing to refuse (skipped)";
			}
			Vars["cantpay"] = "test";
			DayNight.LastRefusedWhat = "";
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "cash:0"
			});
			return "services " + num + ", cash " + cashBefore + " -> 0 on the server";
		});
		Delay("cash-applied", 1f);
		Step("cant-pay-indicator", 10f, delegate(BotStep st)
		{
			if (Vars["cantpay"] == "skip")
			{
				st.Detail = "skipped (no paid services)";
				return StepStatus.Pass;
			}
			int playerCash = DayNightBotBase.Ps.playerCash;
			ReadyBar.View view;
			try
			{
				DayNightBotBase.Ps.playerCash = 0;
				view = DayNight.ReadyView();
			}
			finally
			{
				DayNightBotBase.Ps.playerCash = playerCash;
			}
			st.Detail = "active " + view.Active + ", can press " + view.CanPress + ", note: " + view.Note;
			return (view.Active && !view.CanPress && (view.Note ?? "").StartsWith("Not enough cash", StringComparison.Ordinal)) ? StepStatus.Pass : StepStatus.Fail;
		}).Soft();
		Step("cant-pay-ready", 15f, delegate(BotStep st)
		{
			if (Vars["cantpay"] == "skip")
			{
				st.Detail = "skipped (no paid services)";
				return StepStatus.Pass;
			}
			if (!Vars.ContainsKey("cpsent"))
			{
				Vars["cpsent"] = "1";
				Net.ToServer("dayNight.ready", new MsgDnReady
				{
					ready = true
				});
				return StepStatus.Running;
			}
			if (DayNight.LastRefusedWhat != "ready")
			{
				return StepStatus.Running;
			}
			st.Detail = "refused: " + DayNight.LastRefusedWhy;
			return StepStatus.Pass;
		}).Soft();
		Do("cash-back", delegate
		{
			if (Vars["cantpay"] == "skip")
			{
				return "nothing to restore";
			}
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "cash:" + cashBefore
			});
			if (DayNight.IsReady(Net.LocalPlayer))
			{
				Net.ToServer("dayNight.ready", new MsgDnReady
				{
					ready = false
				});
			}
			return "cash back to " + cashBefore;
		});
		Do("buyout", delegate
		{
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "gameover:buyout"
			});
			return "lab op gameover:buyout";
		});
		WaitFor("buyout-shown", 20f, () => DayNightRun.LastShown.StartsWith("buyout@", StringComparison.Ordinal) && GameOverUIManager.Instance.IsShowing && !DayNightRun.RunOver, () => "shown " + DayNightRun.LastShown + ", panel " + GameOverUIManager.Instance.IsShowing + ", run over " + DayNightRun.RunOver);
		Agree("same-after-buyout", 20f, "dn.runover", "day", "dn.state");
		Do("keep-playing", delegate
		{
			GameOverUIManager.Instance.ContinuePlayClick();
			return "continue clicked";
		});
		Check("buyout-panel-gone", () => !GameOverUIManager.Instance.IsShowing);
		Do("game-over", delegate
		{
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "gameover:rent"
			});
			return "lab op gameover:rent";
		});
		WaitFor("ending-shown", 30f, () => DayNightRun.RunOver && (Object)(object)GameOverUIManager.Instance != (Object)null && GameOverUIManager.Instance.IsShowing, () => "run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + "), panel " + ((Object)(object)GameOverUIManager.Instance != (Object)null && GameOverUIManager.Instance.IsShowing));
		Agree("same-ended", 20f, "dn.runover");
		WaitServer("slot-moved-aside", 20f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "dn.slot").StartsWith("0/", StringComparison.Ordinal) && DayNightBotBase.V(v, "dn.slot") != "0/0", "dn.slot", "dn.runover");
		Do("gated", delegate
		{
			DayNight.LastRefusedWhat = "";
			Net.ToServer("test.gated", new MsgDnEmpty());
			return "sent kind test.gated";
		});
		WaitFor("gated-refused", 15f, () => DayNight.LastRefusedWhat == "test.gated", () => "refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Do("open-after-end", delegate
		{
			DayNight.LastRefusedWhat = "";
			Net.ToServer("dayNight.open", new MsgDnOpen());
			return "sent dayNight.open";
		});
		WaitFor("open-refused", 15f, () => DayNight.LastRefusedWhat == "open", () => "refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Do("vote-alone", delegate
		{
			DayNightRun.RequestNewRun();
			return "voted for a new run (B does not vote)";
		});
		WaitFor("vote-counted", 15f, () => DayNightRun.NewRunVotes.Contains(Net.LocalPlayer) && DayNightRun.NewRunNeeded == 2, () => "votes " + string.Join(",", DayNightRun.NewRunVotes) + ", needed " + DayNightRun.NewRunNeeded);
		Delay("vote-holds", 2f);
		Check("still-over", () => DayNightRun.RunOver, () => "run over " + DayNightRun.RunOver);
		Do("new-run", delegate
		{
			loadsBefore = DayNightProbes.Loads;
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "newrun"
			});
			return "lab op newrun";
		});
		WaitFor("new-run-loaded", 150f, () => DayNightBotBase.Live && DayNightProbes.Loads > loadsBefore && !DayNightRun.RunOver && DayNightBotBase.Day == 1 && DayNightBotBase.State == "MORNING", () => "loads " + DayNightProbes.Loads + " (" + loadsBefore + "), run over " + DayNightRun.RunOver + ", day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Check("panel-gone", () => (Object)(object)GameOverUIManager.Instance != (Object)null && !GameOverUIManager.Instance.IsShowing);
		Agree("same-new-run", 30f, "day", "cash", "dn.state", "dn.digest", "dn.runover");
		WaitServer("slot-saved-again", 20f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "dn.slot").StartsWith("1/", StringComparison.Ordinal), "dn.slot", "dn.runover");
		Do("open-4", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened-4", 30f, () => DayNightBotBase.State == "OPEN");
		WaitFor("shutter-settled-4", 15f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0);
		Do("queue-4", delegate
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
		WaitFor("closable-4", 15f, () => DayNightBotBase.Ps.CanEndDay());
		Do("close-4", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "sent";
		});
		WaitFor("closed-4", 30f, () => DayNightBotBase.State == "CLOSED");
		Do("arm-night-ending", delegate
		{
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "nightend:aug"
			});
			return "lab op nightend:aug";
		});
		Delay("armed", 1f);
		Ready("ready-4");
		WaitFor("night-ending", 120f, () => DayNightBotBase.Live && DayNightRun.RunOver && DayNightRun.Ending == "aug" && GameOverUIManager.Instance.IsShowing && DayNightBotBase.Day == 2, () => "run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + "), panel " + GameOverUIManager.Instance.IsShowing + ", day " + DayNightBotBase.Day + ", live " + DayNightBotBase.Live);
		Agree("same-night-ending", 30f, "day", "cash", "dn.state", "dn.digest", "dn.runover");
		WaitServer("slot-moved-again", 20f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "dn.slot") == "0/2", "dn.slot", "dn.runover");
		Do("loads-after-ending", delegate
		{
			loadsBefore = DayNightProbes.Loads;
			return "snapshot loads " + loadsBefore;
		});
		Delay("no-resync-loop-wait", 25f);
		Check("no-resync-loop", () => DayNightProbes.Loads == loadsBefore && GameOverUIManager.Instance.IsShowing, () => "snapshot loads " + DayNightProbes.Loads + " (was " + loadsBefore + "), panel " + GameOverUIManager.Instance.IsShowing);
		Agree("still-same", 20f, "day", "cash", "dn.digest", "dn.runover");
		Check("no-mismatch", () => DayNight.Mismatches == 0, () => "day-night mismatches " + DayNight.Mismatches + ", applied " + DayNight.Applied);
	}
}
