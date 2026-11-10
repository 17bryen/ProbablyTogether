using ProbablyTogether.Systems;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PresenceUiCoopProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("ui.runover", () => (!DayNightRun.RunOver) ? "0" : "1");
		BotProbes.Add("ui.notice", () => Notices.Last);
	}
}
