using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

/// Buying trades through Joe Wild: a free trade goes to the buyer with its kit; a held trade is refused.
public abstract class WildTradeBotBase : DayNightBotBase
{
	protected static int Other => Net.Players.Keys.FirstOrDefault((int p) => p != Net.LocalPlayer);

	protected static string Trades => string.Join("; ", from m in Members.State.list
		select m.n + ": " + (m.x.ContainsKey("spec") ? m.x["spec"] : "-") + (m.x.ContainsKey(Specs.MoreFact) ? ("+" + m.x[Specs.MoreFact]) : "") + " [" + (m.x.ContainsKey("mach") ? m.x["mach"] : "") + "]");
}

public sealed class WildTradeH : WildTradeBotBase
{
	private string bought = "";

	public override string Name => "wt-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 420f;

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		WaitFor("b-is-the-chemist", 120f, () => Specs.TakenBy("DRUG_DEALER") == Members.SeatOf(Other) && Specs.TakenBy("WATER_MERCHANT") == Members.LocalSeat, () => Trades);
		Check("the-chemists-referral-is-not-mine-to-buy", () => (Specs.WildRefusal(Net.LocalPlayer, "RETIRED_CHEMIST") ?? "").Contains("is the shop's chemist") && Specs.WildRefusal(Other, "RETIRED_CHEMIST") == null, () => "host: '" + Specs.WildRefusal(Net.LocalPlayer, "RETIRED_CHEMIST") + "'; the chemist: '" + Specs.WildRefusal(Other, "RETIRED_CHEMIST") + "'");
		Check("my-own-trades-entry-is-mine", () => Specs.WildRefusal(Net.LocalPlayer, "WATER_TRADER") == null && Specs.WildRefusal(Other, "WATER_TRADER") != null, () => "host: '" + Specs.WildRefusal(Net.LocalPlayer, "WATER_TRADER") + "'; the other: '" + Specs.WildRefusal(Other, "WATER_TRADER") + "'");
		Check("the-rest-of-the-list-is-everyones", () => Specs.WildRefusal(Net.LocalPlayer, "DOSSIER_UPGRADE") == null && Specs.WildRefusal(Other, "DOSSIER_UPGRADE") == null && Specs.WildRefusal(Other, "JACKSON1") == null && Specs.WildTrade("LANDLORD_GRACE") == null, () => "dossier, Dr. Jackson and landlord entries are not tied to a trade");
		Check("a-free-trade-is-open-to-both", () => Specs.WildRefusal(Net.LocalPlayer, "RETIRED_RANCHER") == null && Specs.WildRefusal(Other, "RETIRED_RANCHER") == null && Specs.WildRefusal(Other, "GUN_PERMIT_I") == null, () => "nobody is the rancher or the gunsmith yet");
		Check("the-list-says-so", () => Specs.WildNote("RETIRED_CHEMIST").Contains(Members.NameOf(Members.SeatOf(Other))) && Specs.WildNote("RETIRED_RANCHER").Contains("Whoever buys") && Specs.WildNote("WATER_TRADER").Contains("You are") && Specs.WildNote("DOSSIER_UPGRADE") == "", () => "chemist entry: '" + Specs.WildNote("RETIRED_CHEMIST") + "' | rancher entry: '" + Specs.WildNote("RETIRED_RANCHER") + "' | water entry: '" + Specs.WildNote("WATER_TRADER") + "'");
		Do("b-buys-the-ranchers-referral", delegate
		{
			string text = Specs.WildRefusal(Other, "RETIRED_RANCHER");
			if (text != null)
			{
				throw new InvalidOperationException("refused: " + text);
			}
			bought = Economy.ServerBuy("RETIRED_RANCHER", 0) ?? "bought";
			Specs.WildBought(Other, "RETIRED_RANCHER");
			return "the game's own purchase: " + bought + "; cash now " + DayNightBotBase.Ps.playerCash;
		});
		WaitFor("b-is-the-rancher-too", 20f, () => bought == "bought" && Specs.TakenBy("RANCHER") == Members.SeatOf(Other) && Specs.OwnerOfType("animal_cage") == Members.SeatOf(Other) && NetworkUpgrade.IsRetiredRancherUnlocked(), () => Trades + "; purchase: " + bought + "; unlocked " + NetworkUpgrade.IsRetiredRancherUnlocked());
		Check("still-the-chemist", () => Specs.TakenBy("DRUG_DEALER") == Members.SeatOf(Other) && Specs.OwnerOfType("heating_plate") == Members.SeatOf(Other), () => Trades);
		Check("now-it-is-theirs-alone", () => (Specs.WildRefusal(Net.LocalPlayer, "RETIRED_RANCHER") ?? "").Contains("is the shop's rancher"), () => "host: '" + Specs.WildRefusal(Net.LocalPlayer, "RETIRED_RANCHER") + "'");
		Delay("b-checks", 25f);
	}
}

public sealed class WildTradeB : WildTradeBotBase
{
	public override string Name => "wt-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 420f;

	protected override void Build()
	{
		JoinSteps();
		WaitFor("my-bay-is-here", 60f, () => Bays.BayOf(Members.LocalSeat) != null, () => "seat " + Members.LocalSeat);
		Do("pick", delegate
		{
			ProbablyTogether.UI.Chat.Send("/trade chemist");
			return "typed '/trade chemist' in chat";
		});
		WaitFor("i-am-the-chemist", 30f, () => Specs.Of(Members.LocalSeat) == "DRUG_DEALER", () => Trades + "; refusal '" + Specs.LastRefusal + "'");
		WaitFor("i-am-the-rancher-too", 120f, () => Specs.Holds(Members.LocalSeat, "RANCHER") && Specs.OwnerOfType("animal_cage") == Members.LocalSeat && NetworkUpgrade.IsRetiredRancherUnlocked(), () => Trades + "; unlocked here " + NetworkUpgrade.IsRetiredRancherUnlocked());
		Check("still-the-chemist", () => Specs.Holds(Members.LocalSeat, "DRUG_DEALER") && Specs.OwnerOfType("heating_plate") == Members.LocalSeat, () => Trades);
		Check("my-list-says-so", () => Specs.WildNote("RETIRED_RANCHER").Contains("You are") && Specs.WildNote("RETIRED_CHEMIST").Contains("You are") && Specs.WildNote("WATER_TRADER").Contains(Members.NameOf(1)), () => "rancher entry: '" + Specs.WildNote("RETIRED_RANCHER") + "' | water entry: '" + Specs.WildNote("WATER_TRADER") + "'");
		Agree("same-as-the-host", 30f, "core.digest", "items.digest", "cash");
	}
}
