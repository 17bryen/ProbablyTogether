using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class NewRunProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("run.count", () => (!Net.IsServer) ? "" : BotProbeSet.Str(ServerMode.NewRunsStarted));
	}
}

public abstract class NewRunBotBase : SpecsBotBase
{
	protected static bool HasPack(GameItem bay)
	{
		return bay != null && GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)bay, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).Any((GameItem i) => i.identifier == "fanny_pack");
	}
}

/// A new run with everybody connected: seats kept, new bays with a fanny pack each,
/// seat 1 gets the shop's trade again, seat 2 picks afresh.
public sealed class NewRunServer : NewRunBotBase
{
	public override string Name => "newrun-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop && Game.ShopLoaded && ProbablyTogether.Server.ShopReady, () => "scene " + BotProbes.Scene);
		WaitFor("first-run-set-up", 240f, () => Specs.Of(1) == SpecsBotBase.ShopScenario && Specs.Of(2) == "DRUG_DEALER" && Bays.BayOf(1) != null && Bays.BayOf(2) != null, () => "seat 1: " + (Specs.Of(1) ?? "none") + ", seat 2: " + (Specs.Of(2) ?? "none") + "; " + Members.Describe());
		Check("a-pack-each", () => HasPack(Bays.BayOf(1)) && HasPack(Bays.BayOf(2)), () => "fanny pack in seat 1's bay: " + HasPack(Bays.BayOf(1)) + ", in seat 2's: " + HasPack(Bays.BayOf(2)));
		Delay("players-settle", 4f);
		Check("start-a-new-run", () => ServerMode.StartNewRun("rig test") == null, () => "new runs started: " + ServerMode.NewRunsStarted);
		WaitFor("new-shop-up", 240f, () => ProbablyTogether.Server.ShopReady && Game.ShopLoaded && DayNightBotBase.Day == 1 && Members.Count == 2, () => "ready " + ProbablyTogether.Server.ShopReady + ", day " + DayNightBotBase.Day + "; " + Members.Describe());
		WaitFor("seats-kept-bays-new", 120f, () => ProbablyTogether.Server.PlayerCount == 2 && Bays.BayOf(1) != null && Bays.BayOf(2) != null && Specs.Of(1) == SpecsBotBase.ShopScenario, () => ProbablyTogether.Server.PlayerCount + " connected; bay of seat 1 " + ((Bays.BayOf(1) != null) ? "here" : "missing") + ", of seat 2 " + ((Bays.BayOf(2) != null) ? "here" : "missing") + "; seat 1: " + (Specs.Of(1) ?? "none") + "; " + Members.Describe());
		WaitFor("seat-2-picked-again", 240f, () => Specs.Of(2) == "FARMER", () => "seat 2: " + (Specs.Of(2) ?? "none") + "; last refusal '" + Specs.LastRefusal + "'");
		Check("a-pack-each-again", () => HasPack(Bays.BayOf(1)) && HasPack(Bays.BayOf(2)), () => "fanny pack in seat 1's bay: " + HasPack(Bays.BayOf(1)) + ", in seat 2's: " + HasPack(Bays.BayOf(2)));
		Delay("clients-finish", 12f);
	}
}

public sealed class NewRunA : NewRunBotBase
{
	public override string Name => "newrun-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		JoinSteps();
		Check("seat-1", () => Members.LocalSeat == 1, () => "my seat " + Members.LocalSeat);
		WaitFor("first-run-set-up", 240f, () => Specs.Of(1) == SpecsBotBase.ShopScenario && BaysBotBase.MyBay != null && Specs.Of(2) == "DRUG_DEALER", () => "seat 1: " + (Specs.Of(1) ?? "none") + ", seat 2: " + (Specs.Of(2) ?? "none"));
		WaitServer("new-run-started", 240f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "run.count") >= 1, "run.count");
		WaitFor("back-in-the-new-shop", 240f, () => DayNightBotBase.Live && DayNightBotBase.Day == 1 && Members.LocalSeat == 1 && Members.Count == 2 && BaysBotBase.MyBay != null && Specs.Of(1) == SpecsBotBase.ShopScenario && Specs.Of(2) != "DRUG_DEALER", () => "live " + DayNightBotBase.Live + ", my seat " + Members.LocalSeat + "; " + Members.Describe() + "; my bay " + ((BaysBotBase.MyBay != null) ? "here" : "missing") + "; seat 1: " + (Specs.Of(1) ?? "none") + ", seat 2: " + (Specs.Of(2) ?? "none"));
		Delay("kit-settles", 3f);
		Check("my-bay-is-stocked-again", () => BaysBotBase.CountIn(BaysBotBase.MyBay) > 2 && HasPack(BaysBotBase.MyBay) && !BaysBotBase.MyBay.contentWindow.isLocked, () => "my new bay holds " + BaysBotBase.CountIn(BaysBotBase.MyBay) + " item(s), fanny pack " + HasPack(BaysBotBase.MyBay));
		WaitFor("seat-2-picked-again", 240f, () => Specs.Of(2) == "FARMER", () => "seat 2: " + (Specs.Of(2) ?? "none"));
		Delay("their-kit-settles", 3f);
		Agree("same-in-the-new-run", 40f, "core.digest", "items.digest", "spec.list", "bay.owners", "mem.count", "cash");
	}
}

public sealed class NewRunB : NewRunBotBase
{
	public override string Name => "newrun-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		JoinSteps();
		Check("seat-2", () => Members.LocalSeat == 2, () => "my seat " + Members.LocalSeat);
		WaitFor("my-bay", 120f, () => BaysBotBase.MyBay != null && Specs.Of(1) != null, () => "my bay " + ((BaysBotBase.MyBay != null) ? "here" : "missing"));
		Do("pick", delegate
		{
			ProbablyTogether.UI.Chat.Send("/trade chemist");
			return "typed '/trade chemist'";
		});
		WaitFor("i-am-the-chemist", 30f, () => Specs.Of(2) == "DRUG_DEALER", () => "mine: " + (Specs.Of(2) ?? "none"));
		WaitServer("new-run-started", 240f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "run.count") >= 1, "run.count");
		WaitFor("back-in-the-new-shop", 240f, () => DayNightBotBase.Live && DayNightBotBase.Day == 1 && Members.LocalSeat == 2 && Members.Count == 2 && BaysBotBase.MyBay != null && Specs.Of(2) == null, () => "live " + DayNightBotBase.Live + ", my seat " + Members.LocalSeat + "; " + Members.Describe() + "; my bay " + ((BaysBotBase.MyBay != null) ? "here" : "missing") + "; mine: " + (Specs.Of(2) ?? "none"));
		Check("a-fresh-bay-with-a-pack", () => HasPack(BaysBotBase.MyBay) && BaysBotBase.CountIn(BaysBotBase.MyBay) == 1 && !BaysBotBase.MyBay.contentWindow.isLocked, () => "my new bay holds " + BaysBotBase.CountIn(BaysBotBase.MyBay) + " item(s), fanny pack " + HasPack(BaysBotBase.MyBay));
		Do("pick-again", delegate
		{
			ProbablyTogether.UI.Chat.Send("/trade hydroponic");
			return "typed '/trade hydroponic'";
		});
		WaitFor("i-am-the-farmer", 30f, () => Specs.Of(2) == "FARMER", () => "mine: " + (Specs.Of(2) ?? "none") + "; refusal '" + Specs.LastRefusal + "'");
		Delay("kit-settles", 3f);
		Agree("same-in-the-new-run", 40f, "core.digest", "items.digest", "spec.list", "bay.owners", "mem.count", "cash");
	}
}
