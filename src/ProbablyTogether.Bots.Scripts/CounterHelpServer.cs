using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CounterHelpServer : CounterHelpBot
{
	public override string Name => "counterhelp-server";

	public override BotRole Role => BotRole.Server;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene);
		WaitFor("two-ready", 900f, () => CustomersBot.Int(CustomersBot.Mine("cust.ready")) >= 2, () => "ready players " + CustomersBot.Mine("cust.ready"));
		Step("tools", 0f, delegate(BotStep st)
		{
			st.Detail = CounterHelpProbes.Do("tools");
			return (!st.Detail.Contains("NOT-PLACED") && !st.Detail.Contains("missing")) ? StepStatus.Pass : StepStatus.Fail;
		});
		WaitFor("opened-by-a", 600f, () => (int)Game.Store.storeState == 1, CustomersBot.ShowMine);
		Step("queue", 0f, delegate(BotStep st)
		{
			st.Detail = Customers.BotDo("add", "scavGeneral") + "; head " + CustomersBot.Mine("cust.head") + ", queue " + CustomersBot.Mine("cust.queue");
			Customers.BotMarkNames.Add("s:queued");
			return (CustomersBot.Mine("cust.head") == "scavGeneral") ? StepStatus.Pass : StepStatus.Fail;
		});
		WaitFor("a-wants-goods", 900f, () => Customers.BotMarkNames.Contains("a:want-goods"), () => "marks " + string.Join(",", Customers.BotMarkNames));
		Step("goods", 0f, delegate(BotStep st)
		{
			st.Detail = CounterHelpProbes.Do("goods");
			Customers.BotMarkNames.Add("s:goods");
			return (!st.Detail.Contains("NOT-PLACED") && !st.Detail.Contains("missing")) ? StepStatus.Pass : StepStatus.Fail;
		});
		WaitFor("clients-done", 2000f, () => Customers.BotDone + Customers.BotFailed >= 2, () => "done/failed " + CustomersBot.Mine("cust.botdone") + "; " + CustomersBot.ShowMine());
		Check("clients-passed", () => Customers.BotFailed == 0, () => "done/failed " + CustomersBot.Mine("cust.botdone"));
		Do("final", () => CustomersBot.Mine("help.srv") + "; views " + CustomersBot.Mine("help.views") + "; spent strips " + CustomersBot.Mine("help.strips") + "; refusals " + Customers.Refusals + "; locks " + string.Join(",", from kv in Locks.Table()
			select kv.Key + "=" + kv.Value));
	}
}
