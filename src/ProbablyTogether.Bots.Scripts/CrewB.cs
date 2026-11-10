using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using HarmonyLib;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CrewB : CrewBot
{
	private static readonly MethodInfo onSelect = AccessTools.Method(typeof(MachineryButton), "OnSelect", (Type[])null, (Type[])null);

	private string address;

	private string name;

	private string pw;

	private int health0;

	private int out0;

	private int loadsBefore;

	private int replayBefore;

	public override string Name => "crew-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 1700f;

	protected override void Build()
	{
		CrewJoin();
		Check("names", () => Net.PlayerName(Net.LocalPlayer) == CrewBot.Partner, () => "this copy is '" + Net.PlayerName(Net.LocalPlayer) + "' (the checks expect " + CrewBot.Partner + ")").Soft();
		WaitCrewMark("a-opened", 200f);
		Step("repair", 30f, delegate(BotStep st)
		{
			GameItem val = CrewLab.Item("cage");
			GameItem val2 = CrewLab.Item("scrap");
			if (!Vars.ContainsKey("repair"))
			{
				if (val == null || val2 == null)
				{
					st.Detail = "no cage or no scrap";
					return StepStatus.Fail;
				}
				health0 = CrewLab.CageHealth();
				Vars["repair"] = "scrap #" + Ids.Of(val2) + " on cage #" + Ids.Of(val) + " (health " + health0 + ", may target " + val2.MayTarget(val) + ")";
				val2.Target(val);
				return StepStatus.Running;
			}
			if (CrewLab.CageHealth() > health0)
			{
				st.Detail = Vars["repair"] + " -> health " + CrewLab.CageHealth() + " after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		}).TimeoutDetail = () => (Vars.TryGetValue("repair", out var value) ? value : "") + "; health now " + CrewLab.CageHealth() + "; last use refusal '" + ItemsUse.LastRefusal + "'";
		Converge("same-cage", 15f, "crew.cage");
		Step("print", 30f, delegate(BotStep st)
		{
			GameItem val = CrewLab.Item("printer");
			if (val == null)
			{
				st.Detail = "no printer";
				return StepStatus.Fail;
			}
			if (!Vars.ContainsKey("print"))
			{
				MachineryButton val2 = Machines.Widgets(val).OfType<MachineryButton>().FirstOrDefault();
				if (val2 == null)
				{
					st.Detail = "no button";
					return StepStatus.Fail;
				}
				out0 = CrewLab.PrinterOut();
				Vars["print"] = "printer #" + Ids.Of(val) + " output " + out0;
				onSelect.Invoke(val2, null);
				return StepStatus.Running;
			}
			if (CrewLab.PrinterOut() > out0)
			{
				st.Detail = Vars["print"] + " -> " + CrewLab.PrinterOut() + " after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		}).TimeoutDetail = () => (Vars.TryGetValue("print", out var value) ? value : "") + "; output now " + CrewLab.PrinterOut() + "; machines rejects " + Machines.RejectCount + " (" + Machines.LastReject + ")";
		Converge("same-printer", 15f, "crew.out");
		WaitServer("server-counted-work", 30f, (IReadOnlyDictionary<string, string> d) => CrewLab.Diff(FullDayBot.V(d, "crew.table"), CrewBot.Partner, CrewLab.WantB) == "" && CrewLab.Diff(FullDayBot.V(d, "crew.table"), CrewBot.Me, CrewLab.AnyTimeA, mustExist: false) == "", "crew.table");
		Check("nothing-sent-mid-day", () => Crew.Received == 0 && Crew.Today == null, () => "tables received here " + BotProbes.Read("crew.recv")).Soft();
		if ((Args.Get("--br-crew-chat") ?? "").Trim() == "1")
		{
			WaitFor("a-at-counter", 40f, () => Locks.HeldBy("counter") > 0 && Net.PlayerName(Locks.HeldBy("counter")) == CrewBot.Me, () => "counter " + Locks.HeldBy("counter")).Soft();
			Delay("chat-wait", 2f);
			Do("chat-work", () => (!Chat.Send("Cage is fixed. Printer's on too.")) ? "not sent" : "sent 'Cage is fixed. Printer's on too.'");
			Delay("chat-gap", 4f);
			Do("chat-deal", () => (!Chat.Send("He's loaded. Push the price up.")) ? "not sent" : "sent 'He's loaded. Push the price up.'");
		}
		CrewMark("b-worked");
		WaitCrewMark("a-sold", 300f);
		Do("leave", delegate
		{
			address = Args.Join;
			name = Args.Name ?? CrewBot.PartnerName;
			pw = Args.Password ?? "";
			EscapeUIManager escapeUIManager = StoreUIManager.Instance.escapeUIManager;
			escapeUIManager.OpenEscapeMenu();
			escapeUIManager.OnMainMenu();
			StoreUIManager.Instance.confirmPanelUIManager.OnYesClick();
			return "escape menu > Main menu > Yes (" + FullDayBot.Shop() + ")";
		});
		WaitFor("at-menu", 90f, () => Net.Role == ProbablyTogether.Role.Off && Ui.AtMenu, () => "role " + Net.Role.ToString() + ", scene " + BotProbes.Scene);
		Delay("away", 20f);
		Do("rejoin", delegate
		{
			Net.Connect(address, pw, name);
			return "Net.Connect(" + address + ") as " + name;
		});
		WaitFor("rejoined-live", 240f, () => FullDayBot.Live, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
		Check("rejoined-name", () => Net.PlayerName(Net.LocalPlayer) == CrewBot.Partner, () => "rejoined as '" + Net.PlayerName(Net.LocalPlayer) + "', player " + Net.LocalPlayer);
		WaitServer("server-kept-row", 30f, (IReadOnlyDictionary<string, string> d) => CrewLab.Diff(FullDayBot.V(d, "crew.table"), CrewBot.Partner, CrewLab.WantB) == "" && CrewLab.Diff(FullDayBot.V(d, "crew.table"), CrewBot.Me, CrewLab.AnyTimeA, mustExist: false) == "" && ("," + FullDayBot.V(d, "crew.on") + ",").Contains("," + CrewBot.Partner + ","), "crew.table", "crew.on");
		CrewMark("b-rejoined");
		WaitCrewMark("a-closed", 300f);
		WaitFor("closed-here", 30f, () => FullDayBot.State == "CLOSED", FullDayBot.Shop);
		PanelChecks("b");
		CrewMark("b-checked");
		WaitCrewMark("a-checked", 200f);
		Do("resync", delegate
		{
			loadsBefore = FullDayLab.Loads;
			replayBefore = Crew.ReceivedInReplay;
			Snapshot.RequestResync();
			return "asked for a resync (snapshots loaded " + loadsBefore + ", tables from a replay " + replayBefore + ")";
		});
		WaitFor("resynced", 120f, () => FullDayBot.Live && FullDayLab.Loads > loadsBefore, () => "state " + Net.ClientState.ToString() + ", loads " + FullDayLab.Loads);
		Check("table-from-day-log", () => Crew.ReceivedInReplay > replayBefore && FullDayBot.State == "CLOSED", () => "tables from a replay " + replayBefore + " -> " + Crew.ReceivedInReplay + ", state " + FullDayBot.State + ", last load " + (DayLog.Last?.ToString() ?? "-"));
		PanelChecks("b-resync");
		CrewMark("b-resynced");
		CrewMark("b-done");
		CrewMark("b-leaving");
		WaitCrewMark("b-leaving", 30f);
		Do("leave-for-good", delegate
		{
			EscapeUIManager escapeUIManager = StoreUIManager.Instance.escapeUIManager;
			escapeUIManager.OpenEscapeMenu();
			escapeUIManager.OnMainMenu();
			StoreUIManager.Instance.confirmPanelUIManager.OnYesClick();
			return "escape menu > Main menu > Yes (" + FullDayBot.Shop() + ")";
		});
		WaitFor("left", 90f, () => Net.Role == ProbablyTogether.Role.Off && Ui.AtMenu, () => "role " + Net.Role.ToString() + ", scene " + BotProbes.Scene);
		Check("forgot-table", () => Crew.Today == null && !CrewPanel.Visible, () => "table here " + Crew.Canon(Crew.Today) + ", panel " + CrewPanel.Visible);
	}
}
