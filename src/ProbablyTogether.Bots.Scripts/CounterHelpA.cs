using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CounterHelpA : CounterHelpBot
{
	private readonly Dictionary<string, string> before = new Dictionary<string, string>();

	private string lastNegoRefusal = "";

	private long buyId;

	public override string Name => "counterhelp-a";

	public override BotRole Role => BotRole.Client;

	private BotStep NoteDeal(string name)
	{
		return Do(name, delegate
		{
			before.Clear();
			string[] dealState = CounterHelpBot.DealState;
			foreach (string text in dealState)
			{
				before[text] = CustomersBot.Mine(text);
			}
			return string.Join(" ", CounterHelpBot.DealState.Select((string p) => p + "=" + before[p]));
		});
	}

	private BotStep Unchanged(string name)
	{
		return Check(name, () => CounterHelpBot.DealState.All((string p) => CustomersBot.Mine(p) == before[p]), () => string.Join("; ", from p in CounterHelpBot.DealState
			where CustomersBot.Mine(p) != before[p]
			select p + " " + before[p] + " -> " + CustomersBot.Mine(p)) + " (empty = unchanged)");
	}

	protected override void Build()
	{
		Joined();
		Do("listen", delegate
		{
			Negotiation.Refused += delegate(string k, string r)
			{
				lastNegoRefusal = k + ": " + r;
			};
			return "negotiation refusals are logged";
		});
		PollServer("both-in", 900f, (Dictionary<string, string> s) => CustomersBot.Int(s["cust.ready"]) >= 2);
		Do("options", delegate
		{
			EmporiumOption instance = EmporiumOption.Instance;
			if ((Object)(object)instance != (Object)null)
			{
				instance.autoCallNext = false;
				instance.autoNextDialog = false;
			}
			return "autoCallNext off, autoNextDialog off (the bot paces)";
		});
		Do("open-shutter", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "clicked the shutter (state " + CustomersBot.Mine("cust.state") + ")";
		});
		WaitFor("open-here", 30f, () => CustomersBot.Mine("cust.state") == "OPEN", CustomersBot.ShowMine);
		Mark("a:open");
		WaitMark("queued", 60f, "s:queued");
		PollServer("seller-first", 30f, (Dictionary<string, string> s) => s["cust.head"] == "scavGeneral");
		NegoOp("bargain-flag", "unlock");
		WaitFor("flags", 20f, () => CustomersBot.Mine("ctr.flags").StartsWith("b"), () => "flags " + CustomersBot.Mine("ctr.flags"));
		Do("call", delegate
		{
			v1 = Customers.Visit;
			Customers.RequestCall();
			return "visit " + v1 + ", head " + CustomersBot.Mine("cust.head");
		});
		WaitFor("a-takes", 15f, () => CustomersBot.Mine("cust.current") == "scavGeneral" && CounterBot.Holder() == BotScript.Str(CounterBot.Me), () => "customer " + CustomersBot.Mine("cust.current") + ", holder " + CounterBot.Holder() + ", me " + CounterBot.Me);
		WaitFor("arrived", 30f, () => CustomersBot.Mine("cust.arrived") == "1", CustomersBot.ShowMine);
		PaceTalk("talk-1", 60f, needStart: true);
		Mark("a:want-goods");
		WaitMark("goods-placed", 60f, "s:goods");
		WaitFor("goods-here", 20f, () => CustomersBot.Mine("help.goods").Split(' ').All((string r) => r.Contains("@counter")), () => CustomersBot.Mine("help.goods"));
		Converge("same-goods", 20f, "help.goods", "cust.stock", "ctr.nego", "cust.digest", "cust.action");
		NoteDeal("deal-before");
		Do("notices-before", delegate
		{
			Vars["notices0"] = BotScript.Str(Notices.Shown);
			return "game notices shown so far " + Vars["notices0"] + " (last '" + Notices.Last + "')";
		});
		Mark("a:holding");
		WaitMark("b-checked", 400f, "b:checked");
		Check("five-lines", () => CounterHelp.FromOthers >= 5 && new string[5] { "look", "scan", "chem", "strip", "seed" }.All((string w) => CounterHelp.Whats.Values.Contains(w)), () => "help.checked from others " + CounterHelp.FromOthers + ", kinds " + string.Join(",", CounterHelp.Whats.Values));
		Check("results-match-mine", () => CounterHelp.ResMismatch == 0, () => "results that differ from this copy's own computation: " + CounterHelp.ResMismatch);
		Check("shown-in-chat", () => CounterHelp.ChatShown >= 5 && Chat.Lines.Any((Chat.Line l) => l.Text == Kit.Safe(CounterHelp.LastText)), () => "chat lines " + CounterHelp.ChatShown + ", last '" + CounterHelp.LastText + "', in chat " + Chat.Lines.Any((Chat.Line l) => l.Text == Kit.Safe(CounterHelp.LastText)));
		Check("no-game-notice", () => BotScript.Str(Notices.Shown) == Vars["notices0"] && Notices.LastSource != "help", () => "game notices " + Vars["notices0"] + " -> " + Notices.Shown + " (last '" + Notices.Last + "' from '" + Notices.LastSource + "'): B's checks must leave the game's notifier alone");
		Do("line-texts", () => "last line: " + CounterHelp.LastText + " | views here " + CounterHelp.ViewsText(CounterHelp.Views));
		Converge("same-views", 20f, "help.views", "help.count", "help.strips");
		Unchanged("deal-unchanged");
		Converge("same-after-checks", 15f, CounterHelpBot.DealState);
		Mark("a:verified");
		WaitMark("b-refused", 200f, "b:refused");
		Converge("same-after-refusals", 15f, CounterHelpBot.DealState);
		Unchanged("deal-unchanged-2");
		Check("still-holding", () => CounterBot.Holder() == BotScript.Str(CounterBot.Me), () => "holder " + CounterBot.Holder());
		Check("seller-still-here", () => CustomersBot.Mine("cust.current") == "scavGeneral", () => "customer " + CustomersBot.Mine("cust.current") + " (want scavGeneral)");
		Step("step-away", 20f, delegate(BotStep st)
		{
			if (st.Elapsed < 0.05f)
			{
				Customers.RequestReleaseCounter();
				return StepStatus.Running;
			}
			if (CounterBot.Holder() != "-1")
			{
				return StepStatus.Running;
			}
			st.Detail = "customers.release: the counter is free with " + CustomersBot.Mine("cust.current") + " still there";
			return (CustomersBot.Mine("cust.current") == "scavGeneral") ? StepStatus.Pass : StepStatus.Fail;
		});
		Mark("a:released");
		WaitMark("b-free-check", 120f, "b:free-check");
		Check("nobody-took-it", () => CustomersBot.Mine("cust.current") == "scavGeneral" && CounterBot.Holder() == "-1", () => "holder " + CounterBot.Holder() + ", customer " + CustomersBot.Mine("cust.current") + " after B's check with the counter free");
		WaitMark("b-resynced", 300f, "b:resynced");
		WaitMark("b-water2-panels", 120f, "b:water2-panels");
		Step("to-back", 20f, delegate(BotStep st)
		{
			GameItem val = CounterHelpProbes.LabItem("water2");
			if (val == null)
			{
				st.Detail = "no second bottle on the counter";
				return StepStatus.Fail;
			}
			if (buyId == 0L)
			{
				buyId = Ids.Of(val);
				Vars["buy"] = val.identifier + "#" + buyId + " asking " + val.GetNegociatedValue();
				if ((object)val.parentInventory != CounterBot.E.backInvinvElement)
				{
					ItemsMove.QuickMove(val, (GraphNodeStorage)(object)CounterBot.E.backInvinvElement, all: true, -1, "bot");
					return StepStatus.Running;
				}
			}
			if ((object)val.parentInventory != CounterBot.E.backInvinvElement)
			{
				return StepStatus.Running;
			}
			st.Detail = Vars["buy"] + " on the back counter; holder " + CounterBot.Holder();
			return StepStatus.Pass;
		});
		Do("buy", delegate
		{
			GameItem val = Ids.Find(buyId);
			Vars["cash0"] = CustomersBot.Mine("cash");
			bool flag = val != null && Negotiation.RequestDeal("nego.buy", new List<GameItem> { val }, single: true, ignoreBudget: false);
			return (flag ? "nego.buy sent" : "nego.buy NOT sent") + " for " + Vars["buy"] + ", cash " + Vars["cash0"];
		});
		WaitFor("bought", 20f, () => Ids.Find(buyId) != null && GeneralHelper.IsItemOwned(Ids.Find(buyId)) && CustomersBot.Int(CustomersBot.Mine("cash")) < CustomersBot.Int(Vars["cash0"]), () => "#" + buyId + " owned " + (Ids.Find(buyId) != null && GeneralHelper.IsItemOwned(Ids.Find(buyId))) + ", cash " + Vars["cash0"] + " -> " + CustomersBot.Mine("cash") + ", last nego refusal " + lastNegoRefusal);
		Converge("same-buy", 20f, CounterHelpBot.DealState);
		Check("bought-into-storage", () => CounterBot.Holder() == BotScript.Str(CounterBot.Me) && !ItemsUse.InCounter(Ids.Find(buyId)) && CustomersBot.Mine("cust.current") == "scavGeneral", () => "holder " + CounterBot.Holder() + ", bottle on the counter " + ItemsUse.InCounter(Ids.Find(buyId)) + " (the game's BuyItem puts it in our storage), customer " + CustomersBot.Mine("cust.current"));
		Mark("a:bought-water2");
		WaitMark("b-saw-storage", 60f, "b:saw-storage");
		Step("to-counter-to-sell", 20f, delegate(BotStep st)
		{
			GameItem val = Ids.Find(buyId);
			if (val == null)
			{
				st.Detail = "the bottle is gone";
				return StepStatus.Fail;
			}
			if (st.Elapsed < 0.05f)
			{
				ItemsMove.QuickMove(val, (GraphNodeStorage)(object)CounterBot.E.backInvinvElement, all: true, -1, "bot");
				return StepStatus.Running;
			}
			if ((object)val.parentInventory != CounterBot.E.backInvinvElement)
			{
				return StepStatus.Running;
			}
			st.Detail = "our " + val.identifier + "#" + buyId + " back on the back counter; holder " + CounterBot.Holder();
			return StepStatus.Pass;
		});
		Check("holds-with-ours-on-counter", () => CounterBot.Holder() == BotScript.Str(CounterBot.Me) && ItemsUse.InCounter(Ids.Find(buyId)) && GeneralHelper.IsItemOwned(Ids.Find(buyId)) && CustomersBot.Mine("cust.current") == "scavGeneral", () => "holder " + CounterBot.Holder() + ", our bottle on the counter " + ItemsUse.InCounter(Ids.Find(buyId)) + ", customer " + CustomersBot.Mine("cust.current"));
		Mark("a:water2-on-counter");
		WaitMark("b-owned-checked", 120f, "b:owned-checked");
		Step("water2-home", 20f, delegate(BotStep st)
		{
			GameItem val = Ids.Find(buyId);
			if (val == null)
			{
				st.Detail = "the bottle is gone";
				return StepStatus.Fail;
			}
			if (st.Elapsed < 0.05f)
			{
				ItemsMove.QuickMove(val, (GraphNodeStorage)(object)CounterBot.E.invElement, all: true, -1, "bot");
				return StepStatus.Running;
			}
			if (ItemsUse.InCounter(val))
			{
				return StepStatus.Running;
			}
			string[] obj = new string[8]
			{
				"our ",
				val.identifier,
				"#",
				buyId.ToString(),
				" back in storage; label ",
				null,
				null,
				null
			};
			ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(val);
			obj[5] = ((waterFeature != null && waterFeature.IsExposed()) ? "matched" : "as bought");
			obj[6] = "; holder ";
			obj[7] = CounterBot.Holder();
			st.Detail = string.Concat(obj);
			return StepStatus.Pass;
		});
		WaitMark("b-panels-open", 120f, "b:panels-open");
		Do("haggle", delegate
		{
			BargainUIManager instance = BargainUIManager.Instance;
			Vars["nego-h"] = CustomersBot.Mine("ctr.nego");
			return ((Object)(object)instance == (Object)null || !instance.CanDemandBuyingDiscount()) ? "no buying discount possible with this seller now (skipped)" : ((Negotiation.RequestBargain((BargainType)3, 10) ? "bargain.offer BuyingDiscount 10 sent" : "NOT sent") + " (last nego refusal " + lastNegoRefusal + ")");
		});
		Delay("haggle-settles", 3f);
		Converge("same-haggle", 20f, "cust.current", "cust.holder", "ctr.nego", "ctr.feat", "cust.dlgfp", "cust.digest", "cust.action");
		Do("haggle-outcome", () => "nego " + Vars["nego-h"] + " -> " + CustomersBot.Mine("ctr.nego") + ", features " + CustomersBot.Mine("ctr.feat") + ", line " + CustomersBot.Mine("cust.dlgfp") + ", customer " + CustomersBot.Mine("cust.current"));
		PaceTalk("talk-haggle", 60f, needStart: false).Soft();
		Step("dismiss", 30f, delegate(BotStep st)
		{
			if (CustomersBot.Mine("cust.current") == "none")
			{
				st.Detail = "the seller is gone; counter " + CounterBot.Holder();
				if (!(CounterBot.Holder() == "-1"))
				{
					return StepStatus.Running;
				}
				return StepStatus.Pass;
			}
			if (st.Elapsed < 0.05f || (st.Elapsed > 8f && st.Elapsed < 8.05f))
			{
				Customers.RequestDismiss(callNext: false);
			}
			return StepStatus.Running;
		});
		Converge("same-end", 20f, "cust.current", "cust.holder", "cash", "cust.stock", "ctr.nego", "cust.digest", "cust.action", "help.strips");
		Check("no-mismatch", () => Customers.Mismatches == 0 && ItemsUse.MismatchCount == 0, () => "customers mismatches " + Customers.Mismatches + ", items-use mismatches " + ItemsUse.MismatchCount).Soft();
		ReportDone();
	}
}
