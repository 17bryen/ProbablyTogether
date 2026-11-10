using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PingProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("ping.ok", () => BotProbeSet.Str(Pointing.ServerAccepted));
		BotProbes.Add("ping.no", () => BotProbeSet.Str(Pointing.ServerRefused));
		BotProbes.Add("ping.norate", () => BotProbeSet.Str(Pointing.ServerRefusedRate));
		BotProbes.Add("ping.nogone", () => BotProbeSet.Str(Pointing.ServerRefusedGone));
		BotProbes.Add("ping.lastto", () => string.Join(",", Pointing.ServerLastTo.Select((int i) => i.ToString(CultureInfo.InvariantCulture))));
		BotProbes.Add("ping.loading", () => (!Net.IsServer) ? "" : string.Join(",", from p in Net.Players.Keys
			where !Net.IsPlayerLive(p)
			orderby p
			select p.ToString(CultureInfo.InvariantCulture)));
		BotProbes.Add("ping.daylog", () => (!Net.IsServer) ? "" : BotProbeSet.Str(PingLab.PingFramesInLog(out var _)));
		BotProbes.Add("ping.buckets", () => BotProbeSet.Str(Pointing.BucketCount));
		BotProbes.Add("ping.recv", () => BotProbeSet.Str(Pointing.Received));
		BotProbes.Add("ping.marks", () => BotProbeSet.Str(PointMarks.Marks.Count));
	}
}
