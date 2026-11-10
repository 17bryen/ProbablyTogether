namespace ProbablyTogether.Bots.Scripts;

public sealed class SecurityServer : BotScript
{
	public override string Name => "security-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 1400f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop, () => "scene " + BotProbes.Scene);
		Do("state", () => BotProbes.Format(BotProbes.ReadAll(), "day", "cash", "sec.wrongful", "sec.aug", "sec.unlocked", "sec.poster", "sec.fp"));
		WaitFor("two-players", 400f, () => Net.Players.Count >= 2, () => "players " + Net.Players.Count).Soft();
		WaitFor("clients-done", 1300f, () => SecurityProbes.Done >= 2, () => "done " + SecurityProbes.Done + ", phase " + SecurityProbes.Phase);
		Do("final", () => BotProbes.Format(BotProbes.ReadAll(), "day", "cash", "sec.wrongful", "sec.wanted8", "sec.aug", "sec.crimes", "sec.slips", "sec.rep", "sec.bribe", "sec.eleanor", "sec.fp", "sec.ended", "sec.state"));
	}
}
