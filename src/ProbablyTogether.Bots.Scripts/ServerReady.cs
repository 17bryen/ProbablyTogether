namespace ProbablyTogether.Bots.Scripts;

public sealed class ServerReady : BotScript
{
	public override string Name => "server-ready";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 400f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop, () => "scene " + BotProbes.Scene);
		Do("state", () => BotProbes.Format(BotProbes.ReadAll(), "day", "cash", "favor", "players"));
	}
}
