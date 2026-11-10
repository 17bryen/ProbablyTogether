using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class StuckB : StuckBase
{
	public override string Name => "stuck-b";

	protected override void Build()
	{
		JoinSteps();
		WaitMark("wait-a-mid", "a-mid", 400f);
		switch (StuckBase.Mode)
		{
		case "leave":
		case "drop":
			WaitFor("a-gone", 60f, () => Net.Players.Count == 1, () => Net.Players.Count + " player(s)");
			Do("a-gone-state", () => "A is gone: " + StuckBase.Why());
			Delay("a-away", 3f);
			break;
		case "resync":
			WaitMark("wait-a-on", "a-on", 300f);
			break;
		case "idle":
		{
			Do("a-idle-state", () => "A stops pacing: " + StuckBase.Why());
			float lastPress = 0f;
			int idlePresses = 0;
			int holderA = -1;
			Step("counter-freed-early", 50f, delegate(BotStep st)
			{
				int num = Locks.HeldBy("counter");
				if (holderA < 0)
				{
					holderA = num;
				}
				if (holderA > 0 && num != holderA)
				{
					st.Detail = "A's counter freed after " + FullDayBot.F(st.Elapsed) + " s of A idle and " + idlePresses + " press(es) by B (holder now " + num + ")";
					return StepStatus.Pass;
				}
				if (FullDayBot.Now - lastPress < 1.2f)
				{
					return StepStatus.Running;
				}
				lastPress = FullDayBot.Now;
				DialogUIManager instance = DialogUIManager.Instance;
				CounterChannel counter = DialogueSystem.Counter;
				if (counter.Current != null && (Object)(object)instance != (Object)null)
				{
					idlePresses++;
					if (counter.IsChoiceNow)
					{
						instance.SelectChoice(0);
					}
					else
					{
						instance.OnAdvancePressed();
					}
				}
				st.Detail = "holder " + num + " (A " + holderA + "), " + idlePresses + " press(es); " + StuckBase.Why();
				return StepStatus.Running;
			});
			break;
		}
		}
		FinishVisit("b-finish", (StuckBase.Mode == "idle") ? 180 : 150);
		Same("visit-over", FullDayBot.All.Concat(new string[2] { "fd.holder", "fd.customer" }).ToArray());
		Close("b-close");
		Same("closed");
		Mark("b-closed");
		Night("1");
		Mark("b-done");
	}
}
