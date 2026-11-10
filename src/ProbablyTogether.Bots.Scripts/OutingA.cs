using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class OutingA : DayNightBotBase
{
	private int itemsBefore;

	private int carriedNew;

	private int searches;

	private long packId;

	public override string Name => "outing-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		JoinSteps();
		WaitServer("both-live", 150f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "dn.live") >= 2, "dn.live", "players");
		Check("individual-on", () => Outings.Individual, () => "the server told us individual outings " + Outings.Individual);
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return "day " + day0;
		});
		Do("open-click", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened", 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		WaitFor("shutter-settled", 15f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0, () => "flag " + DayNightBotBase.Ps.lockShutterButtonFlag);
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
			return "lab op clearqueue";
		});
		WaitFor("closable", 15f, () => DayNightBotBase.Ps.CanEndDay(), () => "queue " + DayNightBotBase.Ps.storeClientManager.clientStack.Count);
		Do("close", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "sent";
		});
		WaitFor("closed", 20f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		Delay("past-close-timers", 5f);
		Agree("same-close", 20f, "dn.state", "dn.digest");
		Do("door", delegate
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			GameItem val = instance.GetAllOwnedItemsByTag("BACKPACK_TAG").FirstOrDefault();
			instance.selectedBackpack = val;
			packId = ((val != null) ? Ids.Of(val) : 0);
			itemsBefore = Items.All().Count();
			MapUIManager.Instance.OpenUI();
			return "asked to go out with backpack " + packId + "; " + itemsBefore + " items in the shop; cash " + DayNightBotBase.Ps.playerCash;
		});
		WaitFor("private", 20f, () => Outings.Private && TransitUI.IsOpen, () => "private " + Outings.Private + ", map " + TransitUI.IsOpen + ", refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		WaitServer("server-sees-out", 20f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "out.count") == 1, "out.count", "dn.loc");
		Do("travel", delegate
		{
			MapUIManager.Instance.Travel(AfterhourLocation.Get("dumping_ground"));
			return "travelling (no request: dayNight.travel sent " + DayNightBotBase.SentOf("dayNight.travel") + ")";
		});
		WaitFor("at-dumping-ground", 20f, () => DayNightOuting.LocationId == "dumping_ground", () => "at " + DayNightOuting.LocationId);
		Step("search-all", 90f, delegate(BotStep st)
		{
			if (!ScavHelper.CanScavenge())
			{
				st.Detail = searches + " search(es); wound " + DayNightBotBase.Ps.healthData.woundState + "; " + ((GameInventory)EmporiumEntry.Instance.afterhourInventory).childItems.Count + " item(s) on the ground";
				return StepStatus.Pass;
			}
			ScavHelper.ScavengeDumpingGrounds();
			searches++;
			return StepStatus.Running;
		});
		Do("pocket", delegate
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			List<GameItem> list = ((GameInventory)instance.afterhourInventory).childItems.ToList();
			carriedNew = 0;
			if (list.Count > 0)
			{
				GraphUtils.TryAcceptAll((GraphNodeStorage)(object)instance.afterhourPocketSlotInvLeft, list[0], -1);
				if (instance.afterhourPocketSlotInvLeft.childItem == list[0])
				{
					carriedNew++;
				}
			}
			if (list.Count > 1)
			{
				GraphUtils.TryAcceptAll((GraphNodeStorage)(object)instance.afterhourPocketSlotInvRight, list[1], -1);
				if (instance.afterhourPocketSlotInvRight.childItem == list[1])
				{
					carriedNew++;
				}
			}
			GameItem childItem = instance.afterhourPocketSlotInvBackpack.childItem;
			return list.Count + " found; pocketed " + carriedNew + " (" + string.Join(",", list.Take(2).Select((GameItem x) => x.identifier)) + "); backpack in its slot " + ((childItem != null) ? childItem.identifier : "none");
		});
		Do("return", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "coming home (no request: dayNight.return sent " + DayNightBotBase.SentOf("dayNight.return") + ")";
		});
		WaitFor("home-resynced", 60f, () => !Outings.Private && DayNightBotBase.Live && DayNightOuting.LocationId == "", () => "private " + Outings.Private + ", live " + DayNightBotBase.Live + ", at " + DayNightOuting.LocationId);
		WaitServer("server-sees-home", 20f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "out.count") == 0, "out.count");
		Check("items-home", () => Items.All().Count() == itemsBefore + carriedNew, () => "items now " + Items.All().Count() + ", before " + itemsBefore + " + carried " + carriedNew);
		Check("visit-used", () => DayNightBotBase.Ps.healthData.wentToAfterhoursLastNight, () => "visits left " + DayNightBotBase.Ps.visitLeftTonight + ", went " + DayNightBotBase.Ps.healthData.wentToAfterhoursLastNight);
		Agree("same-after", 30f, "dn.digest", "cash", "dn.visit", "dn.state");
		Ready("ready");
		WaitFor("night", 150f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Agree("same-night", 30f, "day", "cash", "dn.state", "dn.digest");
	}
}
