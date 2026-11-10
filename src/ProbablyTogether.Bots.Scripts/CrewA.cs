using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CrewA : CrewBot
{
	public override string Name => "crew-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 1700f;

	protected override void Build()
	{
		CrewJoin();
		Check("names", () => Net.PlayerName(Net.LocalPlayer) == CrewBot.Me, () => "this copy is '" + Net.PlayerName(Net.LocalPlayer) + "' (the checks expect " + CrewBot.Me + ")").Soft();
		Step("open", 40f, delegate(BotStep st)
		{
			if (FullDayBot.State == "OPEN" && FullDayBot.Ps.lockShutterButtonFlag == 0)
			{
				st.Detail = FullDayBot.Shop();
				return StepStatus.Pass;
			}
			if (!Vars.ContainsKey("opened"))
			{
				Vars["opened"] = "1";
				StoreShutterButton.Instance.OnClick();
			}
			return StepStatus.Running;
		});
		Talk("morning-visitor", 90f, (Dialogue d) => 0, () => FullDayLab.CurrentId() == "none" || (FullDayBot.Ps.lockNextBellFlag == 0 && FullDayBot.NoDialogue && FullDayBot.Ps.IsClientDimissable())).Soft();
		DismissUntilGone("morning-visitor-gone", 60f).Soft();
		CrewMark("a-opened");
		Step("deal", 240f, DealPoll()).TimeoutDetail = () => (Vars.TryGetValue("deal", out var value) ? value : "") + "; " + FullDayBot.Shop();
		WaitServer("server-counted-sale", 30f, (IReadOnlyDictionary<string, string> d) => CrewLab.Diff(FullDayBot.V(d, "crew.table"), CrewBot.Me, CrewLab.WantA) == "" && CrewLab.Diff(FullDayBot.V(d, "crew.table"), CrewBot.Partner, CrewLab.AnyTimeB, mustExist: false) == "", "crew.table");
		Talk("after-deal", 30f, (Dialogue d) => 0, () => FullDayBot.NoDialogue).Soft();
		DismissUntilGone("merchant-leaves", 60f).Soft();
		CrewMark("a-sold");
		WaitCrewMark("b-worked", 300f);
		WaitServer("partner-away-kept", 200f, delegate(IReadOnlyDictionary<string, string> d)
		{
			string canon = FullDayBot.V(d, "crew.table");
			return FullDayBot.I(d, "players") == 1 && CrewLab.Diff(canon, CrewBot.Partner, CrewLab.WantB) == "" && CrewLab.Diff(canon, CrewBot.Me, CrewLab.WantA) == "" && !("," + FullDayBot.V(d, "crew.on") + ",").Contains("," + CrewBot.Partner + ",");
		}, "players", "crew.table", "crew.on");
		WaitCrewMark("b-rejoined", 300f);
		CloseShop("close");
		CrewMark("a-closed");
		PanelChecks("a");
		CrewMark("a-checked");
		WaitCrewMark("b-resynced", 300f);
		Converge("same-after-b-resync", 20f, "crew.table");
		WaitCrewMark("b-leaving", 200f);
		WaitServer("partner-gone", 60f, (IReadOnlyDictionary<string, string> d) => FullDayBot.I(d, "players") == 1, "players", "crew.on");
		Do("tablet-again", delegate
		{
			if (!EndOfDayUIManager.Instance.IsOpen)
			{
				EndOfDayUIManager.Instance.OpenUI();
			}
			return "tablet open " + EndOfDayUIManager.Instance.IsOpen;
		});
		WaitFor("leaver-on-panel", 20f, () => CrewPanel.Visible && CrewPanel.Text.Contains("|" + CrewBot.Partner + " (left)|"), () => "panel " + CrewPanel.Visible + ": " + CrewPanel.Text.Replace("\n", " / "));
		Converge("same-after-leave", 20f, "crew.table");
		Check("leaver-figures", () => CrewLab.DiffDay(BotProbes.Read("crew.table")) == "", () => "table " + BotProbes.Read("crew.table") + "; differs: " + CrewLab.DiffDay(BotProbes.Read("crew.table")));
		Do("wide-preview", delegate
		{
			CrewPanel.LabPreview = CrewLab.WideTable(FullDayBot.Day);
			return "a lab table of 8 players (6 shown) in place of today's (tablet open " + EndOfDayUIManager.Instance.IsOpen + ")";
		});
		WaitFor("wide-shown", 10f, () => CrewPanel.Visible && CrewPanel.Text.Contains("|Wanderer 6|") && CrewPanel.PlateArea.HasValue, () => "panel " + CrewPanel.Visible + ", anchor '" + CrewPanel.Anchor + "', text " + CrewPanel.Text.Replace("\n", " / "));
		Delay("wide-settle", 1f);
		Check("wide-clear-of-tablet", () => WideClear() == "", () => WideClear() + " " + WideText());
		Do("wide-measure", (Func<string>)WideText);
		Do("wide-off", delegate
		{
			CrewPanel.LabPreview = null;
			return "today's table again";
		});
		WaitFor("wide-back", 10f, () => CrewPanel.Visible && CrewPanel.Text.Contains("|" + CrewBot.Partner + " (left)|"), () => "panel " + CrewPanel.Visible + ": " + CrewPanel.Text.Replace("\n", " / "));
		NightAndMorning();
		CrewMark("a-done");
	}

	private static string WideClear()
	{
		Rect? plateArea = CrewPanel.PlateArea;
		Rect? tabletRect = CrewPanel.TabletRect;
		if (!plateArea.HasValue)
		{
			return "panel not placed (" + CrewPanel.Anchor + ")";
		}
		if (!tabletRect.HasValue)
		{
			return "tablet not measured (" + CrewPanel.Anchor + ")";
		}
		List<string> list = new List<string>();
		Rect value = plateArea.Value;
		Rect value2 = tabletRect.Value;
		if (!(value.xMin >= value2.xMax - 0.002f) && !(value.xMax <= value2.xMin + 0.002f))
		{
			list.Add("covers the tablet");
		}
		if (value.xMin < -0.002f || value.xMax > 1.002f || value.yMin < -0.002f || value.yMax > 1.002f)
		{
			list.Add("outside the game area");
		}
		if (CrewPanel.PerBlock >= 6)
		{
			list.Add("six columns in one block");
		}
		return string.Join(", ", list);
	}

	private static string WideText()
	{
		return "panel " + R(CrewPanel.PlateArea) + ", tablet " + R(CrewPanel.TabletRect) + ", " + CrewPanel.PerBlock + " a block; " + CrewPanel.Anchor + "; text " + CrewPanel.Text.Replace("\n", " / ");
		static string R(Rect? r)
		{
			if (!r.HasValue)
			{
				return "-";
			}
			string[] array = new string[7];
			Rect value = r.Value;
			array[0] = value.xMin.ToString("F3", CultureInfo.InvariantCulture);
			array[1] = "..";
			value = r.Value;
			array[2] = value.xMax.ToString("F3", CultureInfo.InvariantCulture);
			array[3] = " x ";
			value = r.Value;
			array[4] = value.yMin.ToString("F3", CultureInfo.InvariantCulture);
			array[5] = "..";
			value = r.Value;
			array[6] = value.yMax.ToString("F3", CultureInfo.InvariantCulture);
			return string.Concat(array);
		}
	}

	private Func<BotStep, StepStatus> DealPoll()
	{
		string phase = "queue";
		int tries = 0;
		int cash0 = 0;
		float at = 0f;
		float lastNudge = 0f;
		float lastRing = -100f;
		float movedAt = -100f;
		bool marked = false;
		return delegate(BotStep st)
		{
			float now = Time.realtimeSinceStartup;
			string text = FullDayLab.CurrentId();
			if (phase != "queue" && phase != "call" && phase != "sold" && (text == "none" || text != "merchant"))
			{
				Note("the merchant left during " + phase);
				if (++tries >= 3)
				{
					st.Detail = Vars["dealLog"];
					return StepStatus.Fail;
				}
				Go("queue");
			}
			switch (phase)
			{
			case "queue":
				if (text != "none")
				{
					if (FullDayBot.Ps.isClientArrived && FullDayBot.NoDialogue && now - at > 3f)
					{
						FullDayBot.Ps.TryPlayerDismissCurrentClient(false);
						at = now;
					}
					else
					{
						Talking();
					}
					return StepStatus.Running;
				}
				Net.ToServer("crew.lab", new MsgCrewLab
				{
					op = "queue",
					a = "merchant"
				});
				Go("call");
				return StepStatus.Running;
			case "call":
				if (text == "merchant" && FullDayBot.Ps.isClientArrived && FullDayLab.Holder() == Net.LocalPlayer)
				{
					Note("merchant at the counter, held by me");
					Go("talk");
					return StepStatus.Running;
				}
				if (BotProbes.Read("fd.head") == "merchant" && text == "none" && FullDayBot.Ps.lockNextBellFlag == 0 && now - lastRing > 6f)
				{
					lastRing = now;
					FullDayBot.RingBell();
				}
				if (now - at > 40f)
				{
					Note("no merchant came");
					Go("queue");
				}
				return StepStatus.Running;
			case "talk":
				if (Talking())
				{
					return StepStatus.Running;
				}
				if (now - at < 2f)
				{
					return StepStatus.Running;
				}
				Go("to-back");
				return StepStatus.Running;
			case "to-back":
			{
				GameItem val2 = CrewLab.Item("fl" + Math.Min(tries, 1));
				if (val2 == null || !GeneralHelper.IsItemOwned(val2))
				{
					val2 = CrewLab.Item("fl1");
				}
				if (val2 == null)
				{
					st.Detail = "no flashlight left";
					return StepStatus.Fail;
				}
				if ((object)val2.parentInventory == FullDayBot.E.backInvinvElement)
				{
					Vars["fl"] = BotScript.Str(Ids.Of(val2));
					Note("flashlight #" + Ids.Of(val2) + " on the back counter");
					Go("markup");
					return StepStatus.Running;
				}
				if (movedAt < at || now - movedAt > 8f)
				{
					movedAt = now;
					ItemsMove.QuickMove(val2, (GraphNodeStorage)(object)FullDayBot.E.backInvinvElement, all: false);
				}
				return StepStatus.Running;
			}
			case "markup":
			{
				if (Talking())
				{
					return StepStatus.Running;
				}
				BargainUIManager instance = BargainUIManager.Instance;
				if (!marked && (Object)(object)instance != (Object)null && instance.CanOfferMarkup())
				{
					marked = true;
					instance.OfferMarkup(10);
					Note("OfferMarkup(10)");
					Go("answer");
				}
				else
				{
					Note(marked ? "markup already offered" : ("markup not offered (can offer " + ((Object)(object)instance != (Object)null && instance.CanOfferMarkup()) + ")"));
					Go("sell");
				}
				return StepStatus.Running;
			}
			case "answer":
				if (BargainUIManager.Instance.MarkupOffered)
				{
					if (Talking())
					{
						return StepStatus.Running;
					}
					Note("markup " + (Negotiation.HasFeatureOnCounter("bargainMarkup") ? "accepted" : "refused, the merchant stays"));
					Go("sell");
				}
				else if (now - at > 20f)
				{
					Note("no answer to the markup");
					Go("sell");
				}
				return StepStatus.Running;
			case "sell":
			{
				if (Talking())
				{
					return StepStatus.Running;
				}
				cash0 = FullDayBot.Ps.playerCash;
				List<GameItem> sellableItem = NegociationUIManager.Instance.GetSellableItem();
				NegociationUIManager.Instance.SellMultiple(sellableItem, false);
				Note("SellMultiple(" + sellableItem.Count + ") at cash " + cash0);
				Go("sold");
				return StepStatus.Running;
			}
			case "sold":
			{
				string value;
				long num = (Vars.TryGetValue("fl", out value) ? long.Parse(value, CultureInfo.InvariantCulture) : 0);
				GameItem val = ((num != 0L) ? Items.Find(num) : null);
				if (FullDayBot.Ps.playerCash > cash0 && (val == null || !GeneralHelper.IsItemOwned(val)))
				{
					st.Detail = Vars["dealLog"] + "; sold: cash " + cash0 + " -> " + FullDayBot.Ps.playerCash + " after " + FullDayBot.F(st.Elapsed) + " s";
					return StepStatus.Pass;
				}
				if (now - at > 20f)
				{
					Note("not sold in 20 s (cash " + FullDayBot.Ps.playerCash + ", nego refusals: " + string.Join(" / ", FullDayLab.Notes.Skip(Math.Max(0, FullDayLab.Notes.Count - 2))) + ")");
					if (++tries >= 3)
					{
						st.Detail = Vars["dealLog"];
						return StepStatus.Fail;
					}
					Go((text == "merchant") ? "sell" : "queue");
				}
				return StepStatus.Running;
			}
			default:
				return StepStatus.Running;
			}
		};
		void Go(string p)
		{
			phase = p;
			at = Time.realtimeSinceStartup;
			Vars["deal"] = "try " + tries + ": " + p + (Vars.TryGetValue("dealLog", out var value) ? (" (" + value + ")") : "");
		}
		void Note(string s)
		{
			Vars["dealLog"] = (Vars.TryGetValue("dealLog", out var value) ? (value + "; ") : "") + s;
		}
		bool Talking()
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
	}
}
