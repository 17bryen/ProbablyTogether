using System.Globalization;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CrewQueueProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("crewq.decision", () => (!Net.IsServer) ? "" : CrewCustomers.LastDecision);
		BotProbes.Add("crewq.today", () => (!Net.IsServer) ? "" : CrewCustomers.Today.ToString(CultureInfo.InvariantCulture));
	}
}
