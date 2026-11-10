using System.Linq;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class StuckA : StuckBase
{
	private string address;

	private string name;

	private string pw;

	public override string Name => "stuck-a";

	protected override void Build()
	{
		JoinSteps();
		Do("mode", () => "mode " + StuckBase.Mode + ((StuckBase.Cust.Length > 0) ? (", customer " + StuckBase.Cust) : ""));
		Do("open-shutter", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "clicked the shutter: " + FullDayBot.Shop();
		});
		WaitFor("open", 40f, () => FullDayBot.State == "OPEN" && FullDayBot.Ps.lockShutterButtonFlag == 0, FullDayBot.Shop).Soft();
		Talk("morning-visitor", 90f, (Dialogue d) => 0, () => FullDayLab.CurrentId() == "none" || (FullDayBot.Ps.lockNextBellFlag == 0 && FullDayBot.NoDialogue && FullDayBot.Ps.IsClientDimissable())).Soft();
		DismissUntilGone("morning-visitor-gone", 60f).Soft();
		if (StuckBase.Cust.Length > 0)
		{
			Do("queue-cust", delegate
			{
				FullDayBot.Lab("add", StuckBase.Cust);
				return "asked the server to queue " + StuckBase.Cust + " next";
			});
			Delay("queued", 2f);
		}
		CallStep("call", StuckBase.Cust.Length == 0);
		Talk("to-mid", 90f, (Dialogue d) => 0, delegate
		{
			CounterChannel counter = DialogueSystem.Counter;
			return counter.Current != null && !counter.LineUnfinished && (counter.WaitingManual || counter.IsChoiceNow || counter.EndPending) && DialogueSync.Seq("counter") >= Dlg0 + 2;
		});
		Do("mid", () => "mid-conversation: " + StuckBase.Why() + "; customer " + FullDayLab.CurrentId());
		Same("mid", FullDayBot.All.Concat(new string[2] { "fd.holder", "fd.customer" }).ToArray());
		Mark("a-mid");
		switch (StuckBase.Mode)
		{
		case "leave":
		case "drop":
			Do("go", delegate
			{
				address = Args.Join;
				name = Args.Name ?? "bot1";
				pw = Args.Password ?? "";
				if (StuckBase.Mode == "drop")
				{
					Client.Disconnect("lab: connection cut mid-conversation");
					return "connection cut (" + FullDayBot.Shop() + ")";
				}
				EscapeUIManager escapeUIManager = StoreUIManager.Instance.escapeUIManager;
				escapeUIManager.OpenEscapeMenu();
				escapeUIManager.OnMainMenu();
				StoreUIManager.Instance.confirmPanelUIManager.OnYesClick();
				return "escape menu > Main menu > Yes (" + FullDayBot.Shop() + ")";
			});
			WaitFor("at-menu", 90f, () => Net.Role == ProbablyTogether.Role.Off && Ui.AtMenu, () => "role " + Net.Role.ToString() + ", scene " + BotProbes.Scene);
			Delay("away", 25f);
			Do("rejoin", delegate
			{
				Net.Connect(address, pw, name);
				return "Net.Connect(" + address + ") as " + name;
			});
			WaitFor("rejoined", 240f, () => FullDayBot.Live, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
			break;
		case "resync":
			Do("resync", delegate
			{
				loads0 = FullDayLab.Loads;
				Snapshot.RequestResync("lab: stuck-a asks mid-conversation");
				return "asked for a resync mid-conversation (loads " + loads0 + ")";
			});
			WaitFor("resynced", 120f, () => FullDayBot.Live && FullDayLab.Loads > loads0, () => "state " + Net.ClientState.ToString() + ", loads " + FullDayLab.Loads);
			Do("after-resync", () => "after the reload: " + StuckBase.Why());
			FinishVisit("a-finish", 150f);
			break;
		}
		Mark("a-on");
		if (StuckBase.Mode == "leave" || StuckBase.Mode == "drop")
		{
			WaitMark("wait-b-done", "b-done", 400f);
			Same("after-b", FullDayBot.All);
		}
		else
		{
			WaitMark("wait-b-closed", "b-closed", 400f);
			WaitFor("closed-here", 30f, () => FullDayBot.State == "CLOSED", FullDayBot.Shop).Soft();
			Night("1");
		}
		Mark("a-done");
	}
}
