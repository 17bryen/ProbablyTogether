using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

/// Perks through a night: each pick's hand-outs, the shared addiction, each player's own withdrawal clock, and a restart that keeps the shop's toolboxes.
public abstract class PerksNightBotBase : DayNightBotBase
{
	protected static int Me => Members.LocalSeat;

	protected static int Other => Net.Players.Keys.FirstOrDefault((int p) => p != Net.LocalPlayer);

	protected static int OtherSeat => Members.SeatOf(Other);

	protected static HealthData H => DayNightBotBase.Ps.healthData;

	protected static int Toolboxes => Items.All().Count((GameItem i) => i != null && i.identifier == "toolbox" && GeneralHelper.IsItemOwned(i));

	protected static List<string> InBay(int seat)
	{
		GameItem val = Bays.BayOf(seat);
		return (val == null) ? new List<string>() : (from i in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null)
			select i.identifier).ToList();
	}

	protected static bool InShopList(string id)
	{
		return DayNightBotBase.Ps.startingPerks != null && DayNightBotBase.Ps.startingPerks.Any((StartingPerk p) => p != null && p.id == id);
	}

	protected static string State => "record {" + Perks.Describe() + "}; shop's list [" + string.Join(",", (DayNightBotBase.Ps.startingPerks ?? new List<StartingPerk>()).Select((StartingPerk p) => p.id)) + "]; rent " + DayNightBotBase.Ps.rentValue + ", luck " + H.luck + ", toolboxes " + Toolboxes + ", penalty " + H.GetTotalAddictionPenalty() + "; my bay [" + string.Join(",", InBay(Me)) + "]; deaths " + Perks.Deaths + "; last refusal '" + Perks.LastRefusal + "'; last applied " + Perks.LastApplied;
}

public sealed class PerksNightH : PerksNightBotBase
{
	private int rent0;

	private int luck0;

	private int day0;

	private int bay0;

	public override string Name => "pn-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 700f;

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && DayNightBotBase.Live && Me == 1 && Bays.BayOf(1) != null, () => "role " + Net.Role + ", live " + Net.IsLive);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2 && OtherSeat > 1 && Bays.BayOf(OtherSeat) != null, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		Delay("b-settles", 5f);
		Do("before", delegate
		{
			rent0 = DayNightBotBase.Ps.rentValue;
			luck0 = H.luck;
			day0 = DayNightBotBase.Day;
			bay0 = InBay(1).Count;
			Perks.Pick(new string[4] { "handy", "nicotine_addiction", "prime_real_estate", "lucky" });
			return State;
		});
		WaitFor("picked", 30f, () => Perks.Picked(1), () => State);
		Check("a-toolbox-for-the-shop-and-my-cigarettes", () => Toolboxes == 1 && Items.All().Count((GameItem i) => i != null && i.identifier == "packet_red_cigarette" && GeneralHelper.IsItemOwned(i)) == 1 && DayNightBotBase.Ps.rentValue == rent0 + 75 && H.luck == luck0 + 15, () => "bay had " + bay0 + " thing(s); rent was " + rent0 + ", luck " + luck0 + "; " + State);
		WaitFor("the-other-player-picked", 120f, () => Perks.Picked(OtherSeat), () => State);
		Check("stacking-counts-per-holder", () => Toolboxes == 2 && DayNightBotBase.Ps.rentValue == rent0 + 150 && H.luck == luck0 + 15 && Perks.Count("prime_real_estate") == 2, () => "rent was " + rent0 + "; " + State);
		Check("the-shop's-list-has-the-addiction-once-and-no-sentinel", () => DayNightBotBase.Ps.startingPerks.Count((StartingPerk p) => p.id == "nicotine_addiction") == 1 && !InShopList("sentinel") && InShopList("handy"), () => State);
		Check("i-am-addicted-and-not-ill", () => HealthData.IsNicotineAddicted() && !HealthData.IsSentinel() && !StartingPerk.IsSentinel(), () => "addicted " + HealthData.IsNicotineAddicted() + ", sentinel " + HealthData.IsSentinel());
		Check("two-in-withdrawal-is-sixteen", () => H.GetTotalAddictionPenalty() == 16, () => State);
		Do("the-clocks-are-set-for-tonight", delegate
		{
			Perks.LabClock(1, 0, 19);
			Perks.LabClock(OtherSeat, 4, 14);
			Perks.LabClock(OtherSeat, 5, 19);
			return State;
		});
		Delay("clocks-arrive", 3f);
		Do("open-click", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened", 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		WaitFor("shutter-settled", 20f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0, () => "flag " + DayNightBotBase.Ps.lockShutterButtonFlag);
		Do("queue", delegate
		{
			if (DayNightBotBase.Ps.CanEndDay())
			{
				return "every waiting customer can be sent away";
			}
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "clearqueue"
			});
			return "lab op clearqueue";
		});
		WaitFor("closable", 15f, () => DayNightBotBase.Ps.CanEndDay(), () => "queue " + DayNightBotBase.Ps.storeClientManager.clientStack.Count);
		Do("close", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "sent";
		});
		WaitFor("closed", 20f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State + "; run over " + DayNightRun.RunOver);
		Check("the-run-goes-on", () => !DayNightRun.RunOver, () => "run over " + DayNightRun.RunOver);
		Check("i-am-cured-and-only-i", () => !Perks.Has(1, "nicotine_addiction") && !HealthData.IsNicotineAddicted(), () => State);
		WaitFor("the-other-player-did-not-survive-and-starts-over", 60f, () => Perks.Deaths == 1 && !Perks.Picked(OtherSeat) && Members.BySeat(OtherSeat) != null, () => State);
		Check("what-their-perks-kept-doing-has-ended", () => DayNightBotBase.Ps.rentValue == rent0 + 75 && !InShopList("nicotine_addiction") && InShopList("handy") && InShopList("prime_real_estate") && Toolboxes == 2 && H.GetTotalAddictionPenalty() == 0, () => "rent was " + rent0 + "; " + State);
		Check("the-morning-report-names-them", () => DayNightBotBase.Ps.nightLogs != null && DayNightBotBase.Ps.nightLogs.Any((string l) => l.Contains(Members.NameOf(OtherSeat) + ": ")), () => "night log: " + string.Join(" | ", DayNightBotBase.Ps.nightLogs ?? new List<string>()));
		Delay("three-comparisons", 32f);
		Agree("same-shop", 60f, "core.digest", "items.digest", "econ.hash");
		Check("no-reload", () => DayNight.Mismatches == 0, () => "day/night mismatches " + DayNight.Mismatches + "; " + State);
	}
}

public sealed class PerksNightB : PerksNightBotBase
{
	private int day0;

	public override string Name => "pn-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 700f;

	protected override void Build()
	{
		JoinSteps();
		WaitFor("my-bay", 60f, () => Me > 1 && Bays.BayOf(Me) != null, () => State);
		WaitFor("the-founder-picked", 120f, () => Perks.Picked(1), () => State);
		Do("pick", delegate
		{
			day0 = DayNightBotBase.Day;
			Perks.Pick(new string[4] { "sentinel", "nicotine_addiction", "handy", "prime_real_estate" });
			return "asked";
		});
		WaitFor("picked", 30f, () => Perks.Picked(Me), () => State);
		Check("the-injector-and-the-cigarette-are-in-my-bay", () => InBay(Me).Count((string i) => i.IndexOf("injector", StringComparison.OrdinalIgnoreCase) >= 0) >= 1 && InBay(Me).Count >= 5, () => State);
		Check("i-am-addicted-and-ill", () => HealthData.IsNicotineAddicted() && HealthData.IsSentinel() && StartingPerk.IsSentinel() && HealthData.CanUseImmunivax(), () => "addicted " + HealthData.IsNicotineAddicted() + ", sentinel " + HealthData.IsSentinel());
		Check("two-in-withdrawal-is-sixteen-here-too", () => H.GetTotalAddictionPenalty() == 16, () => State);
		WaitFor("closed-by-the-founder", 300f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		WaitFor("i-start-over", 60f, () => !Perks.Picked(Me) && Members.LocalSeat > 1 && !StartingPerk.IsSentinel(), () => State);
		Check("the-founder-is-cured-here-too", () => !Perks.Has(1, "nicotine_addiction") && Perks.Has(1, "handy"), () => State);
		Delay("three-comparisons", 32f);
		Agree("same-shop", 60f, "core.digest", "items.digest", "econ.hash");
		Check("no-reload", () => Digest.ReloadsAsked == 0 && DayNight.Mismatches == 0, () => "reloads asked " + Digest.ReloadsAsked + "; day/night mismatches " + DayNight.Mismatches + "; " + State);
	}
}
