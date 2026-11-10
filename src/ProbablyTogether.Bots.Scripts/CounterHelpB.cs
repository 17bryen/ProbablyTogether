using System;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CounterHelpB : CounterHelpBot
{
	private int a;

	private string aName = "";

	private int refused0;

	private string lastNegoRefusal = "";

	public override string Name => "counterhelp-b";

	public override BotRole Role => BotRole.Client;

	private BotStep CheckWith(string what, string toolId, string role, bool expectHolderA = true, string suffix = "")
	{
		int ref0 = 0;
		int recv0 = 0;
		long seq = 0L;
		BotStep botStep = Step("check-" + what + (expectHolderA ? "" : "-free") + suffix, 30f, delegate(BotStep st)
		{
			if (ItemsUse.RefusedCount > ref0)
			{
				st.Detail = "REFUSED: " + ItemsUse.LastRefusal;
				return StepStatus.Fail;
			}
			if (Vars["sent-" + what] != "1")
			{
				st.Detail = "not sent: " + Vars["why-" + what];
				return StepStatus.Fail;
			}
			if (seq == 0L)
			{
				if (CounterHelp.Received <= recv0 || CounterHelp.LastBy != CounterBot.Me || CounterHelp.LastWhat != what)
				{
					return StepStatus.Running;
				}
				seq = CounterHelp.LastSeq;
			}
			GameItem val = CounterHelpProbes.LabItem(role);
			string text = CounterHelp.PanelText(what, val);
			if (text == null)
			{
				if (!(st.Elapsed < 10f))
				{
					return Fail(st, "help.checked #" + seq + " came back but the " + what + " panel is not open on the item here");
				}
				return StepStatus.Running;
			}
			CounterHelpProbes.RealViews[seq] = text;
			st.Detail = toolId + " on the customer's " + val.identifier + "#" + val.uniqueId + ": use #" + seq + ", panel '" + CounterHelpBot.Flat(text) + "', result '" + CounterHelp.LastRes + "', holder " + CounterBot.Holder() + (expectHolderA ? (" (" + aName + ")") : " (free)") + ", refusals here " + ItemsUse.RefusedCount;
			string text2 = (expectHolderA ? BotScript.Str(a) : "-1");
			return (CounterBot.Holder() == text2) ? StepStatus.Pass : StepStatus.Fail;
		}, delegate
		{
			ref0 = ItemsUse.RefusedCount;
			recv0 = CounterHelp.Received;
			seq = 0L;
			GameItem val = CounterHelpBot.Tool(toolId);
			GameItem val2 = CounterHelpProbes.LabItem(role);
			Vars["why-" + what] = ((val == null) ? ("no " + toolId + " of ours off the counter") : ((val2 == null) ? ("no " + role + " on the counter") : ""));
			Vars["sent-" + what] = ((val != null && val2 != null && ItemsUse.RequestTarget(val, val2)) ? "1" : "0");
		});
		botStep.TimeoutDetail = () => "no help.checked for " + what + " (received " + CounterHelp.Received + ", last by " + CounterHelp.LastBy + " " + CounterHelp.LastWhat + "); refusals " + ItemsUse.RefusedCount + " " + ItemsUse.LastRefusal;
		return botStep;
	}

	private static StepStatus Fail(BotStep st, string why)
	{
		st.Detail = why;
		return StepStatus.Fail;
	}

	private BotStep Close(string what, string role)
	{
		return Do("close-" + what, () => (CounterHelp.ClosePanel(what, CounterHelpProbes.LabItem(role), onlyIfShowing: false) ? "closed" : "no panel open") + " the " + what + " panel");
	}

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
		Do("listen", delegate
		{
			Negotiation.Refused += delegate(string k, string r)
			{
				lastNegoRefusal = k + ": " + r;
			};
			return "negotiation refusals are logged";
		});
		WaitMark("a-holding", 900f, "a:holding");
		Converge("same-goods", 20f, "help.goods", "cust.stock", "ctr.nego", "cust.digest", "cust.action");
		Do("note-a", delegate
		{
			a = CustomersBot.Int(CounterBot.Holder());
			aName = Net.PlayerName(a);
			refused0 = ItemsUse.RefusedCount;
			return "holder " + a + " (" + aName + "), customer " + CustomersBot.Mine("cust.current") + "; goods " + CustomersBot.Mine("help.goods");
		});
		Check("a-holds", () => a > 0 && a != CounterBot.Me, () => "holder " + CounterBot.Holder() + ", me " + CounterBot.Me);
		Check("tools-here", () => new string[6] { "magnifier", "aquascan", "chem_scanner", "water_test_strip", "plant_analyser", "labeler_blue" }.All((string id) => CounterHelpBot.Tool(id) != null), () => string.Join(", ", new string[6] { "magnifier", "aquascan", "chem_scanner", "water_test_strip", "plant_analyser", "labeler_blue" }.Select((string id) => id + " " + (CounterHelpBot.Tool(id) != null))));
		CheckWith("look", "magnifier", "cig");
		Close("look", "cig");
		CheckWith("scan", "aquascan", "water");
		Close("scan", "water");
		CheckWith("chem", "chem_scanner", "water");
		Close("chem", "water");
		CheckWith("strip", "water_test_strip", "water");
		Close("strip", "water");
		CheckWith("seed", "plant_analyser", "seed");
		Close("seed", "seed");
		Check("never-refused", () => ItemsUse.RefusedCount == refused0, () => "items-use refusals " + refused0 + " -> " + ItemsUse.RefusedCount + " (" + ItemsUse.LastRefusal + ")");
		Check("no-self-line", () => CounterHelp.FromOthers == 0 && CounterHelp.ChatShown == 0, () => "lines about others " + CounterHelp.FromOthers + ", shown " + CounterHelp.ChatShown);
		Check("strip-spent", () => CustomersBot.Mine("help.strips") == "1", () => "spent strips " + CustomersBot.Mine("help.strips"));
		Converge("panels-equal-server", 20f, "help.views", "help.count", "help.strips");
		Check("still-a", () => CounterBot.Holder() == BotScript.Str(a), () => "holder " + CounterBot.Holder());
		Mark("b:checked");
		WaitMark("a-verified", 200f, "a:verified");
		CheckWith("look", "magnifier", "cig", expectHolderA: true, "-again");
		Do("tick-an-issue", delegate
		{
			InspectionUIManager instance = InspectionUIManager.Instance;
			instance.toggle3.isOn = true;
			return "toggle 3 on (confront panel " + ((Object)(object)instance.confrontPanel != (Object)null && instance.confrontPanel.activeSelf) + ")";
		});
		Do("confront-here", delegate
		{
			lastNegoRefusal = "";
			Vars["nego0"] = CustomersBot.Mine("ctr.nego");
			InspectionUIManager.Instance.OnConfront();
			return "InspectionUIManager.OnConfront() through the client prefix: last nego refusal '" + lastNegoRefusal + "'";
		});
		Check("confront-refused-here", () => lastNegoRefusal == "nego.confront: " + aName + " is at the counter.", () => "last nego refusal '" + lastNegoRefusal + "' (want 'nego.confront: " + aName + " is at the counter.')");
		Close("look", "cig");
		Step("confront-refused-by-server", 20f, delegate(BotStep st)
		{
			if (st.Elapsed < 0.05f)
			{
				lastNegoRefusal = "";
				GameItem item = CounterHelpProbes.LabItem("cig");
				Net.ToServer("nego.confront", new MsgNegoConfront
				{
					v = Negotiation.LocalVisit(),
					itemId = Ids.Of(item),
					via = "inspect",
					reason = "CIGARETTE_SEAL_ISSUE"
				});
				return StepStatus.Running;
			}
			if (lastNegoRefusal == "")
			{
				return StepStatus.Running;
			}
			st.Detail = "raw nego.confront: the server answered '" + lastNegoRefusal + "'";
			return (lastNegoRefusal == "nego.confront: " + aName + " is at the counter.") ? StepStatus.Pass : StepStatus.Fail;
		});
		Step("labeler-refused-by-server", 20f, delegate(BotStep st)
		{
			if (st.Elapsed < 0.05f)
			{
				Vars["r0"] = BotScript.Str(ItemsUse.RefusedCount);
				bool flag = ItemsUse.RequestTarget(CounterHelpBot.Tool("labeler_blue"), CounterHelpProbes.LabItem("ours"));
				Vars["lsent"] = (flag ? "sent" : "NOT sent");
				return StepStatus.Running;
			}
			if (ItemsUse.RefusedCount <= CustomersBot.Int(Vars["r0"]))
			{
				return StepStatus.Running;
			}
			string[] obj = new string[7] { "blue labeler on our ", null, null, null, null, null, null };
			GameItem obj2 = CounterHelpProbes.LabItem("ours");
			obj[1] = ((obj2 != null) ? obj2.identifier : null);
			obj[2] = " on the counter (";
			obj[3] = Vars["lsent"];
			obj[4] = "): refused '";
			obj[5] = ItemsUse.LastRefusal;
			obj[6] = "'";
			st.Detail = string.Concat(obj);
			return ItemsUse.LastRefusal.EndsWith(aName + " is at the counter.") ? StepStatus.Pass : StepStatus.Fail;
		});
		Converge("nothing-moved", 15f, CounterHelpBot.DealState);
		Check("no-confront", () => CustomersBot.Mine("ctr.nego") == Vars["nego0"] && !CounterHelpProbes.LabItem("cig").IsTag("ALREADY_CONFRONTED_TAG"), () => "nego " + Vars["nego0"] + " -> " + CustomersBot.Mine("ctr.nego"));
		Mark("b:refused");
		WaitMark("a-released", 300f, "a:released");
		Check("seller-here", () => CustomersBot.Mine("cust.current") == "scavGeneral" && CounterBot.Holder() == "-1", () => "customer " + CustomersBot.Mine("cust.current") + ", holder " + CounterBot.Holder() + " (want scavGeneral, -1)");
		CheckWith("scan", "aquascan", "water", expectHolderA: false);
		Delay("lock-settles", 1.5f);
		Check("free-stays-free", () => CounterBot.Holder() == "-1", () => "holder " + CounterBot.Holder() + " (want -1: a help check never takes the counter)");
		Close("scan", "water");
		Mark("b:free-check");
		Do("resync", delegate
		{
			Snapshot.RequestResync();
			return "asked for the morning snapshot + today's log";
		});
		WaitFor("reloading", 30f, () => Net.ClientState != ClientState.Ready, () => "state " + Net.ClientState);
		WaitFor("reloaded", 150f, () => Net.IsLive && BotProbes.InShop, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
		Converge("same-after-replay", 30f, "cust.current", "cust.visit", "cust.holder", "cash", "cust.stock", "ctr.nego", "help.goods", "help.strips", "cust.digest", "cust.action");
		Mark("b:resynced");
		CheckWith("scan", "aquascan", "water2", expectHolderA: false, "-water2");
		CheckWith("strip", "water_test_strip", "water2", expectHolderA: false, "-water2");
		Check("two-panels-on-water2", () => CounterHelpBot.ScanOpenOn(CounterHelpProbes.LabItem("water2")) && CounterHelpBot.StripOpenOn(CounterHelpProbes.LabItem("water2")), CounterHelpBot.Panels);
		Do("note-held", delegate
		{
			Vars["held0"] = BotScript.Str(CounterHelp.ClosedHeld);
			return CounterHelpBot.Panels();
		});
		Mark("b:water2-panels");
		WaitMark("a-bought-water2", 120f, "a:bought-water2");
		Delay("storage-settles", 1.5f);
		Check("open-while-in-storage", delegate
		{
			GameItem val = CounterHelpProbes.LabItem("water2");
			return val != null && GeneralHelper.IsItemOwned(val) && !ItemsUse.InCounter(val) && CounterHelpBot.ScanOpenOn(val) && CounterHelpBot.StripOpenOn(val) && CounterHelp.ClosedHeld == CustomersBot.Int(Vars["held0"]);
		}, () => "water2 " + CustomersBot.Mine("help.goods") + ", " + CounterHelpBot.Panels() + " (ours in storage: no lock involved, the panels stay)");
		Mark("b:saw-storage");
		WaitMark("a-water2-on-counter", 60f, "a:water2-on-counter");
		WaitFor("closed-when-ours", 10f, () => !CounterHelpBot.ScanOpenOn(null) && !CounterHelpBot.StripOpenOn(null) && CounterHelp.ClosedHeld >= CustomersBot.Int(Vars["held0"]) + 2, CounterHelpBot.Panels);
		Check("bought-on-held-counter", delegate
		{
			GameItem val = CounterHelpProbes.LabItem("water2");
			return val != null && GeneralHelper.IsItemOwned(val) && ItemsUse.InCounter(val) && CounterBot.Holder() == BotScript.Str(a);
		}, () => "water2 " + CustomersBot.Mine("help.goods") + ", holder " + CounterBot.Holder() + ", last notice '" + Notices.Last + "'");
		Step("strip-reopen-closed", 15f, delegate(BotStep st)
		{
			if (st.Elapsed < 0.05f)
			{
				GameItem val = CounterHelpProbes.LabItem("water2");
				GameItem val2 = null;
				foreach (GameItem item2 in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)CounterBot.E.invElement, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
				{
					if (item2 != null && item2.identifier == "water_test_strip" && item2.IsTag("IS_SPENT") && val != null && item2.GetTagReadonly("ASSOCIATED_ITEM_ID").GetInt() == val.uniqueId)
					{
						val2 = item2;
						break;
					}
				}
				Vars["held1"] = BotScript.Str(CounterHelp.ClosedHeld);
				Vars["r1"] = BotScript.Str(ItemsUse.RefusedCount);
				Vars["asent"] = ((val2 == null) ? "no spent strip for the bottle" : (ItemsUse.RequestActivate(val2) ? ("activate sent for strip #" + val2.uniqueId) : "activate NOT sent"));
				return StepStatus.Running;
			}
			if (CounterHelp.ClosedHeld <= CustomersBot.Int(Vars["held1"]) || CounterHelpBot.StripOpenOn(null))
			{
				return StepStatus.Running;
			}
			st.Detail = Vars["asent"] + ": the strip panel opened on our bottle and closed by itself (" + CounterHelp.LastClosed + "), refusals " + Vars["r1"] + " -> " + ItemsUse.RefusedCount;
			return CounterHelp.LastClosed.StartsWith("strip:held:", StringComparison.Ordinal) ? StepStatus.Pass : StepStatus.Fail;
		}).TimeoutDetail = () => Vars["asent"] + "; " + CounterHelpBot.Panels() + "; refusals " + ItemsUse.RefusedCount + " " + ItemsUse.LastRefusal;
		Step("production-match-local", 10f, delegate(BotStep st)
		{
			GameItem val = CounterHelpProbes.LabItem("water2");
			string text = Customers.LocalCounterRefusal();
			int sentCount = Production.SentCount;
			int refusedCount = Production.RefusedCount;
			bool flag = Production.Let(ProdOp.WaterMatch, val);
			ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(val);
			bool flag2 = waterFeature != null && waterFeature.IsExposed();
			string text2 = "WaterMatch: " + text;
			st.Detail = "Let -> " + flag + ", sent " + sentCount + " -> " + Production.SentCount + ", refusals " + refusedCount + " -> " + Production.RefusedCount + ", last '" + Production.LastRefusal + "' (want '" + text2 + "'), label exposed " + flag2;
			return (text != null && text.EndsWith(" is at the counter.", StringComparison.Ordinal) && !flag && Production.SentCount == sentCount && Production.RefusedCount == refusedCount + 1 && Production.LastRefusal == text2 && !flag2) ? StepStatus.Pass : StepStatus.Fail;
		});
		Step("production-match-server", 10f, delegate(BotStep st)
		{
			GameItem val = CounterHelpProbes.LabItem("water2");
			if (st.Elapsed < 0.05f)
			{
				Vars["pr0"] = BotScript.Str(Production.RefusedCount);
				Vars["want"] = "WaterMatch: " + Customers.LocalCounterRefusal();
				Net.ToServer("production.req", new Production.MsgReq
				{
					n = 900001,
					op = 10,
					m = ((val != null) ? val.uniqueId : 0)
				});
				return StepStatus.Running;
			}
			bool num = Production.RefusedCount > CustomersBot.Int(Vars["pr0"]);
			ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(val);
			bool flag = waterFeature != null && waterFeature.IsExposed();
			if (!num && !flag)
			{
				return StepStatus.Running;
			}
			st.Detail = "raw production.req WaterMatch on our bottle on the counter (holder " + CounterBot.Holder() + "): " + (flag ? "ACCEPTED: the label changed without the counter" : ("refused '" + Production.LastRefusal + "' (want '" + Vars["want"] + "')"));
			return (!flag && Production.LastRefusal == Vars["want"]) ? StepStatus.Pass : StepStatus.Fail;
		}).TimeoutDetail = () => "no answer from the server; refusals " + Production.RefusedCount + " " + Production.LastRefusal;
		Mark("b:owned-checked");
		CheckWith("look", "magnifier", "cig", expectHolderA: true, "-late");
		CheckWith("scan", "aquascan", "water", expectHolderA: true, "-late");
		Check("two-panels-open", () => CounterHelpBot.LookOpenOn(CounterHelpProbes.LabItem("cig")) && CounterHelpBot.ScanOpenOn(CounterHelpProbes.LabItem("water")), CounterHelpBot.Panels);
		Do("note-gone", delegate
		{
			Vars["gone0"] = BotScript.Str(CounterHelp.ClosedGone);
			return CounterHelpBot.Panels();
		});
		Mark("b:panels-open");
		WaitFor("seller-gone", 180f, () => CustomersBot.Mine("cust.current") == "none", CustomersBot.ShowMine);
		WaitFor("panels-closed-when-gone", 5f, () => !CounterHelpBot.LookOpenOn(null) && !CounterHelpBot.ScanOpenOn(null) && CounterHelp.ClosedGone >= CustomersBot.Int(Vars["gone0"]) + 2, CounterHelpBot.Panels);
		Check("no-mismatch", () => Customers.Mismatches == 0 && ItemsUse.MismatchCount == 0, () => "customers mismatches " + Customers.Mismatches + ", items-use mismatches " + ItemsUse.MismatchCount + "; replay checks ok " + Replay.ChecksOk + " bad " + Replay.ChecksBad).Soft();
		ReportDone();
	}
}
