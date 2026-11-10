using System;
using System.Linq;

namespace ProbablyTogether.Bots.Scripts;

public sealed class FullDayServer : FullDayBot
{
	public override string Name => "fullday-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 2400f;

	private static int LiveCount()
	{
		return ProbablyTogether.Server.ByPlayer.Values.Count((Server.Peer p) => p.State == ProbablyTogether.Server.PeerState.Ready);
	}

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop", 300f, () => Game.ShopLoaded && ProbablyTogether.Server.ShopReady, () => "scene " + BotProbes.Scene);
		Do("fixture", delegate
		{
			string text = FullDayLab.SpawnFixture();
			if (!Snapshot.NewMorning("lab fixture (fullday bot)"))
			{
				throw new InvalidOperationException("the fresh morning snapshot failed");
			}
			if (Net.Players.Count > 0)
			{
				Snapshot.SendToAll();
			}
			return text + "; players already in " + Net.Players.Count;
		});
		Do("state", () => FullDayLab.Core() + "; queue head " + BotProbes.Read("fd.head") + " of " + BotProbes.Read("fd.queue") + "; upgrades " + BotProbes.Read("fd.upgrades"));
		WaitFor("two-live", 300f, () => LiveCount() >= 2, () => LiveCount() + " live player(s)");
		Step("clients-done", 2200f, delegate(BotStep st)
		{
			bool num = FullDayLab.Marks.Contains("a-done");
			bool flag = FullDayLab.Marks.Contains("b-done");
			st.Detail = "marks " + string.Join(",", FullDayLab.Marks) + "; " + FullDayLab.Core();
			return (num && flag) ? StepStatus.Pass : StepStatus.Running;
		}).Soft();
		Do("final", () => "morning events sent " + MorningEvents.Sent + " (last day " + MorningEvents.LastDay + "); DayLog " + DayLog.Count + "; " + FullDayLab.Core() + "; " + FullDayLab.Counters() + "; parts " + FullDayLab.Parts());
	}
}
