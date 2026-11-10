using System.Linq;

namespace ProbablyTogether.Bots.Scripts;

public sealed class EscServer : BotScript
{
	public override string Name => "esc-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 400f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop", 300f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene);
		WaitFor("a-player", 200f, () => Net.Players.Keys.Any(Net.IsPlayerLive), () => Net.Players.Count + " player(s)");
		Delay("idle", 45f);
	}
}
