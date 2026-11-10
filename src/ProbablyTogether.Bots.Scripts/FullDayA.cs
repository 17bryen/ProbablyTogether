using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class FullDayA : FullDayBot
{
	private int cash0;

	private int day0;

	private int loads0;

	private int ended0;

	private int backBefore;

	private string upgrade;

	private string seller;

	private List<long> buyIds = new List<long>();

	private bool haggleAccepted;

	private bool customerLeftOnHaggle;

	private bool standInStock;

	private float lastTouch;

	private int markAsk;

	private float markNext;

	private float markAskedAt;

	private Dictionary<string, string> markLast;

	public override string Name => "fullday-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 2200f;

	private int CheapestUpgrade()
	{
		int num = 0;
		foreach (KeyValuePair<string, NetworkUpgrade> item in FullDayBot.Ps.networkUpgrade)
		{
			if (item.Value != null && !item.Value.IsUnlocked() && item.Value.IsReady() && !NetworkUpgrade.IsHiddenForRun(item.Key))
			{
				int cost = item.Value.GetCost();
				if (num == 0 || cost < num)
				{
					num = cost;
				}
			}
		}
		return num;
	}

	protected override void Build()
	{
		JoinSteps();
		Check("morning", () => FullDayBot.State == "MORNING" && FullDayBot.Day == 1, FullDayBot.Shop).Soft();
		Do("open-shutter", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "clicked the shutter: " + FullDayBot.Shop();
		});
		WaitFor("open", 40f, () => FullDayBot.State == "OPEN" && FullDayBot.Ps.lockShutterButtonFlag == 0, () => FullDayBot.Shop() + ", shutter flag " + FullDayBot.Ps.lockShutterButtonFlag + ", notes " + string.Join(" / ", FullDayLab.Notes)).Soft();
		Same("opened");
		Talk("morning-visitor", 90f, (Dialogue d) => 0, () => FullDayLab.CurrentId() == "none" || (FullDayBot.Ps.lockNextBellFlag == 0 && FullDayBot.NoDialogue && FullDayBot.Ps.IsClientDimissable())).Soft();
		DismissUntilGone("morning-visitor-gone", 60f).Soft();
		Do("note-queue", delegate
		{
			seller = BotProbes.Read("fd.head");
			return "queue head " + seller + " of " + BotProbes.Read("fd.queue") + "; " + FullDayBot.Shop();
		});
		CallStep("call", standIn: false);
		Same("called", FullDayBot.All.Concat(new string[4] { "fd.holder", "fd.customer", "fd.visit", "fd.queue" }).ToArray());
		Mark("a-at-counter");
		Talk("main-dialogue", 90f, (Dialogue d) => 0, () => FullDayBot.NoDialogue && DialogueSync.Seq("counter") > Dlg0 && (FullDayLab.CustItems((GameInventory)(object)FullDayBot.E.frontInvinvElement) + FullDayLab.CustItems((GameInventory)(object)FullDayBot.E.backInvinvElement) > 0 || FullDayBot.Now - TalkStart > 15f)).Soft();
		Same("after-dialogue", FullDayBot.All.Concat(new string[1] { "fd.counter" }).ToArray());
		Step("seller-stock", 30f, delegate(BotStep st)
		{
			int num = FullDayLab.CustItems((GameInventory)(object)FullDayBot.E.frontInvinvElement) + FullDayLab.CustItems((GameInventory)(object)FullDayBot.E.backInvinvElement);
			if (num > 0)
			{
				st.Detail = num + " customer item(s) on the counters " + (standInStock ? "(stand-in stock)" : "(the seller's own rolls)") + ": " + FullDayLab.Counter();
				return StepStatus.Pass;
			}
			if (!standInStock && FullDayLab.CurrentId() != "none")
			{
				standInStock = true;
				StoreClient clientBlueprint = FullDayBot.Ps.currentClientInstance.GetClientBlueprint();
				BotLog.Info(Name, "stand-in: " + clientBlueprint.identifier + " (intent " + clientBlueprint.clientIntent.ToString() + ") put nothing on the counter; asking the server for 2 customer items");
				FullDayBot.Lab("stock");
			}
			return StepStatus.Running;
		}).TimeoutDetail = () => "no customer item on the counters: " + FullDayBot.Shop();
		Same("stock", FullDayBot.All.Concat(new string[1] { "fd.counter" }).ToArray());
		Step("to-back", 25f, delegate(BotStep st)
		{
			int num = FullDayLab.CustItems((GameInventory)(object)FullDayBot.E.backInvinvElement);
			int num2 = FullDayLab.CustItems((GameInventory)(object)FullDayBot.E.frontInvinvElement);
			if (num > 0 && num2 <= 0)
			{
				st.Detail = num + " item(s) on the back counter (" + Vars["toBack"] + "): " + FullDayLab.Counter();
				return StepStatus.Pass;
			}
			if (!Vars.ContainsKey("toBack"))
			{
				StoreTransferButton val = Object.FindObjectOfType<StoreTransferButton>();
				if ((Object)(object)val != (Object)null)
				{
					val.OnClick();
					Vars["toBack"] = "transfer button (front window visible " + (FullDayBot.E.frontInvWindow != null && FullDayBot.E.frontInvWindow.IsVisible()) + ")";
				}
				else
				{
					Vars["toBack"] = "no transfer button";
				}
				return StepStatus.Running;
			}
			if (st.Elapsed > 6f && !Vars.ContainsKey("toBack2") && num2 > 0)
			{
				int num3 = 0;
				foreach (GameItem item in ((GameInventory)FullDayBot.E.frontInvinvElement).childItems.Where((GameItem i) => i != null && !GeneralHelper.IsItemOwned(i)).ToList())
				{
					if (FullDayBot.Spot((GameInventory)(object)FullDayBot.E.backInvinvElement, item, out var x, out var y) && ItemsMove.Move(item, (GraphNodeStorage)(object)FullDayBot.E.backInvinvElement, x, y, item.modifiedShape.flipped, item.modifiedShape.orientation, -1, rep: false, "fullday") != 0)
					{
						num3++;
					}
				}
				Vars["toBack2"] = "1";
				Dictionary<string, string> vars = Vars;
				vars["toBack"] = vars["toBack"] + "; then " + num3 + " drag(s)";
			}
			return StepStatus.Running;
		}).TimeoutDetail = () => (Vars.TryGetValue("toBack", out var value) ? value : "") + ": " + FullDayLab.Counter() + "; last items reject " + ItemsMove.LastReject?.why;
		Same("on-back", FullDayBot.All.Concat(new string[1] { "fd.counter" }).ToArray());
		Mark("a-on-back");
		Step("wait-b-refusals", 180f, delegate(BotStep st)
		{
			if (Time.realtimeSinceStartup - lastTouch > 15f && FullDayLab.CurrentId() != "none")
			{
				lastTouch = Time.realtimeSinceStartup;
				Customers.RequestTakeCounter();
			}
			return PollMark(st, "b-refusals-done");
		}).Soft();
		Same("after-b-refusals", FullDayBot.All.Concat(new string[4] { "fd.counter", "fd.holder", "fd.customer", "fd.state" }).ToArray());
		Do("note-cash", delegate
		{
			cash0 = FullDayBot.Ps.playerCash;
			backBefore = FullDayLab.CustItems((GameInventory)(object)FullDayBot.E.backInvinvElement);
			return "cash " + cash0 + ", " + backBefore + " item(s) to buy; can demand a discount " + BargainUIManager.Instance.CanDemandBuyingDiscount();
		});
		Do("haggle", delegate
		{
			BargainUIManager.Instance.DemandBuyingDiscount(10);
			return "DemandBuyingDiscount(10) (bargain.offer, the server rolls)";
		});
		Step("haggle-answer", 25f, delegate(BotStep st)
		{
			BargainUIManager instance = BargainUIManager.Instance;
			bool flag = FullDayLab.CurrentId() == "none" || (int)FullDayBot.Ps.currentClientInstance.GetClientBlueprint().clientIntent == 5;
			if (instance.BuyingDiscountOffered || flag)
			{
				haggleAccepted = Negotiation.HasFeatureOnCounter("bargainBuyingDiscount");
				customerLeftOnHaggle = flag;
				st.Detail = (haggleAccepted ? "accepted" : (flag ? "the customer walks out (rolled)" : "refused, stays")) + "; " + FullDayBot.DlgText() + "; " + FullDayLab.Counter();
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		}).TimeoutDetail = () => "no answer to the haggle: " + FullDayBot.DlgText() + "; notes " + string.Join(" / ", FullDayLab.Notes);
		Talk("haggle-dialogue", 40f, (Dialogue d) => 0, () => FullDayBot.NoDialogue).Soft();
		Same("haggled", FullDayBot.All.Concat(new string[1] { "fd.counter" }).ToArray());
		Step("buy", 30f, delegate(BotStep st)
		{
			if (customerLeftOnHaggle || FullDayLab.CurrentId() == "none")
			{
				st.Detail = "no sale: the customer left after the haggle";
				return StepStatus.Fail;
			}
			if (!Vars.ContainsKey("bought"))
			{
				int num = CheapestUpgrade() + 20;
				int num2 = FullDayBot.Ps.playerCash + FullDayBot.Ps.wildFavor - num;
				int num3 = 0;
				List<GameItem> list = (from x in FullDayBot.E.GetBackInvItems()
					where !GeneralHelper.IsItemOwned(x)
					orderby (int)x.GetNegociatedValue(), Ids.Of(x)
					select x).ToList();
				List<GameItem> list2 = new List<GameItem>();
				foreach (GameItem item2 in list)
				{
					int num4 = (int)item2.GetNegociatedValue();
					if (list2.Count > 0 && num3 + num4 > num2)
					{
						break;
					}
					list2.Add(item2);
					num3 += num4;
				}
				buyIds = list2.Select(Ids.Of).ToList();
				NegociationUIManager.Instance.BuyMultipleItem(list2);
				Vars["bought"] = list2.Count + " of " + list.Count + " item(s) (" + string.Join(",", list2.Select((GameItem i) => i.identifier + "#" + Ids.Of(i) + "@" + (int)i.GetNegociatedValue())) + "; keeping " + num + " for the tablet)";
				return StepStatus.Running;
			}
			if (buyIds.All(delegate(long id)
			{
				GameItem val = Items.Find(id);
				return val != null && GeneralHelper.IsItemOwned(val);
			}) && FullDayBot.Ps.playerCash < cash0)
			{
				st.Detail = "BuyMultipleItem(" + Vars["bought"] + ") " + (haggleAccepted ? "with the 10% discount" : "at the asking price") + ": cash " + cash0 + " -> " + FullDayBot.Ps.playerCash + " after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		}).Soft().TimeoutDetail = () => "not bought: cash " + cash0 + " -> " + FullDayBot.Ps.playerCash + ", " + FullDayLab.Counter() + "; notes " + string.Join(" / ", FullDayLab.Notes);
		Same("sold", FullDayBot.All.Concat(new string[1] { "fd.counter" }).ToArray());
		Talk("after-deal-dialogue", 30f, (Dialogue d) => 0, () => FullDayBot.NoDialogue).Soft();
		Mark("a-sold");
		Step("wait-b-backroom", 300f, delegate(BotStep st)
		{
			if (Time.realtimeSinceStartup - lastTouch > 15f && FullDayLab.CurrentId() != "none")
			{
				lastTouch = Time.realtimeSinceStartup;
				Customers.RequestTakeCounter();
			}
			return PollMark(st, "b-backroom-done");
		});
		DismissUntilGone("customer-leaves", 60f).Soft();
		WaitFor("counter-free", 30f, () => FullDayLab.Holder() == -1, FullDayBot.Shop).Soft();
		Same("customer-left", FullDayBot.All.Concat(new string[3] { "fd.holder", "fd.customer", "fd.counter" }).ToArray());
		Mark("a-done-counter");
		WaitMark("wait-b-called-dismissed", "b-called-dismissed", 300f);
		Same("after-b", FullDayBot.All.Concat(new string[3] { "fd.holder", "fd.customer", "fd.visit" }).ToArray());
		Do("wild-phone", delegate
		{
			EconomyLab.Op("phone", "8376", "Regular");
			return "Wild's phone -> Regular (economy lab op)";
		});
		WaitFor("wild-phone-set", 20f, () => (int)EconomyLab.WildPhone() == 4);
		Step("network-by-phone", 60f, EconomyLab.PhoneBrowsePoll());
		Step("tablet-buy", 30f, delegate(BotStep st)
		{
			if (upgrade == null)
			{
				List<string> list = new List<string>();
				foreach (KeyValuePair<string, NetworkUpgrade> item3 in FullDayBot.Ps.networkUpgrade.OrderBy(delegate(KeyValuePair<string, NetworkUpgrade> k)
				{
					NetworkUpgrade value = k.Value;
					return (value == null) ? int.MaxValue : value.GetCost();
				}))
				{
					NetworkUpgrade up;
					string text2 = Economy.CheckBuy(item3.Key, out up);
					if (text2 == null)
					{
						upgrade = item3.Key;
						break;
					}
					if (list.Count < 6)
					{
						list.Add(item3.Key + ": " + text2);
					}
				}
				if (upgrade == null)
				{
					st.Detail = "nothing on the network the shop can buy now (" + FullDayBot.Ps.networkUpgrade.Count + " upgrades; " + string.Join("; ", list) + "), cash " + FullDayBot.Ps.playerCash + ", favor " + FullDayBot.Ps.wildFavor;
					return StepStatus.Fail;
				}
				cash0 = FullDayBot.Ps.playerCash + FullDayBot.Ps.wildFavor;
				FullDayLab.ClickBuy(upgrade);
				Vars["cost"] = FullDayBot.S(FullDayBot.Ps.networkUpgrade[upgrade].GetCost());
				return StepStatus.Running;
			}
			if (FullDayBot.Ps.networkUpgrade[upgrade].IsUnlocked())
			{
				st.Detail = "bought " + upgrade + " for " + Vars["cost"] + ": cash+favor " + cash0 + " -> " + (FullDayBot.Ps.playerCash + FullDayBot.Ps.wildFavor) + " after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		}).Soft().TimeoutDetail = () => "upgrade " + upgrade + " not unlocked here; economy refusals " + Economy.Refusals + " (" + Economy.LastRefusal + ")";
		Do("network-close", delegate
		{
			WildNetworkUI.Close();
			return "closed the network window";
		});
		Same("tablet", FullDayBot.All.Concat(new string[1] { "fd.upgrades" }).ToArray());
		string text = (Args.Get("--br-fd-phone") ?? "").Trim();
		if (text == "hangup" || text == "end")
		{
			Step("phone-left-" + text, 120f, FullDayPhone.Poll(text == "end")).Soft();
			Same("phone-left", FullDayBot.All.Concat(new string[1] { "fd.phone" }).ToArray());
		}
		Mark("a-bought");
		WaitMark("wait-b-closed", "b-closed", 300f);
		WaitFor("closed-here", 30f, () => FullDayBot.State == "CLOSED", FullDayBot.Shop).Soft();
		Same("closed");
		Do("note-day", delegate
		{
			day0 = FullDayBot.Day;
			loads0 = FullDayLab.Loads;
			ended0 = FullDayLab.DaysEnded;
			cash0 = FullDayBot.Ps.playerCash;
			return "day " + day0 + ", cash " + cash0 + ", snapshots loaded " + loads0;
		});
		Delay("after-close-timers", 6f);
		ReadyStep("ready", () => ended0);
		WaitFor("morning-2", 300f, () => FullDayBot.Live && FullDayLab.Loads > loads0 && FullDayBot.Day == day0 + 1 && FullDayBot.State == "MORNING", () => FullDayBot.Shop() + ", snapshots loaded " + FullDayLab.Loads + ", night " + BotProbes.Read("fd.night"));
		Same("morning-2", 30f, FullDayBot.All.Concat(new string[7] { "fd.upgrades", "fd.counter", "fd.queue", "fd.head", "fd.holder", "fd.events", "fd.phone" }).ToArray());
		CountersSoft("counters-day-1");
		Mark("a-morning");
		WaitMark("wait-b-morning", "b-morning", 120f);
		Do("open-shutter-2", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "clicked the shutter: " + FullDayBot.Shop();
		});
		WaitFor("open-2", 40f, () => FullDayBot.State == "OPEN" && FullDayBot.Ps.lockShutterButtonFlag == 0, FullDayBot.Shop).Soft();
		Same("opened-2");
		Talk("morning-visitor-2", 90f, (Dialogue d) => 0, () => FullDayLab.CurrentId() == "none" || (FullDayBot.Ps.lockNextBellFlag == 0 && FullDayBot.NoDialogue && FullDayBot.Ps.IsClientDimissable())).Soft();
		DismissUntilGone("morning-visitor-2-gone", 60f).Soft();
		CallStep("call-2", standIn: true);
		Same("called-2", FullDayBot.All.Concat(new string[2] { "fd.holder", "fd.customer" }).ToArray());
		Talk("dialogue-2", 60f, (Dialogue d) => 0, () => FullDayBot.NoDialogue && DialogueSync.Seq("counter") > Dlg0).Soft();
		Same("talked-2", FullDayBot.All.Concat(new string[2] { "fd.holder", "fd.customer" }).ToArray());
		DismissUntilGone("dismiss-2", 60f).Soft();
		Same("dismissed-2", FullDayBot.All.Concat(new string[2] { "fd.holder", "fd.customer" }).ToArray());
		FindSeller("find-seller", 150f);
		Same("found-seller", FullDayBot.All.Concat(new string[4] { "fd.holder", "fd.customer", "fd.visit", "fd.counter" }).ToArray());
		ToBack("to-back-2");
		Same("on-back-2", FullDayBot.All.Concat(new string[3] { "fd.holder", "fd.customer", "fd.counter" }).ToArray());
		Mark("a-midday");
		Step("b-gone", 180f, delegate(BotStep st)
		{
			if (Time.realtimeSinceStartup - lastTouch > 15f && FullDayLab.CurrentId() != "none")
			{
				lastTouch = Time.realtimeSinceStartup;
				Customers.RequestTakeCounter();
			}
			return (Net.Players.Count == 1) ? Pass(st, "B left; players here " + Net.Players.Count + ", counter holder " + FullDayLab.Holder()) : StepStatus.Running;
		});
		Step("act-while-away", 25f, delegate(BotStep st)
		{
			if (!Vars.ContainsKey("away"))
			{
				int x2;
				int y2;
				GameItem val = ((IEnumerable<GameItem>)FullDayBot.Movables((GameInventory)(object)FullDayBot.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => FullDayBot.Spot((GameInventory)(object)FullDayBot.E.invElement, i, out x2, out y2)));
				if (val == null)
				{
					st.Detail = "nothing movable in the back room";
					return StepStatus.Fail;
				}
				FullDayBot.Spot((GameInventory)(object)FullDayBot.E.invElement, val, out var x, out var y);
				int num = ItemsMove.Move(val, (GraphNodeStorage)(object)FullDayBot.E.invElement, x, y, val.modifiedShape.flipped, val.modifiedShape.orientation, -1, rep: false, "fullday");
				Vars["away"] = FullDayBot.S(num);
				Vars["awayItem"] = val.identifier + "#" + Ids.Of(val) + " -> inv " + x + "," + y;
				return StepStatus.Running;
			}
			int.TryParse(Vars["away"], out var result);
			string text2 = ItemsMove.Outcome(result);
			if (text2 == "applied")
			{
				st.Detail = "moved " + Vars["awayItem"] + " while B was away (request " + result + ")";
				return StepStatus.Pass;
			}
			if (text2.StartsWith("rejected", StringComparison.Ordinal))
			{
				st.Detail = Vars["awayItem"] + ": " + text2;
				return StepStatus.Fail;
			}
			return StepStatus.Running;
		}).Soft();
		Same("while-b-away", FullDayBot.All.Concat(new string[1] { "fd.holder" }).ToArray());
		Mark("a-acted");
		Step("wait-b-rejoined", 400f, delegate(BotStep st)
		{
			if (Time.realtimeSinceStartup - lastTouch > 15f && FullDayLab.CurrentId() != "none")
			{
				lastTouch = Time.realtimeSinceStartup;
				Customers.RequestTakeCounter();
			}
			return PollMark(st, "b-rejoined");
		});
		Same("b-back", 30f, FullDayBot.All.Concat(new string[4] { "fd.holder", "fd.customer", "fd.counter", "fd.upgrades" }).ToArray());
		Do("haggle-2", delegate
		{
			cash0 = FullDayBot.Ps.playerCash;
			bool num = BargainUIManager.Instance.CanDemandBuyingDiscount();
			if (num)
			{
				BargainUIManager.Instance.DemandBuyingDiscount(10);
			}
			return (!num) ? ("no haggle possible here (" + FullDayLab.CurrentId() + ")") : "DemandBuyingDiscount(10) with B back in the shop";
		});
		Delay("haggle-2-answer", 3f);
		Talk("haggle-2-dialogue", 40f, (Dialogue d) => 0, () => FullDayBot.NoDialogue).Soft();
		Step("buy-2", 30f, delegate(BotStep st)
		{
			if (!Vars.ContainsKey("b2"))
			{
				GameItem val = (from x in FullDayBot.E.GetBackInvItems()
					where !GeneralHelper.IsItemOwned(x)
					orderby (int)x.GetNegociatedValue(), Ids.Of(x)
					select x).FirstOrDefault();
				if (val == null || FullDayLab.CurrentId() == "none")
				{
					st.Detail = "nothing to buy (customer " + FullDayLab.CurrentId() + "): " + FullDayLab.Counter();
					return StepStatus.Fail;
				}
				if ((int)val.GetNegociatedValue() > FullDayBot.Ps.playerCash + FullDayBot.Ps.wildFavor)
				{
					st.Detail = "the cheapest item (" + (int)val.GetNegociatedValue() + ") is more than the cash " + FullDayBot.Ps.playerCash;
					return StepStatus.Fail;
				}
				buyIds = new List<long> { Ids.Of(val) };
				Vars["b2"] = val.identifier + "#" + Ids.Of(val) + "@" + (int)val.GetNegociatedValue() + " (discount on the counter " + Negotiation.HasFeatureOnCounter("bargainBuyingDiscount") + ")";
				NegociationUIManager.Instance.BuyMultipleItem(new List<GameItem> { val });
				return StepStatus.Running;
			}
			GameItem val2 = Items.Find(buyIds[0]);
			if (val2 != null && GeneralHelper.IsItemOwned(val2) && FullDayBot.Ps.playerCash < cash0)
			{
				st.Detail = "bought " + Vars["b2"] + ": cash " + cash0 + " -> " + FullDayBot.Ps.playerCash + " after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		}).Soft().TimeoutDetail = () => "not bought: " + (Vars.TryGetValue("b2", out var value) ? value : "-") + ", cash " + cash0 + " -> " + FullDayBot.Ps.playerCash + "; notes " + string.Join(" / ", FullDayLab.Notes.Skip(Math.Max(0, FullDayLab.Notes.Count - 3)));
		Talk("after-buy-2", 20f, (Dialogue d) => 0, () => FullDayBot.NoDialogue).Soft();
		Same("sold-2", FullDayBot.All.Concat(new string[3] { "fd.holder", "fd.customer", "fd.counter" }).ToArray());
		Mark("a-sold-2");
		Step("wait-b-checked", 120f, delegate(BotStep st)
		{
			if (Time.realtimeSinceStartup - lastTouch > 15f && FullDayLab.CurrentId() != "none")
			{
				lastTouch = Time.realtimeSinceStartup;
				Customers.RequestTakeCounter();
			}
			return PollMark(st, "b-checked-sale");
		}).Soft();
		Same("final", 30f, FullDayBot.All.Concat(new string[4] { "fd.holder", "fd.customer", "fd.counter", "fd.upgrades" }).ToArray());
		CountersSoft("counters-final");
		Mark("a-done");
		Step("visit-for-leave", 60f, delegate(BotStep st)
		{
			if (FullDayLab.CurrentId() != "none" && FullDayBot.Ps.isClientArrived && FullDayLab.Holder() == Net.LocalPlayer)
			{
				st.Detail = (Vars.TryGetValue("vfl", out var value) ? value : "the seller is still there") + ": " + FullDayBot.Shop();
				return StepStatus.Pass;
			}
			if (!Vars.ContainsKey("vfl") && FullDayLab.CurrentId() == "none" && FullDayBot.Ps.lockNextBellFlag == 0)
			{
				if (BotProbes.Read("fd.queue") == "0")
				{
					BotLog.Info(Name, "stand-in: the queue is empty; asking for a seller");
					FullDayBot.Lab("add", "scavGeneral");
					Vars["vfl"] = "a stand-in seller called";
					return StepStatus.Running;
				}
				Vars["vfl"] = "called the next customer (" + FullDayBot.RingBell() + ")";
			}
			else if (Vars.ContainsKey("vfl") && FullDayLab.CurrentId() == "none" && FullDayBot.Ps.lockNextBellFlag == 0 && st.Elapsed > 8f && (int)(st.Elapsed * 10f) % 80 == 0)
			{
				FullDayBot.RingBell();
			}
			return StepStatus.Running;
		}).Soft();
		Talk("leave-visit-dialogue", 40f, (Dialogue d) => 0, () => FullDayBot.NoDialogue && FullDayBot.Now - TalkStart > 3f).Soft();
		ToBack("leave-visit-to-back");
		Same("before-leaving", FullDayBot.All.Concat(new string[3] { "fd.holder", "fd.customer", "fd.counter" }).ToArray());
		Mark("a-leaving");
		Delay("before-leaving", 2f);
		Do("leave-mid-deal", delegate
		{
			string text2 = FullDayBot.Shop();
			EscapeUIManager escapeUIManager = StoreUIManager.Instance.escapeUIManager;
			escapeUIManager.OpenEscapeMenu();
			escapeUIManager.OnMainMenu();
			StoreUIManager.Instance.confirmPanelUIManager.OnYesClick();
			return "escape menu > Main menu > Yes, holding the counter mid-deal (" + text2 + ")";
		});
		WaitFor("at-menu", 90f, () => Net.Role == ProbablyTogether.Role.Off && Ui.AtMenu, () => "role " + Net.Role.ToString() + ", scene " + BotProbes.Scene).Soft();
	}

	private BotStep FindSeller(string name, float timeout)
	{
		float lastAct = -100f;
		float arrivedAt = -1f;
		int visits = 0;
		int rings = 0;
		bool standIn = false;
		string seen = "";
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			StoreClientInstance currentClientInstance = FullDayBot.Ps.currentClientInstance;
			StoreClient val = ((currentClientInstance != null) ? currentClientInstance.GetClientBlueprint() : null);
			float now = FullDayBot.Now;
			if (val == null)
			{
				arrivedAt = -1f;
				if (FullDayBot.Ps.lockNextBellFlag > 0 || now - lastAct < 6f)
				{
					return StepStatus.Running;
				}
				if (!standIn && (visits >= 3 || BotProbes.Read("fd.queue") == "0"))
				{
					standIn = true;
					lastAct = now;
					BotLog.Info(Name, "stand-in: no seller after " + visits + " visit(s) (" + seen + "), queue " + BotProbes.Read("fd.queue") + "; asking the server for one");
					FullDayBot.Lab("add", "scavGeneral");
					return StepStatus.Running;
				}
				lastAct = now;
				rings++;
				FullDayBot.RingBell();
				return StepStatus.Running;
			}
			if (!FullDayBot.Ps.isClientArrived || FullDayLab.Holder() != Net.LocalPlayer)
			{
				return StepStatus.Running;
			}
			if (arrivedAt < 0f)
			{
				arrivedAt = now;
				visits++;
				seen = seen + ((seen.Length > 0) ? "," : "") + val.identifier + "/" + val.clientIntent.ToString();
			}
			int num = FullDayLab.CustItems((GameInventory)(object)FullDayBot.E.frontInvinvElement) + FullDayLab.CustItems((GameInventory)(object)FullDayBot.E.backInvinvElement);
			CounterChannel counter = DialogueSystem.Counter;
			if (val.IsClientSelling() && num > 0 && counter.Current == null)
			{
				st.Detail = "seller " + val.identifier + " with " + num + " item(s) after " + visits + " visit(s) (" + seen + (standIn ? ", stand-in" : "") + "), " + rings + " bell press(es) after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			if (counter.Current != null)
			{
				if (now - lastAct > 1.2f)
				{
					lastAct = now;
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
				}
				return StepStatus.Running;
			}
			if (now - arrivedAt < 4f || now - lastAct < 4f)
			{
				return StepStatus.Running;
			}
			lastAct = now;
			FullDayBot.Ps.TryPlayerDismissCurrentClient(false);
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "no seller: visits " + seen + ", " + rings + " bell press(es); " + FullDayBot.Shop();
		return botStep.Soft();
	}

	private BotStep ToBack(string name)
	{
		string how = null;
		bool dragged = false;
		BotStep botStep = Step(name, 25f, delegate(BotStep st)
		{
			int num = FullDayLab.CustItems((GameInventory)(object)FullDayBot.E.backInvinvElement);
			int num2 = FullDayLab.CustItems((GameInventory)(object)FullDayBot.E.frontInvinvElement);
			if (num > 0 && num2 <= 0)
			{
				st.Detail = num + " item(s) on the back counter (" + how + "): " + FullDayLab.Counter();
				return StepStatus.Pass;
			}
			if (how == null)
			{
				StoreTransferButton val = Object.FindObjectOfType<StoreTransferButton>();
				if ((Object)(object)val != (Object)null)
				{
					val.OnClick();
					how = "transfer button (front window visible " + (FullDayBot.E.frontInvWindow != null && FullDayBot.E.frontInvWindow.IsVisible()) + ")";
				}
				else
				{
					how = "no transfer button";
				}
				return StepStatus.Running;
			}
			if (st.Elapsed > 6f && !dragged && num2 > 0)
			{
				int num3 = 0;
				foreach (GameItem item in ((GameInventory)FullDayBot.E.frontInvinvElement).childItems.Where((GameItem i) => i != null && !GeneralHelper.IsItemOwned(i)).ToList())
				{
					if (FullDayBot.Spot((GameInventory)(object)FullDayBot.E.backInvinvElement, item, out var x, out var y) && ItemsMove.Move(item, (GraphNodeStorage)(object)FullDayBot.E.backInvinvElement, x, y, item.modifiedShape.flipped, item.modifiedShape.orientation, -1, rep: false, "fullday") != 0)
					{
						num3++;
					}
				}
				dragged = true;
				how = how + "; then " + num3 + " drag(s)";
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => how + ": " + FullDayLab.Counter() + "; last items reject " + ItemsMove.LastReject?.why;
		return botStep.Soft();
	}

	private static StepStatus Pass(BotStep st, string d)
	{
		st.Detail = d + " after " + FullDayBot.F(st.Elapsed) + " s";
		return StepStatus.Pass;
	}

	private StepStatus PollMark(BotStep st, string mark)
	{
		if (markAsk != 0)
		{
			Dictionary<string, string> dictionary = BotProbes.TakeReply(markAsk);
			if (dictionary != null)
			{
				markAsk = 0;
				markLast = dictionary;
				if (FullDayBot.HasMark(dictionary, mark))
				{
					st.Detail = "server marks " + FullDayBot.V(dictionary, "fd.marks") + " after " + FullDayBot.F(st.Elapsed) + " s";
					return StepStatus.Pass;
				}
				markNext = Time.realtimeSinceStartup + 1f;
			}
			else if (Time.realtimeSinceStartup - markAskedAt > 10f)
			{
				markAsk = 0;
			}
		}
		if (markAsk == 0 && Time.realtimeSinceStartup >= markNext)
		{
			markAsk = BotProbes.AskServer();
			markAskedAt = Time.realtimeSinceStartup;
		}
		st.Detail = "waiting for " + mark + "; server marks " + FullDayBot.V(markLast, "fd.marks");
		return StepStatus.Running;
	}
}
