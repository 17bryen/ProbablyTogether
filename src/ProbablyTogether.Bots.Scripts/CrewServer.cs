using System;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CrewServer : CrewBot
{
	public override string Name => "crew-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 1800f;

	private static int LiveCount()
	{
		return ProbablyTogether.Server.ByPlayer.Values.Count((Server.Peer p) => p.State == ProbablyTogether.Server.PeerState.Ready);
	}

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		Check("best-reselect", () => CrewLab.BestSelfTest().StartsWith("ok", StringComparison.Ordinal), CrewLab.BestSelfTest);
		Do("best-reselect-steps", (Func<string>)CrewLab.BestSelfTest);
		WaitFor("shop", 300f, () => Game.ShopLoaded && ProbablyTogether.Server.ShopReady, () => "scene " + BotProbes.Scene);
		Do("fixtures", delegate
		{
			string text = CrewLab.Spawn();
			if (!Snapshot.NewMorning("lab fixtures (crew bot)"))
			{
				throw new InvalidOperationException("the fresh morning snapshot failed");
			}
			if (Net.Players.Count > 0)
			{
				Snapshot.SendToAll();
			}
			return text + "; players already in " + Net.Players.Count;
		});
		WaitFor("two-live", 300f, () => LiveCount() >= 2, () => LiveCount() + " live player(s)");
		Step("clients-done", 1700f, delegate(BotStep st)
		{
			bool flag = CrewLab.Marks.Contains("a-done");
			bool flag2 = CrewLab.Marks.Contains("b-done");
			st.Detail = "marks " + string.Join(",", CrewLab.Marks) + "; tables sent " + Crew.Sent + ", counted today " + Crew.Counted;
			return (flag && flag2) ? StepStatus.Pass : StepStatus.Running;
		}).Soft();
		Do("final", () => "tables sent " + Crew.Sent + " (last " + BotProbes.Read("crew.last") + "); today " + Crew.Canon(Crew.Build("final")) + "; day log " + DayLog.Count);
	}
}
