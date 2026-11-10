using System.Linq;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CoreOpsB : DayNightBotBase
{
	public override string Name => "coreops-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		JoinSteps();
		WaitFor("say-line", 120f, () => Chat.Lines.Any((Chat.Line l) => l.System && l.Text.Contains("Hello from the panel")), () => "lines " + Chat.Lines.Count);
		WaitFor("kicked", 120f, () => (Client.LastRefusal ?? "").Contains("refused (kicked): Removed by the server owner.") && Net.ClientState == ClientState.Idle, () => "last refusal '" + Client.LastRefusal + "', client " + Net.ClientState);
		WaitFor("menu", 60f, () => BotProbes.Scene == "EmporiumMenu" && Net.Role == ProbablyTogether.Role.Off, () => "scene " + BotProbes.Scene + ", role " + Net.Role);
	}
}
