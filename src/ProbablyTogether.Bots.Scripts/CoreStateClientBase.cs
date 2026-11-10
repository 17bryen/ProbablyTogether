namespace ProbablyTogether.Bots.Scripts;

public abstract class CoreStateClientBase : BotScript
{
	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 400f;

	protected abstract string R { get; }

	protected void Common()
	{
		WaitFor("live", 200f, () => Net.IsClient && Net.IsLive && BotProbes.InShop, () => "player " + Net.LocalPlayer + "; " + DayLog.Last);
		Do("hi", delegate
		{
			CstLab.role = R;
			Net.ToServer("cst.hi", new CstHi
			{
				role = R
			});
			return "I am " + R + " (player " + Net.LocalPlayer + ")";
		});
		Check("apply-nesting", delegate
		{
			int actor;
			int actor2;
			int actor3;
			using (Net.Apply(3))
			{
				actor = Net.Actor;
				using (Net.Apply())
				{
					actor2 = Net.Actor;
				}
				using (Net.Apply(-1))
				{
					actor3 = Net.Actor;
				}
			}
			int actor4 = Net.Actor;
			Vars["n"] = "Apply(3) " + actor + ", nested Apply() " + actor2 + ", Apply(-1) " + actor3 + ", after " + actor4;
			return actor == 3 && actor2 == 3 && actor3 == -1 && actor4 == -1;
		}, () => Vars["n"]);
		WaitFor("features-read", 30f, () => !string.IsNullOrEmpty(BotProbes.Read("cst.features")) && BotProbes.Read("cst.water").Contains(":"), () => BotProbes.Read("cst.features") + "; " + BotProbes.Read("cst.water"));
		AskServer("ask");
		SameAsServer("same-features", "cst.features");
		SameAsServer("same-water", "cst.water");
		Do("gated", delegate
		{
			Net.ToServer("cst.gated", new CstEmpty
			{
				n = 1
			});
			return "sent a request a server gate refuses";
		});
		WaitFor("denied", 15f, () => Net.LastDeniedKind == "cst.gated" && Net.LastDeniedReason == "Lab gate: refused on purpose.", () => "sys.denied " + Net.LastDeniedKind + ": " + Net.LastDeniedReason);
		WaitFor("actors", 60f, () => CstLab.ActorsOk(3, replayed: false), () => "seen " + string.Join(" ", CstLab.actorSeen) + " (n:actor/expected)");
	}

	protected void Tick()
	{
		Step("obey", 300f, delegate(BotStep st)
		{
			CstLab.ClientTick();
			st.Detail = "orders followed; last lock reply " + ((CstLab.lastReply == null) ? "-" : (CstLab.lastReply.op + " " + (CstLab.lastReply.ok ? "ok" : "refused") + " holder " + CstLab.lastReply.holder));
			return CstLab.doneOrdered ? StepStatus.Pass : StepStatus.Running;
		});
	}
}
