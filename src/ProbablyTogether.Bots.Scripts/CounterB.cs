using System.Collections.Generic;
using ProbablyTogether.Systems;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CounterB : CounterBot
{
	private int a;

	private int gapQ;

	private int srvQ;

	private string aName = "";

	private long kId;

	private long gapId;

	private long freeId;

	private long jId;

	public override string Name => "counter-b";

	public override BotRole Role => BotRole.Client;

	protected override void Build()
	{
		Joined();
		Do("options", delegate
		{
			EmporiumOption instance = EmporiumOption.Instance;
			if ((Object)(object)instance != (Object)null)
			{
				instance.autoCallNext = false;
				instance.autoNextDialog = false;
			}
			return "autoCallNext off, autoNextDialog off";
		});
		WaitMark("a-holding", 900f, "a:holding");
		Converge("same-talk-1", 15f, CounterBot.Same);
		Do("note-a", delegate
		{
			a = CustomersBot.Int(CounterBot.Holder());
			aName = Net.PlayerName(a);
			v1 = Customers.Visit;
			return "holder " + a + " (" + aName + "), customer " + CustomersBot.Mine("cust.current") + ", visit " + v1;
		});
		Check("a-holds", () => a > 0 && a != CounterBot.Me && CustomersBot.Mine("cust.current") == "scavGeneral", () => "holder " + CounterBot.Holder() + ", me " + CounterBot.Me + ", customer " + CustomersBot.Mine("cust.current"));
		Do("pick", delegate
		{
			GameItem val = CounterBot.OwnedFitting((GameInventory)(object)CounterBot.E.backInvinvElement);
			kId = ((val == null) ? 0 : Ids.Of(val));
			return (val != null) ? (val.identifier + "#" + kId) : "nothing fits the back counter";
		});
		MoveRefusedHere("b-move-refused-here", () => Ids.Find(kId), () => (GameInventory)(object)CounterBot.E.backInvinvElement, () => aName + " is at the counter.");
		Do("skip-own-check", delegate
		{
			ItemsMove.LabSkipClientCheck = true;
			return "lab: this copy's counter check off (to prove the server refuses too)";
		});
		Step("b-move-refused-by-server", 20f, delegate(BotStep st)
		{
			if (srvQ == 0)
			{
				srvQ = ItemsMove.QuickMove(Ids.Find(kId), (GraphNodeStorage)(object)CounterBot.E.backInvinvElement, all: false, -1, "bot");
			}
			if (srvQ == 0)
			{
				st.Detail = "not sent: " + ItemsMove.LastNotice;
				return StepStatus.Fail;
			}
			string text = ItemsMove.Outcome(srvQ);
			if (text == "pending" || text == "")
			{
				return StepStatus.Running;
			}
			st.Detail = "#" + kId + " sent anyway: " + text + ", text '" + ItemsMove.LastReject?.text + "'";
			return (text == "rejected:counter" && ItemsMove.LastReject?.text == aName + " is at the counter.") ? StepStatus.Pass : StepStatus.Fail;
		});
		Do("own-check-on", delegate
		{
			ItemsMove.LabSkipClientCheck = false;
			return "this copy's counter check on again";
		});
		Check("not-moved", delegate
		{
			GameItem obj = Ids.Find(kId);
			return (object)((obj != null) ? obj.parentInventory : null) == CounterBot.E.invElement;
		}, delegate
		{
			string text = kId.ToString();
			GameItem obj = Ids.Find(kId);
			return "#" + text + " in " + Items.RefOf((GraphNodeStorage)(object)((obj != null) ? obj.parentInventory : null));
		});
		HaggleRefusedHere("b-haggle-refused-here", (BargainType)3, 10, () => aName);
		Converge("nothing-changed", 15f, CounterBot.Same);
		Check("still-a", () => CounterBot.Holder() == BotScript.Str(a), () => "holder " + CounterBot.Holder());
		Do("pick-gap", delegate
		{
			GameItem val = CounterBot.OwnedFitting((GameInventory)(object)CounterBot.E.backInvinvElement);
			gapId = ((val == null) ? 0 : Ids.Of(val));
			return (val != null) ? ("ready to move " + val.identifier + "#" + gapId + " the moment A's visit ends") : "nothing fits";
		});
		Mark("b:refused");
		Step("b-gap-move", 240f, delegate(BotStep st)
		{
			if (gapQ == 0)
			{
				if (Customers.Visit == v1 && CustomersBot.Mine("cust.current") != "none")
				{
					return StepStatus.Running;
				}
				if (gapId == 0L)
				{
					st.Detail = "no item picked";
					return StepStatus.Fail;
				}
				Vars["gapCust"] = CustomersBot.Mine("cust.current") + "/holder " + CounterBot.Holder();
				gapQ = ItemsMove.QuickMove(Ids.Find(gapId), (GraphNodeStorage)(object)CounterBot.E.backInvinvElement, all: false, -1, "bot");
				if (gapQ == 0)
				{
					st.Detail = "not sent: " + ItemsMove.LastNotice;
					return StepStatus.Fail;
				}
				return StepStatus.Running;
			}
			string text = ItemsMove.Outcome(gapQ);
			if (text == "pending")
			{
				return StepStatus.Running;
			}
			st.Detail = "sent when the counter showed " + Vars["gapCust"] + "; " + text + "; holder after " + CounterBot.Holder() + ", customer now " + CustomersBot.Mine("cust.current");
			return (text == "applied" && CounterBot.Holder() != BotScript.Str(CounterBot.Me)) ? StepStatus.Pass : StepStatus.Fail;
		}).Soft();
		WaitFor("c2-here", 40f, () => Customers.Visit == v1 + 1 && CustomersBot.Mine("cust.current") == "scavGeneral" && CustomersBot.Mine("cust.arrived") == "1", CustomersBot.ShowMine);
		Converge("same-c2", 15f, "cust.current", "cust.visit", "cust.holder", "ctr.lock", "cust.digest");
		Check("c2-came-free", () => CounterBot.Holder() == "-1", () => "holder " + CounterBot.Holder() + " (the visit end freed the counter; the auto-called customer is nobody's)");
		Do("blackmail", () => (Negotiation.RequestBargain((BargainType)4, 20) ? "bargain.offer Blackmail sent" : "NOT sent") + " (first counter action on customer 2, intent " + CustomersBot.Mine("ctr.intent") + ")");
		WaitFor("b-took-2", 15f, () => CounterBot.Holder() == BotScript.Str(CounterBot.Me), () => "holder " + CounterBot.Holder() + ", me " + CounterBot.Me);
		Converge("same-blackmail", 20f, CounterBot.Same);
		Do("blackmail-outcome", () => "features " + CustomersBot.Mine("ctr.feat") + ", intent " + CustomersBot.Mine("ctr.intent") + ", rep " + CustomersBot.Mine("ctr.rep") + ", line " + DialogueSync.CurrentFp("counter"));
		Mark("b:took-2");
		WaitMark("a-refused", 120f, "a:refused");
		Converge("a-changed-nothing", 15f, CounterBot.Same);
		Do("bell-2", delegate
		{
			Customers.RequestDismiss(callNext: true);
			return "the bell: dismiss " + CustomersBot.Mine("cust.current") + " and call the next (visit " + Customers.Visit + ")";
		});
		WaitFor("gone-2", 20f, () => Customers.Visit > v1 + 1, CustomersBot.ShowMine);
		Mark("b:bell-2");
		WaitMark("a-took-3", 120f, "a:took-3");
		Converge("same-threaten", 20f, CounterBot.Same);
		Check("a-holds-3", () => CounterBot.Holder() == BotScript.Str(a), () => "holder " + CounterBot.Holder());
		MoveRefusedHere("b-move-refused-3", () => CounterBot.OwnedFitting((GameInventory)(object)CounterBot.E.backInvinvElement), () => (GameInventory)(object)CounterBot.E.backInvinvElement, () => aName + " is at the counter.");
		Mark("b:refused-3");
		WaitMark("a-empty", 180f, "a:empty");
		Converge("same-empty", 15f, CounterBot.Same);
		Step("b-free-move", 20f, delegate(BotStep st)
		{
			if (freeId == 0L)
			{
				GameItem val = CounterBot.OwnedFitting((GameInventory)(object)CounterBot.E.backInvinvElement);
				if (val == null)
				{
					st.Detail = "nothing fits the back counter";
					return StepStatus.Fail;
				}
				freeId = Ids.Of(val);
				ItemsMove.QuickMove(val, (GraphNodeStorage)(object)CounterBot.E.backInvinvElement, all: false, -1, "bot");
				return StepStatus.Running;
			}
			GameItem val2 = Ids.Find(freeId);
			if (val2 == null || (object)val2.parentInventory != CounterBot.E.backInvinvElement)
			{
				return StepStatus.Running;
			}
			st.Detail = val2.identifier + "#" + freeId + " -> back counter with nobody at the counter; holder after " + CounterBot.Holder() + " (no lock taken)";
			return (CounterBot.Holder() == "-1" && CustomersBot.Mine("cust.current") == "none") ? StepStatus.Pass : StepStatus.Fail;
		});
		Step("b-free-back", 20f, delegate(BotStep st)
		{
			if (st.Elapsed < 0.05f)
			{
				ItemsMove.QuickMove(Ids.Find(freeId), (GraphNodeStorage)(object)CounterBot.E.invElement, all: false, -1, "bot");
			}
			GameItem val = Ids.Find(freeId);
			if (val == null || (object)val.parentInventory != CounterBot.E.invElement)
			{
				return StepStatus.Running;
			}
			st.Detail = "#" + freeId + " back to the main storage; holder " + CounterBot.Holder();
			return (CounterBot.Holder() == "-1") ? StepStatus.Pass : StepStatus.Fail;
		});
		Converge("same-free", 15f, CounterBot.Same);
		Mark("b:free");
		WaitMark("a-jackson", 240f, "a:jackson");
		WaitFor("at-jackson", 30f, () => CustomersBot.Mine("ctr.loc") == "dr_jackson" && CustomersBot.Mine("cust.current") == "inventor", () => "at " + CustomersBot.Mine("ctr.loc") + ", customer " + CustomersBot.Mine("cust.current"));
		Converge("same-jackson", 20f, "ctr.loc", "cust.current", "ctr.jlock", "cust.holder", "cust.action");
		Step("b-jackson-take", 20f, delegate(BotStep st)
		{
			if (jId == 0L)
			{
				GameItem val = CounterBot.CheapestGoods();
				if (val == null)
				{
					st.Detail = "Dr. Jackson has nothing on the counter";
					return StepStatus.Fail;
				}
				jId = Ids.Of(val);
				Vars["jq"] = BotScript.Str(ItemsMove.QuickMove(val, (GraphNodeStorage)(object)CounterBot.E.backInvinvElement, all: true, -1, "bot"));
				Vars["jname"] = val.identifier + " asking " + val.GetNegociatedValue();
				return StepStatus.Running;
			}
			GameItem val2 = Ids.Find(jId);
			if (val2 == null || (object)val2.parentInventory != CounterBot.E.backInvinvElement)
			{
				return StepStatus.Running;
			}
			st.Detail = Vars["jname"] + " #" + jId + " -> back counter at Dr. Jackson's; outing.jackson " + CustomersBot.Mine("ctr.jlock") + ", counter " + CustomersBot.Mine("cust.holder") + ", me " + CounterBot.Me;
			return (CustomersBot.Mine("ctr.jlock") == BotScript.Str(CounterBot.Me) && CustomersBot.Mine("cust.holder") == "-1") ? StepStatus.Pass : StepStatus.Fail;
		});
		Converge("same-jackson-take", 15f, "ctr.loc", "ctr.jlock", "cust.holder", "cust.action");
		Mark("b:jackson-took");
		WaitMark("a-jackson-refused", 120f, "a:jackson-refused");
		Do("jackson-buy", delegate
		{
			GameItem val = Ids.Find(jId);
			if (val == null)
			{
				return "item gone";
			}
			Vars["cash0"] = CustomersBot.Mine("cash");
			bool flag = Negotiation.RequestDeal("nego.buy", new List<GameItem> { val }, single: true, ignoreBudget: false);
			return (flag ? "nego.buy sent" : "nego.buy NOT sent") + " for #" + jId + " (asking " + val.GetNegociatedValue() + ", cash " + CustomersBot.Mine("cash") + ")";
		});
		WaitFor("jackson-bought", 20f, () => Ids.Find(jId) != null && GeneralHelper.IsItemOwned(Ids.Find(jId)), () => "item #" + jId + " owned " + (Ids.Find(jId) != null && GeneralHelper.IsItemOwned(Ids.Find(jId))) + ", cash " + (Vars.TryGetValue("cash0", out var value) ? value : "?") + " -> " + CustomersBot.Mine("cash")).Soft();
		Converge("same-jackson-buy", 20f, "ctr.loc", "ctr.jlock", "cash", "ctr.nego", "cust.action");
		Mark("b:jackson-done");
		WaitMark("a-return-refused", 60f, "a:return-refused");
		Do("step-away", delegate
		{
			Customers.RequestReleaseCounter();
			return "customers.release sent (outing.jackson holder " + CustomersBot.Mine("ctr.jlock") + ")";
		});
		WaitFor("jackson-freed", 15f, () => CustomersBot.Mine("ctr.jlock") == "-1", () => "outing.jackson holder " + CustomersBot.Mine("ctr.jlock"));
		Mark("b:stepped-away");
		WaitMark("a-home", 120f, "a:home");
		Converge("same-home", 20f, "ctr.loc", "cust.current", "ctr.jlock", "cust.holder", "cash", "cust.action");
		Do("resync", delegate
		{
			Snapshot.RequestResync();
			return "asked for the morning snapshot + today's log";
		});
		WaitFor("reloading", 30f, () => Net.ClientState != ClientState.Ready, () => "state " + Net.ClientState);
		WaitFor("reloaded", 150f, () => Net.IsLive && BotProbes.InShop, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
		Converge("same-after-replay", 30f, "cust.state", "cust.current", "cust.visit", "cust.holder", "ctr.jlock", "ctr.loc", "cash", "ctr.rep", "ctr.flags", "cust.digest", "cust.action");
		Check("no-mismatch", () => Customers.Mismatches == 0 && ItemsMove.ApplyMismatches == 0, () => "customers mismatches " + Customers.Mismatches + ", items apply mismatches " + ItemsMove.ApplyMismatches + "; replay checks ok " + Replay.ChecksOk + " bad " + Replay.ChecksBad).Soft();
		ReportDone();
	}
}
