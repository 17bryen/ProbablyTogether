namespace ProbablyTogether.Bots.Scripts;

public sealed class SyncDayCash : BotScript
{
	public override string Name => "sync-day-cash";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 400f;

	protected override void Build()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer + ", scene " + BotProbes.Scene);
		WaitFor("shop-loaded", 180f, () => BotProbes.InShop, () => "scene " + BotProbes.Scene + ", day " + BotProbes.Read("day"));
		Delay("settle", 2f);
		AskServer("ask-server");
		SameAsServer("same-day", "day");
		SameAsServer("same-cash", "cash");
	}
}
