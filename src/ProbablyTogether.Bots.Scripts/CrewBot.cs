using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public abstract class CrewBot : FullDayBot
{
	internal static readonly string MeName = CrewName(0, "bot1");

	internal static readonly string PartnerName = CrewName(1, "bot2");

	protected static readonly string Me = MeName;

	protected static readonly string Partner = PartnerName;

	internal const string ChatWork = "Cage is fixed. Printer's on too.";

	internal const string ChatDeal = "He's loaded. Push the price up.";

	protected int Loads0;

	protected int Ended0;

	protected int Day0;

	private static string CrewName(int i, string fallback)
	{
		string[] array = (Args.Get("--br-crew-names") ?? "").Split(',');
		string text = ((array.Length == 2) ? array[i].Trim() : "");
		if (text.Length <= 0 || text.Length > 16)
		{
			return fallback;
		}
		return text;
	}

	protected BotStep CrewMark(string mark)
	{
		return Do("mark-" + mark, delegate
		{
			Net.ToServer("crew.lab", new MsgCrewLab
			{
				op = "mark",
				a = mark
			});
			return "told the server";
		});
	}

	protected static bool HasCrewMark(IReadOnlyDictionary<string, string> d, string mark)
	{
		return ("," + FullDayBot.V(d, "crew.marks") + ",").Contains("," + mark + ",");
	}

	protected BotStep WaitCrewMark(string mark, float timeout)
	{
		return WaitServer("wait-" + mark, timeout, (IReadOnlyDictionary<string, string> d) => HasCrewMark(d, mark), "crew.marks");
	}

	protected void CrewJoin()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer);
		WaitFor("live", 240f, () => FullDayBot.Live, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
		Do("options", delegate
		{
			EmporiumOption instance = EmporiumOption.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				return "no options object";
			}
			instance.autoCallNext = false;
			instance.autoNextDialog = false;
			return "autoCallNext off, autoNextDialog off (player " + Net.LocalPlayer + " '" + Net.PlayerName(Net.LocalPlayer) + "')";
		});
		WaitServer("both-live", 240f, (IReadOnlyDictionary<string, string> d) => FullDayBot.I(d, "crew.live") >= 2 && FullDayBot.V(d, "crew.fx").Contains("scrap="), "crew.live", "crew.fx");
		Step("fixtures-here", 30f, delegate(BotStep st)
		{
			if (CrewLab.Item("scrap") != null && CrewLab.Item("cage") != null && CrewLab.Item("printer") != null && CrewLab.Item("fl0") != null)
			{
				st.Detail = CrewLab.FxText() + "; cage health " + CrewLab.CageHealth() + ", printer output " + CrewLab.PrinterOut();
				return StepStatus.Pass;
			}
			if (!Vars.ContainsKey("fxAsk"))
			{
				Vars["fxAsk"] = BotScript.Str(BotProbes.AskServer());
			}
			Dictionary<string, string> dictionary = BotProbes.TakeReply(int.Parse(Vars["fxAsk"], CultureInfo.InvariantCulture));
			if (dictionary != null)
			{
				CrewLab.ParseFx(FullDayBot.V(dictionary, "crew.fx"));
				Vars.Remove("fxAsk");
			}
			return StepStatus.Running;
		});
	}

	protected BotStep Converge(string name, float timeout, params string[] probes)
	{
		int id = 0;
		float lastAsk = -10f;
		string diff = "no answer yet";
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (id != 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary != null)
				{
					id = 0;
					List<string> list = new List<string>();
					string[] array = probes;
					foreach (string text in array)
					{
						string text2 = BotProbes.Read(text);
						string text3 = FullDayBot.V(dictionary, text);
						if (text2 != text3 || text2 == "")
						{
							list.Add(text + " here '" + text2 + "' server '" + text3 + "'");
						}
					}
					diff = string.Join("; ", list);
					if (list.Count == 0)
					{
						st.Detail = string.Join(" ", probes.Select((string p) => p + "=" + BotProbes.Read(p))) + " (same as the server) after " + FullDayBot.F(st.Elapsed) + " s";
						return StepStatus.Pass;
					}
				}
				else if (Time.realtimeSinceStartup - lastAsk > 10f)
				{
					id = 0;
				}
			}
			if (id == 0 && Time.realtimeSinceStartup - lastAsk > 0.7f)
			{
				lastAsk = Time.realtimeSinceStartup;
				id = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "still different: " + diff;
		return botStep;
	}

	protected void PanelChecks(string tag)
	{
		Do("tablet-" + tag, delegate
		{
			EndOfDayUIManager.Instance.OpenUI();
			return "the end-of-day tablet opened (open " + EndOfDayUIManager.Instance.IsOpen + ")";
		});
		WaitFor("table-" + tag, 30f, () => Crew.Today != null, () => "crew table here " + ((Crew.Today != null) ? "yes" : "no") + ": received " + BotProbes.Read("crew.recv") + ", day " + FullDayBot.Day + ", state " + FullDayBot.State);
		Converge("same-table-" + tag, 30f, "crew.table");
		WaitFor("panel-" + tag, 10f, () => CrewPanel.Visible && CrewPanel.Text.Length > 0, () => "panel visible " + CrewPanel.Visible + ", tablet open " + EndOfDayUIManager.Instance.IsOpen + ", text '" + CrewPanel.Text + "'");
		Check("figures-" + tag, () => CrewLab.DiffDay(BotProbes.Read("crew.table")) == "", () => "table " + BotProbes.Read("crew.table") + "; differs: " + CrewLab.DiffDay(BotProbes.Read("crew.table")));
		Check("shown-" + tag, delegate
		{
			string shownLines = CrewPanel.ShownLines;
			string text = CrewPanel.Text;
			bool num = shownLines.Contains("Items sold") && shownLines.Contains("Money made") && shownLines.Contains("Customers served") && shownLines.Contains("Repairs") && shownLines.Contains("Machine uses") && shownLines.Contains("Items made") && !shownLines.Contains("Stolen goods caught");
			bool flag = text.Contains("|" + Me + "|") && text.Contains("|" + Partner);
			return num && flag;
		}, () => "lines '" + CrewPanel.ShownLines + "'; text '" + CrewPanel.Text.Replace("\n", " / ") + "'");
		Do("panel-text-" + tag, () => "panel: " + CrewPanel.Text.Replace("\n", " / ") + " ; " + CrewPanel.Anchor);
	}

	protected BotStep CloseShop(string name)
	{
		string value;
		BotStep botStep = Step(name, 60f, delegate(BotStep st)
		{
			if (FullDayBot.State == "CLOSED")
			{
				st.Detail = (Vars.TryGetValue("close", out value) ? value : "") + "; closed after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			if (!Vars.ContainsKey("close"))
			{
				StoreShutterButton.Instance.OnClick();
				Vars["close"] = "shutter clicked (can end day " + FullDayBot.Ps.CanEndDay() + ")";
				return StepStatus.Running;
			}
			if (st.Elapsed > 4f && !Vars.ContainsKey("close2"))
			{
				Vars["close2"] = "1";
				if (!FullDayBot.Ps.CanEndDay())
				{
					Net.ToServer("dayNight.bot", new MsgDnBot
					{
						op = "clearqueue"
					});
					Vars["close"] += "; stand-in clearqueue";
				}
				Vars["closeAt"] = FullDayBot.F(st.Elapsed);
				return StepStatus.Running;
			}
			if (Vars.ContainsKey("closeAt") && !Vars.ContainsKey("close3") && st.Elapsed > float.Parse(Vars["closeAt"], CultureInfo.InvariantCulture) + 2f)
			{
				Vars["close3"] = "1";
				Vars["close"] += "; the confirm panel's yes (EndAndClose)";
				StoreShutterButton.Instance.EndAndClose();
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => (Vars.TryGetValue("close", out value) ? value : "") + "; " + FullDayBot.Shop() + "; last day-night refusal '" + DayNight.LastRefusedWhy + "'";
		return botStep;
	}

	protected void NightAndMorning()
	{
		Do("note-day", delegate
		{
			Day0 = FullDayBot.Day;
			Loads0 = FullDayLab.Loads;
			Ended0 = FullDayLab.DaysEnded;
			return "day " + Day0 + ", snapshots loaded " + Loads0;
		});
		ReadyStep("ready", () => Ended0);
		WaitFor("morning", 300f, () => FullDayBot.Live && FullDayLab.Loads > Loads0 && FullDayBot.Day == Day0 + 1 && FullDayBot.State == "MORNING", () => FullDayBot.Shop() + ", snapshots loaded " + FullDayLab.Loads);
		Check("table-gone-here", () => Crew.Today == null && !CrewPanel.Visible, () => "today's table here " + Crew.Canon(Crew.Today) + ", panel " + CrewPanel.Visible);
		WaitServer("table-reset-server", 30f, (IReadOnlyDictionary<string, string> d) => FullDayBot.V(d, "crew.table") == "d=" + (Day0 + 1) + ";best=-", "crew.table");
	}
}
