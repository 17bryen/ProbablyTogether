using System.Collections.Generic;
using System.Globalization;

namespace ProbablyTogether.Bots.Scripts;

public sealed class BotReask : BotScript
{
	public override string Name => "bot-reask";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 300f;

	protected override void Build()
	{
		int id = 0;
		int resends0 = 0;
		int held0 = 0;
		bool sawLoading = false;
		WaitFor("live", 180f, () => Net.IsClient && Net.IsLive && BotProbes.InShop, () => "player " + Net.LocalPlayer + ", scene " + BotProbes.Scene);
		Delay("after-join-snapshot", 12f);
		Do("resync-and-ask", delegate
		{
			resends0 = BotProbes.Resends;
			held0 = BotProbes.Held;
			Snapshot.RequestResync();
			id = BotProbes.AskServer();
			return "asked for a resync, then probe ask #" + id + " in the same frame (live " + Net.IsLive + ")";
		});
		Step("reloaded", 120f, delegate(BotStep st)
		{
			if (!Net.IsLive)
			{
				sawLoading = true;
			}
			st.Detail = "left live " + sawLoading + ", live now " + Net.IsLive + ", in shop " + BotProbes.InShop;
			return (sawLoading && Net.IsLive && BotProbes.InShop) ? StepStatus.Pass : StepStatus.Running;
		});
		Step("answered", 60f, delegate(BotStep st)
		{
			Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
			if (dictionary == null)
			{
				return StepStatus.Running;
			}
			st.Detail = "ask #" + id + " answered after the reload: server " + BotProbes.Format(dictionary, "role", "day", "cash", "players") + "; asked again " + (BotProbes.Resends - resends0) + ", held while loading " + (BotProbes.Held - held0);
			return StepStatus.Pass;
		}).TimeoutDetail = () => "ask #" + id + " never answered; asked again " + (BotProbes.Resends - resends0) + ", held " + (BotProbes.Held - held0);
		Check("recovered-a-drop", () => BotProbes.Resends - resends0 >= 1, () => "asked again " + (BotProbes.Resends - resends0).ToString(CultureInfo.InvariantCulture) + " time(s) (the first send was dropped while the server counted this copy as loading)");
	}
}
