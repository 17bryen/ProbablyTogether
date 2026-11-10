using System;
using System.Collections.Generic;
using System.Globalization;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CustomersA : CustomersBot
{
	private int v;

	private int dlg0;

	private int cash0;

	private int dealtAt;

	private long itemId;

	private readonly bool[] noStock = new bool[4];

	public override string Name => "customers-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 1000f;

	private GameItem Item
	{
		get
		{
			if (itemId != 0L)
			{
				return Ids.Find(itemId);
			}
			return null;
		}
	}

	private BotStep Talk(string name, float timeout)
	{
		float lastNudge = 0f;
		float stockSince = 0f;
		string stock = null;
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			CounterChannel counter = DialogueSystem.Counter;
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (counter.Current != null)
			{
				if (realtimeSinceStartup - lastNudge > 1.5f)
				{
					lastNudge = realtimeSinceStartup;
					if (counter.IsChoiceNow)
					{
						DialogueSync.RequestChoose("counter", 0);
					}
					else if (counter.LineUnfinished)
					{
						DialogueSync.RequestShown("counter");
					}
					else
					{
						DialogueSync.RequestAdvance("counter");
					}
				}
				stock = null;
				return StepStatus.Running;
			}
			if (DialogueSync.Seq("counter") <= dlg0 + 1 || CustomersBot.Mine("cust.started") != "1")
			{
				return StepStatus.Running;
			}
			string text = CustomersBot.StockText();
			if (text != stock)
			{
				stock = text;
				stockSince = realtimeSinceStartup;
				return StepStatus.Running;
			}
			if (realtimeSinceStartup - stockSince < 2f)
			{
				return StepStatus.Running;
			}
			st.Detail = "conversation over: dialogue steps " + dlg0 + " -> " + DialogueSync.Seq("counter") + ", goods on the counter " + stock + " after " + F2(st.Elapsed) + " s";
			return StepStatus.Pass;
		});
		botStep.TimeoutDetail = () => "conversation not over: line " + DialogueSync.CurrentFp("counter") + ", unfinished " + DialogueSystem.Counter.LineUnfinished + ", steps " + dlg0 + " -> " + DialogueSync.Seq("counter") + "; " + CustomersBot.ShowMine();
		return botStep;
	}

	private static string F2(float s)
	{
		return s.ToString("F1", CultureInfo.InvariantCulture);
	}

	private void Call(string tag)
	{
		Do("call-" + tag, delegate
		{
			v = Customers.Visit;
			dlg0 = DialogueSync.Seq("counter");
			Customers.RequestCall();
			return "visit " + v + ", queue " + CustomersBot.Mine("cust.queue") + ", head " + CustomersBot.Mine("cust.head");
		});
		WaitFor("called-" + tag, 15f, () => Customers.Visit == v + 1 && CustomersBot.Mine("cust.current") != "none", CustomersBot.ShowMine);
		WaitFor("arrived-" + tag, 30f, () => CustomersBot.Mine("cust.arrived") == "1", CustomersBot.ShowMine);
		Converge("same-arrived-" + tag, 10f, "cust.current", "cust.sprite", "cust.visit", "cust.seq", "cust.arrived", "cust.holder", "cust.digest");
		Check("holder-is-me-" + tag, () => Locks.HeldBy("counter") == Net.LocalPlayer, () => "holder " + CustomersBot.Mine("cust.holder") + ", me " + Net.LocalPlayer);
	}

	protected override void Build()
	{
		Joined();
		PollServer("both-in", 240f, (Dictionary<string, string> s) => CustomersBot.Int(s["cust.ready"]) >= 2);
		Do("open-shutter", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "clicked the shutter (state " + CustomersBot.Mine("cust.state") + ")";
		});
		WaitFor("open-here", 30f, () => CustomersBot.Mine("cust.state") == "OPEN", CustomersBot.ShowMine);
		Mark("a:open");
		PollServer("sellers-queued", 60f, (Dictionary<string, string> s) => s["cust.head"] == "scavGeneral" && CustomersBot.Int(s["cust.queue"]) >= 5);
		Converge("same-queue", 20f, "cust.state", "cust.queue", "cust.head", "cust.current", "cust.digest", "cust.action");
		Check("shutter-digest-ok", () => DayNight.Mismatches == 0, () => "dayNight mismatches " + DayNight.Mismatches + " (customers part included)");
		WaitMark("b-open", 90f, "b:open");
		for (int num = 1; num <= 3; num++)
		{
			int kk = num;
			string text = num.ToString(CultureInfo.InvariantCulture);
			Func<bool> done = () => dealtAt > 0 && dealtAt < kk;
			Func<bool> skip = () => done() || noStock[kk];
			string why = "a deal happened on an earlier visit";
			CustomersBot.Unless(done, why, Do("call-" + text, delegate
			{
				v = Customers.Visit;
				dlg0 = DialogueSync.Seq("counter");
				cash0 = CustomersBot.Int(CustomersBot.Mine("cash"));
				Customers.RequestCall();
				return "visit " + v + ", queue " + CustomersBot.Mine("cust.queue") + ", head " + CustomersBot.Mine("cust.head");
			}));
			CustomersBot.Unless(done, why, WaitFor("called-" + text, 15f, () => Customers.Visit == v + 1 && CustomersBot.Mine("cust.current") == "scavGeneral", CustomersBot.ShowMine));
			CustomersBot.Unless(done, why, WaitFor("arrived-" + text, 30f, () => CustomersBot.Mine("cust.arrived") == "1", CustomersBot.ShowMine));
			CustomersBot.Unless(done, why, Converge("same-arrived-" + text, 10f, "cust.current", "cust.sprite", "cust.visit", "cust.seq", "cust.arrived", "cust.holder", "cust.digest"));
			CustomersBot.Unless(done, why, Check("holder-is-me-" + text, () => Locks.HeldBy("counter") == Net.LocalPlayer, () => "holder " + CustomersBot.Mine("cust.holder") + ", me " + Net.LocalPlayer));
			CustomersBot.Unless(done, why, Talk("talk-" + text, 60f));
			CustomersBot.Unless(done, why, Converge("same-talk-" + text, 15f, CustomersBot.TalkProbes));
			CustomersBot.Unless(done, why, Mark("a:talked-" + text));
			CustomersBot.Unless(done, why, WaitMark("b-talked-" + text, 60f, "b:talked-" + text));
			if (num == 1)
			{
				int applied0 = 0;
				int refused0 = 0;
				string tool0 = "";
				CustomersBot.Unless(done, why, Do("use-on-counter", delegate
				{
					applied0 = ItemsUse.AppliedCount;
					refused0 = ItemsUse.RefusedCount;
					tool0 = CustomersBot.ToolStateText();
					CustomersBot.CounterTool().ToggleSlotItem();
					return "toggled our gene scanner on the counter (was " + tool0 + ")";
				}));
				CustomersBot.Unless(done, why, WaitFor("use-allowed", 15f, () => CustomersBot.ToolStateText() != tool0 && ItemsUse.AppliedCount > applied0, () => "scanner " + tool0 + " -> " + CustomersBot.ToolStateText() + ", applied " + applied0 + " -> " + ItemsUse.AppliedCount + ", refused " + refused0 + " -> " + ItemsUse.RefusedCount + " " + ItemsUse.LastRefusal));
				CustomersBot.Unless(done, why, Converge("same-use", 10f, "cust.tool", "cust.action", "cust.digest"));
			}
			CustomersBot.Unless(done, why, Do("pick-" + text, delegate
			{
				EmporiumEntry instance = EmporiumEntry.Instance;
				GameItem val = null;
				long num2 = long.MaxValue;
				foreach (GameItem childItem in ((GameInventory)instance.frontInvinvElement).childItems)
				{
					if (childItem != null && !GeneralHelper.IsItemOwned(childItem))
					{
						long negociatedValue = childItem.GetNegociatedValue();
						if (negociatedValue < num2)
						{
							num2 = negociatedValue;
							val = childItem;
						}
					}
				}
				if (val == null)
				{
					noStock[kk] = true;
					itemId = 0L;
					return "he brought nothing: dismiss and call the next";
				}
				itemId = Ids.Of(val);
				return "cheapest of his goods: " + val.identifier + " #" + itemId + " asking " + num2 + " (cash " + CustomersBot.Mine("cash") + ")";
			}));
			CustomersBot.Unless(skip, "no deal on this visit", Do("to-back-" + text, delegate
			{
				int num2 = ItemsMove.QuickMove(Item, (GraphNodeStorage)(object)EmporiumEntry.Instance.backInvinvElement, all: true, -1, "bot");
				string[] obj = new string[7]
				{
					"items request #",
					num2.ToString(),
					" ",
					null,
					null,
					null,
					null
				};
				GameItem item = Item;
				obj[3] = ((item != null) ? item.identifier : null);
				obj[4] = " #";
				obj[5] = itemId.ToString();
				obj[6] = " -> back counter";
				return string.Concat(obj);
			}));
			CustomersBot.Unless(skip, "no deal on this visit", WaitFor("on-back-" + text, 15f, () => Item != null && (object)Item.parentInventory == EmporiumEntry.Instance.backInvinvElement, delegate
			{
				string text2 = itemId.ToString();
				GameItem item = Item;
				return "item #" + text2 + " in " + (((item == null) ? null : ((object)item.parentInventory)?.ToString()) ?? "nothing");
			}));
			CustomersBot.Unless(skip, "no deal on this visit", Converge("same-back-" + text, 10f, "cust.stock", "cust.action", "cust.digest"));
			CustomersBot.Unless(skip, "no deal on this visit", Do("buy-" + text, delegate
			{
				bool flag = Negotiation.RequestDeal("nego.buy", new List<GameItem> { Item }, single: true, ignoreBudget: false);
				return (flag ? "nego.buy sent" : "nego.buy NOT sent") + " for #" + itemId + ", cash " + CustomersBot.Mine("cash");
			}));
			CustomersBot.Unless(skip, "no deal on this visit", WaitFor("bought-" + text, 15f, () => Item != null && GeneralHelper.IsItemOwned(Item) && CustomersBot.Int(CustomersBot.Mine("cash")) < cash0, () => "item #" + itemId + " owned " + (Item != null && GeneralHelper.IsItemOwned(Item)) + ", cash " + cash0 + " -> " + CustomersBot.Mine("cash")));
			CustomersBot.Unless(skip, "no deal on this visit", Converge("same-buy-" + text, 10f, "cash", "cust.stock", "cust.dm", "cust.action", "cust.digest"));
			CustomersBot.Unless(skip, "no deal on this visit", Do("deal-done-" + text, delegate
			{
				dealtAt = kk;
				Customers.BotMark("a:dealt-" + kk);
				return "bought #" + itemId + " for " + (cash0 - CustomersBot.Int(CustomersBot.Mine("cash"))) + "; told the server a:dealt-" + kk;
			}));
			CustomersBot.Unless(skip, "no deal on this visit", WaitMark("b-dealt-" + text, 60f, "b:dealt-" + text));
			CustomersBot.Unless(done, why, Do("dismiss-" + text, delegate
			{
				Customers.RequestDismiss(callNext: false);
				return "visit " + Customers.Visit;
			}));
			CustomersBot.Unless(done, why, WaitFor("gone-" + text, 20f, () => CustomersBot.Mine("cust.current") == "none", CustomersBot.ShowMine));
			CustomersBot.Unless(done, why, Converge("same-gone-" + text, 15f, CustomersBot.AfterProbes));
			CustomersBot.Unless(done, why, Check("counter-free-" + text, () => CustomersBot.Mine("cust.holder") == "-1", CustomersBot.ShowMine));
			CustomersBot.Unless(done, why, Check("bought-item-kept-" + text, () => itemId == 0L || noStock[kk] || (Item != null && GeneralHelper.IsItemOwned(Item)), () => "item #" + itemId + " " + ((Item == null) ? "gone" : ("in " + (object)Item.parentInventory))));
			CustomersBot.Unless(done, why, Mark("a:gone-" + text));
			CustomersBot.Unless(done, why, WaitMark("b-gone-" + text, 60f, "b:gone-" + text));
		}
		Check("a-deal-happened", () => dealtAt > 0, () => (dealtAt <= 0) ? "three sellers in a row brought nothing (each was dismissed)" : ("deal on visit attempt " + dealtAt)).Soft();
		Mark("a:e2e");
		WaitMark("b-e2e", 60f, "b:e2e");
		Call("idle");
		Talk("talk-idle", 60f);
		Converge("same-talk-idle", 15f, CustomersBot.TalkProbes);
		WaitFor("idle-timeout", 120f, () => Locks.HeldBy("counter") == -1, () => "holder " + CustomersBot.Mine("cust.holder") + ", customer " + CustomersBot.Mine("cust.current"));
		Mark("a:idle-out");
		WaitMark("b-idle", 90f, "b:idle");
		Converge("same-after-idle", 15f, CustomersBot.AfterProbes);
		Call("leave");
		Check("no-mismatch", () => Customers.Mismatches == 0, () => "customers mismatches " + Customers.Mismatches + ", applied " + Customers.Applied + "; replay checks ok " + Replay.ChecksOk + " bad " + Replay.ChecksBad).Soft();
		ReportDone();
		Mark("a:leaving");
		Delay("flush", 1f);
		Do("leave", delegate
		{
			Net.Disconnect();
			return "disconnected while holding the counter";
		});
	}
}
