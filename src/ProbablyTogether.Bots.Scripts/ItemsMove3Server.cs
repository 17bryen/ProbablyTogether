namespace ProbablyTogether.Bots.Scripts;

public sealed class ItemsMove3Server : ItemsBot3Base
{
	public override string Name => "items-move3-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 1500f;

	protected override void Build()
	{
		WaitFor("shop-loaded", 300f, () => Items.Ready && Net.IsLive, () => "scene " + BotProbes.Scene);
		Quiet();
		Do("state", () => "digest " + ItemsMove.Digest() + "; " + ItemsMoveProbes.IdCheck());
		WaitFor("clients-done", 1400f, () => ItemsMove3Probes.Done >= 2, () => "done " + ItemsMove3Probes.Done + ", marks " + ItemsMove3Probes.Marks + "; players " + Net.Players.Count);
		Do("final", () => "digest " + ItemsMove.Digest() + "; " + ItemsMoveProbes.IdCheck() + "; sec " + ItemsMove3Probes.Sec() + "; customer " + ItemsBot3Base.Cur());
	}
}
