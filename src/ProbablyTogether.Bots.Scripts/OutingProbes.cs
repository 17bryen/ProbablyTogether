using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class OutingProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("out.count", () => (!Net.IsServer) ? "" : BotProbeSet.Str(Outings.OutCount));
	}
}
