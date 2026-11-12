using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

/// A deal counts for the player who put the item on the counter, whoever presses Sell; both games agree on the ledger.
public abstract class StandingBotBase : DayNightBotBase
{
	protected static int Other => Net.Players.Keys.FirstOrDefault((int p) => p != Net.LocalPlayer);

	protected static int OtherSeat => Members.SeatOf(Other);

	protected static EmporiumEntry E => EmporiumEntry.Instance;

	protected static List<GameItem> Lights => (from i in Items.All()
		where i != null && i.identifier == "flashlight" && GeneralHelper.IsItemOwned(i)
		orderby Ids.Of(i)
		select i).ToList();

	protected static string At => FullDayLab.CurrentId() + (DayNightBotBase.Ps.isClientArrived ? " (arrived)" : "") + ", counter held by " + FullDayLab.Holder() + ", talking " + (DialogueSystem.Counter.Current != null);

	protected static string Ledger => "ledger [" + Standing.Raw + "]; deals counted for a player " + Standing.Credited + ", for nobody " + Standing.Unclaimed + "; cash " + DayNightBotBase.Ps.playerCash + "; at the counter " + At + "; flashlights " + string.Join(",", Lights.Select((GameItem i) => "#" + Ids.Of(i) + ((i.parentInventory == (object)E.backInvinvElement) ? ("@counter by seat " + Standing.PlacedBy(i)) : "")));

	private float lastNudge;

	/// Clicks the current line along. True while somebody is still talking.
	protected bool Talking()
	{
		CounterChannel counter = DialogueSystem.Counter;
		if (counter.Current == null)
		{
			return false;
		}
		if (Time.realtimeSinceStartup - lastNudge < 1.2f)
		{
			return true;
		}
		lastNudge = Time.realtimeSinceStartup;
		DialogUIManager instance = DialogUIManager.Instance;
		if (counter.LineUnfinished)
		{
			instance.OnCurrentTextDisplayed();
		}
		else if (counter.IsChoiceNow)
		{
			instance.SelectChoice(0);
		}
		else
		{
			instance.ContinueConversation();
		}
		return true;
	}

	protected static bool OnCounter(GameItem item)
	{
		return item != null && item.parentInventory == (object)E.backInvinvElement;
	}

	/// Presses Sell until the shop's cash has gone up.
	protected void SellStep(string name, Func<int> cashBefore)
	{
		float last = -100f;
		Step(name, 90f, delegate(BotStep st)
		{
			st.Detail = Ledger;
			if (DayNightBotBase.Ps.playerCash > cashBefore())
			{
				return StepStatus.Pass;
			}
			if (Talking() || Time.realtimeSinceStartup - last < 6f)
			{
				return StepStatus.Running;
			}
			last = Time.realtimeSinceStartup;
			List<GameItem> sellableItem = NegociationUIManager.Instance.GetSellableItem();
			if (sellableItem.Count > 0)
			{
				NegociationUIManager.Instance.SellMultiple(sellableItem, false);
			}
			return StepStatus.Running;
		});
	}

	/// Drags a flashlight onto the players' side of the counter until it lies there in this player's name.
	protected void PlaceStep(string name, Func<GameItem> which)
	{
		float last = -100f;
		Step(name, 90f, delegate(BotStep st)
		{
			GameItem val = which();
			st.Detail = Ledger;
			if (val != null && OnCounter(val) && Standing.PlacedBy(val) == Members.LocalSeat)
			{
				return StepStatus.Pass;
			}
			if (val == null || FullDayLab.CurrentId() != "merchant" || !DayNightBotBase.Ps.isClientArrived || DialogueSystem.Counter.Current != null || Time.realtimeSinceStartup - last < 5f)
			{
				return StepStatus.Running;
			}
			last = Time.realtimeSinceStartup;
			ItemsMove.QuickMove(val, (GraphNodeStorage)(object)E.backInvinvElement, all: false);
			return StepStatus.Running;
		});
	}
}

public sealed class StandingH : StandingBotBase
{
	private int cash0;

	private int cash1;

	private int cash2;

	private long first;

	private long second;

	public override string Name => "st-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 600f;

	private void Merchant(string tag)
	{
		float lastRing = -100f;
		Do("a-merchant-" + tag, () => Customers.BotDo("add", "merchant"));
		Step("merchant-at-the-counter-" + tag, 120f, delegate(BotStep st)
		{
			st.Detail = At;
			if (FullDayLab.CurrentId() == "merchant" && DayNightBotBase.Ps.isClientArrived)
			{
				return Talking() ? StepStatus.Running : StepStatus.Pass;
			}
			if (FullDayLab.CurrentId() == "none" && DayNightBotBase.Ps.lockNextBellFlag == 0 && Time.realtimeSinceStartup - lastRing > 6f)
			{
				lastRing = Time.realtimeSinceStartup;
				StoreBell val = Object.FindObjectOfType<StoreBell>();
				if ((Object)(object)val != (Object)null)
				{
					val.OnClick();
				}
			}
			return StepStatus.Running;
		});
	}

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2 && OtherSeat > 1, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		Delay("b-settles", 6f);
		Do("two-flashlights", delegate
		{
			LabKit.Do("give", "flashlight", "2");
			DayNightBotBase.Ps.isBargainIntroduced = true;
			return "lab op give";
		});
		WaitFor("they-are-in-the-shop", 20f, delegate
		{
			List<GameItem> lights = Lights;
			if (lights.Count < 2)
			{
				return false;
			}
			first = Ids.Of(lights[0]);
			second = Ids.Of(lights[1]);
			return true;
		}, () => Ledger);
		Check("an-empty-ledger", () => Standing.Active && Standing.Of(1).Earned == 0 && Standing.Of(OtherSeat).Earned == 0, () => Ledger);
		Do("open-click", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened", 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		WaitFor("shutter-settled", 20f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0, () => "flag " + DayNightBotBase.Ps.lockShutterButtonFlag);
		Do("queue", delegate
		{
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "clearqueue"
			});
			return "lab op clearqueue";
		});
		WaitFor("empty-queue", 15f, () => DayNightBotBase.Ps.storeClientManager.clientStack.Count == 0, () => "queue " + DayNightBotBase.Ps.storeClientManager.clientStack.Count);
		// Deal 1: their flashlight, my click.
		Merchant("1");
		WaitFor("b-put-the-first-flashlight-on-the-counter", 120f, () => OnCounter(Items.Find(first)) && Standing.PlacedBy(Items.Find(first)) == OtherSeat, () => Ledger);
		Do("before-1", delegate
		{
			cash0 = DayNightBotBase.Ps.playerCash;
			return Ledger;
		});
		SellStep("i-press-sell", () => cash0);
		Check("the-sale-counts-for-them-not-for-me", delegate
		{
			cash1 = DayNightBotBase.Ps.playerCash;
			Standing.Row row = Standing.Of(OtherSeat);
			return row.Sold == 1 && row.Earned == cash1 - cash0 && Standing.Of(1).Earned == 0 && Standing.Of(1).Sold == 0 && Standing.Of(0).Earned == 0;
		}, () => Ledger + "; cash before " + cash0);
		// Deal 2: my flashlight, their click.
		Step("the-merchant-is-gone-or-sent-away", 60f, delegate(BotStep st)
		{
			st.Detail = At;
			if (FullDayLab.CurrentId() == "none")
			{
				return StepStatus.Pass;
			}
			if (!Talking() && DayNightBotBase.Ps.isClientArrived && DayNightBotBase.Ps.IsClientDimissable() && st.Elapsed > 4f)
			{
				DayNightBotBase.Ps.TryPlayerDismissCurrentClient(false);
			}
			return StepStatus.Running;
		});
		Merchant("2");
		PlaceStep("i-put-the-second-flashlight-on-the-counter", () => Items.Find(second));
		Do("before-2", delegate
		{
			cash1 = DayNightBotBase.Ps.playerCash;
			return Ledger;
		});
		WaitFor("b-pressed-sell", 120f, () => DayNightBotBase.Ps.playerCash > cash1, () => Ledger);
		Check("that-one-counts-for-me", delegate
		{
			cash2 = DayNightBotBase.Ps.playerCash;
			Standing.Row row = Standing.Of(1);
			Standing.Row row2 = Standing.Of(OtherSeat);
			return row.Sold == 1 && row.Earned == cash2 - cash1 && row2.Sold == 1 && row2.Earned == cash1 - cash0 && Standing.Of(0).Earned == 0;
		}, () => Ledger + "; cash " + cash0 + " -> " + cash1 + " -> " + cash2);
		Check("the-ledger-reads-well", () => Standing.ChatCommand().Contains(Members.NameOf(1) + ": in ") && Standing.ChatCommand().Contains(Members.NameOf(OtherSeat) + ": in ") && Standing.ChatCommand().Contains("net +"), () => "/ledger says: " + Standing.ChatCommand());
		Delay("b-checks", 40f);
	}
}

public sealed class StandingB : StandingBotBase
{
	private int cash1;

	public override string Name => "st-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		JoinSteps();
		WaitFor("two-flashlights", 120f, () => Lights.Count >= 2, () => Ledger);
		PlaceStep("i-put-the-first-flashlight-on-the-counter", () => Lights.FirstOrDefault());
		WaitFor("the-founder-sold-it-and-it-counts-for-me", 120f, () => Standing.Of(Members.LocalSeat).Sold == 1 && Standing.Of(Members.LocalSeat).Earned > 0 && Standing.Of(1).Earned == 0, () => Ledger);
		Agree("same-ledger-as-the-founder", 30f, "core.digest", "cash");
		WaitFor("the-founder-put-the-second-one-on-the-counter", 180f, delegate
		{
			GameItem val = Lights.FirstOrDefault();
			return val != null && OnCounter(val) && Standing.PlacedBy(val) == 1 && FullDayLab.CurrentId() == "merchant";
		}, () => Ledger);
		Do("before", delegate
		{
			cash1 = DayNightBotBase.Ps.playerCash;
			return Ledger;
		});
		SellStep("i-press-sell", () => cash1);
		Check("it-counts-for-the-founder-not-for-me", () => Standing.Of(1).Sold == 1 && Standing.Of(1).Earned == DayNightBotBase.Ps.playerCash - cash1 && Standing.Of(Members.LocalSeat).Sold == 1, () => Ledger + "; cash before " + cash1);
		Delay("two-comparisons-with-the-server", 22f);
		Agree("same-ledger-at-the-end", 30f, "core.digest", "cash");
		Check("no-reload", () => Digest.ReloadsAsked == 0 && DayNight.Mismatches == 0, () => "reloads asked " + Digest.ReloadsAsked + "; day/night mismatches " + DayNight.Mismatches + "; " + Ledger);
	}
}
