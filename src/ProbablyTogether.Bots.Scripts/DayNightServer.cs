using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DayNightServer : BotScript
{
	public override string Name => "daynight-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 400f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop", 300f, () => Game.ShopLoaded && ProbablyTogether.Server.ShopReady, () => "scene " + BotProbes.Scene);
		Do("lab-timeout", delegate
		{
			DayNight.SetLabTimeout(20);
			return "ready timeout " + DayNight.ReadyTimeoutSeconds + " s";
		});
		Do("gated-kind", delegate
		{
			DnUi.RegisterGatedKind();
			return "test.gated has a lab handler (the core's gates see only kinds with a handler)";
		});
		Do("state", () => BotProbes.Format(BotProbes.ReadAll(), "day", "cash", "dn.state", "dn.slot", "dn.endless"));
	}
}
