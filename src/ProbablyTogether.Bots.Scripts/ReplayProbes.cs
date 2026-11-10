namespace ProbablyTogether.Bots.Scripts;

public sealed class ReplayProbes : BotProbeSet
{
	public override void Register()
	{
		ReplayLab.Register();
		BotProbes.Add("replay.digest", () => (!Game.ShopLoaded) ? "" : ReplayLab.Hex(Replay.Digest()));
		BotProbes.Add("replay.uid", () => (!Game.ShopLoaded) ? "" : BotProbeSet.Str(Game.Store.currentUniqueId));
		BotProbes.Add("replay.sent", () => BotProbeSet.Str(ReplayLab.sent));
		BotProbes.Add("daylog.count", () => (!Net.IsServer) ? "" : BotProbeSet.Str(DayLog.Count));
	}
}
