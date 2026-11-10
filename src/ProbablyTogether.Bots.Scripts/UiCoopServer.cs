using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class UiCoopServer : UiCoopBase
{
	public override string Name => "ui-coop-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		Do("wire", delegate
		{
			DialogueBotNet.Ensure();
			return "dialogue lab channel wired (marks, phone-open)";
		});
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop", 300f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene);
		WaitFor("two-live", 240f, () => Net.Players.Keys.Count(Net.IsPlayerLive) >= 2, () => Net.Players.Count + " player(s), live: " + string.Join(",", Net.Players.Keys.Where(Net.IsPlayerLive)));
		WaitFor("run-over", 480f, () => DayNightRun.RunOver, () => "run over " + DayNightRun.RunOver + ", ending '" + DayNightRun.Ending + "'");
		Do("ending", () => "the run ended (" + DayNightRun.Ending + "); waiting for the players' votes");
		S("after-votes", 90f, delegate(BotStep st)
		{
			if (!DayNightRun.RunOver && Net.IsLive && Game.ShopLoaded)
			{
				st.Detail = "day-night started the new run (day " + Game.Day + ", cash " + Game.Store?.playerCash + ")";
				return StepStatus.Pass;
			}
			if (st.Elapsed >= 60f)
			{
				st.Detail = "the run is still over after 60 s (day-night refused the votes, or does not count them yet)";
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		});
	}
}
