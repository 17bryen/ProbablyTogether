using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CustomersServer : CustomersBot
{
	public override string Name => "customers-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 1100f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene);
		Check("morning", () => (int)Game.Store.storeState == 2, () => "state " + Game.Store.storeState.ToString() + ", day " + BotProbes.Read("day"));
		WaitFor("two-ready", 300f, () => CustomersBot.Int(CustomersBot.Mine("cust.ready")) >= 2, () => "ready players " + CustomersBot.Mine("cust.ready"));
		WaitFor("opened-by-a", 180f, () => (int)Game.Store.storeState == 1, CustomersBot.ShowMine);
		Check("nobody-at-counter", () => CustomersBot.Mine("cust.current") == "none", CustomersBot.ShowMine);
		Step("add-sellers", 0f, delegate(BotStep st)
		{
			st.Detail = Customers.BotDo("add", "scavGeneral", "scavGeneral", "scavGeneral", "scavGeneral", "scavGeneral");
			return (CustomersBot.Mine("cust.head") == "scavGeneral") ? StepStatus.Pass : StepStatus.Fail;
		});
		Step("our-scanner", 0f, delegate(BotStep st)
		{
			st.Detail = Customers.BotDo("owned", "gene_scanner");
			return (CustomersBot.CounterTool() != null) ? StepStatus.Pass : StepStatus.Fail;
		});
		WaitFor("b-ready-lou", 900f, () => Customers.BotMarkNames.Contains("b:lou-ready"), () => "marks " + string.Join(",", Customers.BotMarkNames) + "; " + CustomersBot.ShowMine());
		Step("lou-waiting", 0f, delegate(BotStep st)
		{
			st.Detail = Customers.BotDo("wait", "lou");
			return (CustomersBot.Mine("cust.current") == "wanted5morning") ? StepStatus.Pass : StepStatus.Fail;
		});
		Check("counter-passed-at-once", () => Locks.Holder("counter") > 0, () => "holder " + CustomersBot.Mine("cust.holder") + " (" + Net.PlayerName(Locks.Holder("counter")) + "); dismissable " + CustomersBot.Mine("cust.dismissable"));
		WaitFor("lou-talks", 20f, () => DialogueSystem.Counter.Current != null, () => "morning dialogue line " + CustomersBot.Mine("cust.dlgfp") + ", dialogue steps " + CustomersBot.Mine("cust.dlgseq"));
		WaitFor("b-refused-lou", 90f, () => Customers.BotMarkNames.Contains("b:lou-refused"), () => "marks " + string.Join(",", Customers.BotMarkNames) + "; " + CustomersBot.ShowMine());
		Step("timer-dismiss", 0f, delegate(BotStep st)
		{
			int num = DialogueSync.Seq("counter");
			bool flag = DialogueSystem.Counter.Current != null;
			st.Detail = Customers.BotTimerDismiss() + "; dialogue was " + (flag ? "on" : "off") + ", steps " + num + " -> " + DialogueSync.Seq("counter") + ", line now " + CustomersBot.Mine("cust.dlgfp");
			return (CustomersBot.Mine("cust.current") == "none" && DialogueSync.Seq("counter") == num && DialogueSystem.Counter.Current == null) ? StepStatus.Pass : StepStatus.Fail;
		});
		WaitFor("clients-done", 600f, () => Customers.BotDone + Customers.BotFailed >= 2, () => "done/failed " + CustomersBot.Mine("cust.botdone") + "; " + CustomersBot.ShowMine());
		Check("clients-passed", () => Customers.BotFailed == 0, () => "done/failed " + CustomersBot.Mine("cust.botdone"));
		Do("final", () => CustomersBot.ShowMine() + "; refusals " + Customers.Refusals + "; " + Customers.DigestText());
	}
}
