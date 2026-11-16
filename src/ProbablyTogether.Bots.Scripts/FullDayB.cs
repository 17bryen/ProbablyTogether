using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class FullDayB : FullDayBot
{
	private long wId;

	private long cId;

	private GameInventory wFrom;

	private int outBefore;

	private int rejectsBefore;

	private int day0;

	private int loads0;

	private int ended0;

	private int q;

	private int refusalsBefore;

	private int cashBefore;

	private long b3;

	private string counterBefore = "";

	private string address;

	private string name;

	private string pw;

	public override string Name => "fullday-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 2200f;

	protected override void Build()
	{
		JoinSteps();
		WaitServer("a-at-counter", 300f, (IReadOnlyDictionary<string, string> d) => FullDayBot.I(d, "fd.holder") > 0 && FullDayBot.I(d, "fd.holder") != Net.LocalPlayer && FullDayBot.V(d, "fd.arrived") == "1" && FullDayBot.V(d, "fd.customer") != "none", "fd.holder", "fd.customer", "fd.arrived", "fd.state");
		Same("a-at-counter", FullDayBot.All.Concat(new string[2] { "fd.holder", "fd.customer" }).ToArray());
		Step("backroom-group", 25f, delegate(BotStep st)
		{
			if (wId == 0L)
			{
				GameItem val = ((IEnumerable<GameItem>)FullDayBot.Movables((GameInventory)(object)FullDayBot.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => FullDayBot.E.hiddenElement.TryFindOneValidInventorySlot(i, false) != null));
				if (val == null)
				{
					st.Detail = "nothing in the back room fits the stash";
					return StepStatus.Fail;
				}
				wId = Ids.Of(val);
				wFrom = val.parentInventory;
				ItemsCapture.Ctx c;
				bool entered = ItemsCapture.Enter("group", out c);
				try
				{
					GraphUtils.TryAcceptAll((GraphNodeStorage)(object)FullDayBot.E.hiddenElement, val, -1);
				}
				finally
				{
					ItemsCapture.Exit(entered);
				}
				Vars["w"] = val.identifier + "#" + wId + " (sent " + ItemsMove.LastSentKind + " " + ItemsMove.LastSentQ + ")";
				return StepStatus.Running;
			}
			GameItem obj = Items.Find(wId);
			if ((object)((obj != null) ? obj.parentInventory : null) == FullDayBot.E.hiddenElement)
			{
				st.Detail = Vars["w"] + " -> stash after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			string text = ItemsMove.Outcome(ItemsMove.LastSentQ);
			if (text.StartsWith("rejected", StringComparison.Ordinal))
			{
				st.Detail = Vars["w"] + ": " + text;
				return StepStatus.Fail;
			}
			return StepStatus.Running;
		}).Soft();
		Same("backroom-group");
		Step("backroom-drag", 25f, delegate(BotStep st)
		{
			GameItem val = Items.Find(wId);
			if (val == null)
			{
				st.Detail = "item " + wId + " not found";
				return StepStatus.Fail;
			}
			if (q == 0)
			{
				if (!FullDayBot.Spot((GameInventory)(object)FullDayBot.E.invElement, val, out var x, out var y))
				{
					st.Detail = "no free spot in the back room";
					return StepStatus.Fail;
				}
				q = ItemsMove.Move(val, (GraphNodeStorage)(object)FullDayBot.E.invElement, x, y, val.modifiedShape.flipped, val.modifiedShape.orientation, -1, rep: false, "fullday");
				Vars["to"] = x + "," + y;
				if (q == 0)
				{
					st.Detail = "request not sent";
					return StepStatus.Fail;
				}
				return StepStatus.Running;
			}
			string text = ItemsMove.Outcome(q);
			if (text == "applied" && (object)val.parentInventory == FullDayBot.E.invElement)
			{
				st.Detail = val.identifier + "#" + wId + " stash -> inv " + Vars["to"] + " (request " + q + ") after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			if (text.StartsWith("rejected", StringComparison.Ordinal))
			{
				st.Detail = text;
				return StepStatus.Fail;
			}
			return StepStatus.Running;
		}).Soft();
		Same("backroom-drag");
		Step("machine", 25f, delegate(BotStep st)
		{
			GameItem val = FullDayLab.Machine();
			if (val == null)
			{
				st.Detail = "no printer here (#" + FullDayLab.Fx + ")";
				return StepStatus.Fail;
			}
			if (!Vars.ContainsKey("pressed"))
			{
				MachineryButton val2 = Machines.Widgets(val).OfType<MachineryButton>().FirstOrDefault();
				if (val2 == null)
				{
					st.Detail = "the printer has no button (" + Machines.Widgets(val).Count + " widgets)";
					return StepStatus.Fail;
				}
				outBefore = FullDayLab.Out();
				Vars["pressed"] = "'" + val2.text + "' (" + FullDayLab.Box() + " before)";
				FullDayLab.Press(val2);
				return StepStatus.Running;
			}
			if (FullDayLab.Out() > outBefore)
			{
				st.Detail = "pressed " + Vars["pressed"] + " -> " + FullDayLab.Box() + " after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		}).Soft().TimeoutDetail = () => "no bottle: " + FullDayLab.Box() + "; machines applied " + Machines.AppliedCount + ", rejects " + Machines.RejectCount + " (" + Machines.LastReject + ")";
		Same("machine", FullDayBot.All.Concat(new string[1] { "fd.mach" }).ToArray());
		Step("counter-refused-here", 10f, delegate(BotStep st)
		{
			int x2;
			int y2;
			GameItem val = ((IEnumerable<GameItem>)FullDayBot.Movables((GameInventory)(object)FullDayBot.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => FullDayBot.Spot((GameInventory)(object)FullDayBot.E.backInvinvElement, i, out x2, out y2)));
			if (val == null)
			{
				st.Detail = "nothing fits the back counter";
				return StepStatus.Fail;
			}
			cId = Ids.Of(val);
			FullDayBot.Spot((GameInventory)(object)FullDayBot.E.backInvinvElement, val, out var x, out var y);
			int num = ItemsMove.Move(val, (GraphNodeStorage)(object)FullDayBot.E.backInvinvElement, x, y, val.modifiedShape.flipped, val.modifiedShape.orientation, -1, rep: false, "fullday");
			st.Detail = val.identifier + "#" + cId + " -> back counter: " + ((num == 0) ? ("not sent (the counter is held by " + Net.PlayerName(FullDayLab.Holder()) + ")") : ("SENT as request " + num));
			return (num == 0 && (object)val.parentInventory != FullDayBot.E.backInvinvElement) ? StepStatus.Pass : StepStatus.Fail;
		}).Soft();
		Step("counter-refused-server", 15f, delegate(BotStep st)
		{
			GameItem val = Items.Find(cId);
			if (val == null)
			{
				st.Detail = "no item";
				return StepStatus.Fail;
			}
			if (!Vars.ContainsKey("forged"))
			{
				FullDayBot.Spot((GameInventory)(object)FullDayBot.E.backInvinvElement, val, out var x, out var y);
				rejectsBefore = ItemsMove.RejectCount;
				Net.ToServer("items.move", new ItemsReq
				{
					q = 900001,
					i = cId,
					n = -1,
					src = Items.RefOf((GraphNodeStorage)(object)val.parentInventory),
					sx = val.modifiedShape.minX,
					sy = val.modifiedShape.minY,
					to = Items.RefOf((GraphNodeStorage)(object)FullDayBot.E.backInvinvElement),
					x = x,
					y = y,
					f = val.modifiedShape.flipped,
					rot = val.modifiedShape.orientation,
					via = "fullday-forged"
				});
				Vars["forged"] = "1";
				return StepStatus.Running;
			}
			string text = ItemsMove.Outcome(900001);
			if (text.Length == 0 || text == "pending")
			{
				return StepStatus.Running;
			}
			st.Detail = "forged items.move to the back counter: server " + text + " ('" + ItemsMove.LastReject?.text + "'), item still in " + Items.RefOf((GraphNodeStorage)(object)val.parentInventory)?.r;
			return (text == "rejected:counter" && (object)val.parentInventory != FullDayBot.E.backInvinvElement) ? StepStatus.Pass : StepStatus.Fail;
		}).Soft().TimeoutDetail = () => "no answer to the forged move (rejects " + rejectsBefore + " -> " + ItemsMove.RejectCount + ")";
		Same("refused", FullDayBot.All.Concat(new string[1] { "fd.counter" }).ToArray());
		WaitMark("wait-a-on-back", "a-on-back", 180f);
		Do("note-before", delegate
		{
			refusalsBefore = FullDayLab.Notes.Count;
			cashBefore = FullDayBot.Ps.playerCash;
			counterBefore = FullDayLab.Counter();
			return "cash " + cashBefore + ", " + counterBefore;
		});
		Step("close-refused", 15f, delegate(BotStep st)
		{
			if (!Vars.ContainsKey("closeTry"))
			{
				DayNight.LastRefusedWhat = "";
				StoreShutterButton.Instance.EndAndClose();
				Vars["closeTry"] = "1";
				return StepStatus.Running;
			}
			if (DayNight.LastRefusedWhat == "close")
			{
				st.Detail = "the shutter's close (EndAndClose, the confirm's yes) refused: '" + DayNight.LastRefusedWhy + "'; state " + FullDayBot.State;
				if (!(FullDayBot.State == "OPEN"))
				{
					return StepStatus.Fail;
				}
				return StepStatus.Pass;
			}
			if (FullDayBot.State != "OPEN")
			{
				st.Detail = "CLOSED while A was mid-deal: " + FullDayBot.Shop();
				return StepStatus.Fail;
			}
			return StepStatus.Running;
		}).Soft().TimeoutDetail = () => "no refusal arrived; state " + FullDayBot.State + ", day-night sent close " + (DayNight.Sent.TryGetValue("dayNight.close", out var value) ? value : 0) + "; notes " + string.Join(" / ", FullDayLab.Notes.Skip(refusalsBefore));
		Step("bell-refused", 15f, delegate(BotStep st)
		{
			if (!Vars.ContainsKey("bellTry"))
			{
				Vars["bellTry"] = FullDayBot.RingBell();
				Vars["custBefore"] = FullDayBot.S(Customers.Refusals);
				return StepStatus.Running;
			}
			if (FullDayLab.Notes.Skip(refusalsBefore).Any((string x) => x.StartsWith("customers refused", StringComparison.Ordinal)))
			{
				st.Detail = "the bell (dismiss with A's customer there) refused: " + FullDayLab.Notes.Skip(refusalsBefore).Last((string x) => x.StartsWith("customers refused", StringComparison.Ordinal)) + "; customer " + FullDayLab.CurrentId();
				if (!(FullDayLab.CurrentId() != "none"))
				{
					return StepStatus.Fail;
				}
				return StepStatus.Pass;
			}
			if (FullDayLab.CurrentId() == "none")
			{
				st.Detail = "A's customer was sent away by B's bell: " + FullDayBot.Shop();
				return StepStatus.Fail;
			}
			return StepStatus.Running;
		}).Soft().TimeoutDetail = () => "no refusal for the bell: " + Vars["bellTry"] + "; " + FullDayBot.Shop();
		Step("buy-refused", 10f, delegate(BotStep st)
		{
			if (!Vars.ContainsKey("buyTry"))
			{
				List<GameItem> list = (from x in FullDayBot.E.GetBackInvItems()
					where !GeneralHelper.IsItemOwned(x)
					select x).ToList();
				if (list.Count == 0)
				{
					st.Detail = "no customer item on the back counter to try";
					return StepStatus.Fail;
				}
				Vars["buyTry"] = list.Count + " item(s)";
				NegociationUIManager.Instance.BuyMultipleItem(list);
				return StepStatus.Running;
			}
			if (st.Elapsed < 4f)
			{
				return StepStatus.Running;
			}
			bool flag = FullDayBot.Ps.playerCash == cashBefore && FullDayLab.Counter() == counterBefore;
			st.Detail = "BuyMultipleItem(" + Vars["buyTry"] + ") by B: cash " + cashBefore + " -> " + FullDayBot.Ps.playerCash + ", counters " + ((FullDayLab.Counter() == counterBefore) ? "unchanged" : ("CHANGED: " + FullDayLab.Counter()));
			return flag ? StepStatus.Pass : StepStatus.Fail;
		}).Soft();
		Step("haggle-refused", 10f, delegate(BotStep st)
		{
			if (!Vars.ContainsKey("hagTry"))
			{
				Vars["hagTry"] = "1";
				BargainUIManager.Instance.DemandBuyingDiscount(25);
				return StepStatus.Running;
			}
			if (st.Elapsed < 4f)
			{
				return StepStatus.Running;
			}
			bool flag = Negotiation.HasFeatureOnCounter("bargainBuyingDiscount");
			st.Detail = "DemandBuyingDiscount(25) by B: discount on the counter " + flag + ", offered flag here " + BargainUIManager.Instance.BuyingDiscountOffered + ", counters " + ((FullDayLab.Counter() == counterBefore) ? "unchanged" : "CHANGED");
			return (!flag && FullDayLab.Counter() == counterBefore) ? StepStatus.Pass : StepStatus.Fail;
		}).Soft();
		Step("report-refused", 10f, delegate(BotStep st)
		{
			if (!Vars.ContainsKey("repTry"))
			{
				Vars["repTry"] = "1";
				FullDayBot.Ps.OnCustomerReported();
				return StepStatus.Running;
			}
			string text = FullDayLab.Notes.Skip(refusalsBefore).LastOrDefault((string x) => x.StartsWith("security refused", StringComparison.Ordinal));
			if (text != null)
			{
				st.Detail = "the security button (OnCustomerReported) by B: " + text + "; customer " + FullDayLab.CurrentId();
				if (!(FullDayLab.CurrentId() != "none"))
				{
					return StepStatus.Fail;
				}
				return StepStatus.Pass;
			}
			if (st.Elapsed > 5f)
			{
				st.Detail = "no refusal; customer " + FullDayLab.CurrentId() + ", arresting " + FullDayBot.Ps.isClientBeingArrested;
				return StepStatus.Fail;
			}
			return StepStatus.Running;
		}).Soft();
		Same("after-refusals", FullDayBot.All.Concat(new string[5] { "fd.counter", "fd.holder", "fd.customer", "fd.state", "fd.sec" }).ToArray());
		Mark("b-refusals-done");
		Mark("b-backroom-done");
		WaitMark("wait-a-done-counter", "a-done-counter", 400f);
		WaitServer("counter-free", 30f, (IReadOnlyDictionary<string, string> d) => FullDayBot.V(d, "fd.holder") == "-1" && FullDayBot.V(d, "fd.customer") == "none", "fd.holder", "fd.customer", "fd.queue").Soft();
		CallStep("call", standIn: true);
		Same("b-called", FullDayBot.All.Concat(new string[4] { "fd.holder", "fd.customer", "fd.visit", "fd.queue" }).ToArray());
		Talk("b-dialogue", 60f, (Dialogue d) => 0, () => FullDayBot.NoDialogue && DialogueSync.Seq("counter") > Dlg0).Soft();
		DismissUntilGone("b-dismiss", 60f).Soft();
		WaitFor("b-counter-free", 30f, () => FullDayLab.Holder() == -1, FullDayBot.Shop).Soft();
		Same("b-dismissed", FullDayBot.All.Concat(new string[2] { "fd.holder", "fd.customer" }).ToArray());
		Mark("b-called-dismissed");
		WaitMark("wait-a-bought", "a-bought", 300f);
		Step("close", 40f, delegate(BotStep st)
		{
			if (FullDayBot.State == "CLOSED")
			{
				st.Detail = (Vars.TryGetValue("close", out var value) ? value : "") + "; closed after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			if (!Vars.ContainsKey("close"))
			{
				bool flag = FullDayBot.Ps.CanEndDay();
				StoreShutterButton.Instance.OnClick();
				Vars["close"] = "shutter clicked (queue " + BotProbes.Read("fd.queue") + ", can end day " + flag + ")";
				return StepStatus.Running;
			}
			if (st.Elapsed > 4f && !Vars.ContainsKey("close2"))
			{
				Vars["close2"] = "1";
				if (!FullDayBot.Ps.CanEndDay())
				{
					BotLog.Info(Name, "stand-in: a customer who cannot be sent away blocks closing; day-night's lab clearqueue");
					Net.ToServer("dayNight.bot", new MsgDnBot
					{
						op = "clearqueue"
					});
					Vars["close"] += "; stand-in clearqueue";
				}
				else
				{
					Vars["close"] += "; the confirm panel's yes (EndAndClose)";
				}
				Vars["closeAt"] = FullDayBot.F(st.Elapsed);
				return StepStatus.Running;
			}
			if (Vars.ContainsKey("closeAt") && !Vars.ContainsKey("close3") && st.Elapsed > float.Parse(Vars["closeAt"], CultureInfo.InvariantCulture) + 2f)
			{
				Vars["close3"] = "1";
				StoreShutterButton.Instance.EndAndClose();
			}
			return StepStatus.Running;
		}).Soft().TimeoutDetail = () => (Vars.TryGetValue("close", out var value) ? value : "") + "; " + FullDayBot.Shop() + "; last day-night refusal '" + DayNight.LastRefusedWhy + "'";
		Same("closed");
		Mark("b-closed");
		Do("note-day", delegate
		{
			day0 = FullDayBot.Day;
			loads0 = FullDayLab.Loads;
			ended0 = FullDayLab.DaysEnded;
			return "day " + day0 + ", snapshots loaded " + loads0;
		});
		Delay("after-close-timers", 6f);
		ReadyStep("ready", () => ended0);
		WaitFor("morning-2", 300f, () => FullDayBot.Live && FullDayLab.Loads > loads0 && FullDayBot.Day == day0 + 1 && FullDayBot.State == "MORNING", () => FullDayBot.Shop() + ", snapshots loaded " + FullDayLab.Loads + ", night " + BotProbes.Read("fd.night"));
		Same("morning-2", 30f, FullDayBot.All.Concat(new string[7] { "fd.upgrades", "fd.counter", "fd.queue", "fd.head", "fd.holder", "fd.events", "fd.phone" }).ToArray());
		CountersSoft("counters-day-1");
		Mark("b-morning");
		WaitMark("wait-a-midday", "a-midday", 300f);
		Same("midday", FullDayBot.All.Concat(new string[2] { "fd.holder", "fd.customer" }).ToArray());
		Step("machine-2", 25f, delegate(BotStep st)
		{
			GameItem val = FullDayLab.Machine();
			if (val == null)
			{
				st.Detail = "no printer";
				return StepStatus.Fail;
			}
			if (!Vars.ContainsKey("pressed2"))
			{
				MachineryButton val2 = Machines.Widgets(val).OfType<MachineryButton>().FirstOrDefault();
				if (val2 == null)
				{
					st.Detail = "no button";
					return StepStatus.Fail;
				}
				outBefore = FullDayLab.Out();
				Vars["pressed2"] = FullDayLab.Box();
				FullDayLab.Press(val2);
				return StepStatus.Running;
			}
			if (FullDayLab.Out() > outBefore)
			{
				st.Detail = "day 2: " + Vars["pressed2"] + " -> " + FullDayLab.Box() + " after " + FullDayBot.F(st.Elapsed) + " s (a machine action in today's log before B leaves)";
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		}).Soft().TimeoutDetail = () => "no bottle: " + FullDayLab.Box() + "; machines rejects " + Machines.RejectCount + " (" + Machines.LastReject + ")";
		Same("machine-2", FullDayBot.All.Concat(new string[1] { "fd.mach" }).ToArray());
		Do("leave", delegate
		{
			address = Args.Join;
			name = Args.Name ?? "bot2";
			pw = Args.Password ?? "";
			EscapeUIManager escapeUIManager = StoreUIManager.Instance.escapeUIManager;
			escapeUIManager.OpenEscapeMenu();
			escapeUIManager.OnMainMenu();
			StoreUIManager.Instance.confirmPanelUIManager.OnYesClick();
			return "escape menu > Main menu > Yes (" + FullDayBot.Shop() + ")";
		});
		WaitFor("at-menu", 90f, () => Net.Role == ProbablyTogether.Role.Off && Ui.AtMenu && (Object)(object)JoinMenu.Button != (Object)null, () => "role " + Net.Role.ToString() + ", scene " + BotProbes.Scene);
		Delay("away", 20f);
		Do("rejoin", delegate
		{
			JoinMenu.OpenDialog();
			if (!JoinMenu.LabFill(address, name, pw))
			{
				Net.Connect(address, pw, name);
				return "no Join window: Net.Connect(" + address + ")";
			}
			JoinMenu.Join();
			return "Join window: " + address + " as " + name;
		});
		WaitFor("rejoined-live", 240f, () => FullDayBot.Live, () => "state " + Net.ClientState.ToString() + ", status '" + JoinMenu.Status + "', scene " + BotProbes.Scene);
		Do("replay-stats", delegate
		{
			DayLog.ReplayStats last = DayLog.Last;
			return (last != null) ? ("log expected " + last.Expected + ", replayed " + last.Replayed + ", live held " + last.Live + ", " + last.Ms + " ms, replay checks ok/bad " + last.ChecksOk + "/" + last.ChecksBad + ((last.FirstBad != null) ? (" (first bad " + last.FirstBad + ")") : "")) : "no load record";
		});
		WaitMark("wait-a-acted", "a-acted", 120f).Soft();
		Same("rejoined", 30f, FullDayBot.All.Concat(new string[7] { "fd.holder", "fd.customer", "fd.counter", "fd.upgrades", "fd.queue", "fd.visit", "fd.events" }).ToArray());
		Mark("b-rejoined");
		WaitMark("wait-a-sold-2", "a-sold-2", 180f).Soft();
		Same("after-sale-2", 30f, FullDayBot.All.Concat(new string[4] { "fd.holder", "fd.customer", "fd.counter", "fd.upgrades" }).ToArray());
		Mark("b-checked-sale");
		WaitMark("wait-a-leaving", "a-leaving", 180f).Soft();
		WaitFor("a-left", 120f, () => Net.Players.Count == 1, () => Net.Players.Count + " player(s) here; " + FullDayBot.Shop());
		WaitFor("counter-freed", 20f, () => FullDayLab.Holder() == -1, FullDayBot.Shop).Soft();
		Same("a-left", FullDayBot.All.Concat(new string[3] { "fd.holder", "fd.customer", "fd.counter" }).ToArray());
		Step("take-counter", 20f, delegate(BotStep st)
		{
			if (FullDayLab.Holder() == Net.LocalPlayer)
			{
				st.Detail = "B holds the counter: " + FullDayBot.Shop() + " after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			if (!Vars.ContainsKey("take"))
			{
				Vars["take"] = "1";
				Customers.RequestTakeCounter();
			}
			return StepStatus.Running;
		}).Soft();
		Step("buy-3", 30f, delegate(BotStep st)
		{
			if (!Vars.ContainsKey("b3"))
			{
				GameItem val = (from x in FullDayBot.E.GetBackInvItems()
					where !GeneralHelper.IsItemOwned(x)
					orderby (int)x.GetNegociatedValue(), Ids.Of(x)
					select x).FirstOrDefault();
				if (FullDayLab.CurrentId() == "none")
				{
					st.Detail = "the customer left with A: " + FullDayBot.Shop();
					return StepStatus.Fail;
				}
				StoreClient clientBlueprint = FullDayBot.Ps.currentClientInstance.GetClientBlueprint();
				if (val == null || !clientBlueprint.IsClientSelling())
				{
					st.Detail = "no deal to finish: " + clientBlueprint.identifier + " (intent " + clientBlueprint.clientIntent.ToString() + ") has no stock on the back counter; " + FullDayLab.Counter();
					return StepStatus.Pass;
				}
				if ((int)val.GetNegociatedValue() > FullDayBot.Ps.playerCash + FullDayBot.Ps.wildFavor)
				{
					st.Detail = "the cheapest item (" + (int)val.GetNegociatedValue() + ") is more than the cash " + FullDayBot.Ps.playerCash + " (no deal possible; not a sync fault)";
					return StepStatus.Pass;
				}
				b3 = Ids.Of(val);
				cashBefore = FullDayBot.Ps.playerCash;
				Vars["b3"] = val.identifier + "#" + b3 + "@" + (int)val.GetNegociatedValue();
				NegociationUIManager.Instance.BuyMultipleItem(new List<GameItem> { val });
				return StepStatus.Running;
			}
			GameItem val2 = Items.Find(b3);
			if (val2 != null && GeneralHelper.IsItemOwned(val2) && FullDayBot.Ps.playerCash < cashBefore)
			{
				st.Detail = "B finished A's deal: bought " + Vars["b3"] + ", cash " + cashBefore + " -> " + FullDayBot.Ps.playerCash + " after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		}).Soft().TimeoutDetail = () => "not bought: " + (Vars.TryGetValue("b3", out var value) ? value : "-") + ", cash " + cashBefore + " -> " + FullDayBot.Ps.playerCash + "; notes " + string.Join(" / ", FullDayLab.Notes.Skip(Math.Max(0, FullDayLab.Notes.Count - 3)));
		Talk("after-buy-3", 20f, (Dialogue d) => 0, () => FullDayBot.NoDialogue).Soft();
		Same("sold-3", FullDayBot.All.Concat(new string[3] { "fd.holder", "fd.customer", "fd.counter" }).ToArray());
		DismissUntilGone("dismiss-3", 60f).Soft();
		Same("dismissed-3", FullDayBot.All.Concat(new string[4] { "fd.holder", "fd.customer", "fd.counter", "fd.queue" }).ToArray());
		Step("close-2", 40f, delegate(BotStep st)
		{
			if (FullDayBot.State == "CLOSED")
			{
				st.Detail = (Vars.TryGetValue("c2", out var value) ? value : "") + "; closed after " + FullDayBot.F(st.Elapsed) + " s, queue now " + BotProbes.Read("fd.queue");
				return StepStatus.Pass;
			}
			if (!Vars.ContainsKey("c2"))
			{
				Vars["c2"] = "shutter clicked with " + BotProbes.Read("fd.queue") + " customer(s) waiting (can end day " + FullDayBot.Ps.CanEndDay() + ", confirm panel ";
				StoreShutterButton.Instance.OnClick();
				Dictionary<string, string> vars = Vars;
				vars["c2"] = vars["c2"] + ((Object)(object)StoreUIManager.Instance.closeShutterConfirmationPanel != (Object)null && StoreUIManager.Instance.closeShutterConfirmationPanel.activeSelf) + ")";
				return StepStatus.Running;
			}
			if (st.Elapsed > 3f && !Vars.ContainsKey("c2yes"))
			{
				Vars["c2yes"] = "1";
				if (!FullDayBot.Ps.CanEndDay())
				{
					Net.ToServer("dayNight.bot", new MsgDnBot
					{
						op = "clearqueue"
					});
					Vars["c2"] += "; stand-in clearqueue (a customer who cannot be sent away)";
					Vars["c2late"] = "1";
					return StepStatus.Running;
				}
				Vars["c2"] += "; the confirm panel's yes (EndAndClose)";
				StoreShutterButton.Instance.EndAndClose();
			}
			if (Vars.ContainsKey("c2late") && st.Elapsed > 6f && !Vars.ContainsKey("c2late2"))
			{
				Vars["c2late2"] = "1";
				StoreShutterButton.Instance.EndAndClose();
			}
			return StepStatus.Running;
		}).Soft().TimeoutDetail = () => (Vars.TryGetValue("c2", out var value) ? value : "") + "; " + FullDayBot.Shop() + "; last day-night refusal '" + DayNight.LastRefusedWhy + "'";
		Same("closed-2", FullDayBot.All.Concat(new string[3] { "fd.queue", "fd.customer", "fd.state" }).ToArray());
		Step("power-off", 20f, delegate(BotStep st)
		{
			StoreService val = ((IEnumerable<StoreService>)FullDayBot.Ps.storeService).FirstOrDefault((Func<StoreService, bool>)((StoreService x) => x != null && x.id == "POWER_SERVICE"));
			if (val == null || !val.unlocked)
			{
				st.Detail = "no power service on the tablet";
				return StepStatus.Fail;
			}
			if (!Vars.ContainsKey("pw"))
			{
				Vars["pw"] = "was " + (val.isUsing ? "on" : "off");
				EndOfDayUIManager.Instance.OpenUI();
				FullDayBot.ToggleService(val, on: false);
				return StepStatus.Running;
			}
			if (!val.isUsing)
			{
				st.Detail = "POWER_SERVICE " + Vars["pw"] + ", now off (the server's answer applied here) after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		}).Soft().TimeoutDetail = () => "power still on here; economy refusals " + Economy.Refusals + " (" + Economy.LastRefusal + ")";
		Same("power-off", FullDayBot.All.Concat(new string[1] { "fd.services" }).ToArray());
		Do("note-day-2", delegate
		{
			day0 = FullDayBot.Day;
			loads0 = FullDayLab.Loads;
			ended0 = FullDayLab.DaysEnded;
			return "day " + day0 + ", cash " + FullDayBot.Ps.playerCash + ", snapshots loaded " + loads0;
		});
		Delay("after-close-timers-2", 6f);
		ReadyStep("ready-2", () => ended0);
		WaitFor("morning-3", 300f, () => FullDayBot.Live && FullDayLab.Loads > loads0 && FullDayBot.Day == day0 + 1 && FullDayBot.State == "MORNING", () => FullDayBot.Shop() + ", snapshots loaded " + FullDayLab.Loads);
		Same("morning-3", 30f, FullDayBot.All.Concat(new string[7] { "fd.upgrades", "fd.counter", "fd.queue", "fd.head", "fd.holder", "fd.services", "fd.events" }).ToArray());
		Do("open-shutter-3", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "clicked the shutter: " + FullDayBot.Shop();
		});
		WaitFor("open-3", 40f, () => FullDayBot.State == "OPEN" && FullDayBot.Ps.lockShutterButtonFlag == 0, FullDayBot.Shop).Soft();
		Same("opened-3", FullDayBot.All.Concat(new string[2] { "fd.state", "fd.events" }).ToArray());
		Talk("morning-visitor-3", 90f, (Dialogue d) => 0, () => FullDayLab.CurrentId() == "none" || (FullDayBot.Ps.lockNextBellFlag == 0 && FullDayBot.NoDialogue && FullDayBot.Ps.IsClientDimissable())).Soft();
		DismissUntilGone("morning-visitor-3-gone", 60f).Soft();
		CallStep("call-3", standIn: true);
		Same("called-3", FullDayBot.All.Concat(new string[4] { "fd.holder", "fd.customer", "fd.visit", "fd.queue" }).ToArray());
		Talk("dialogue-3", 60f, (Dialogue d) => 0, () => FullDayBot.NoDialogue && DialogueSync.Seq("counter") > Dlg0).Soft();
		DismissUntilGone("dismiss-3b", 60f).Soft();
		WaitFor("counter-free-3", 30f, () => FullDayLab.Holder() == -1, FullDayBot.Shop).Soft();
		Same("day-3", 30f, FullDayBot.All.Concat(new string[5] { "fd.holder", "fd.customer", "fd.counter", "fd.queue", "fd.events" }).ToArray());
		CountersSoft("counters-final");
		Mark("b-done");
	}
}
