using System;
using System.Collections.Generic;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

/// The player changes its own cash by 7 behind the mod's back and asks for a reload; the server must save both sides
/// and log what differed.
public sealed class DesyncReportProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("dr.received", () => (!Net.IsServer) ? "" : BotProbeSet.Str(DesyncReport.Received));
		BotProbes.Add("dr.summary", () => (!Net.IsServer) ? "" : DesyncReport.LastSummary);
		BotProbes.Add("dr.file", () => (!Net.IsServer) ? "" : ((DesyncReport.LastFile.Length > 0 && System.IO.File.Exists(DesyncReport.LastFile)) ? (new System.IO.FileInfo(DesyncReport.LastFile).Length + " bytes: " + DesyncReport.LastFile) : "none"));
	}
}

public sealed class DesyncReportB : DayNightBotBase
{
	private int loaded0;

	private int cash0;

	public override string Name => "dr-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 240f;

	protected override void Build()
	{
		JoinSteps();
		Delay("settle", 3f);
		Do("differ-and-ask", delegate
		{
			cash0 = DayNightBotBase.Ps.playerCash;
			DayNightBotBase.Ps.playerCash = cash0 + 7;
			loaded0 = Machines.LoadedCount;
			Snapshot.RequestResync("rig: my cash is 7 more than the shop's");
			return "cash here " + (cash0 + 7) + " (the shop's is " + cash0 + "); asked for a reload; reports built here " + DesyncReport.Built;
		});
		WaitFor("reloaded", 40f, () => Machines.LoadedCount > loaded0 && Net.IsLive && DayNightBotBase.Ps.playerCash == cash0, () => "loads " + Machines.LoadedCount + ", cash " + DayNightBotBase.Ps.playerCash);
		WaitServer("the-server-has-my-side", 20f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "dr.received") != "" && DayNightBotBase.V(v, "dr.received") != "0", "dr.received", "dr.file");
		WaitServer("its-log-names-what-differed", 20f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "dr.summary").Contains("cash: theirs " + (cash0 + 7) + " / ours " + cash0), "dr.summary");
		WaitServer("both-sides-are-in-a-file", 20f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "dr.file").Contains("desync-reports") && DayNightBotBase.V(v, "dr.file").Contains("bytes"), "dr.file");
	}
}
