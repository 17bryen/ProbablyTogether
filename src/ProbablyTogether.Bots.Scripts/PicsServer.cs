using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PicsServer : UiCoopBase
{
	public override string Name => "pics-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		Do("wire", delegate
		{
			DialogueBotNet.Ensure();
			return "dialogue lab channel wired (marks, phone-open)";
		});
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop", 300f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene);
		WaitFor("two-live", 400f, () => Net.Players.Keys.Count(Net.IsPlayerLive) >= 2, () => Net.Players.Count + " player(s)");
		WaitFor("run-over", 800f, () => DayNightRun.RunOver, () => "run over " + DayNightRun.RunOver);
		Delay("after-ending", 40f);
	}
}
