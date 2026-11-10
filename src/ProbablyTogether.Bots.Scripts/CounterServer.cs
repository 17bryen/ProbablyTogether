using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CounterServer : CounterBot
{
	public override string Name => "counter-server";

	public override BotRole Role => BotRole.Server;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene);
		WaitFor("two-ready", 900f, () => CustomersBot.Int(CustomersBot.Mine("cust.ready")) >= 2, () => "ready players " + CustomersBot.Mine("cust.ready"));
		WaitFor("opened-by-a", 600f, () => (int)Game.Store.storeState == 1, CustomersBot.ShowMine);
		Step("queue", 0f, delegate(BotStep st)
		{
			st.Detail = Customers.BotDo("aug") + "; " + Customers.BotDo("add", "scavGeneral", "scavGeneral") + "; head " + CustomersBot.Mine("cust.head") + ", queue " + CustomersBot.Mine("cust.queue");
			return (CustomersBot.Mine("cust.head") == "scavGeneral") ? StepStatus.Pass : StepStatus.Fail;
		});
		WaitFor("a-wants-cig", 900f, () => Customers.BotMarkNames.Contains("a:want-cig"), () => "marks " + string.Join(",", Customers.BotMarkNames));
		Step("cig-placed", 0f, delegate(BotStep st)
		{
			st.Detail = Customers.BotDo("sell", "fake_cigarette");
			Customers.BotMarkNames.Add("s:cig");
			return st.Detail.Contains("on the counter") ? StepStatus.Pass : StepStatus.Fail;
		});
		WaitFor("a-wants-talk", 300f, () => Customers.BotMarkNames.Contains("a:want-talk"), () => "marks " + string.Join(",", Customers.BotMarkNames));
		Step("talk-ready", 0f, delegate(BotStep st)
		{
			st.Detail = Customers.BotDo("talk");
			Customers.BotMarkNames.Add("s:talk");
			return st.Detail.Contains("can be interrogated") ? StepStatus.Pass : StepStatus.Fail;
		});
		WaitFor("clients-done", 2000f, () => Customers.BotDone + Customers.BotFailed >= 2, () => "done/failed " + CustomersBot.Mine("cust.botdone") + "; " + CustomersBot.ShowMine());
		Check("clients-passed", () => Customers.BotFailed == 0, () => "done/failed " + CustomersBot.Mine("cust.botdone"));
		Do("final", () => CustomersBot.ShowMine() + "; refusals " + Customers.Refusals + "; locks " + string.Join(",", from kv in Locks.Table()
			select kv.Key + "=" + kv.Value));
	}
}
