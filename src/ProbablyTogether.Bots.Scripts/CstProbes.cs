namespace ProbablyTogether.Bots.Scripts;

public sealed class CstProbes : BotProbeSet
{
	public override void Register()
	{
		CstLab.Register();
		BotProbes.Add("cst.features", CstLab.Features);
		BotProbes.Add("cst.water", CstLab.Water);
		BotProbes.Add("cst.digest", () => (!Game.ShopLoaded) ? "" : CstLab.Hex(Replay.Digest()));
	}
}
