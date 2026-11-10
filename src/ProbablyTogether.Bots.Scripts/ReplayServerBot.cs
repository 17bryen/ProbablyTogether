using System.Linq;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ReplayServerBot : BotScript
{
	private float next;

	public override string Name => "replay-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 420f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 300f, () => Net.IsLive && BotProbes.InShop, () => "scene " + BotProbes.Scene + ", day log " + DayLog.Count + " (morning snapshot #" + DayLog.MorningSnapshot + ")");
		WaitFor("live-player", 180f, () => ReplayLab.PeersIn(ProbablyTogether.Server.PeerState.Ready) > 0, () => Net.PlayerName(ReplayLab.PeerIn(ProbablyTogether.Server.PeerState.Ready)?.Info.Id ?? (-1)) + " is live");
		WaitFor("a-player-left", 150f, () => ReplayLab.left > 0, () => ReplayLab.left + " left; " + ProbablyTogether.Server.PlayerCount + " connected");
		Step("send-5", 40f, delegate(BotStep st)
		{
			if (Time.realtimeSinceStartup < next)
			{
				return StepStatus.Running;
			}
			next = Time.realtimeSinceStartup + 1f;
			ReplayLab.Send();
			if (ReplayLab.sent < 5)
			{
				return StepStatus.Running;
			}
			st.Detail = "sent #1-#5 (seeded); day log " + DayLog.Count + " message(s); last " + ReplayLab.lastServer;
			return (DayLog.Count >= 5) ? StepStatus.Pass : StepStatus.Fail;
		});
		Step("send-while-loading", 150f, delegate(BotStep st)
		{
			Server.Peer peer = ReplayLab.PeerIn(ProbablyTogether.Server.PeerState.Syncing);
			if (peer == null)
			{
				return StepStatus.Running;
			}
			st.Detail = "#6 sent to all EXCEPT " + peer.Info.Name + " while they load morning snapshot #" + peer.SnapshotSeq + " + " + DayLog.Count + " log message(s) (they must still get it): " + ReplayLab.Send(peer.Info.Id);
			return StepStatus.Pass;
		});
		WaitFor("both-live", 150f, () => ReplayLab.PeersIn(ProbablyTogether.Server.PeerState.Ready) >= 2, () => string.Join(", ", ProbablyTogether.Server.ByPlayer.Values.Select((Server.Peer p) => p.Info.Name + "=" + p.State)));
		Do("send-live", () => "#7 sent with both live: " + ReplayLab.Send());
		Step("send-after-break", 120f, delegate(BotStep st)
		{
			if (ReplayLab.CueCount("broke") < 1)
			{
				return StepStatus.Running;
			}
			st.Detail = "client 1 broke its copy; #8 sent: " + ReplayLab.Send();
			return StepStatus.Pass;
		});
		WaitFor("clients-done", 150f, () => ReplayLab.CueCount("done") >= 2, () => ReplayLab.CueCount("done") + " client(s) done");
		Do("final", () => "day log " + DayLog.Count + " message(s); digest " + ReplayLab.Hex(Replay.Digest()) + " uid " + Game.Store.currentUniqueId + " " + BotProbes.Format(BotProbes.ReadAll(), "day", "cash"));
	}
}
