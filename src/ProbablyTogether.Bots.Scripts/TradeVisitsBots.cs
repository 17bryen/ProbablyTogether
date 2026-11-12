using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

/// A player's trade in a hosted shop: B becomes the chemist after day 1 opens, so the cartel's man queues for B at the next opening.
public abstract class TradeVisitsBotBase : DayNightBotBase
{
	protected static bool InQueue(string id)
	{
		List<StoreClient> list = DayNightBotBase.Ps?.storeClientManager?.clientStack;
		return list != null && list.Any((StoreClient c) => c != null && c.identifier == id);
	}

	protected static bool KnowsCartel => DayNightBotBase.Ps.storeReputations.Any((StoreReputation r) => r != null && r.factionId == "FACTION_CARTEL");

	protected static string FirstLine(string id)
	{
		StoreClient storeClient = DayNightBotBase.Ps?.storeClientManager?.clientStack?.FirstOrDefault((StoreClient c) => c != null && c.identifier == id);
		return storeClient?.mainDialogue?.plainText ?? "";
	}

	protected static string Where => "day " + DayNightBotBase.Day + " " + DayNightBotBase.State + ", shop scenario " + DayNightBotBase.Ps.startType + ", chemist seat " + Specs.TakenBy("DRUG_DEALER") + " (first day " + (Members.Fact(Specs.TakenBy("DRUG_DEALER"), TradeVisits.DayFact) ?? "-") + "), cartel man in queue " + InQueue("cartelIntro") + " for seat " + TradeVisits.SeatFor("cartelIntro") + ", standing with the cartel " + KnowsCartel + ", added for trades " + TradeVisits.Added + " (" + TradeVisits.LastAdded + ")";
}

public sealed class TradeVisitsH : TradeVisitsBotBase
{
	public override string Name => "tv-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive + ", day " + DayNightBotBase.Day);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return Where;
		});
		Check("not-a-chemists-shop", () => DayNightBotBase.Ps.startType != NewGameData.StartType.DRUG_DEALER, () => Where);
		Delay("b-settles", 6f);
		Do("open-click", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened", 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		WaitFor("b-became-the-chemist-after-the-opening", 90f, () => Specs.TakenBy("DRUG_DEALER") != 0, () => Where);
		Check("its-first-day-is-tomorrow", () => Members.Fact(Specs.TakenBy("DRUG_DEALER"), TradeVisits.DayFact) == (day0 + 1).ToString(), () => Where);
		Check("nothing-of-the-cartel-today", () => !InQueue("cartelIntro") && !KnowsCartel, () => Where);
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
		WaitFor("shutter-settled", 20f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0, () => "flag " + DayNightBotBase.Ps.lockShutterButtonFlag);
		Delay("a-moment-open", 3f);
		WaitFor("closable", 15f, () => DayNightBotBase.Ps.CanEndDay(), () => "queue " + DayNightBotBase.Ps.storeClientManager.clientStack.Count);
		Do("close", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "sent";
		});
		WaitFor("closed", 20f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		Delay("past-close-timers", 14f);
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Delay("b-loads-the-morning", 10f);
		Do("open-click-next-day", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened-next-day", 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		Check("the-cartels-man-is-in-todays-queue-for-b", () => InQueue("cartelIntro") && TradeVisits.SeatFor("cartelIntro") == Specs.TakenBy("DRUG_DEALER") && KnowsCartel, () => Where);
		Check("he-says-who-he-came-for", () => FirstLine("cartelIntro").Contains(Members.NameOf(Specs.TakenBy("DRUG_DEALER"))) && FirstLine("cartelIntro").Contains("chemist"), () => "his first line: '" + FirstLine("cartelIntro") + "'");
		Check("the-shop-is-what-it-was", () => DayNightBotBase.Ps.startType != NewGameData.StartType.DRUG_DEALER && StoreStation.Instance.dayCounter == day0 + 1, () => Where + "; day counter " + StoreStation.Instance.dayCounter);
		Do("my-queue-next-day", () => Customers.DigestText());
		Delay("b-compares-the-queue", 30f);
	}
}

public sealed class TradeVisitsB : TradeVisitsBotBase
{
	private string theirs = "";

	public override string Name => "tv-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		JoinSteps();
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return Where;
		});
		WaitFor("opened-by-host", 240f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		WaitFor("my-bay-is-here", 60f, () => Bays.BayOf(Members.LocalSeat) != null, () => "seat " + Members.LocalSeat);
		Do("pick-after-the-opening", delegate
		{
			ProbablyTogether.UI.Chat.Send("/trade chemist");
			return "typed '/trade chemist' in chat with the shop open";
		});
		WaitFor("i-am-the-chemist", 30f, () => Specs.Of(Members.LocalSeat) == "DRUG_DEALER", () => Where + "; refusal '" + Specs.LastRefusal + "'");
		Check("my-first-day-is-tomorrow", () => Members.Fact(Members.LocalSeat, TradeVisits.DayFact) == (day0 + 1).ToString(), () => Where);
		WaitFor("closed-by-host", 120f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		Delay("past-close-timers", 6f);
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		WaitFor("opened-next-day", 120f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		Delay("the-opening-lands", 2f);
		Check("the-cartels-man-is-in-my-queue-for-me", () => InQueue("cartelIntro") && TradeVisits.SeatFor("cartelIntro") == Members.LocalSeat && KnowsCartel, () => Where);
		Check("he-says-he-came-for-me", () => FirstLine("cartelIntro").Contains(Members.NameOf(Members.LocalSeat)) && TradeVisits.Openers.ContainsKey("cartelIntro"), () => "his first line here: '" + FirstLine("cartelIntro") + "'");
		WaitServer("the-hosts-queue", 20f, delegate(IReadOnlyDictionary<string, string> v)
		{
			theirs = DayNightBotBase.V(v, "cust.text");
			return theirs != "";
		}, "cust.text");
		Check("same-customers-as-the-host", () => Customers.DigestText() == theirs, () => (Customers.DigestText() == theirs) ? "same" : ("here " + Customers.DigestText() + " / host " + theirs));
		Check("no-reload-at-the-openings", () => DayNight.Mismatches == 0, () => "day/night mismatches " + DayNight.Mismatches + "; reloads asked " + Digest.ReloadsAsked);
	}
}
