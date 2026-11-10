using System;
using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PingServer : PingBotBase
{
	public override string Name => "ping-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		Do("wire", delegate
		{
			PingLab.Ensure();
			return "ping lab channel wired";
		});
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop", 300f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene);
		WaitFor("two-live", 300f, () => Net.Players.Keys.Count(Net.IsPlayerLive) >= 2, () => Net.Players.Count + " player(s)");
		WaitFor("a-done", 780f, () => PingLab.Store.TryGetValue("a-done", out var value) && value == "1", () => "marks " + string.Join(",", PingLab.Store.Keys.OrderBy<string, string>((string k) => k, StringComparer.Ordinal)) + "; shown " + Pointing.ServerAccepted + ", refused " + Pointing.ServerRefused);
		Step("daylog-clean", 0f, delegate(BotStep st)
		{
			int total;
			int num = PingLab.PingFramesInLog(out total);
			st.Detail = num + " ping.* message(s) among " + total + " in today's day log (day " + DayLog.Day + ")";
			return (num == 0 && total > 0) ? StepStatus.Pass : StepStatus.Fail;
		});
		Check("counters", () => Pointing.ServerAccepted == Expect() && Pointing.ServerRefusedRate == 4 && Pointing.ServerRefusedGone == 1, () => "shown " + Pointing.ServerAccepted + " (want " + Expect() + ", ping-a's count: item, spot, an undrawn item, a covered item when there is one, " + 4 + " of a raw 8, 3 of a client burst, 4 honest, 4 behind a lag spike, 1 during a reload), refused for rate " + Pointing.ServerRefusedRate + " (want " + 4 + ": only the raw burst; no honest ping in the whole run), refused gone " + Pointing.ServerRefusedGone + " (want 1), all refused " + Pointing.ServerRefused);
		Check("buckets", () => Pointing.BucketCount <= Net.Players.Count, () => Pointing.BucketCount + " rate entries, " + Net.Players.Count + " player(s) connected");
		WaitFor("b-done", 120f, () => PingLab.Store.ContainsKey("b-done"), () => "b-done not set");
	}

	private static int Expect()
	{
		if (!PingLab.Store.TryGetValue("a-expect", out var value) || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return -1;
		}
		return result;
	}
}
