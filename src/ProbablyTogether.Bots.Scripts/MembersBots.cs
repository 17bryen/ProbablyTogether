using System;
using System.Collections.Generic;
using System.Globalization;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class MembersProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("mem.count", () => BotProbeSet.Str(Members.Count));
		BotProbes.Add("mem.list", () => Members.Describe());
		BotProbes.Add("mem.rent", () => (!PlayerStore.HasInstance) ? "" : BotProbeSet.Str(PlayerStore.Instance.GetRentValue()));
		BotProbes.Add("mem.online", () => BotProbeSet.Str(Net.Players.Count));
		BotProbes.Add("mem.refused", () => (!Net.IsServer) ? "" : BotProbeSet.Str(Members.Refused));
	}
}

public abstract class MembersBotBase : DayNightBotBase
{
	protected static int BaseRent => DayNightBotBase.Ps.rentValue + DayNightBotBase.Ps.temporaryRent;

	protected static bool RentIs(double factor)
	{
		int num = (int)Math.Round((double)BaseRent * factor, MidpointRounding.AwayFromZero);
		return DayNightBotBase.Ps.GetRentValue() == num && Members.Rent(BaseRent) == num;
	}

	protected static string RentText()
	{
		return "rent " + DayNightBotBase.Ps.GetRentValue() + " on a base of " + BaseRent + "; " + Members.Describe();
	}
}

/// Dedicated server: two seats fill, one leaves, a stranger is refused, the leaver gets the same seat back.
public sealed class MembersServer : MembersBotBase
{
	public override string Name => "members-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop && Game.ShopLoaded && ProbablyTogether.Server.ShopReady, () => "scene " + BotProbes.Scene);
		Check("alone-is-baseline", () => Members.Count == 0 && RentIs(1.0), RentText);
		WaitFor("two-seats", 240f, () => Members.Count == 2 && ProbablyTogether.Server.PlayerCount == 2, () => Members.Describe());
		Check("rent-150", () => RentIs(1.5), RentText);
		Check("save-with-two-seats", delegate
		{
			LabKit.Do("save");
			return LabKit.Last.StartsWith("saved", StringComparison.Ordinal);
		}, () => LabKit.Last);
		WaitFor("opened", 120f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		Check("two-more-customers", () => CrewCustomers.Today == 2, () => "extra customers today " + CrewCustomers.Today + " (" + CrewCustomers.LastDecision + ")");
		WaitFor("b-left", 240f, () => ProbablyTogether.Server.PlayerCount == 1, () => ProbablyTogether.Server.PlayerCount + " connected");
		Check("seat-kept-while-away", () => Members.Count == 2 && RentIs(1.5), RentText);
		WaitFor("stranger-refused", 240f, () => Members.Refused >= 1, () => "refusals " + Members.Refused);
		Check("still-two-seats", () => Members.Count == 2 && ProbablyTogether.Server.PlayerCount == 1, () => Members.Describe() + "; " + ProbablyTogether.Server.PlayerCount + " connected");
		WaitFor("b-back", 240f, delegate
		{
			foreach (PlayerInfo value in Net.Players.Values)
			{
				if (value.Seat == 2)
				{
					return ProbablyTogether.Server.PlayerCount == 2;
				}
			}
			return false;
		}, () => ProbablyTogether.Server.PlayerCount + " connected; " + Members.Describe());
		Check("same-two-seats", () => Members.Count == 2 && RentIs(1.5), RentText);
		Delay("clients-finish", 14f);
	}
}

/// Seat 1: stays throughout, opens the shop once seat 2 is in, pays the rent through the lab.
public sealed class MembersA : MembersBotBase
{
	private int cashBefore;

	private int due;

	private bool sawAlone;

	public override string Name => "members-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		JoinSteps();
		Check("seat-1", () => Members.LocalSeat == 1 && Members.Count >= 1, () => "my seat " + Members.LocalSeat + "; " + Members.Describe());
		WaitFor("second-player", 240f, () => Members.Count == 2 && Net.Players.Count == 2, () => Members.Describe());
		Check("rent-150", () => RentIs(1.5), RentText);
		Check("calendar-shows-what-is-charged", delegate
		{
			int rentValue = DayNightBotBase.Ps.rentValue;
			AdvCalendarUIManager.Instance.OnCalendarButtonClicked();
			string text = AdvCalendarUIManager.Instance.landlordTMP.text ?? "";
			return text.Contains(Members.Rent(BaseRent).ToString()) && DayNightBotBase.Ps.rentValue == rentValue;
		}, () => "calendar: '" + AdvCalendarUIManager.Instance.landlordTMP.text + "'; charged " + Members.Rent(BaseRent) + ", base in the save still " + DayNightBotBase.Ps.rentValue);
		Agree("same-with-two", 30f, "core.digest", "mem.count", "mem.rent", "cash");
		WaitServer("server-saved", 60f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "lab.applied") >= 1, "lab.applied");
		Do("open-click", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened", 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		Do("pay-rent", delegate
		{
			cashBefore = DayNightBotBase.Ps.playerCash;
			due = DayNightBotBase.Ps.GetRentValue();
			LabKit.Do("set", "playerCash", (cashBefore + due + 100).ToString(CultureInfo.InvariantCulture));
			LabKit.Do("rentpaid");
			return "cash " + cashBefore + " (+" + (due + 100) + " from the lab), rent due " + due;
		});
		WaitFor("rent-taken", 20f, () => DayNightBotBase.Ps.playerCash == cashBefore + 100, () => "cash " + DayNightBotBase.Ps.playerCash + ", expected " + (cashBefore + 100) + " after paying " + due);
		Agree("same-after-rent", 30f, "core.digest", "mem.rent", "cash");
		WaitFor("b-left-and-back", 400f, delegate
		{
			if (Net.Players.Count == 1)
			{
				sawAlone = true;
			}
			return sawAlone && Net.Players.Count == 2 && Members.Count == 2;
		}, () => "saw myself alone " + sawAlone + "; " + Net.Players.Count + " connected; " + Members.Describe());
		Check("rent-still-150", () => RentIs(1.5), RentText);
		Agree("same-at-the-end", 30f, "core.digest", "mem.count", "mem.rent", "cash");
	}
}

/// Seat 2: joins, checks, leaves; rerun with the same identity, it must get seat 2 back.
public sealed class MembersB : MembersBotBase
{
	public override string Name => "members-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 300f;

	protected override void Build()
	{
		JoinSteps();
		Check("seat-2", () => Members.LocalSeat == 2 && Members.Count == 2, () => "my seat " + Members.LocalSeat + "; " + Members.Describe());
		Check("rent-150", () => RentIs(1.5), RentText);
		Delay("settle", 3f);
		Agree("same-as-server", 30f, "core.digest", "mem.count", "mem.rent", "cash");
		Delay("stay-a-while", 22f);
	}
}

/// Joins when every seat is taken and must be refused with the reason.
public sealed class MembersStranger : BotScript
{
	public override string Name => "members-stranger";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 120f;

	protected override void Build()
	{
		WaitFor("refused", 90f, () => Net.LastRefuseCode == "full", () => "refuse code '" + Net.LastRefuseCode + "', state " + Net.ClientState);
		Check("told-why", () => (Net.LastRefuseReason ?? "").Contains("already has its"), () => Net.LastRefuseReason);
	}
}

/// After a restart from the save: one player alone in a two-member shop.
public sealed class MembersSolo : MembersBotBase
{
	public override string Name => "members-solo";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 300f;

	protected override void Build()
	{
		JoinSteps();
		Check("seat-1-again", () => Members.LocalSeat == 1, () => "my seat " + Members.LocalSeat + "; " + Members.Describe());
		Check("shop-remembers-two", () => Members.Count == 2 && Net.Players.Count == 1, () => Net.Players.Count + " connected; " + Members.Describe());
		Check("rent-150-alone", () => RentIs(1.5), RentText);
		Delay("settle", 3f);
		Agree("same-as-server", 30f, "core.digest", "mem.count", "mem.rent", "cash");
		Delay("stranger-tries", 30f);
		WaitServer("stranger-was-refused", 60f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "mem.refused") >= 1 && DayNightBotBase.I(v, "mem.count") == 2, "mem.refused", "mem.count", "mem.list");
	}
}
