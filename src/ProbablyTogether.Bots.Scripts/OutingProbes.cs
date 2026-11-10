using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class OutingProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("out.nego", () => Negotiation.Explain());
		BotProbes.Add("out.count", () => (!Net.IsServer) ? "" : BotProbeSet.Str(Outings.OutCount));
		BotProbes.Add("out.host", () => (!Net.IsServer) ? "" : (HostTrip.Out ? "1" : "0"));
		BotProbes.Add("out.jackson", () => (!Net.IsServer) ? "" : (HostTrip.CountersBorrowed ? "1" : "0"));
	}
}
