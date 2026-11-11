using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

/// The host as traveller (run with --br-host): its game must come home to the same shop as everyone else.
/// Trip 1: the dumping ground with a backpack. Trip 2: Dr. Jackson's, whose stall takes the counters.
public sealed class OutingHost : DayNightBotBase
{
	private int itemsBefore;

	private int carriedNew;

	private int searches;

	private int budget;

	private int cloneBefore;

	private int frontBefore;

	private int given0;

	private string repFaction = "";

	private double repAfter;

	private int cashAfter;

	private int soldId;

	private int moveQ;

	private long xId;

	private string tripLoc = "";

	public override string Name => "outing-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 900f;

	private static EmporiumEntry E => EmporiumEntry.Instance;

	private static int Count(GameInventory inv)
	{
		return (inv == null) ? (-1) : inv.childItems.Count;
	}

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive + ", day " + DayNightBotBase.Day);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		Check("individual-on", () => Outings.Individual, () => "individual outings " + Outings.Individual);
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return "day " + day0;
		});
		Delay("b-settles", 6f);
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
		Delay("past-close-timers", 14f);
		Check("budget", delegate
		{
			budget = DayNightBotBase.Ps.visitLeftTonight;
			return budget == ProbablyTogether.Server.PlayerCount && budget >= 2;
		}, () => "night trips " + DayNightBotBase.Ps.visitLeftTonight + " for " + ProbablyTogether.Server.PlayerCount + " player(s)");

		// Trip 1: the dumping ground
		Do("door", delegate
		{
			GameItem val = ((IEnumerable<GameItem>)E.GetAllOwnedItemsByTag("BACKPACK_TAG")).FirstOrDefault((Func<GameItem, bool>)delegate(GameItem b)
			{
				// not a bag that sits in another player's storage bay
				GameItem bay;
				int num = Bays.OwnerAround((GraphNodeStorage)(object)b.parentInventory, out bay);
				return num == 0 || num == Members.LocalSeat;
			});
			E.selectedBackpack = val;
			itemsBefore = Items.All().Count();
			MapUIManager.Instance.OpenUI();
			return "asked to go out with backpack " + ((val != null) ? Ids.Of(val) : 0) + "; " + itemsBefore + " items in the shop; cash " + DayNightBotBase.Ps.playerCash;
		});
		WaitFor("private", 20f, () => Outings.Private && HostTrip.Out && TransitUI.IsOpen, () => "private " + Outings.Private + ", host out " + HostTrip.Out + ", map " + TransitUI.IsOpen + ", refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Check("no-group-trip", () => DayNightBotBase.SentOf("dayNight.goOut") == 0, () => "dayNight.goOut sent " + DayNightBotBase.SentOf("dayNight.goOut"));
		Do("travel", delegate
		{
			MapUIManager.Instance.Travel(AfterhourLocation.Get("dumping_ground"));
			return "travelling";
		});
		WaitFor("at-dumping-ground", 20f, () => DayNightOuting.LocationId == "dumping_ground", () => "at " + DayNightOuting.LocationId);
		Step("search-all", 90f, delegate(BotStep st)
		{
			if (!ScavHelper.CanScavenge())
			{
				st.Detail = searches + " search(es); wound " + DayNightBotBase.Ps.healthData.woundState + "; " + Count((GameInventory)(object)E.afterhourInventory) + " item(s) on the ground";
				return StepStatus.Pass;
			}
			ScavHelper.ScavengeDumpingGrounds();
			searches++;
			return StepStatus.Running;
		});
		Check("home-is-untouched", delegate
		{
			string text;
			int num;
			int visitLeftTonight;
			using (HostTrip.AtHome())
			{
				text = DayNightOuting.LocationId;
				num = DayNightBotBase.Ps.scavengingAttempts;
				visitLeftTonight = DayNightBotBase.Ps.visitLeftTonight;
			}
			tripLoc = DayNightOuting.LocationId;
			return text == "" && num == 0 && visitLeftTonight == budget - 1 && tripLoc == "dumping_ground" && DayNightBotBase.Ps.scavengingAttempts == searches;
		}, () => "seen from home: searches 0 and nobody out; on the trip: at " + tripLoc + " after " + DayNightBotBase.Ps.scavengingAttempts + " search(es)");
		Do("pocket", delegate
		{
			List<GameItem> list = ((GameInventory)E.afterhourInventory).childItems.ToList();
			carriedNew = 0;
			if (list.Count > 0)
			{
				GraphUtils.TryAcceptAll((GraphNodeStorage)(object)E.afterhourPocketSlotInvLeft, list[0], -1);
				if (E.afterhourPocketSlotInvLeft.childItem == list[0])
				{
					carriedNew++;
				}
			}
			if (list.Count > 1)
			{
				GraphUtils.TryAcceptAll((GraphNodeStorage)(object)E.afterhourPocketSlotInvRight, list[1], -1);
				if (E.afterhourPocketSlotInvRight.childItem == list[1])
				{
					carriedNew++;
				}
			}
			GameItem childItem = E.afterhourPocketSlotInvBackpack.childItem;
			return list.Count + " found; pocketed " + carriedNew + "; backpack in its slot " + ((childItem != null) ? childItem.identifier : "none");
		});
		Delay("b-compares-while-out", 14f);
		Do("return", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "coming home";
		});
		WaitFor("home", 40f, () => !HostTrip.Out && !Outings.Private && !Outings.AnyOut && DayNightOuting.LocationId == "", () => "host out " + HostTrip.Out + ", private " + Outings.Private + ", anyone out " + Outings.AnyOut + ", at " + DayNightOuting.LocationId);
		Check("items-home", () => Items.All().Count() == itemsBefore + carriedNew, () => "items now " + Items.All().Count() + ", before " + itemsBefore + " + carried " + carriedNew);
		Check("body-home", () => (int)DayNightBotBase.Ps.playerLocation == 0 && DayNightBotBase.Ps.scavengingAttempts == 0 && DayNightBotBase.Ps.visitLeftTonight == budget - 1 && DayNightBotBase.Ps.healthData.wentToAfterhoursLastNight, () => "searches " + DayNightBotBase.Ps.scavengingAttempts + " (home never searched), trips left " + DayNightBotBase.Ps.visitLeftTonight + ", wound " + DayNightBotBase.Ps.healthData.woundState);
		Delay("b-compares-after", 10f);

		// Trip 2: Dr. Jackson's, with an item on the back counter
		Step("x-on-counter", 25f, delegate(BotStep st)
		{
			if (moveQ == 0)
			{
				int x = 0;
				int y = 0;
				GameItem val = ItemsBotBase.Movables((GameInventory)(object)E.invElement).FirstOrDefault((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)E.backInvinvElement, i, i.unitCount, i.modifiedShape.orientation, out x, out y));
				if (val == null)
				{
					st.Detail = "no item fits the back counter";
					return StepStatus.Fail;
				}
				xId = Ids.Of(val);
				ItemsBotBase.Spot((GameInventory)(object)E.backInvinvElement, val, val.unitCount, val.modifiedShape.orientation, out x, out y);
				moveQ = ItemsMove.Move(val, (GraphNodeStorage)(object)E.backInvinvElement, x, y, val.modifiedShape.flipped, val.modifiedShape.orientation, -1, rep: false, "bot");
				st.Detail = val.identifier + "#" + xId + " -> back counter " + x + "," + y;
				return StepStatus.Running;
			}
			string text = ItemsMove.Outcome(moveQ);
			if (text.StartsWith("rejected", StringComparison.Ordinal))
			{
				st.Detail = st.Detail + "; server " + text;
				return StepStatus.Fail;
			}
			GameItem val2 = Ids.Find(xId);
			return (val2 != null && (object)val2.parentInventory == E.backInvinvElement) ? StepStatus.Pass : StepStatus.Running;
		});
		Do("door-2", delegate
		{
			GameItem val = ((IEnumerable<GameItem>)E.GetAllOwnedItemsByTag("BACKPACK_TAG")).FirstOrDefault((Func<GameItem, bool>)delegate(GameItem b)
			{
				// not a bag that sits in another player's storage bay
				GameItem bay;
				int num = Bays.OwnerAround((GraphNodeStorage)(object)b.parentInventory, out bay);
				return num == 0 || num == Members.LocalSeat;
			});
			E.selectedBackpack = val;
			itemsBefore = Items.All().Count();
			cloneBefore = Count((GameInventory)(object)E.backInvinvElement);
			frontBefore = Count((GameInventory)(object)E.frontInvinvElement);
			given0 = LabKit.Given;
			MapUIManager.Instance.OpenUI();
			return "asked to go out again with backpack " + ((val != null) ? Ids.Of(val) : 0) + "; " + itemsBefore + " items in the shop, " + cloneBefore + " on the back counter";
		});
		WaitFor("private-2", 20f, () => Outings.Private && HostTrip.Out && TransitUI.IsOpen, () => "private " + Outings.Private + ", host out " + HostTrip.Out + ", refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Check("jackson-known", delegate
		{
			AfterhourLocation val = AfterhourLocation.Get("dr_jackson");
			return val != null && val.IsDiscovered() && val.IsAvailable();
		}, () => "Dr. Jackson can be visited tonight");
		Do("travel-2", delegate
		{
			MapUIManager.Instance.Travel(AfterhourLocation.Get("dr_jackson"));
			return "travelling";
		});
		WaitFor("at-jackson", 20f, () => DayNightOuting.LocationId == "dr_jackson" && HostTrip.CountersBorrowed, () => "at " + DayNightOuting.LocationId + ", counters borrowed " + HostTrip.CountersBorrowed);
		Check("stall-on-the-counters", delegate
		{
			GameItem val = Ids.Find(xId);
			cloneBefore = Count((GameInventory)(object)E.backInvinvElementCounter);
			return val != null && (object)val.parentInventory == E.backInvinvElementCounter && cloneBefore > 0 && Count((GameInventory)(object)E.backInvinvElement) == 0 && Count((GameInventory)(object)E.frontInvinvElement) > 0;
		}, () => "the stall shows " + Count((GameInventory)(object)E.frontInvinvElement) + " ware(s); our " + Count((GameInventory)(object)E.backInvinvElementCounter) + " back-counter item(s) wait in the clone; the trade counter holds " + Count((GameInventory)(object)E.backInvinvElement));
		Check("home-sees-its-counters", delegate
		{
			int num;
			int num2;
			string text;
			string text2;
			using (HostTrip.AtHome())
			{
				List<GraphNodeStorage> list = Game.Roots();
				num = GraphUtils.FindAllChildrenType<GameItem>(list[1], (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).Count((GameItem i) => (object)i.parentInventory == list[1]);
				num2 = GraphUtils.FindAllChildrenType<GameItem>(list[2], (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).Count((GameItem i) => (object)i.parentInventory == list[2]);
				text = DayNightOuting.LocationId;
				StoreClientInstance currentClientInstance = DayNightBotBase.Ps.currentClientInstance;
				text2 = ((currentClientInstance != null) ? "a customer" : "nobody");
			}
			return num == cloneBefore && num2 == frontBefore && text == "" && text2 == "nobody" && DayNightBotBase.Ps.currentClientInstance != null;
		}, () => "from home: " + cloneBefore + " item(s) on the back counter, the front counter as it was (" + frontBefore + " item(s), none of the stall's " + Count((GameInventory)(object)E.frontInvinvElement) + "), nobody at the counter; on the trip Dr. Jackson is at the counter");
		WaitFor("b-used-the-clone", 90f, () => Count((GameInventory)(object)E.backInvinvElementCounter) == cloneBefore + 1, () => "the clone holds " + Count((GameInventory)(object)E.backInvinvElementCounter) + " item(s), " + cloneBefore + " before");
		WaitFor("b-cleared-the-clone", 90f, () => Count((GameInventory)(object)E.backInvinvElementCounter) == 0, () => "the clone holds " + Count((GameInventory)(object)E.backInvinvElementCounter) + " item(s) after B pressed clear-the-counter at home; the stall still shows " + Count((GameInventory)(object)E.frontInvinvElement) + " ware(s)");
		Check("stall-untouched", () => Count((GameInventory)(object)E.frontInvinvElement) > 0 && DayNightBotBase.Ps.currentClientInstance != null && DayNightOuting.LocationId == "dr_jackson", () => "still at the stall with " + Count((GameInventory)(object)E.frontInvinvElement) + " ware(s)");
		Delay("b-compares-at-jackson", 14f);
		Do("a-deal-at-the-stall", delegate
		{
			// What selling something to Dr. Jackson does to the shop of record: money in, standing with a faction
			// changed, the thing on the shop's list of sold items.
			StoreReputation val = DayNightBotBase.Ps.storeReputations.FirstOrDefault((StoreReputation r) => r != null);
			repFaction = ((val != null) ? val.factionId : "");
			repAfter = ((val != null) ? (val.GetReputationExact() + 2.5) : 0.0);
			if (val != null)
			{
				val.SetReputation(repAfter);
			}
			GameItem val2 = ItemSpawner.Spawn("bottled_water");
			GeneralHelper.SetItemOwned(val2, false);
			if (val2.uniqueId == 0)
			{
				val2.uniqueId = DayNightBotBase.Ps.GetUniqueID();
			}
			((GameInventory)E.soldElement).UncheckedAccept(val2);
			soldId = val2.uniqueId;
			cashAfter = DayNightBotBase.Ps.playerCash + 37;
			DayNightBotBase.Ps.playerCash = cashAfter;
			return "sold something for 37: cash " + cashAfter + ", " + repFaction + " standing " + repAfter.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ", sold item #" + soldId;
		});
		Delay("b-lives-with-the-difference", 24f);
		Do("return-2", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "coming home";
		});
		WaitFor("home-2", 40f, () => !HostTrip.Out && !Outings.Private && !Outings.AnyOut && DayNightOuting.LocationId == "", () => "host out " + HostTrip.Out + ", private " + Outings.Private + ", at " + DayNightOuting.LocationId);
		Check("counters-are-ours-again", delegate
		{
			GameItem val = Ids.Find(xId);
			bool flag = ((GameInventory)E.frontInvinvElement).childItems.All((GameItem i) => GeneralHelper.IsItemOwned(i));
			return !HostTrip.CountersBorrowed && val != null && (object)val.parentInventory == E.invElement && Count((GameInventory)(object)E.backInvinvElementCounter) == 0 && flag && DayNightBotBase.Ps.currentClientInstance == null;
		}, () => "our item is in the inventory where B's clear-the-counter sent it; back counter " + Count((GameInventory)(object)E.backInvinvElement) + " item(s), clone " + Count((GameInventory)(object)E.backInvinvElementCounter) + ", front counter " + Count((GameInventory)(object)E.frontInvinvElement) + ", customer " + ((DayNightBotBase.Ps.currentClientInstance != null) ? "still there" : "gone"));
		Check("the-deal-is-the-shops", delegate
		{
			StoreReputation val = DayNightBotBase.Ps.storeReputations.FirstOrDefault((StoreReputation r) => r != null && r.factionId == repFaction);
			return DayNightBotBase.Ps.playerCash == cashAfter && (val == null || val.GetReputationExact() == repAfter) && ((GameInventory)E.soldElement).childItems.Any((GameItem i) => i.uniqueId == soldId) && HostTrip.LastLeak.Contains("sold:1");
		}, () => "cash " + DayNightBotBase.Ps.playerCash + " (expected " + cashAfter + "); what the trip changed in the shop: " + HostTrip.LastLeak);
		Check("items-home-2", () => Items.All().Count() == itemsBefore + (LabKit.Given - given0) + 1, () => "items now " + Items.All().Count() + ", before " + itemsBefore + " plus " + (LabKit.Given - given0) + " the lab handed to the player at home");
		Check("trips-used", () => DayNightBotBase.Ps.visitLeftTonight == budget - 2, () => "trips left " + DayNightBotBase.Ps.visitLeftTonight + " of " + budget);
		Delay("b-compares-after-2", 12f);
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		// The players' customers must match the host's from the moment the shutter goes up.
		Delay("b-loads-the-morning", 10f);
		Do("open-click-next-day", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened-next-day", 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		Do("my-queue-next-day", () => Customers.DigestText());
		Delay("b-compares-the-queue", 30f);
	}
}
