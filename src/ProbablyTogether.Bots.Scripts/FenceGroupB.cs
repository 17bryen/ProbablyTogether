using System.Collections.Generic;

namespace ProbablyTogether.Bots.Scripts;

public sealed class FenceGroupB : ItemsBotBase
{
	public override string Name => "fence-group-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		JoinSteps();
		DigestSame("join-digest");
		int id = 0;
		float at = -10f;
		Step("wait-a", 500f, delegate(BotStep st)
		{
			if (id == 0)
			{
				if (st.Elapsed - at < 3f)
				{
					return StepStatus.Running;
				}
				id = BotProbes.AskServer();
				at = st.Elapsed;
				return StepStatus.Running;
			}
			Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
			if (dictionary == null)
			{
				return StepStatus.Running;
			}
			id = 0;
			if (dictionary.TryGetValue("items.botdone", out var value) && value == "1")
			{
				st.Detail = "client A done; " + ItemsMove.AppliedCount + " action(s) applied here";
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		});
		DigestSame("final-digest");
		Check("no-mismatch", () => ItemsMove.ApplyMismatches == 0 && Items.TapeMismatches == 0, () => "applied " + ItemsMove.AppliedCount + ", mismatches " + ItemsMove.ApplyMismatches + ", tape mismatches " + Items.TapeMismatches);
	}
}
