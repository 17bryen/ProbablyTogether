using System.Globalization;
using System.Linq;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ReplayLiveBot : BotScript
{
	private int loadsBefore;

	private int badBefore;

	public override string Name => "replay-live";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 420f;

	protected override void Build()
	{
		WaitFor("live", 180f, () => Net.IsClient && Net.IsLive && BotProbes.InShop, () => "player " + Net.LocalPlayer + "; " + DayLog.Last);
		WaitFor("got-7", 200f, () => ReplayLab.Find(7) != null, () => "seen " + ReplayLab.SeenText());
		Check("rolls-match", () => ReplayLab.seen.Count >= 7 && ReplayLab.seen.All((ReplayLab.Seen x) => x.ok), () => ReplayLab.seen.Count + " test actions, all rolls/uid/pick equal to the server's: " + string.Join(" | ", ReplayLab.seen.Select((ReplayLab.Seen x) => "#" + x.n + (x.replayed ? "r " : " ") + x.detail)));
		Check("replay-checks", () => Replay.ChecksBad == 0 && Replay.ChecksOk >= 7, () => "checks ok " + Replay.ChecksOk + ", differ " + Replay.ChecksBad + ", uid drift " + Replay.UidDrift + ((Replay.ChecksBad > 0) ? (" (" + Replay.LastMismatch + ")") : ""));
		AskServer("ask");
		SameAsServer("same-digest", "replay.digest");
		SameAsServer("same-uid", "replay.uid");
		SameAsServer("same-cash", "cash");
		Do("break-own-copy", delegate
		{
			loadsBefore = ReplayLab.loads;
			badBefore = Replay.ChecksBad;
			int playerCash = Game.Store.playerCash;
			Game.Store.playerCash = playerCash + 7;
			ReplayLab.askAgain = true;
			ReplayLab.Cue("broke");
			return "cash " + playerCash + " -> " + Game.Store.playerCash + " here only; cue sent";
		});
		WaitFor("check-caught", 60f, () => Replay.ChecksBad > badBefore, () => "replay check: " + Replay.LastMismatch);
		WaitFor("resynced", 120f, () => ReplayLab.loads > loadsBefore && Net.IsLive, () => "reloaded: " + DayLog.Last?.ToString() + "; " + ReplayLab.askAgainResult);
		Check("replay-after-resync", () => DayLog.Last != null && DayLog.Last.Replayed >= 8 && DayLog.Last.Replayed == DayLog.Last.Expected && DayLog.Last.ChecksBad == 0 && DayLog.Last.ChecksOk >= 8 && ReplayLab.seen.Count >= 8 && (from x in ReplayLab.seen.Take(8)
			select x.n).SequenceEqual(Enumerable.Range(1, 8)) && ReplayLab.seen.Take(8).All((ReplayLab.Seen x) => x.replayed && x.ok), () => DayLog.Last?.ToString() + "; seen " + ReplayLab.SeenText());
		Step("one-snapshot", 30f, delegate(BotStep st)
		{
			st.Detail = ReplayLab.loads - loadsBefore + " snapshot load(s) " + st.Elapsed.ToString("F0", CultureInfo.InvariantCulture) + " s after the reload for two asks in one frame";
			if (ReplayLab.loads > loadsBefore + 1)
			{
				return StepStatus.Fail;
			}
			return (st.Elapsed >= 13f) ? StepStatus.Pass : StepStatus.Running;
		});
		AskServer("ask-2");
		SameAsServer("same-digest-2", "replay.digest");
		SameAsServer("same-uid-2", "replay.uid");
		SameAsServer("same-cash-2", "cash");
		Do("done", delegate
		{
			ReplayLab.Cue("done");
			return "cue sent";
		});
	}
}
