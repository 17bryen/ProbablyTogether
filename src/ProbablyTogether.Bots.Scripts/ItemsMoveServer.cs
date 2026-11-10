namespace ProbablyTogether.Bots.Scripts;

public sealed class ItemsMoveServer : ItemsBotBase
{
	public override string Name => "items-move-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 1500f;

	protected override void Build()
	{
		WaitFor("shop-loaded", 300f, () => Items.Ready, () => "scene " + BotProbes.Scene);
		Do("state", () => "digest " + ItemsMove.Digest() + "; " + ItemsMoveProbes.IdCheck());
		WaitFor("clients-done", 1300f, () => ItemsMoveProbes.Done, () => "digest " + ItemsMove.Digest() + "; players " + Net.Players.Count);
		Do("final", () => "digest " + ItemsMove.Digest() + "; " + ItemsMoveProbes.IdCheck() + "; uid " + Game.Store?.currentUniqueId);
	}
}
