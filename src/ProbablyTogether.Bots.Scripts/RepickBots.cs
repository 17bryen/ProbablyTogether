using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

/// A trade and perks chosen again (Systems.Repick): nothing changes until the player confirms.
public abstract class RepickBotBase : PerkSuiteBase
{
	protected static readonly string[] First = new string[3] { "saving", "lucky", "bad_lease" };

	protected static readonly string[] Second = new string[3] { "saving", "handy", "prime_real_estate" };

	protected static void Say(string text)
	{
		Net.ToServer("chat.say", new MsgChatSay
		{
			text = text
		});
	}

	protected static bool Same(int seat, string[] ids)
	{
		List<string> list = Perks.Of(seat).Ids;
		return list.Count == ids.Length && !ids.Except(list).Any();
	}

	protected static string Where => "seat 2: trade " + (Specs.Of(2) ?? "-") + ", trades left " + Leaving.Done + "; offers " + Repick.Offers + ", called off " + Repick.Cancelled + ", gone through " + Repick.Done + ", mine '" + Repick.Mine + "', last refusal '" + Repick.LastRefusal + "'; screens: trade " + PerkScreens.SpecOpen + ", perks " + PerkScreens.PerksOpen + "; " + State;
}

public sealed class RepickH : RepickBotBase
{
	private int items;

	private int cash;

	public override string Name => "rp-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 700f;

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && DayNightBotBase.Live && Me == 1 && Bays.BayOf(1) != null, () => "role " + Net.Role + ", live " + Net.IsLive);
		Do("i-take-no-perks", delegate
		{
			Perks.Pick(new string[0]);
			return "asked";
		});
		WaitFor("picked", 30f, () => Perks.Picked(1), () => State);
		Do("the-game's-screens-are-on", delegate
		{
			PerkScreens.LabOn = true;
			return "on";
		});
		WaitFor("they-are-the-gunsmith-with-three-perks", 300f, () => OtherSeat == 2 && Specs.Of(2) == "GUNSMITH" && Same(2, First), () => Where);
		Before();
		Do("count", delegate
		{
			items = Items.All().Count;
			return items + " items";
		});
		// 1. an offer changes nothing; going back changes nothing
		Delay("before-retrade", 6f);
		Do("retrade", delegate
		{
			Say("/retrade " + Members.NameOf(2));
			return "typed";
		});
		WaitFor("offered", 20f, () => Repick.Offers == 1, () => Where);
		Delay("they-look-at-the-trade-screen", 4f);
		Check("nothing-has-changed-yet", () => Leaving.Done == 0 && Specs.Of(2) == "GUNSMITH" && Items.All().Count == items, () => Where + "; items " + Items.All().Count + " of " + items);
		WaitFor("they-went-back", 60f, () => Repick.Cancelled == 1, () => Where);
		Check("still-nothing", () => Leaving.Done == 0 && Specs.Of(2) == "GUNSMITH" && Items.All().Count == items && Same(2, First), () => Where + "; items " + Items.All().Count + " of " + items);
		// 2. confirmed
		Delay("before-retrade-again", 6f);
		Do("retrade-again", delegate
		{
			Say("/retrade " + Members.NameOf(2));
			return "typed";
		});
		WaitFor("they-are-the-farmer", 90f, () => Repick.Done == 1 && Specs.Of(2) == "FARMER", () => Where);
		Check("the-old-trade-left-once-and-their-perks-stayed", () => Leaving.Done == 1 && Same(2, First) && Count("hydroponic") >= 1 && Count("storage_bay_gun") == 0, () => Where + "; hydroponic " + Count("hydroponic") + ", gun storage " + Count("storage_bay_gun"));
		// 3. perks: offered, called off
		Delay("before-reperk", 6f);
		Do("reperk", delegate
		{
			Say("/reperk " + Members.NameOf(2));
			return "typed";
		});
		WaitFor("offered-perks", 20f, () => Repick.Offers == 3, () => Where);
		WaitFor("they-went-back-again", 60f, () => Repick.Cancelled == 2, () => Where);
		Check("their-perks-are-what-they-were", () => Same(2, First) && S.rentValue == rent0 && H.luck == luck0 && S.playerCash == cash0, () => Where);
		// 4. perks: confirmed
		Delay("before-reperk-again", 6f);
		Do("reperk-again", delegate
		{
			Say("/reperk " + Members.NameOf(2));
			return "typed";
		});
		WaitFor("their-perks-changed", 90f, () => Repick.Done == 2 && Same(2, Second), () => Where);
		Check("savings-kept-is-not-paid-twice", () => S.playerCash == cash0, () => "cash " + cash0 + " -> " + S.playerCash);
		Check("lucky-given-up-takes-its-luck-along", () => H.luck == luck0 - 15, () => "luck " + luck0 + " -> " + H.luck);
		Check("bad-lease-given-up-and-prime-real-estate-taken", () => S.rentValue == rent0 - 40 + 75, () => "rent " + rent0 + " -> " + S.rentValue);
		Check("handy-taken-a-toolbox", () => Count("toolbox") == 1, () => "toolboxes " + Count("toolbox"));
		Check("the-shop's-list-follows", () => Listed("saving") && Listed("handy") && Listed("prime_real_estate") && !S.startingPerks.Any((StartingPerk p) => p.id == "lucky" || p.id == "bad_lease"), () => State);
		// 5. the founder's own
		Delay("before-reperk-myself", 6f);
		Do("reperk-myself", delegate
		{
			cash = S.playerCash;
			Say("/reperk " + Members.NameOf(1));
			return "typed";
		});
		WaitFor("my-perk-screen-is-up", 30f, () => Repick.Mine == Repick.PerksKind && PerkScreens.PerksOpen, () => Where);
		Do("i-take-savings", delegate
		{
			List<StartingPerk> list = NewGameData.Instance.startingPerks;
			list.Clear();
			list.Add(Perks.Def("saving"));
			PerkScreens.PerksContinue();
			return "selected " + string.Join(",", PerkScreens.Selected());
		});
		WaitFor("mine-too", 40f, () => Perks.Has(1, "saving") && S.playerCash == cash + 150 && !PerkScreens.PerksOpen && Repick.Mine == "", () => Where);
		Check("the-founder's-trade-is-not-on-offer", () => Leaving.ChatCommand(Net.LocalPlayer, whole: false, Members.NameOf(1)).Contains("founder's trade stays"), () => Leaving.ChatCommand(Net.LocalPlayer, whole: false, Members.NameOf(1)));
		End(30f);
	}
}

public sealed class RepickB : RepickBotBase
{
	private int cash;

	public override string Name => "rp-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 700f;

	protected override void Build()
	{
		JoinSteps();
		WaitFor("my-bay", 60f, () => Me > 1 && Bays.BayOf(Me) != null, () => State);
		WaitFor("the-founder-picked", 120f, () => Perks.Picked(1), () => State);
		Do("the-game's-screens-ask-me", delegate
		{
			PerkScreens.LabOn = true;
			return "on";
		});
		WaitFor("the-trade-screen-is-up", 30f, () => PerkScreens.SpecOpen, () => Where);
		Do("gunsmith", delegate
		{
			Specs.Pick("GUNSMITH");
			return "asked";
		});
		WaitFor("the-perk-screen-is-up", 60f, () => Specs.Of(Me) == "GUNSMITH" && PerkScreens.PerksOpen, () => Where);
		Do("three-perks", delegate
		{
			List<StartingPerk> list = NewGameData.Instance.startingPerks;
			list.Clear();
			foreach (string item in First)
			{
				list.Add(Perks.Def(item));
			}
			PerkScreens.PerksContinue();
			return "selected " + string.Join(",", PerkScreens.Selected());
		});
		WaitFor("in-the-shop", 40f, () => Same(Me, First) && !PerkScreens.PerksOpen && !PerkScreens.SpecOpen, () => Where);
		Do("note", delegate
		{
			cash = S.playerCash;
			return "cash " + cash;
		});
		// 1. offered a trade: the screen, and nothing else
		WaitFor("offered-another-trade", 120f, () => Repick.Mine == Repick.Trade && PerkScreens.SpecOpen, () => Where);
		Check("i-am-still-the-gunsmith", () => Specs.Of(Me) == "GUNSMITH" && Leaving.Done == 0 && Count("storage_bay_gun") >= 1, () => Where);
		Delay("i-think-about-it", 6f);
		Do("back", delegate
		{
			PerkScreens.Back();
			return "went back";
		});
		WaitFor("the-screen-is-gone-and-i-keep-my-trade", 20f, () => Repick.Mine == "" && !PerkScreens.SpecOpen && Specs.Of(Me) == "GUNSMITH" && Leaving.Done == 0, () => Where);
		// 2. offered again: one that is taken, then the farmer
		WaitFor("offered-again", 120f, () => Repick.Mine == Repick.Trade && PerkScreens.SpecOpen, () => Where);
		Do("the-founder's-own-trade", delegate
		{
			Repick.Confirm(Repick.Trade, Specs.Of(1), null);
			return "asked for " + Specs.Of(1);
		});
		WaitFor("refused-and-nothing-changed", 20f, () => Specs.LastRefusal.Contains("already has that specialization") && Specs.Of(Me) == "GUNSMITH" && Leaving.Done == 0 && PerkScreens.SpecOpen, () => Where + "; spec refusal '" + Specs.LastRefusal + "'");
		Do("farmer", delegate
		{
			Repick.Confirm(Repick.Trade, "FARMER", null);
			return "asked";
		});
		WaitFor("i-am-the-farmer-and-the-screen-is-gone", 40f, () => Specs.Of(Me) == "FARMER" && Repick.Mine == "" && !PerkScreens.SpecOpen && !PerkScreens.PerksOpen, () => Where);
		Check("my-perks-and-what-they-gave-stayed", () => Same(Me, First) && S.playerCash == cash, () => Where);
		// 3. offered perks: the screen with mine selected; back
		WaitFor("offered-my-perks-again", 120f, () => Repick.Mine == Repick.PerksKind && PerkScreens.PerksOpen, () => Where);
		Check("the-screen-starts-from-what-i-have", () => PerkScreens.Selected().Count == 3 && !First.Except(PerkScreens.Selected()).Any(), () => "selected " + string.Join(",", PerkScreens.Selected()));
		Delay("i-think-about-it-again", 4f);
		Do("back-again", delegate
		{
			PerkScreens.Back();
			return "went back";
		});
		WaitFor("i-keep-my-perks", 20f, () => Repick.Mine == "" && !PerkScreens.PerksOpen && Same(Me, First), () => Where);
		// 4. offered again: too many points, then the second set
		WaitFor("offered-my-perks-once-more", 120f, () => Repick.Mine == Repick.PerksKind && PerkScreens.PerksOpen, () => Where);
		Do("five-points", delegate
		{
			Repick.Confirm(Repick.PerksKind, null, new string[3] { "saving", "well_connected", "alert" });
			return "asked";
		});
		WaitFor("refused-for-the-points", 20f, () => Perks.LastRefusal.Contains("perk points") && Same(Me, First), () => Where + "; perk refusal '" + Perks.LastRefusal + "'");
		Do("the-second-set-on-the-screen", delegate
		{
			List<StartingPerk> list = NewGameData.Instance.startingPerks;
			list.Clear();
			foreach (string item in Second)
			{
				list.Add(Perks.Def(item));
			}
			PerkScreens.PerksContinue();
			return "selected " + string.Join(",", PerkScreens.Selected());
		});
		WaitFor("my-perks-changed-and-the-screen-is-gone", 40f, () => Same(Me, Second) && Repick.Mine == "" && !PerkScreens.PerksOpen, () => Where);
		Check("savings-kept-is-not-paid-twice", () => S.playerCash == cash, () => "cash " + cash + " -> " + S.playerCash);
		Check("a-toolbox-came", () => Count("toolbox") == 1, () => "toolboxes " + Count("toolbox"));
		WaitFor("the-founder-took-savings", 120f, () => Perks.Has(1, "saving") && S.playerCash == cash + 150, () => Where);
		End(30f);
	}
}
