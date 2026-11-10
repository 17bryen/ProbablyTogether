namespace ProbablyTogether.Bots.Scripts;

public sealed class BotFailTest : BotScript
{
	public override string Name => "bot-failtest";

	public override float TimeoutSeconds => 30f;

	protected override void Build()
	{
		WaitFor("never", 2f, () => false, () => "this step is meant to fail");
		Check("not-reached", () => true);
	}
}
