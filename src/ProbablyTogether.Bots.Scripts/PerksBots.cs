using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

/// Both players pick perks (B through the game's own screens); both copies agree on the effects, also after B reloads.
public abstract class PerksBotBase : DayNightBotBase
{
	protected static int Me => Members.LocalSeat;

	protected static int Other => Net.Players.Keys.FirstOrDefault((int p) => p != Net.LocalPlayer);

	protected static int OtherSeat => Members.SeatOf(Other);

	protected static string ShopList => "[" + string.Join(",", (DayNightBotBase.Ps.startingPerks ?? new List<StartingPerk>()).Select((StartingPerk p) => p.id)) + "]";

	protected static string State => "record {" + Perks.Describe() + "}; shop's list " + ShopList + "; cash " + DayNightBotBase.Ps.playerCash + ", rent " + DayNightBotBase.Ps.rentValue + ", security " + Standing.RepNow("FACTION_SECURITY").ToString("0.0") + "; last refusal '" + Perks.LastRefusal + "'; screens opened " + PerkScreens.Opened + ", continued " + PerkScreens.Continued + ", problem '" + PerkScreens.LastProblem + "'";

	protected static bool InShopList(string id)
	{
		return DayNightBotBase.Ps.startingPerks != null && DayNightBotBase.Ps.startingPerks.Any((StartingPerk p) => p != null && p.id == id);
	}
}

public sealed class PerksH : PerksBotBase
{
	private int cash0;

	private int rent0;

	private double sec0;

	private int cash1;

	private string refusedPoints = "";

	private string refusedPair = "";

	public override string Name => "pk-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		WaitFor("shop", 240f, () => Net.IsLive && Items.Ready && Me == 1 && Bays.BayOf(1) != null, () => "state " + Net.ClientState + ", seat " + Me);
		Check("i-have-not-picked", () => !Perks.Picked(1) && Perks.Enabled, () => State);
		Do("before", delegate
		{
			cash0 = DayNightBotBase.Ps.playerCash;
			rent0 = DayNightBotBase.Ps.rentValue;
			sec0 = Standing.RepNow("FACTION_SECURITY");
			return State;
		});
		Do("too-many-points-is-refused-here-already", delegate
		{
			string text = Perks.Refusal(1, new List<string> { "alert", "diplomatic" });
			refusedPoints = text ?? "";
			string text2 = Perks.Refusal(1, new List<string> { "convict", "model_citizen" });
			refusedPair = text2 ?? "";
			return text + " / " + text2;
		});
		Check("four-points-and-a-pair-that-does-not-go-together-are-refused", () => refusedPoints.Contains("points") && refusedPair.Contains("do not go together"), () => refusedPoints + " / " + refusedPair);
		Do("pick", delegate
		{
			Perks.Pick(new string[4] { "saving", "bad_lease", "model_citizen", "alert" });
			return "asked";
		});
		WaitFor("picked", 30f, () => Perks.Picked(1), () => State);
		Check("savings-and-bad-lease", () => DayNightBotBase.Ps.playerCash == cash0 + 150 && DayNightBotBase.Ps.rentValue == rent0 + 40, () => "cash " + cash0 + " -> " + DayNightBotBase.Ps.playerCash + ", rent " + rent0 + " -> " + DayNightBotBase.Ps.rentValue);
		Check("model-citizen-is-in-my-row", () => Math.Abs(Standing.RepNow("FACTION_SECURITY") - sec0 - 30.0) < 0.01 && Math.Abs(Standing.RepOf(1, "FACTION_SECURITY") - 30.0) < 0.01, () => "security " + sec0 + " -> " + Standing.RepNow("FACTION_SECURITY") + ", my row " + Standing.RepOf(1, "FACTION_SECURITY"));
		Check("the-shop's-list-has-all-four", () => InShopList("saving") && InShopList("bad_lease") && InShopList("model_citizen") && InShopList("alert") && StartingPerk.IsAlert(), () => State);
		Do("a-second-pick-is-refused", delegate
		{
			Perks.Pick(new string[0]);
			return "asked";
		});
		WaitFor("refused", 20f, () => Perks.LastRefusal.Contains("already picked"), () => State);
		Do("before-theirs", delegate
		{
			cash1 = DayNightBotBase.Ps.playerCash;
			return State;
		});
		WaitFor("the-other-player-picked", 300f, () => OtherSeat > 1 && Perks.Picked(OtherSeat), () => State);
		Check("their-savings-count-too", () => DayNightBotBase.Ps.playerCash == cash1 + 150, () => "cash " + cash1 + " -> " + DayNightBotBase.Ps.playerCash);
		Check("side-street-is-the-shop's-and-illiterate-is-not", () => InShopList("side_street") && !InShopList("illiterate") && !InShopList("tinkerer") && Perks.Has(OtherSeat, "illiterate") && Perks.Has(OtherSeat, "tinkerer"), () => State);
		Check("i-can-read", () => !StartingPerk.IsIlliterate() && HealthData.CanRead() && !StartingPerk.IsTinkerer(), () => "illiterate here " + StartingPerk.IsIlliterate());
		Delay("the-other-player-loads-again", 40f);
		Agree("same-shop", 60f, "core.digest", "items.digest", "econ.hash");
		Check("no-reload", () => DayNight.Mismatches == 0, () => "day/night mismatches " + DayNight.Mismatches + "; " + State);
	}
}

public sealed class PerksB : PerksBotBase
{
	private DayLog.ReplayStats before;

	public override string Name => "pk-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role + ", player " + Net.LocalPlayer);
		WaitFor("live", 240f, () => Net.IsLive && Items.Ready, () => "state " + Net.ClientState);
		WaitFor("my-bay", 60f, () => Me > 1 && Bays.BayOf(Me) != null, () => State);
		WaitFor("the-founder-picked", 120f, () => Perks.Picked(1) && Perks.Has(1, "alert"), () => State);
		Do("the-game's-screens-ask-me", delegate
		{
			PerkScreens.LabOn = true;
			return "on";
		});
		WaitFor("the-trade-screen-is-up", 30f, () => PerkScreens.SpecOpen, () => State);
		Do("i-pick-a-trade", delegate
		{
			Specs.Pick("GUNSMITH");
			return "asked";
		});
		WaitFor("the-perk-screen-is-up-instead", 60f, () => Specs.Of(Me) == "GUNSMITH" && PerkScreens.PerksOpen && !PerkScreens.SpecOpen, () => "my trade " + (Specs.Of(Me) ?? "-") + "; trade screen " + PerkScreens.SpecOpen + ", perk screen " + PerkScreens.PerksOpen + "; " + State);
		Check("vigilant-is-locked-with-the-founder's-name", () => PerkScreens.LockedBy(Perks.Def("alert")) != null && PerkScreens.LockedBy(Perks.Def("alert")).rawName.Contains(Members.NameOf(1)) && PerkScreens.LockedBy(Perks.Def("saving")) == null && PerkScreens.LockedBy(Perks.Def("convict")) != null, () => "alert: " + (PerkScreens.LockedBy(Perks.Def("alert"))?.rawName ?? "free") + "; saving: " + (PerkScreens.LockedBy(Perks.Def("saving"))?.rawName ?? "free") + "; convict: " + (PerkScreens.LockedBy(Perks.Def("convict"))?.rawName ?? "free"));
		Do("a-pick-with-vigilant-is-refused", delegate
		{
			Perks.Pick(new string[2] { "alert", "side_street" });
			return "asked";
		});
		WaitFor("refused", 20f, () => Perks.LastRefusal.Contains(Members.NameOf(1)), () => State);
		Do("i-select-four-on-the-screen-and-press-continue", delegate
		{
			List<StartingPerk> list = NewGameData.Instance.startingPerks;
			list.Clear();
			foreach (string item in new string[4] { "saving", "side_street", "illiterate", "tinkerer" })
			{
				list.Add(Perks.Def(item));
			}
			PerkScreens.PerksContinue();
			return "selected " + string.Join(",", PerkScreens.Selected());
		});
		WaitFor("picked-and-the-cover-is-gone", 40f, () => Perks.Picked(Me) && !PerkScreens.PerksOpen && !PerkScreens.SpecOpen, () => State);
		Check("illiterate-and-tinkerer-are-mine", () => StartingPerk.IsIlliterate() && !HealthData.CanRead() && StartingPerk.IsTinkerer() && !InShopList("illiterate"), () => "illiterate here " + StartingPerk.IsIlliterate() + ", can read " + HealthData.CanRead() + "; " + State);
		Check("the-founder's-are-the-shop's", () => StartingPerk.IsAlert() && InShopList("side_street") && StartingPerk.IsSideStreet(), () => State);
		Delay("settle", 12f);
		Agree("same-shop-before-the-reload", 30f, "core.digest", "items.digest", "econ.hash");
		Do("load-the-shop-again", delegate
		{
			before = DayLog.Last;
			Snapshot.RequestResync("rig: load the day again");
			return "asked";
		});
		WaitFor("loaded-again", 120f, () => DayLog.Last != null && DayLog.Last != before && Net.IsLive && Items.Ready, () => "last replay " + (DayLog.Last?.ToString() ?? "none"));
		Check("every-step-of-the-day-replayed", () => DayLog.Last.ChecksBad == 0, () => DayLog.Last.ToString());
		Delay("a-look-over-passes", 3f);
		Check("still-mine-after-the-load", () => Perks.Picked(Me) && StartingPerk.IsIlliterate() && Perks.Has(1, "alert") && !PerkScreens.PerksOpen, () => State);
		Delay("two-comparisons-with-the-server", 22f);
		Agree("same-shop", 30f, "core.digest", "items.digest", "econ.hash");
		Check("one-reload-only", () => Digest.ReloadsAsked <= 1, () => "reloads asked " + Digest.ReloadsAsked);
	}
}
