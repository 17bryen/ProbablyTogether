using System;

namespace ProbablyTogether.Bots.Scripts;

/// Idles: the screenshot tour (ProbablyTogether.UI.ShotTour) drives the copy, which gets the bots' player identity.
public sealed class ShotsBot : BotScript
{
	public override string Name => "shots";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		WaitFor("the-tour-is-over", 880f, () => false, () => "a tour of the screens is running");
	}
}
