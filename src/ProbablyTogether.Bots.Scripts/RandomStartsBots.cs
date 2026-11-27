using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

/// Random trades and perks (Systems.RandomStarts) in a shop whose server.json deals the founder's at random and forces
/// both on players who join: the draws, the refusals of a hand pick, the reveal, /retrade and /reperk, a new run.
public abstract class RandomStartsBase : PerkSuiteBase
{
	protected static void Say(string text)
	{
		Net.ToServer("chat.say", new MsgChatSay
		{
			text = text
		});
	}

	/// What the screen would accept: points, slots, no pair the game keeps apart.
	protected static bool Valid(List<string> ids)
	{
		return ids != null && Perks.Points(ids) <= 3 && ids.Count <= Perks.Slots(ids) && ids.Distinct().Count() == ids.Count && !ids.Any((string a) => ids.Any((string b) => a != b && Perks.Excludes(a, b)));
	}

	protected static bool Dealt(int seat)
	{
		List<string> list = RandomStarts.PerksOf(seat);
		return Specs.Of(seat) != null && RandomStarts.TradeOf(seat) == Specs.Of(seat) && Perks.Picked(seat) && list != null && list.Count == Perks.Of(seat).Ids.Count && !list.Except(Perks.Of(seat).Ids).Any();
	}

	protected static string Seat(int seat)
	{
		return "seat " + seat + ": trade " + (Specs.Of(seat) ?? "-") + " (dealt " + (RandomStarts.TradeOf(seat) ?? "-") + "), perks [" + string.Join(",", Perks.Of(seat).Ids) + "] (dealt " + ((RandomStarts.PerksOf(seat) != null) ? ("[" + string.Join(",", RandomStarts.PerksOf(seat)) + "]") : "-") + ", " + Perks.Points(Perks.Of(seat).Ids) + " pt)";
	}

	protected static string Where => Seat(1) + "; " + Seat(2) + "; forced trade " + RandomStarts.ForceTrade + ", perks " + RandomStarts.ForcePerks + "; reveals " + RandomReveal.Opened + " (" + RandomReveal.LastShown + "); offers " + Repick.Offers + ", done " + Repick.Done + ", mine '" + Repick.Mine + "'; refusals: spec '" + Specs.LastRefusal + "', perks '" + Perks.LastRefusal + "'; screens: trade " + PerkScreens.SpecOpen + ", perks " + PerkScreens.PerksOpen + ", random trade " + RandomBoxes.TradeOn + ", random perks " + RandomBoxes.PerksOn;

	protected void CloseReveal(string name, int count)
	{
		WaitFor(name, 40f, () => RandomReveal.Opened >= count && HostPanels.MessageOpen(HostPanels.RevealWindow), () => Where);
		Do("close-" + name, delegate
		{
			HostPanels.CloseMessage(HostPanels.RevealWindow);
			return RandomReveal.LastShown;
		});
	}
}

public sealed class RandomStartsH : RandomStartsBase
{
	private string theirTrade;

	private string myTrade;

	private List<string> myPerks;

	private List<string> theirPerks;

	private int loads;

	private int invalid = -1;

	private int mixed;

	public override string Name => "rs-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		Do("the-reveal-shows-on-a-bot", delegate
		{
			PerkScreens.LabOn = true;
			return "on";
		});
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && DayNightBotBase.Live && Me == 1 && Bays.BayOf(1) != null, () => "role " + Net.Role + ", live " + Net.IsLive);
		WaitFor("my-start-was-dealt", 30f, () => Dealt(1), () => Where);
		Check("the-shop-is-the-dealt-trade", () => Game.Store.startType.ToString() == Specs.Of(1), () => "store " + Game.Store.startType.ToString() + "; " + Where);
		Check("my-perks-are-a-valid-pick", () => Valid(Perks.Of(1).Ids), () => Where);
		Check("the-rules-force-both", () => RandomStarts.ForceTrade && RandomStarts.ForcePerks, () => Where);
		CloseReveal("my-reveal", 1);
		Check("my-reveal-showed-both", () => RandomReveal.LastShown.StartsWith(Specs.Of(1)) && RandomReveal.LastShown.Contains("["), () => RandomReveal.LastShown);
		Do("the-dice-many-times", delegate
		{
			// Draws for a seat nobody sits in: the founder's own perks lock their unique ones out.
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			for (int i = 0; i < 300; i++)
			{
				List<string> list = RandomStarts.DrawPerks(3, (string id) => Perks.LockReason(3, id));
				if (!Valid(list) || list.Any((string id) => Perks.LockReason(3, id) != null))
				{
					num++;
				}
				if (Perks.Points(list) == 3 && list.Any((string id) => Perks.Def(id).cost > 0) && list.Any((string id) => Perks.Def(id).cost < 0))
				{
					num2++;
				}
				if (list.Contains("colorful_character"))
				{
					num3++;
				}
				if (list.Count > 4)
				{
					num4++;
				}
			}
			invalid = num;
			mixed = num2;
			return "invalid " + num + ", three points mixed " + num2 + ", with colorful character " + num3 + ", more than four perks " + num4;
		});
		Check("every-draw-is-a-valid-pick", () => invalid == 0, () => invalid + " of 300 not valid");
		Check("nearly-every-draw-spends-three-points-good-and-bad", () => mixed >= 270, () => mixed + " of 300");
		Check("rock-bottom-can-be-dealt", () => RandomStarts.TradePool().Contains("ROCK_BOTTOM") == (Specs.TakenBy("ROCK_BOTTOM") == 0), () => string.Join(",", RandomStarts.TradePool()));
		// The joining player: refused a hand pick, then dealt both.
		WaitFor("they-were-dealt-theirs", 300f, () => OtherSeat == 2 && Dealt(2), () => Where);
		Check("no-two-hold-one-trade", () => Specs.Of(2) != Specs.Of(1), () => Where);
		Check("their-perks-are-a-valid-pick", () => Valid(Perks.Of(2).Ids), () => Where);
		Check("their-perks-took-nothing-unique-of-mine", () => !Perks.Of(2).Ids.Any((string id) => Perks.KindOf(id) == Perks.Unique && Perks.Has(1, id)), () => Where);
		Check("a-hand-pick-was-refused", () => Specs.LastRefusal.Contains("at random") && Perks.LastRefusal.Contains("at random"), () => Where);
		Check("their-draw-is-not-told-to-me", () => RandomReveal.Opened == 1, () => Where);
		// /retrade and /reperk follow the rule.
		Delay("before-retrade", 6f);
		Do("retrade", delegate
		{
			theirTrade = Specs.Of(2);
			Say("/retrade " + Members.NameOf(2));
			return "typed; they are " + theirTrade;
		});
		WaitFor("dealt-another-trade", 120f, () => Repick.Done == 1 && Specs.Of(2) != null && Specs.Of(2) != theirTrade && RandomStarts.TradeOf(2) == Specs.Of(2), () => Where);
		Delay("before-reperk", 6f);
		Do("reperk", delegate
		{
			Say("/reperk " + Members.NameOf(2));
			return "typed";
		});
		WaitFor("dealt-perks-again", 120f, () => Repick.Done == 2 && Dealt(2) && Valid(Perks.Of(2).Ids), () => Where);
		End(25f);
		// A new run: everyone keeps what they were dealt.
		Do("new-run", delegate
		{
			myTrade = Specs.Of(1);
			theirTrade = Specs.Of(2);
			myPerks = Perks.Of(1).Ids.ToList();
			theirPerks = Perks.Of(2).Ids.ToList();
			loads = ServerMode.NewRunsStarted;
			return ServerMode.NewRunCommand(force: true);
		});
		WaitFor("the-new-run-is-up", 240f, () => ServerMode.NewRunsStarted > loads && ProbablyTogether.Server.ShopReady && !ServerMode.SettingUpNewRun && DayNightBotBase.Live && DayNightBotBase.Day == 1 && Bays.BayOf(1) != null, () => "new runs " + ServerMode.NewRunsStarted + "; " + Where);
		WaitFor("i-keep-my-draw", 60f, () => Specs.Of(1) == myTrade && Perks.Picked(1) && Perks.Of(1).Ids.Count == myPerks.Count && !myPerks.Except(Perks.Of(1).Ids).Any(), () => "had " + myTrade + " [" + string.Join(",", myPerks) + "]; " + Where);
		WaitFor("they-keep-theirs", 120f, () => Specs.Of(2) == theirTrade && Perks.Picked(2) && Perks.Of(2).Ids.Count == theirPerks.Count && !theirPerks.Except(Perks.Of(2).Ids).Any(), () => "they had " + theirTrade + " [" + string.Join(",", theirPerks) + "]; " + Where);
		Check("no-reveal-for-a-kept-draw", () => RandomReveal.Opened == 1, () => Where);
		Delay("after-the-new-run", 20f);
		Agree("same-shop-after-the-new-run", 60f, "core.digest", "items.digest", "econ.hash");
	}
}

public sealed class RandomStartsB : RandomStartsBase
{
	private string trade;

	private int loads;

	public override string Name => "rs-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		JoinSteps();
		WaitFor("my-bay", 60f, () => Me > 1 && Bays.BayOf(Me) != null, () => Where);
		Do("note", delegate
		{
			loads = DayNightProbes.Loads;
			return "loads " + loads;
		});
		WaitFor("the-founder-was-dealt", 120f, () => Dealt(1), () => Where);
		Check("the-welcome-says-both-are-forced", () => RandomStarts.ForceTrade && RandomStarts.ForcePerks, () => Where);
		Do("the-game's-screens-ask-me", delegate
		{
			PerkScreens.LabOn = true;
			return "on";
		});
		WaitFor("the-trade-screen-is-up-and-random", 30f, () => PerkScreens.SpecOpen && RandomBoxes.TradeOn, () => Where);
		Do("a-trade-by-hand", delegate
		{
			Specs.Pick("FARMER");
			return "asked";
		});
		WaitFor("refused", 20f, () => Specs.LastRefusal.Contains("at random") && Specs.Of(Me) == null, () => Where);
		Do("continue", delegate
		{
			PerkScreens.SpecContinue();
			return "pressed";
		});
		WaitFor("dealt-a-trade-and-the-perk-screen-is-up", 60f, () => Specs.Of(Me) != null && Specs.Of(Me) != Specs.Of(1) && PerkScreens.PerksOpen && RandomBoxes.PerksOn, () => Where);
		Check("no-reveal-while-choosing", () => RandomReveal.Opened == 0, () => Where);
		Do("perks-by-hand", delegate
		{
			Perks.Pick(new string[1] { "saving" });
			return "asked";
		});
		WaitFor("refused-again", 20f, () => Perks.LastRefusal.Contains("at random") && !Perks.Picked(Me), () => Where);
		Do("continue-again", delegate
		{
			PerkScreens.PerksContinue();
			return "pressed";
		});
		WaitFor("dealt-perks-and-in-the-shop", 40f, () => Dealt(Me) && !PerkScreens.PerksOpen && !PerkScreens.SpecOpen, () => Where);
		CloseReveal("my-reveal", 1);
		Check("the-reveal-showed-both", () => RandomReveal.LastShown.StartsWith(Specs.Of(Me)) && RandomReveal.LastShown.Contains("["), () => RandomReveal.LastShown);
		WaitFor("offered-another-trade", 120f, () => Repick.Mine == Repick.Trade && PerkScreens.SpecOpen, () => Where);
		Check("the-trade-screen-is-random-again", () => RandomBoxes.TradeOn, () => Where);
		Do("continue-the-retrade", delegate
		{
			trade = Specs.Of(Me);
			PerkScreens.SpecContinue();
			return "was " + trade;
		});
		WaitFor("another-trade", 40f, () => Specs.Of(Me) != null && Specs.Of(Me) != trade && Repick.Mine == "" && !PerkScreens.SpecOpen, () => Where);
		CloseReveal("the-new-trade-shown", 2);
		Check("only-the-trade", () => !RandomReveal.LastShown.Contains("["), () => RandomReveal.LastShown);
		WaitFor("offered-my-perks-again", 120f, () => Repick.Mine == Repick.PerksKind && PerkScreens.PerksOpen && RandomBoxes.PerksOn, () => Where);
		Do("continue-the-reperk", delegate
		{
			PerkScreens.PerksContinue();
			return "pressed";
		});
		WaitFor("new-perks", 40f, () => Repick.Mine == "" && !PerkScreens.PerksOpen && Dealt(Me), () => Where);
		CloseReveal("the-new-perks-shown", 3);
		Check("only-the-perks", () => RandomReveal.LastShown.StartsWith("-;"), () => RandomReveal.LastShown);
		End(25f);
		WaitFor("the-new-run-is-up", 300f, () => DayNightProbes.Loads > loads && DayNightBotBase.Live && DayNightBotBase.Day == 1 && Bays.BayOf(Me) != null, () => "loads " + DayNightProbes.Loads + "; " + Where);
		WaitFor("i-keep-my-draw-without-choosing", 120f, () => Dealt(Me) && !PerkScreens.SpecOpen && !PerkScreens.PerksOpen, () => Where);
		Check("no-reveal-for-a-kept-draw", () => RandomReveal.Opened == 3, () => Where);
		Delay("after-the-new-run", 20f);
		Agree("same-shop-after-the-new-run", 60f, "core.digest", "items.digest", "econ.hash");
	}
}
