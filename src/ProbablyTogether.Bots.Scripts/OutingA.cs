using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

/// Individual outing, the traveller: goes to the dump alone, pockets loot, comes home, and must match the server's shop.
public sealed class OutingAJ : OutingA
{
	public OutingAJ()
		: base(toJackson: true)
	{
	}
}

public class OutingA : DayNightBotBase
{
	private readonly bool toJackson;

	private string repFaction = "";

	private double repAfter;

	private int cashAfter;

	public OutingA()
		: this(toJackson: false)
	{
	}

	protected OutingA(bool toJackson)
	{
		this.toJackson = toJackson;
	}

	private int itemsBefore;

	private int carriedNew;

	private int loaded0;

	private string digest0 = "";

	private int searches;

	private long packId;

	public override string Name => toJackson ? "outing-aj" : "outing-a";

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
			GameItem val = ((IEnumerable<GameItem>)instance.GetAllOwnedItemsByTag("BACKPACK_TAG")).FirstOrDefault((Func<GameItem, bool>)delegate(GameItem b)
					{
					// not a bag that sits in another player's storage bay
					GameItem bay;
					int num = Bays.OwnerAround((GraphNodeStorage)(object)b.parentInventory, out bay);
					return num == 0 || num == Members.LocalSeat;
				});
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
			MapUIManager.Instance.Travel(AfterhourLocation.Get(toJackson ? "dr_jackson" : "dumping_ground"));
			return "travelling (no request: dayNight.travel sent " + DayNightBotBase.SentOf("dayNight.travel") + ")";
		});
		WaitFor("at-dumping-ground", 20f, () => DayNightOuting.LocationId == (toJackson ? "dr_jackson" : "dumping_ground"), () => "at " + DayNightOuting.LocationId);
		if (toJackson)
		{
			Check("the-stall-has-the-counters", () => HostTrip.CountersBorrowed && ((GameInventory)EmporiumEntry.Instance.frontInvinvElement).childItems.Count > 0, () => "counters borrowed " + HostTrip.CountersBorrowed + "; wares on the trade counter " + ((GameInventory)EmporiumEntry.Instance.frontInvinvElement).childItems.Count);
			Do("a-deal-at-the-stall", delegate
			{
				// Selling to Dr. Jackson in the traveller's game: money in, faction standing changed, the item on the sold list.
				StoreReputation val = DayNightBotBase.Ps.storeReputations.FirstOrDefault((StoreReputation r) => r != null);
				repFaction = ((val != null) ? val.factionId : "");
				repAfter = ((val != null) ? (val.GetReputationExact() + 2.5) : 0.0);
				if (val != null)
				{
					val.SetReputation(repAfter);
				}
				GameItem val2 = ItemSpawner.Spawn("bottled_water");
				GeneralHelper.SetItemOwned(val2, false);
				((GameInventory)EmporiumEntry.Instance.soldElement).UncheckedAccept(val2);
				cashAfter = DayNightBotBase.Ps.playerCash + 37;
				DayNightBotBase.Ps.playerCash = cashAfter;
				return "sold something for 37: cash " + cashAfter + ", " + repFaction + " standing " + repAfter + ", sold item numbered " + val2.uniqueId;
			});
		}
		Step("search-all", 90f, delegate(BotStep st)
		{
			if (toJackson)
			{
				st.Detail = "not at the dump";
				return StepStatus.Pass;
			}
			if (!ScavHelper.CanScavenge())
			{
				st.Detail = searches + " search(es); wound " + DayNightBotBase.Ps.healthData.woundState + "; " + ((GameInventory)EmporiumEntry.Instance.afterhourInventory).childItems.Count + " item(s) on the ground";
				return StepStatus.Pass;
			}
			ScavHelper.ScavengeDumpingGrounds();
			searches++;
			return StepStatus.Running;
		});
		Do("the-shop-as-i-left-it", delegate
		{
			loaded0 = Machines.LoadedCount;
			using (HostTrip.AtHome())
			{
				digest0 = ItemsMove.Digest();
			}
			return "shop items at home " + digest0 + "; loads so far " + loaded0 + "; my trip numbers its items from " + DayNightBotBase.Ps.currentUniqueId;
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
		WaitServer("b-moved-something-at-home-and-my-shop-has-it", 60f, delegate(IReadOnlyDictionary<string, string> v)
		{
			string text;
			using (HostTrip.AtHome())
			{
				text = ItemsMove.Digest();
			}
			return text != digest0 && text == DayNightBotBase.V(v, "items.digest");
		}, "items.digest");
		Check("still-at-the-dump", () => Outings.Private && DayNightOuting.LocationId == (toJackson ? "dr_jackson" : "dumping_ground"), () => "private " + Outings.Private + ", at " + DayNightOuting.LocationId);
		Do("return", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "coming home (no request: dayNight.return sent " + DayNightBotBase.SentOf("dayNight.return") + ")";
		});
		WaitFor("home-resynced", 60f, () => !Outings.Private && DayNightBotBase.Live && DayNightOuting.LocationId == "", () => "private " + Outings.Private + ", live " + DayNightBotBase.Live + ", at " + DayNightOuting.LocationId);
		Check("no-reload-on-coming-home", () => Machines.LoadedCount == loaded0, () => "loads " + Machines.LoadedCount + " (was " + loaded0 + " before the trip)");
		WaitServer("server-sees-home", 20f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "out.count") == 0, "out.count");
		Check("items-home", () => Items.All().Count() == itemsBefore + carriedNew + (toJackson ? 1 : 0), () => "items now " + Items.All().Count() + ", before " + itemsBefore + " + carried " + carriedNew + (toJackson ? " + 1 sold at the stall" : ""));
		// What the trip sold returns from the server as the shop's own item shortly after the return.
		WaitFor("shop-ids-only", 15f, () => DayNightBotBase.Ps.currentUniqueId < HostTrip.PrivateIdBase && Items.All().All((GameItem i) => i.uniqueId < HostTrip.PrivateIdBase), () => "id counter " + DayNightBotBase.Ps.currentUniqueId + "; items numbered from the trip's range still here: " + string.Join(", ", from i in Items.All()
			where i.uniqueId >= HostTrip.PrivateIdBase
			select i.identifier + "#" + i.uniqueId + " in " + ((i.parentInventory != null) ? i.parentInventory.GetType().Name : "nothing") + (((object)i.parentInventory == EmporiumEntry.Instance.soldElement) ? " (sold list)" : (((object)i.parentInventory == EmporiumEntry.Instance.backInvinvElement) ? " (back counter)" : (((object)i.parentInventory == EmporiumEntry.Instance.frontInvinvElement) ? " (front counter)" : (((object)i.parentInventory == EmporiumEntry.Instance.invElement) ? " (storage)" : ""))))));
		Agree("same-shop-after", 30f, "core.digest", "items.digest");
		if (toJackson)
		{
			Check("my-deal-is-the-shops", delegate
			{
				StoreReputation val = DayNightBotBase.Ps.storeReputations.FirstOrDefault((StoreReputation r) => r != null && r.factionId == repFaction);
				return DayNightBotBase.Ps.playerCash == cashAfter && val != null && Math.Abs(val.GetReputationExact() - repAfter) < 1E-06 && HostTrip.LastLeak.Contains("sold:1");
			}, () => "cash " + DayNightBotBase.Ps.playerCash + " (expected " + cashAfter + "); what the trip changed: " + HostTrip.LastLeak);
			Agree("same-records-as-the-server", 30f, "econ.digest", "cash");
		}
		Check("visit-used", () => DayNightBotBase.Ps.healthData.wentToAfterhoursLastNight, () => "visits left " + DayNightBotBase.Ps.visitLeftTonight + ", went " + DayNightBotBase.Ps.healthData.wentToAfterhoursLastNight);
		Agree("same-after", 30f, "dn.digest", "cash", "dn.visit", "dn.state");
		Ready("ready");
		WaitFor("night", 150f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Agree("same-night", 30f, "day", "cash", "dn.state", "dn.digest");
	}
}
