using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

/// A trade, then a whole player, leaves the shop. Steps:
/// 1. /remove while the player is online: refused. 2. /retrade: the chemist's machines, Wild entry and cartel go.
/// 3. Farmer, then /retrade again. 4. Chemist again: kit, entry and cartel return. 5. /remove at night: all gone.
public abstract class LeavingBotBase : DayNightBotBase
{
	protected static readonly string[] ChemKinds = new string[4] { "blender", "heating_plate", "evaporator", "chem_finisher" };

	protected static int Count(params string[] kinds)
	{
		return Items.All().Count((GameItem i) => i != null && kinds.Contains(i.identifier));
	}

	protected static int Entry(string id)
	{
		NetworkUpgrade value = null;
		DayNightBotBase.Ps?.networkUpgrade?.TryGetValue(id, out value);
		return value?.state ?? (-1);
	}

	protected static readonly string[] ChemKit = new string[6] { "chem_scanner", "chemistry_guide", "1ml_dropper", "5ml_dropper", "25ml_cylinder", "smuggler_bay_mini" };

	protected static readonly string[] FarmKit = new string[3] { "hydroponic_guide", "nutrifruit_seed", "fillerweed_seed" };

	protected static bool Protection => StoreService.GetCartelProtectionService()?.unlocked ?? false;

	protected static bool KnowsCartel => DayNightBotBase.Ps.storeReputations.Any((StoreReputation r) => r != null && r.factionId == "FACTION_CARTEL");

	protected static bool InQueue(string id)
	{
		List<StoreClient> list = DayNightBotBase.Ps?.storeClientManager?.clientStack;
		return list != null && list.Any((StoreClient c) => c != null && c.identifier == id);
	}

	protected static string Where(int seat)
	{
		return "day " + DayNightBotBase.Day + " " + DayNightBotBase.State + "; seat " + seat + " trade [" + (Specs.Of(seat) ?? "-") + "] first day [" + (Members.Fact(seat, TradeVisits.DayFact) ?? "-") + "], bay " + ((Bays.BayOf(seat) != null) ? ("#" + Ids.Of(Bays.BayOf(seat))) : "none") + "; chemist machines " + Count(ChemKinds) + " and kit tools " + Count(ChemKit) + ", hydroponic " + Count("hydroponic") + " and farmer kit " + Count(FarmKit) + "; kit record [" + (Members.Fact(seat, Specs.KitFact) ?? "-") + "]" + "; Joe Wild's chemist entry " + Entry("RETIRED_CHEMIST") + ", farmer entry " + Entry("RETIRED_FARMER") + "; cartel protection " + Protection + ", standing with the cartel " + KnowsCartel + ", the cartel's man in the queue " + InQueue("cartelIntro") + "; members " + Members.Describe() + "; steps done " + Leaving.Done + "; last result [" + Leaving.LastResult + "]; last refusal [" + Leaving.LastRefusal + "]; items " + Items.All().Count;
	}

	protected static void Say(string text)
	{
		Net.ToServer("chat.say", new MsgChatSay
		{
			text = text
		});
	}

	protected void OpenAndClose(string tag, Action afterOpen)
	{
		Do("open-" + tag, delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened-" + tag, 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		WaitFor("shutter-settled-" + tag, 20f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0, () => "flag " + DayNightBotBase.Ps.lockShutterButtonFlag);
		afterOpen?.Invoke();
		Do("queue-" + tag, delegate
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
		WaitFor("closable-" + tag, 15f, () => DayNightBotBase.Ps.CanEndDay(), () => "queue " + DayNightBotBase.Ps.storeClientManager.clientStack.Count);
		Do("close-" + tag, delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "sent";
		});
		WaitFor("closed-" + tag, 20f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
	}
}

public sealed class LeavingH : LeavingBotBase
{
	private int seat;

	private string name = "";

	public override string Name => "lv-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		WaitFor("b-is-the-chemist", 120f, delegate
		{
			seat = Specs.TakenBy("DRUG_DEALER");
			name = Members.NameOf(seat);
			return seat > 1 && Count(ChemKinds) >= 4;
		}, () => Where(seat));
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return Where(seat);
		});
		Delay("settle", 4f);
		Check("a-chemist-in-the-shop", () => Count(ChemKit) == 6 && (Members.Fact(seat, Specs.KitFact) ?? "").StartsWith("DRUG_DEALER:", StringComparison.Ordinal) && Entry("RETIRED_CHEMIST") == 2 && Protection && Bays.BayOf(seat) != null && DayNightBotBase.State == "MORNING", () => Where(seat));
		// 1
		Do("remove-while-they-are-here", delegate
		{
			Say("/remove " + name);
			return "typed /remove " + name;
		});
		WaitFor("refused-they-are-in-the-shop", 15f, () => Leaving.LastRefusal.Contains("in the shop right now") && Leaving.Done == 0 && Members.BySeat(seat) != null, () => Where(seat));
		// 2
		Do("retrade", delegate
		{
			Say("/retrade " + name);
			return "typed /retrade " + name;
		});
		WaitFor("their-trade-left", 15f, () => Leaving.Done == 1 && Specs.Of(seat) == null, () => Where(seat));
		Check("the-chemist-is-out-of-the-shop", () => Count(ChemKinds) == 0 && Count(ChemKit) == 0 && Members.Fact(seat, Specs.KitFact) == null && Entry("RETIRED_CHEMIST") == 1 && !Protection && Specs.TakenBy("DRUG_DEALER") == 0 && Specs.OwnerOfType("chem_finisher") == 0 && !KnowsCartel, () => Where(seat));
		Check("their-place-and-bay-stay", () => Members.BySeat(seat) != null && Bays.BayOf(seat) != null && Leaving.LastResult.StartsWith("4 machine(s) removed, ", StringComparison.Ordinal) && !Leaving.LastResult.Contains(", 0 other") && !Leaving.LastResult.Contains("storage bay") && Items.All().Any((GameItem i) => i != null && i.identifier == "fanny_pack"), () => Where(seat));
		// 3
		WaitFor("they-picked-farmer", 120f, () => Specs.Of(seat) == "FARMER" && Count("hydroponic") >= 1 && Entry("RETIRED_FARMER") == 2, () => Where(seat));
		Delay("b-compares", 4f);
		Do("as-if-picked-on-an-older-build", delegate
		{
			Members.DropFacts(seat, Specs.KitFact);
			return "the kit record of the farmer is taken off the member (a trade picked before 0.7 has none); farmer kit items in the shop " + Count(FarmKit);
		});
		Delay("record-gone-everywhere", 3f);
		Check("no-record-kit-still-there", () => Members.Fact(seat, Specs.KitFact) == null && Count(FarmKit) >= 5, () => Where(seat));
		Do("retrade-again", delegate
		{
			Say("/retrade " + name);
			return "typed /retrade " + name;
		});
		WaitFor("the-farmer-left", 15f, () => Leaving.Done == 2 && Specs.Of(seat) == null, () => Where(seat));
		Check("the-hydroponic-is-gone-what-was-inside-is-kept", () => Count("hydroponic") == 0 && Count(FarmKit) == 0 && Entry("RETIRED_FARMER") == 1 && !Leaving.LastResult.StartsWith("0 machine(s)", StringComparison.Ordinal) && !Leaving.LastResult.Contains("; 0 thing(s)"), () => Where(seat));
		// 4
		WaitFor("they-are-the-chemist-again", 120f, () => Specs.TakenBy("DRUG_DEALER") == seat && Count(ChemKinds) >= 4, () => Where(seat));
		Check("the-chemists-setup-is-back", () => Entry("RETIRED_CHEMIST") == 2 && Protection && Specs.OwnerOfType("chem_finisher") == seat && Members.Fact(seat, TradeVisits.DayFact) != null && !DayNightBotBase.Ps.isCartelExited, () => Where(seat));
		OpenAndClose("1", delegate
		{
			Check("the-cartels-man-comes-for-the-new-chemist", () => InQueue("cartelIntro") && TradeVisits.SeatFor("cartelIntro") == seat && KnowsCartel, () => Where(seat));
			Delay("b-looks-at-the-queue-1", 6f);
		});
		Ready("ready-1");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => Where(seat));
		OpenAndClose("2", delegate
		{
			Check("the-cartel-keeps-coming-on-their-second-day", () => InQueue("cartelSec") && TradeVisits.SeatFor("cartelSec") == seat && KnowsCartel, () => Where(seat) + "; cartelSec in the queue " + InQueue("cartelSec"));
			Delay("b-looks-at-the-queue-2", 6f);
		});
		// 5
		WaitFor("they-left-the-game", 120f, () => ProbablyTogether.Server.PlayerCount == 1, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		Delay("gone", 3f);
		Do("remove", delegate
		{
			Say("/remove " + name);
			return "typed /remove " + name + " at " + DayNightBotBase.State;
		});
		WaitFor("they-are-out-of-the-shop", 15f, () => Leaving.Done == 3 && Members.BySeat(seat) == null, () => Where(seat));
		Check("chemist-and-bay-gone-contents-kept", () => Count(ChemKinds) == 0 && Entry("RETIRED_CHEMIST") == 1 && !Protection && !KnowsCartel && Bays.BayOf(seat) == null && Members.Count == 1 && Leaving.LastResult.Contains("the storage bay too") && Leaving.LastResult.StartsWith("4 machine(s)", StringComparison.Ordinal) && !Leaving.LastResult.Contains("; 0 thing(s)"), () => Where(seat));
		Check("the-fanny-pack-of-the-bay-is-in-the-shop", () => Items.All().Any((GameItem i) => i != null && i.identifier == "fanny_pack" && i.parentInventory == (object)EmporiumEntry.Instance.invElement), () => Where(seat));
		Check("a-new-player-would-start-fresh", () => Wounds.Of(seat).State == 0 && Specs.TakenBy("DRUG_DEALER") == 0 && Specs.OwnerOfType("chem_finisher") == 0 && Specs.Choices().Any((KeyValuePair<string, string> c) => c.Key == "DRUG_DEALER"), () => Where(seat) + "; wounds " + Wounds.Raw + "; trades on offer " + string.Join(",", Specs.Choices().Select((KeyValuePair<string, string> c) => c.Key)));
		Delay("end", 3f);
	}
}

public sealed class LeavingB : LeavingBotBase
{
	private static int Me => Members.LocalSeat;

	public override string Name => "lv-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	private void PickStep(string name, string trade, Func<bool> have)
	{
		Step(name, 60f, delegate(BotStep st)
		{
			if (Specs.Of(Me) == trade && have())
			{
				return StepStatus.Pass;
			}
			if (Specs.Of(Me) == null && st.Detail != "asked")
			{
				Specs.Pick(trade);
				st.Detail = "asked";
			}
			return StepStatus.Running;
		});
	}

	protected override void Build()
	{
		JoinSteps();
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return Where(Me);
		});
		WaitFor("my-bay", 60f, () => Me > 1 && Bays.BayOf(Me) != null, () => Where(Me));
		PickStep("pick-chemist", "DRUG_DEALER", () => Count(ChemKinds) >= 4);
		Check("i-am-the-chemist", () => Entry("RETIRED_CHEMIST") == 2 && Protection && Specs.OwnerOfType("chem_finisher") == Me, () => Where(Me));
		WaitFor("the-founder-took-my-trade", 180f, () => Leaving.Done == 1 && Specs.Of(Me) == null, () => Where(Me));
		Check("the-chemist-is-out-of-my-shop-too", () => Count(ChemKinds) == 0 && Count(ChemKit) == 0 && Entry("RETIRED_CHEMIST") == 1 && !Protection && Specs.OwnerOfType("chem_finisher") == 0, () => Where(Me));
		Check("i-keep-my-place-and-bay", () => Me > 1 && Bays.BayOf(Me) != null, () => Where(Me));
		Agree("same-shop-as-the-founder", 30f, "core.digest", "items.digest", "econ.hash");
		PickStep("pick-farmer", "FARMER", () => Count("hydroponic") >= 1);
		WaitFor("the-founder-took-that-one-too", 180f, () => Leaving.Done == 2 && Specs.Of(Me) == null, () => Where(Me));
		Check("the-hydroponic-is-gone-here-too", () => Count("hydroponic") == 0 && Count(FarmKit) == 0 && Entry("RETIRED_FARMER") == 1, () => Where(Me));
		Agree("same-shop-after-the-second", 30f, "core.digest", "items.digest", "econ.hash");
		PickStep("pick-chemist-again", "DRUG_DEALER", () => Count(ChemKinds) >= 4);
		Check("i-am-the-chemist-again", () => Entry("RETIRED_CHEMIST") == 2 && Protection && Specs.OwnerOfType("chem_finisher") == Me, () => Where(Me));
		WaitFor("opened-by-host-1", 300f, () => DayNightBotBase.State == "OPEN", () => Where(Me));
		WaitFor("the-cartels-man-is-in-my-queue-for-me", 15f, () => InQueue("cartelIntro") && TradeVisits.SeatFor("cartelIntro") == Me && KnowsCartel, () => Where(Me));
		WaitFor("closed-by-host", 300f, () => DayNightBotBase.State == "CLOSED", () => Where(Me));
		Delay("past-close-timers", 6f);
		Agree("same-shop-with-the-chemist-back", 30f, "core.digest", "items.digest", "econ.hash");
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1, () => Where(Me));
		WaitFor("opened-by-host", 120f, () => DayNightBotBase.State == "OPEN", () => Where(Me));
		WaitFor("the-cartel-keeps-coming-for-me", 15f, () => InQueue("cartelSec") && TradeVisits.SeatFor("cartelSec") == Me && KnowsCartel, () => Where(Me));
		WaitFor("closed-again", 120f, () => DayNightBotBase.State == "CLOSED", () => Where(Me));
		Delay("two-comparisons-with-the-server", 22f);
		Agree("same-shop-at-the-end", 30f, "core.digest", "items.digest", "econ.hash");
		Check("no-reload", () => Digest.ReloadsAsked == 0 && DayNight.Mismatches == 0, () => "reloads asked " + Digest.ReloadsAsked + "; day/night mismatches " + DayNight.Mismatches + "; " + Where(Me));
		Do("leave-the-game", delegate
		{
			Net.Disconnect();
			return "left";
		});
	}
}
