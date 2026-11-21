using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class NegotiationRealA : NegotiationRealScript
{
	private static readonly System.Reflection.FieldInfo moveBought = HarmonyLib.AccessTools.Field(typeof(EmporiumOption), "moveBoughtItemsToInventory");

	private int v;

	private int sold;

	private long cashBefore;

	public override string Name => "nego3-a";

	private GameItem Stocked(string identifier, int i = 0)
	{
		if (!NegotiationRealProbes.Made.TryGetValue("stock:" + identifier, out var value) || i >= value.Length)
		{
			return null;
		}
		return Negotiation.Find(value[i]);
	}

	private void Op(string op, string arg = null, int n = 1)
	{
		int before = 0;
		Step("op-" + op + ((arg != null) ? ("-" + arg) : ""), 20f, delegate(BotStep st)
		{
			if (NegotiationRealProbes.OpSerial > before && NegotiationRealProbes.Made.ContainsKey(op + ":" + arg))
			{
				st.Detail = NegotiationRealProbes.LastResult;
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		}, delegate
		{
			before = NegotiationRealProbes.OpSerial;
			Net.ToServer("bot.nego.op", new BotNegoOp
			{
				op = op,
				arg = arg,
				n = n
			});
		});
	}

	private void Customer(string id, float talkTimeout = 60f)
	{
		WaitFor("free-" + id, 30f, () => Negotiation.CurrentCustomer() == null, NegotiationRealScript.Show);
		Step("queue-" + id, 20f, (BotStep st) => NegotiationRealScript.Mine("nego.queue").EndsWith(":" + id) ? StepStatus.Pass : StepStatus.Running, delegate
		{
			Net.ToServer("bot.nego.op", new BotNegoOp
			{
				op = "queue",
				arg = id
			});
		});
		Do("call-" + id, delegate
		{
			v = Customers.Visit;
			Customers.RequestCall();
			return "visit " + v + " -> customers.call";
		});
		WaitFor("arrived-" + id, 40f, () => NegotiationRealScript.Mine("nego.cust") == id && NegotiationRealScript.Mine("nego.arrived") == "1" && Locks.HeldBy("counter") == Net.LocalPlayer, NegotiationRealScript.Show);
		WaitFor("lines-" + id, 10f, () => DialogueSystem.Counter?.Current != null, NegotiationRealScript.Show).Soft();
		TalkThrough("talk-" + id, talkTimeout);
	}

	private BotStep TalkThrough(string name, float timeout, Func<bool> done = null)
	{
		float last = -10f;
		int sent = 0;
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			Dialogue val = DialogueSystem.Counter?.Current;
			if (val == null && (done == null || done()))
			{
				st.Detail = sent + " request(s), dialogue over; " + NegotiationRealScript.Show();
				return StepStatus.Pass;
			}
			if (Time.realtimeSinceStartup - last < 0.5f || val == null)
			{
				return StepStatus.Running;
			}
			last = Time.realtimeSinceStartup;
			if (DialogueSystem.Counter.LineUnfinished ? DialogueSync.RequestShown("counter", skip: true) : ((val.choices == null || val.choices.Count <= 0) ? DialogueSync.RequestAdvance("counter") : DialogueSync.RequestChoose("counter", 0)))
			{
				sent++;
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "dialogue still at " + NegotiationRealScript.Mine("nego.dlg") + "; " + NegotiationRealScript.Show();
		return botStep;
	}

	private void ToBack(string label, Func<GameItem> item)
	{
		long id = 0L;
		Step("to-back-" + label, 20f, delegate(BotStep st)
		{
			GameItem val = ((id != 0L) ? Negotiation.Find(id) : item());
			if (val == null)
			{
				st.Detail = "no item";
				return StepStatus.Fail;
			}
			if ((object)val.parentInventory == EmporiumEntry.Instance.backInvinvElement)
			{
				st.Detail = "item " + val.uniqueId + " (" + val.identifier + ") on the back counter here; tablet " + NegotiationRealScript.Mine("nego.tablet") + " (" + NegotiationRealScript.Mine("nego.gate") + ")";
				return StepStatus.Pass;
			}
			if (id == 0L)
			{
				id = val.uniqueId;
				ItemsMove.QuickMove(val, (GraphNodeStorage)(object)EmporiumEntry.Instance.backInvinvElement, all: false);
			}
			return StepStatus.Running;
		}, delegate
		{
			id = 0L;
		});
	}

	protected override void Build()
	{
		Joined();
		WaitMark("b-ready", 120f);
		Step("open", 30f, delegate(BotStep st)
		{
			if (NegotiationRealScript.Mine("nego.state") == "OPEN")
			{
				st.Detail = "the shop is open";
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		}, delegate
		{
			StoreShutterButton.Instance.OnClick();
		});
		Op("unlock");
		Op("stock", "flashlight", 3);
		Op("stock", "energy_credit_hyp");
		Op("stock", "pink_injector");
		Mark("a-stocked");
		Customer("merchant");
		Do("tablet-closed", delegate
		{
			NegociationUIManager.Instance.CloseUI();
			return "tablet closed before the move";
		});
		ToBack("fl0", () => Stocked("flashlight"));
		ToBack("fl1", () => Stocked("flashlight", 1));
		WaitFor("tablet-opened-for-me", 10f, () => NegotiationRealProbes.TabletOpen(), () => "tablet " + NegotiationRealScript.Mine("nego.tablet") + " gate " + NegotiationRealScript.Mine("nego.gate"));
		Do("bargain-discount", delegate
		{
			BargainUIManager.Instance.OfferDiscount(25);
			return "OfferDiscount(25) through the client prefix";
		});
		WaitFor("discount-applied", 20f, () => BargainUIManager.Instance.DiscountOffered, () => BotProbes.Read("nego.bargain"));
		Do("sell", delegate
		{
			sold = NegotiationRealScript.Int(NegotiationRealScript.Mine("nego.sold"));
			List<GameItem> sellableItem = NegociationUIManager.Instance.GetSellableItem();
			NegociationUIManager.Instance.SellMultiple(sellableItem, false);
			return "SellMultiple(" + sellableItem.Count + " items) through the client prefix";
		});
		WaitFor("sold", 20f, () => NegotiationRealScript.Int(NegotiationRealScript.Mine("nego.sold")) >= sold + 2, NegotiationRealScript.Show);
		Converge("same-sell", 15f, NegotiationRealScript.Shop);
		Mark("a-sell");
		ToBack("credit", () => Stocked("energy_credit_hyp"));
		int confirms = 0;
		Do("accept-over-budget", delegate
		{
			confirms = Negotiation.ConfirmsAsked;
			sold = NegotiationRealScript.Int(NegotiationRealScript.Mine("nego.sold"));
			GameItem val = Stocked("energy_credit_hyp");
			PlayerStore.Instance.currentNegociatedItem = val;
			NegociationUIManager.Instance.OnOfferAcceptClick();
			return "OnOfferAcceptClick() -> SellItem() on item " + val.uniqueId + " worth " + (int)val.GetNegociatedValue() + ", budget " + NegotiationRealScript.Mine("nego.budget");
		});
		WaitFor("confirm-asked", 15f, () => Negotiation.ConfirmsAsked > confirms, () => "confirms " + Negotiation.ConfirmsAsked + "; " + NegotiationRealScript.Show());
		Do("confirm-yes", delegate
		{
			bool num3 = NegotiationRealProbes.ConfirmOpen();
			if (num3)
			{
				NegociationUIManager.Instance.OnConfirmPanelYes();
			}
			else
			{
				NegociationUIManager.Instance.SellMultiple(NegociationUIManager.Instance.GetSellableItem(), true);
			}
			return (!num3) ? ("no confirm panel on this headless copy (frame " + NegotiationRealProbes.HasFrame() + "): SellMultiple(ignoreBudget) = what Yes calls") : "the game's confirm panel is open: OnConfirmPanelYes()";
		});
		WaitFor("sold-over-budget", 20f, () => NegotiationRealScript.Int(NegotiationRealScript.Mine("nego.sold")) >= sold + 1, NegotiationRealScript.Show);
		Converge("same-budget", 15f, NegotiationRealScript.Shop);
		Mark("a-budget");
		Do("send-away", delegate
		{
			Customers.RequestDismiss(callNext: false);
			return "customers.dismiss";
		});
		WaitFor("merchant-gone", 30f, () => Negotiation.CurrentCustomer() == null, NegotiationRealScript.Show);
		Customer("merchant");
		Do("markup-75", delegate
		{
			BargainUIManager.Instance.OfferMarkup(75);
			return "OfferMarkup(75): accept chance below 0, walk-out chance ~90%";
		});
		WaitFor("markup-answered", 15f, () => BargainUIManager.Instance.MarkupOffered || Negotiation.CurrentCustomer() == null, NegotiationRealScript.Show);
		TalkThrough("talk-after-markup", 40f);
		Step("walked-out", 40f, delegate(BotStep st)
		{
			StoreClient val = Negotiation.CurrentCustomer();
			if (val == null)
			{
				st.Detail = "the merchant walked out (customers.left / dismissed by the dialogue end action)";
				return StepStatus.Pass;
			}
			if (Customers.Visit == v + 1 && (int)val.clientIntent != 5 && DialogueSystem.Counter?.Current == null && st.Elapsed > 8f)
			{
				st.Detail = "the roll kept the merchant (refused, stayed): sending him away";
				Customers.RequestDismiss(callNext: false);
			}
			return StepStatus.Running;
		}).Soft();
		Converge("same-walkout", 20f, "cash", "nego.cust", "nego.counter", "nego.rep", "nego.hash", "nego.digest");
		Mark("a-walkout");
		Customer("scavGeneral");
		WaitFor("seller-stock", 15f, () => EmporiumEntry.Instance.GetAllNonOwendItemCount() > 0, NegotiationRealScript.Show);
		ToBack("seller-0", () => ((IEnumerable<GameItem>)((GameInventory)EmporiumEntry.Instance.frontInvinvElement).childItems).FirstOrDefault((Func<GameItem, bool>)((GameItem x) => !GeneralHelper.IsItemOwned(x))) ?? ((IEnumerable<GameItem>)((GameInventory)EmporiumEntry.Instance.backInvinvElement).childItems).FirstOrDefault((Func<GameItem, bool>)((GameItem x) => !GeneralHelper.IsItemOwned(x))));
		Do("buy-one", delegate
		{
			cashBefore = PlayerStore.Instance.playerCash;
			List<GameItem> list = (from x in EmporiumEntry.Instance.GetBackInvItems()
				where !GeneralHelper.IsItemOwned(x)
				select x).Take(1).ToList();
			NegociationUIManager.Instance.BuyMultipleItem(list);
			return "BuyMultipleItem(item " + list[0].uniqueId + " worth " + (int)list[0].GetNegociatedValue() + ")";
		});
		WaitFor("bought", 20f, () => PlayerStore.Instance.playerCash < cashBefore, NegotiationRealScript.Show);
		Converge("same-buy", 15f, NegotiationRealScript.Shop);
		Do("buying-discount", delegate
		{
			BargainUIManager instance = BargainUIManager.Instance;
			if (!instance.CanDemandBuyingDiscount())
			{
				return "no buying discount possible with this seller now (" + NegotiationRealScript.Mine("nego.bargain") + ")";
			}
			instance.DemandBuyingDiscount(10);
			return "DemandBuyingDiscount(10) through the client prefix (seeded roll)";
		});
		Delay("roll", 2f);
		Converge("same-after-bargain", 15f, NegotiationRealScript.Shop);
		Mark("a-buy");
		Do("send-away-seller", delegate
		{
			if (Negotiation.CurrentCustomer() != null)
			{
				Customers.RequestDismiss(callNext: false);
			}
			return "customers.dismiss (or he already left)";
		});
		TalkThrough("talk-seller-out", 30f, () => Negotiation.CurrentCustomer() == null).Soft();
		WaitFor("seller-gone", 30f, () => Negotiation.CurrentCustomer() == null, NegotiationRealScript.Show);
		Customer("addictBarter");
		WaitFor("barter-offer", 15f, () => Negotiation.CurrentCustomer()?.barterOffer != null && EmporiumEntry.Instance.GetAllNonOwendItemCount() > 0, NegotiationRealScript.Show);
		int set0 = 0;
		Do("barter-arrow", delegate
		{
			set0 = Negotiation.CurrentCustomer().barterOffer.currentSetIndex;
			AccessTools.Method(typeof(NegociationUIManager), "CycleBarterOfferSet", (Type[])null, (Type[])null).Invoke(NegociationUIManager.Instance, new object[1] { 1 });
			return "CycleBarterOfferSet(+1) through the client prefix from set " + set0 + " of " + Negotiation.CurrentCustomer().barterOffer.OfferSetCount();
		});
		WaitFor("barter-set-moved", 15f, () => Negotiation.CurrentCustomer().barterOffer.currentSetIndex != set0, NegotiationRealScript.Show);
		Converge("same-barter-set", 15f, "nego.set", "nego.counter", "nego.hash", "nego.digest");
		ToBack("injector", () => Stocked("pink_injector"));
		Do("barter-accept", delegate
		{
			NegociationUIManager.Instance.OnOfferAcceptClick();
			return "OnOfferAcceptClick() on a barter customer (acceptable " + Negotiation.CurrentCustomer().barterOffer.IsBarterAcceptable() + ")";
		});
		WaitFor("barter-done", 20f, () => Negotiation.CurrentCustomer() == null || Negotiation.CurrentCustomer().isBusinessComplete, NegotiationRealScript.Show);
		Converge("same-barter", 15f, NegotiationRealScript.Shop);
		Mark("a-barter");
		TalkThrough("talk-barter-out", 30f).Soft();
		Do("send-away-barter", delegate
		{
			if (Negotiation.CurrentCustomer() != null)
			{
				Customers.RequestDismiss(callNext: false);
			}
			return "customers.dismiss (or he already left)";
		});
		WaitFor("barter-gone", 30f, () => Negotiation.CurrentCustomer() == null, NegotiationRealScript.Show);
		Customer("ll_gun_buyer");
		Op("gun");
		Step("gun-on-back", 20f, delegate(BotStep st)
		{
			GunOrderData val = Negotiation.CurrentCustomer()?.gunOrderData;
			if (val == null)
			{
				st.Detail = "no order";
				return StepStatus.Fail;
			}
			if (val.isServiceOrder)
			{
				st.Detail = "service order: the customer's own gun is on the counter";
				return StepStatus.Pass;
			}
			long[] value;
			GameItem val2 = ((NegotiationRealProbes.Made.TryGetValue("gun:", out value) && value.Length != 0) ? Negotiation.Find(value[0]) : null);
			if (val2 == null)
			{
				st.Detail = "no gun made";
				return StepStatus.Fail;
			}
			if ((object)val2.parentInventory == EmporiumEntry.Instance.backInvinvElement)
			{
				st.Detail = "gun " + val2.uniqueId + " on the back counter";
				return StepStatus.Pass;
			}
			if (st.Elapsed < 0.2f)
			{
				ItemsMove.QuickMove(val2, (GraphNodeStorage)(object)EmporiumEntry.Instance.backInvinvElement, all: false);
			}
			return StepStatus.Running;
		});
		long gunId = 0L;
		Do("gun-accept", delegate
		{
			cashBefore = PlayerStore.Instance.playerCash;
			GunOrderData gunOrderData = Negotiation.CurrentCustomer().gunOrderData;
			GameItem obj = GunOrderData.ResolveOrderGun();
			gunId = ((obj != null) ? obj.uniqueId : 0);
			NegociationUIManager.Instance.OnOfferAcceptClick();
			return "OnOfferAcceptClick() on a gun buyer (" + (gunOrderData.isServiceOrder ? "service" : "new gun") + " order), accepted here " + gunOrderData.IsAccepted() + ", gun " + gunId;
		});
		WaitFor("gun-delivered", 20f, () => gunId != 0L && Negotiation.Find(gunId) == null, () => "gun " + gunId + " still here; " + NegotiationRealScript.Show());
		TalkThrough("talk-gun-out", 40f).Soft();
		Converge("same-gun", 15f, NegotiationRealScript.Shop);
		Mark("a-gun");
		Do("send-away-gun", delegate
		{
			if (Negotiation.CurrentCustomer() != null)
			{
				Customers.RequestDismiss(callNext: false);
			}
			return "customers.dismiss (or he already left)";
		});
		WaitFor("gun-gone", 30f, () => Negotiation.CurrentCustomer() == null, NegotiationRealScript.Show);
		Do("tablet-closed-2", delegate
		{
			NegociationUIManager.Instance.CloseUI();
			return "tablet closed";
		});
		Customer("shadyCustomerInformation");
		WaitFor("info-tablet-opened-for-me", 15f, () => NegotiationRealProbes.TabletOpen(), () => "tablet " + NegotiationRealScript.Mine("nego.tablet") + " gate " + NegotiationRealScript.Mine("nego.gate"));
		Do("info-accept", delegate
		{
			cashBefore = PlayerStore.Instance.playerCash;
			NegociationUIManager.Instance.OnOfferAcceptClick();
			return "OnOfferAcceptClick() on an information dealer (fee " + Negotiation.CurrentCustomer().serviceCost + ")";
		});
		WaitFor("info-paid", 20f, () => PlayerStore.Instance.playerCash < cashBefore, NegotiationRealScript.Show);
		TalkThrough("talk-info-out", 40f).Soft();
		Converge("same-info", 15f, NegotiationRealScript.Shop);
		Mark("a-info");
		Do("send-away-info", delegate
		{
			if (Negotiation.CurrentCustomer() != null)
			{
				Customers.RequestDismiss(callNext: false);
			}
			return "customers.dismiss (or he already left)";
		});
		WaitFor("info-gone", 30f, () => Negotiation.CurrentCustomer() == null, NegotiationRealScript.Show);
		Customer("waterHishonestSellerScav");
		WaitFor("water-stock", 15f, () => EmporiumEntry.Instance.GetAllNonOwendItemCount() > 0, NegotiationRealScript.Show);
		long confronted = 0L;
		Do("confront-strip", delegate
		{
			WaterStripUIManager strip = WaterStripUIManager.Instance;
			if ((Object)(object)strip == (Object)null)
			{
				return "no test strip panel in this scene";
			}
			GameItem val = ((GameInventory)EmporiumEntry.Instance.frontInvinvElement).childItems.Concat(((GameInventory)EmporiumEntry.Instance.backInvinvElement).childItems).FirstOrDefault((Func<GameItem, bool>)((GameItem x) => !GeneralHelper.IsItemOwned(x) && WaterFeatureHelper.GetWaterFeature(x) != null && strip.CanConfront(x)));
			if (val == null)
			{
				return "no mislabeled bottle this visit (the seller's rolls): nothing to confront";
			}
			confronted = val.uniqueId;
			strip.currentWaterContainer = val;
			strip.OnConfront();
			return "WaterStripUIManager.OnConfront() on " + val.identifier + " #" + val.uniqueId + " through the client prefix";
		});
		WaitFor("confronted", 15f, delegate
		{
			if (confronted != 0L)
			{
				GameItem obj = Negotiation.Find(confronted);
				if (obj == null)
				{
					return false;
				}
				return obj.IsTag("ALREADY_CONFRONTED_TAG");
			}
			return true;
		}, delegate
		{
			string text = confronted.ToString();
			GameItem obj = Negotiation.Find(confronted);
			return "item " + text + " confronted " + (obj != null && obj.IsTag("ALREADY_CONFRONTED_TAG"));
		});
		TalkThrough("talk-confront", 30f).Soft();
		long scanned = 0L;
		Do("confront-scanner", delegate
		{
			GameItem val = ((GameInventory)EmporiumEntry.Instance.frontInvinvElement).childItems.Concat(((GameInventory)EmporiumEntry.Instance.backInvinvElement).childItems).FirstOrDefault((Func<GameItem, bool>)((GameItem x) => !GeneralHelper.IsItemOwned(x) && WaterFeatureHelper.GetWaterFeature(x) != null && WaterScannerUIManager.CanConfront(x)));
			if (val == null)
			{
				return "no second mislabeled bottle this visit: scanner path not exercised";
			}
			scanned = val.uniqueId;
			WaterScannerUIManager.ConfrontWater(val, ((Object)(object)WaterScannerUIManager.Instance != (Object)null) ? WaterScannerUIManager.Instance.confrontButton : null);
			return "WaterScannerUIManager.ConfrontWater(" + val.identifier + " #" + val.uniqueId + ") through the client prefix";
		});
		WaitFor("scanner-confronted", 15f, delegate
		{
			if (scanned != 0L)
			{
				GameItem obj = Negotiation.Find(scanned);
				if (obj == null)
				{
					return false;
				}
				return obj.IsTag("ALREADY_CONFRONTED_TAG");
			}
			return true;
		}, delegate
		{
			string text = scanned.ToString();
			GameItem obj = Negotiation.Find(scanned);
			return "item " + text + " confronted " + (obj != null && obj.IsTag("ALREADY_CONFRONTED_TAG"));
		}).Soft();
		TalkThrough("talk-confront-2", 30f).Soft();
		Converge("same-confront", 15f, NegotiationRealScript.Shop);
		Do("wholesale", delegate
		{
			StoreClient val = Negotiation.CurrentCustomer();
			if (val == null || !val.isOfferWholesale)
			{
				return "no wholesale offer";
			}
			cashBefore = PlayerStore.Instance.playerCash;
			int wholesalePrice = PlayerStore.Instance.GetWholesalePrice();
			// Set this PC's "move bought items to inventory" opposite to the server's; the purchase must follow the buyer's setting.
			string flip = "";
			if ((Object)(object)EmporiumOption.Instance != (Object)null && moveBought != null)
			{
				moveBought.SetValue(EmporiumOption.Instance, !EmporiumOption.MoveBoughtItemsToInventory);
				flip = "; my 'move bought items' now " + EmporiumOption.MoveBoughtItemsToInventory;
			}
			NegociationUIManager.Instance.OnWholesaleClick();
			return "OnWholesaleClick() through the client prefix: price " + wholesalePrice + ", cash " + cashBefore + flip;
		});
		WaitFor("wholesale-answered", 15f, () => EmporiumEntry.Instance.GetAllNonOwendItemCount() == 0 || PlayerStore.Instance.playerCash == cashBefore, NegotiationRealScript.Show).Soft();
		Delay("wholesale-settle", 1.5f);
		Converge("same-water", 15f, NegotiationRealScript.Shop);
		Mark("a-water");
		Do("send-away-water", delegate
		{
			if (Negotiation.CurrentCustomer() != null)
			{
				Customers.RequestDismiss(callNext: false);
			}
			return "customers.dismiss (or he already left)";
		});
		TalkThrough("talk-water-out", 30f, () => Negotiation.CurrentCustomer() == null).Soft();
		WaitFor("water-gone", 30f, () => Negotiation.CurrentCustomer() == null, NegotiationRealScript.Show);
		Op("label");
		int clicks = 0;
		Do("label-panel", delegate
		{
			clicks = Negotiation.LabelerClicks;
			GameItem it = Negotiation.Find(NegotiationProbes.LabelItem);
			LabelerUIManager instance = LabelerUIManager.Instance;
			instance.OpenUI(it);
			LabelerElement? obj = ((IEnumerable<LabelerElement>)instance.verticalLayoutGroup.GetComponentsInChildren<LabelerElement>(true)).FirstOrDefault((Func<LabelerElement, bool>)((LabelerElement x) => Negotiation.ElementFeature(x) == it.itemFeatures.First((ItemFeature f) => f.category == "CATEGORY_RUSTED")));
			if ((Object)(object)obj == (Object)null)
			{
				throw new InvalidOperationException("no labeler row for the rusted label");
			}
			obj.dropdown.SetValueWithoutNotify(0);
			obj.OnDropdownValueChanged();
			return "labeler panel, dropdown -> " + ItemConditionList.conditionRusted[0].identifier + ", OnDropdownValueChanged() through the client prefix";
		});
		WaitFor("label-applied", 20f, () => BotProbes.Read("nego.label").StartsWith("fake " + ItemConditionList.conditionRusted[0].identifier, StringComparison.Ordinal), () => BotProbes.Read("nego.label"));
		Check("labeler-click-for-me", () => Negotiation.LabelerClicks == clicks + 1, () => "labeler clicks " + clicks + " -> " + Negotiation.LabelerClicks);
		Do("labeler-close", delegate
		{
			LabelerUIManager.Instance.CloseUI();
			return "closed";
		});
		Do("name", delegate
		{
			GameItem val = Negotiation.Find(NegotiationProbes.LabelItem);
			LabelerBlueUIManager instance = LabelerBlueUIManager.Instance;
			instance.OpenUI(val);
			instance.inputField.text = "Bot shelf";
			instance.OnEditEnd("Bot shelf");
			instance.CloseUI();
			return "blue labeler, OnEditEnd('Bot shelf') through the client prefix";
		});
		WaitFor("named", 20f, () => BotProbes.Read("nego.name") == "Bot shelf", () => BotProbes.Read("nego.name"));
		Converge("same-label", 15f, "nego.label", "nego.name", "nego.guids", "nego.hash");
		Mark("a-label");
		Customer("scavAppraisal");
		WaitFor("appraisal-items-1", 15f, () => EmporiumEntry.Instance.GetAllNonOwendItemCount() > 0, NegotiationRealScript.Show);
		long appraised = 0L;
		int pat0 = 0;
		Do("estimate-perfect", delegate
		{
			cashBefore = PlayerStore.Instance.playerCash;
			pat0 = Negotiation.CurrentCustomer()?.clientImpatience ?? 0;
			GameItem val = ((GameInventory)EmporiumEntry.Instance.frontInvinvElement).childItems.Concat(((GameInventory)EmporiumEntry.Instance.backInvinvElement).childItems).FirstOrDefault((Func<GameItem, bool>)((GameItem x) => x.IsTag("APPRAISAL_ITEM") && !GeneralHelper.IsItemOwned(x)));
			if (val == null)
			{
				return "no appraisal item";
			}
			appraised = val.uniqueId;
			int correctBaseValue = AppraisalHelper.GetCorrectBaseValue(val);
			AppraisalServiceHelper.OnEstimate(val, correctBaseValue);
			return "OnEstimate(item " + val.uniqueId + ", " + correctBaseValue + ") through the client prefix";
		});
		WaitFor("estimate-answered", 15f, () => Negotiation.Find(appraised) == null || (Negotiation.CurrentCustomer()?.clientImpatience ?? 0) > pat0, () => "cash " + PlayerStore.Instance.playerCash + " patience " + NegotiationRealScript.Mine("nego.patience") + "; " + NegotiationRealScript.Show());
		Check("estimate-accepted", () => Negotiation.Find(appraised) == null && PlayerStore.Instance.playerCash > cashBefore, () => "cash " + cashBefore + " -> " + PlayerStore.Instance.playerCash + ", counter " + NegotiationRealScript.Mine("nego.counter")).Soft();
		TalkThrough("talk-estimate", 30f).Soft();
		Converge("same-estimate", 20f, "cash", "nego.cust", "nego.counter", "nego.career", "nego.hash", "nego.digest");
		Mark("a-estimate");
		Do("send-away-appraisal", delegate
		{
			if (Negotiation.CurrentCustomer() != null)
			{
				Customers.RequestDismiss(callNext: false);
			}
			return "customers.dismiss (or he already left)";
		});
		TalkThrough("talk-appraisal-out", 30f, () => Negotiation.CurrentCustomer() == null).Soft();
		WaitFor("appraisal-gone", 30f, () => Negotiation.CurrentCustomer() == null, NegotiationRealScript.Show);
		Customer("scavAppraisal");
		WaitFor("appraisal-items", 15f, () => EmporiumEntry.Instance.GetAllNonOwendItemCount() > 0, NegotiationRealScript.Show);
		for (int num = 0; num < 3; num++)
		{
			int num2 = num;
			int pat1 = 0;
			Do("estimate-low-" + num2, delegate
			{
				pat1 = Negotiation.CurrentCustomer()?.clientImpatience ?? (-1);
				GameItem val = ((GameInventory)EmporiumEntry.Instance.frontInvinvElement).childItems.Concat(((GameInventory)EmporiumEntry.Instance.backInvinvElement).childItems).FirstOrDefault((Func<GameItem, bool>)((GameItem x) => x.IsTag("APPRAISAL_ITEM") && !GeneralHelper.IsItemOwned(x)));
				if (val == null)
				{
					return "no appraisal item left";
				}
				AppraisalServiceHelper.OnEstimate(val, 1);
				return "OnEstimate(item " + val.uniqueId + ", 1) through the client prefix, patience " + pat1;
			});
			WaitFor("estimate-answered-" + num2, 15f, () => Negotiation.CurrentCustomer() == null || Negotiation.CurrentCustomer().clientImpatience > pat1, () => "patience " + NegotiationRealScript.Mine("nego.patience"));
			TalkThrough("talk-estimate-" + num2, 30f).Soft();
		}
		WaitFor("appraisal-walked-out", 40f, () => Negotiation.CurrentCustomer() == null, NegotiationRealScript.Show).Soft();
		Converge("same-appraisal", 20f, "cash", "nego.cust", "nego.counter", "nego.career", "nego.hash", "nego.digest");
		Mark("a-walkout2");
		Op("rank");
		string lowId = null;
		Do("block-type", delegate
		{
			AppraisalItemType lowestRankType = AppraisalItemRegistry.GetLowestRankType();
			lowId = lowestRankType.id;
			AccessTools.Method(typeof(AppraisalItemsWindow), "SetAllowed", (Type[])null, (Type[])null).Invoke(null, new object[2] { lowestRankType, false });
			return "AppraisalItemsWindow.SetAllowed(" + lowestRankType.id + ", false) through the client prefix at rank " + AppraisalRankHelper.GetRank();
		});
		WaitFor("blocked", 20f, () => PlayerStore.Instance.appraisalBlacklist != null && PlayerStore.Instance.appraisalBlacklist.Contains(lowId), () => BotProbes.Read("nego.career"));
		Converge("same-block", 15f, "nego.career", "nego.hash");
		Check("no-resync-asked", () => Replay.ChecksBad == 0, () => "seeded checks ok " + Replay.ChecksOk + " bad " + Replay.ChecksBad + " (" + Replay.LastMismatch + ")").Soft();
		Mark("a-done");
		WaitMark("b-done", 240f);
	}
}
