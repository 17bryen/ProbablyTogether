namespace ProbablyTogether.Bots.Scripts;

public sealed class CoreOpsProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("ops.state", () => (!Net.IsServer) ? "" : ServerMode.StatusState);
		BotProbes.Add("ops.runs", () => (!Net.IsServer) ? "" : BotProbeSet.Str(ServerMode.NewRunsStarted));
		BotProbes.Add("ops.cmds", () => (!Net.IsServer) ? "" : BotProbeSet.Str(PanelLink.CommandsRun));
	}
}
