using System.Linq;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ReplayLateBot : BotScript
{
	private string address;

	public override string Name => "replay-late";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 420f;

	protected override void Build()
	{
		WaitFor("first-live", 180f, () => Net.IsClient && Net.IsLive && BotProbes.InShop, () => "player " + Net.LocalPlayer);
		Do("leave", delegate
		{
			address = (string.IsNullOrEmpty(Args.Join) ? Net.ServerAddress : Args.Join);
			Net.Disconnect();
			return "left " + address;
		});
		WaitFor("menu", 60f, () => Net.ClientState == ClientState.Idle && BotProbes.Scene == "EmporiumMenu", () => "scene " + BotProbes.Scene);
		Delay("let-the-server-log", 12f);
		Do("rejoin", delegate
		{
			Net.Connect(address, Args.Password ?? "", Args.Name);
			return "connecting to " + address;
		});
		WaitFor("live-again", 150f, () => ReplayLab.loads >= 2 && Net.IsLive && BotProbes.InShop, () => "player " + Net.LocalPlayer + "; " + DayLog.Last);
		Check("replayed-log", () => DayLog.Last != null && DayLog.Last.Replayed >= 5 && DayLog.Last.Replayed == DayLog.Last.Expected && DayLog.Last.ChecksBad == 0 && DayLog.Last.ChecksOk >= DayLog.Last.Replayed, () => DayLog.Last?.ToString() ?? "");
		Check("rolls-match-replayed", () => ReplayLab.seen.Count >= 5 && (from x in ReplayLab.seen.Take(5)
			select x.n).SequenceEqual(Enumerable.Range(1, 5)) && ReplayLab.seen.Take(5).All((ReplayLab.Seen x) => x.replayed && x.ok), () => string.Join(" | ", from x in ReplayLab.seen.Take(5)
			select "#" + x.n + (x.replayed ? " replayed " : " live ") + x.detail));
		Check("held-live-after-log", delegate
		{
			ReplayLab.Seen seen = ReplayLab.Find(6);
			return seen != null && !seen.replayed && seen.ok && ReplayLab.seen.IndexOf(seen) == 5 && DayLog.Last.Live >= 1;
		}, delegate
		{
			ReplayLab.Seen seen = ReplayLab.Find(6);
			return (seen != null) ? ("#6 " + (seen.replayed ? "replayed" : "applied live") + " at position " + (ReplayLab.seen.IndexOf(seen) + 1) + " (after the log), " + DayLog.Last.Live + " held live frame(s); " + seen.detail) : ("#6 not seen; seen " + ReplayLab.SeenText());
		});
		WaitFor("got-8", 150f, () => ReplayLab.Find(8) != null, () => "seen " + ReplayLab.SeenText());
		Check("all-match", () => ReplayLab.seen.Count >= 8 && ReplayLab.seen.All((ReplayLab.Seen x) => x.ok) && Replay.ChecksBad == 0, () => "seen " + ReplayLab.SeenText() + "; checks ok " + Replay.ChecksOk + ", differ " + Replay.ChecksBad + ", uid drift " + Replay.UidDrift);
		Delay("settle", 1f);
		AskServer("ask");
		SameAsServer("same-digest", "replay.digest");
		SameAsServer("same-uid", "replay.uid");
		SameAsServer("same-cash", "cash");
		Do("done", delegate
		{
			ReplayLab.Cue("done");
			return "cue sent";
		});
	}
}
