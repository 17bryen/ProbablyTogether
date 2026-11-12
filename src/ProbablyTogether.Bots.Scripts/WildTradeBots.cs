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

	protected static int InBay(int seat)
	{
		GameItem val = Bays.BayOf(seat);
		return (val == null) ? (-1) : GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).Count;
	}

	protected static StoreClient Queued(string id)
	{
		return DayNightBotBase.Ps?.storeClientManager?.clientStack?.FirstOrDefault((StoreClient c) => c != null && c.identifier == id);
	}
}

public sealed class WildTradeH : WildTradeBotBase
{
	private int bay0;

	public override string Name => "wt-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 480f;

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return "day " + day0 + " " + DayNightBotBase.State;
		});
		WaitFor("b-is-the-chemist", 120f, () => Specs.TakenBy("DRUG_DEALER") == Members.SeatOf(Other) && Specs.TakenBy("WATER_MERCHANT") == Members.LocalSeat, () => Trades);
		Check("the-chemists-referral-is-not-mine-to-buy", () => (Specs.WildRefusal(Net.LocalPlayer, "RETIRED_CHEMIST") ?? "").Contains("is the shop's chemist") && Specs.WildRefusal(Other, "RETIRED_CHEMIST") == null, () => "host: '" + Specs.WildRefusal(Net.LocalPlayer, "RETIRED_CHEMIST") + "'; the chemist: '" + Specs.WildRefusal(Other, "RETIRED_CHEMIST") + "'");
		Check("the-rest-of-the-list-is-everyones", () => Specs.WildRefusal(Net.LocalPlayer, "DOSSIER_UPGRADE") == null && Specs.WildRefusal(Other, "DOSSIER_UPGRADE") == null && Specs.WildRefusal(Other, "JACKSON1") == null && Specs.WildTrade("LANDLORD_GRACE") == null, () => "dossier, Dr. Jackson and landlord entries are not tied to a trade");
		Check("the-list-says-so", () => Specs.WildNote("RETIRED_CHEMIST").Contains(Members.NameOf(Members.SeatOf(Other))) && Specs.WildNote("RETIRED_RANCHER").Contains("Whoever buys") && Specs.WildNote("DOSSIER_UPGRADE") == "", () => "chemist entry: '" + Specs.WildNote("RETIRED_CHEMIST") + "' | rancher entry: '" + Specs.WildNote("RETIRED_RANCHER") + "'");
		Do("i-take-the-farmers-and-the-gunsmiths-trade", delegate
		{
			bay0 = InBay(Members.LocalSeat);
			string[] array = new string[2] { "RETIRED_FARMER", "RETIRED_GUNSMITH" };
			foreach (string text in array)
			{
				string text2 = Specs.WildRefusal(Net.LocalPlayer, text);
				if (text2 != null)
				{
					throw new InvalidOperationException(text + " refused: " + text2);
				}
				string text3 = Economy.ServerBuy(text, 0);
				if (text3 != null)
				{
					throw new InvalidOperationException(text + " not bought: " + text3);
				}
				Specs.WildBought(Net.LocalPlayer, text);
			}
			return "bought on the server side; my bay held " + bay0 + " item(s) before";
		});
		WaitFor("i-hold-three-trades-with-their-kits", 20f, () => Specs.Holds(Members.LocalSeat, "FARMER") && Specs.Holds(Members.LocalSeat, "GUNSMITH") && Specs.Holds(Members.LocalSeat, "WATER_MERCHANT") && Specs.OwnerOfType("hydroponic") == Members.LocalSeat && Specs.OwnerOfType("printer") == Members.LocalSeat && Items.All().Any((GameItem i) => i.identifier == "hydroponic") && Items.All().Any((GameItem i) => i.identifier == "printer"), () => Trades + "; my bay holds " + InBay(Members.LocalSeat) + " item(s) (was " + bay0 + "); a hydroponic in the shop: " + Items.All().Any((GameItem i) => i.identifier == "hydroponic") + ", a printer: " + Items.All().Any((GameItem i) => i.identifier == "printer"));
		Check("no-story-comes-with-a-trade-from-wild", () => Members.Fact(Members.LocalSeat, TradeVisits.DayFact + ".GUNSMITH") == null && Members.Fact(Members.LocalSeat, TradeVisits.DayFact + ".FARMER") == null, () => "first-day facts for the gunsmith's and farmer's trade: " + (Members.Fact(Members.LocalSeat, TradeVisits.DayFact + ".GUNSMITH") ?? "none") + " / " + (Members.Fact(Members.LocalSeat, TradeVisits.DayFact + ".FARMER") ?? "none"));
		WaitFor("b-took-the-ranchers-trade-with-a-real-click", 180f, () => Specs.TakenBy("RANCHER") == Members.SeatOf(Other) && Specs.OwnerOfType("animal_cage") == Members.SeatOf(Other) && NetworkUpgrade.IsRetiredRancherUnlocked(), () => Trades + "; last refusal here: '" + Economy.LastRefusal + "'");
		WaitFor("b-was-refused-the-farmers-entry", 60f, () => Economy.LastRefusal.Contains("PLANT_DNA") && Economy.LastRefusal.Contains("is the shop's farmer"), () => "last refusal: '" + Economy.LastRefusal + "'");
		Check("plant-dna-not-sold", () => !NetworkUpgrade.IsPlantDnaUnlocked(), () => "Plant DNA unlocked: " + NetworkUpgrade.IsPlantDnaUnlocked());
		Delay("b-hangs-up", 8f);
		Do("open-click", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened", 40f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State + "; refused: " + DayNight.LastRefusedWhat + " " + DayNight.LastRefusedWhy);
		// The game itself may queue a gun buyer; the mod's calendar must not add one for the trade.
		Check("no-scripted-visitor-for-my-trades-from-wild", () => TradeVisits.SeatFor("ll_gun_buyer") == 0 && !TradeVisits.LastAdded.Contains("ll_gun_buyer"), () => "added by the trade calendar today: " + TradeVisits.LastAdded + "; a gun buyer from the game's own referral in the queue: " + (Queued("ll_gun_buyer") != null));
		Check("the-picked-trade-keeps-its-story", () => Queued("cartelIntro") != null && TradeVisits.SeatFor("cartelIntro") == Members.SeatOf(Other), () => "the cartel's man in the queue: " + (Queued("cartelIntro") != null) + ", for seat " + TradeVisits.SeatFor("cartelIntro") + " (the chemist picked that trade at the start)");
		Delay("b-checks", 30f);
	}
}

public sealed class WildTradeB : WildTradeBotBase
{
	private int bay0;

	public override string Name => "wt-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 480f;

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
		WaitFor("the-host-is-the-farmer", 120f, () => Specs.TakenBy("FARMER") == 1 && Specs.TakenBy("GUNSMITH") == 1, () => Trades);
		Do("wild-phone", delegate
		{
			bay0 = InBay(Members.LocalSeat);
			EconomyLab.Op("phone", "8376", "Regular");
			return "Joe Wild can be called (lab op); my bay holds " + bay0 + " item(s)";
		});
		WaitFor("wild-phone-set", 20f, () => (int)EconomyLab.WildPhone() == 4, () => "Wild's phone " + ((object)EconomyLab.WildPhone()).ToString());
		Step("call-wild-and-open-his-list", 90f, EconomyLab.PhoneBrowsePoll());
		Check("my-list-names-the-holders", () => Specs.WildNote("RETIRED_RANCHER").Contains("Whoever buys") && Specs.WildNote("PLANT_DNA").Contains(Members.NameOf(1)), () => "rancher entry: '" + Specs.WildNote("RETIRED_RANCHER") + "' | Plant DNA entry: '" + Specs.WildNote("PLANT_DNA") + "'");
		Do("press-buy-on-the-ranchers-referral", () => EconomyLab.ClickBuy("RETIRED_RANCHER"));
		WaitFor("i-am-the-rancher", 30f, () => Specs.Holds(Members.LocalSeat, "RANCHER") && NetworkUpgrade.IsRetiredRancherUnlocked() && Specs.OwnerOfType("animal_cage") == Members.LocalSeat, () => Trades + "; unlocked here " + NetworkUpgrade.IsRetiredRancherUnlocked() + "; refusal '" + Economy.LastRefusal + "'");
		WaitFor("the-ranchers-kit-is-in-my-bay", 20f, () => InBay(Members.LocalSeat) >= bay0 + 5 && Items.All().Any((GameItem i) => i.identifier == "animal_cage"), () => "my bay holds " + InBay(Members.LocalSeat) + " item(s) (was " + bay0 + "); a cage in the shop: " + Items.All().Any((GameItem i) => i.identifier == "animal_cage"));
		Check("still-the-chemist", () => Specs.Holds(Members.LocalSeat, "DRUG_DEALER") && Specs.OwnerOfType("heating_plate") == Members.LocalSeat, () => Trades);
		Do("press-buy-on-the-farmers-plant-dna", () => EconomyLab.ClickBuy("PLANT_DNA"));
		WaitFor("refused", 30f, () => Economy.LastRefusal.Contains("PLANT_DNA"), () => "last refusal: '" + Economy.LastRefusal + "'");
		Check("because-the-host-is-the-farmer", () => Economy.LastRefusal.Contains("is the shop's farmer") && !NetworkUpgrade.IsPlantDnaUnlocked(), () => "refusal: '" + Economy.LastRefusal + "'; Plant DNA unlocked " + NetworkUpgrade.IsPlantDnaUnlocked());
		Agree("same-as-the-host", 30f, "core.digest", "items.digest", "cash");
		Do("hang-up", delegate
		{
			WildNetworkUI.Close();
			return "closed Joe Wild's list";
		});
		WaitFor("opened-by-host", 120f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		Delay("the-opening-lands", 2f);
		Check("only-my-picked-trade-has-a-story", () => TradeVisits.SeatFor("ll_gun_buyer") == 0 && Queued("cartelIntro") != null && TradeVisits.SeatFor("cartelIntro") == Members.LocalSeat && (Queued("cartelIntro").mainDialogue?.plainText ?? "").Contains(Members.NameOf(Members.LocalSeat)), () => "gunsmith's first customer here: " + (Queued("ll_gun_buyer") != null) + "; the cartel's man: '" + (Queued("cartelIntro")?.mainDialogue?.plainText ?? "") + "'");
		Check("no-reload", () => DayNight.Mismatches == 0 && Digest.ReloadsAsked == 0, () => "day/night mismatches " + DayNight.Mismatches + "; reloads asked " + Digest.ReloadsAsked);
	}
}
