namespace ProbablyTogether.Bots.Scripts;

public sealed class NegotiationRealServer : BotScript
{
	public override string Name => "nego3-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 1500f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop, () => "scene " + BotProbes.Scene);
		Do("state", () => BotProbes.Format(BotProbes.ReadAll(), "day", "cash", "nego.state", "nego.career"));
		WaitFor("clients-done", 1400f, () => NegotiationProbes.Marks.Contains("b-done"), () => "marks " + string.Join(",", NegotiationProbes.Marks));
		Do("final", () => BotProbes.Format(BotProbes.ReadAll(), "cash", "nego.cust", "nego.hash", "nego.career") + "; refusals logged above");
	}
}
