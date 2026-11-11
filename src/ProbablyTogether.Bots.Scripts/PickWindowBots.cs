using System;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

/// The trade window with two players choosing: B picks first; C's window stays minus that trade and comes back after C reloads.
/// B and C run with --br-spec-window.
public abstract class PickWinBase : SpecsBotBase
{
	protected static int WithTrade => Members.State.list.Count((Member m) => m.x != null && m.x.ContainsKey("spec"));

	protected static string Where => "window " + (Specs.WindowShown ? "up" : "not up") + ", " + Specs.OfferedCount + " on offer, put up again " + Specs.Reoffers + " time(s), seat " + Members.LocalSeat + ", trade " + (Specs.Of(Members.LocalSeat) ?? "none") + ", " + WithTrade + " player(s) with a trade";
}

public sealed class PickWinA : PickWinBase
{
	public override string Name => "pickwin-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 360f;

	protected override void Build()
	{
		JoinSteps();
		WaitFor("seat-1-has-the-shops-trade", 90f, () => Members.LocalSeat == 1 && Specs.Of(1) != null, () => Where);
		Check("i-was-never-asked", () => !Specs.WindowShown, () => Where);
		WaitFor("all-three-have-a-trade", 300f, () => WithTrade >= 3, () => Where);
		Check("three-different-trades", () => (from m in Members.State.list
			where m.x.ContainsKey("spec")
			select m.x["spec"]).Distinct().Count() == 3, () => string.Join(", ", from m in Members.State.list
			select m.n + "=" + (m.x.ContainsKey("spec") ? m.x["spec"] : "-")));
	}
}

public sealed class PickWinB : PickWinBase
{
	private int n;

	public override string Name => "pickwin-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 360f;

	protected override void Build()
	{
		JoinSteps();
		WaitFor("my-window-is-up", 60f, () => Specs.WindowShown && Specs.OfferedCount >= 2, () => Where);
		WaitFor("c-is-here-too", 180f, () => Net.Players.Count >= 3, () => Net.Players.Count + " connected");
		Delay("c-gets-its-window-and-reloads", 30f);
		Check("my-window-is-still-up", () => Specs.WindowShown, () => Where);
		Do("choose", delegate
		{
			n = Specs.OfferedCount;
			if (!Specs.ChooseOffered(0))
			{
				throw new InvalidOperationException("nothing on offer");
			}
			return "pressed Choose on the first of " + n;
		});
		WaitFor("i-have-a-trade", 20f, () => Specs.Of(Members.LocalSeat) != null, () => Where);
		Delay("the-window-goes", 4f);
		Check("my-window-closed", () => !Specs.WindowShown, () => Where);
		WaitFor("c-picked-too", 120f, () => WithTrade >= 3, () => Where);
	}
}

public sealed class PickWinC : PickWinBase
{
	private int n0;

	private int re0;

	private int loaded0;

	public override string Name => "pickwin-c";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 360f;

	protected override void Build()
	{
		JoinSteps();
		WaitFor("my-window-is-up", 60f, () => Specs.WindowShown && Specs.OfferedCount >= 2, () => Where);
		Do("reload-while-choosing", delegate
		{
			loaded0 = Machines.LoadedCount;
			Snapshot.RequestResync();
			return "asked the server for the shop again with the window open";
		});
		WaitFor("reloaded", 40f, () => Machines.LoadedCount > loaded0 && Net.IsLive, () => "loads " + Machines.LoadedCount + " (was " + loaded0 + "), live " + Net.IsLive);
		WaitFor("the-window-is-back-after-the-reload", 10f, () => Specs.WindowShown && Specs.OfferedCount >= 2, () => Where);
		Do("before-b-picks", delegate
		{
			n0 = Specs.OfferedCount;
			re0 = Specs.Reoffers;
			return Where;
		});
		WaitFor("b-picked", 180f, () => WithTrade >= 2, () => Where);
		WaitFor("my-window-stays-with-one-trade-fewer", 10f, () => Specs.WindowShown && Specs.OfferedCount == n0 - 1 && Specs.Reoffers > re0, () => Where + " (was " + n0 + " on offer)");
		Check("i-have-no-trade-yet", () => Specs.Of(Members.LocalSeat) == null, () => Where);
		Do("choose", delegate
		{
			if (!Specs.ChooseOffered(0))
			{
				throw new InvalidOperationException("nothing on offer");
			}
			return "pressed Choose on the first of " + Specs.OfferedCount;
		});
		WaitFor("i-have-a-trade", 20f, () => Specs.Of(Members.LocalSeat) != null, () => Where);
		Delay("the-window-goes", 4f);
		Check("my-window-closed", () => !Specs.WindowShown, () => Where);
		Agree("same-as-server", 30f, "core.digest", "items.digest");
	}
}
