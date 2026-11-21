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
		// Two greeting lines, then the customer's own first line.
		List<string> list = new List<string>();
		Dialogue dialogue = storeClient?.mainDialogue;
		for (int i = 0; i < 3 && dialogue != null; i++)
		{
			list.Add(dialogue.plainText ?? "");
			dialogue = dialogue.nextDialogue;
		}
		return string.Join(" / ", list);
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
		Check("the-chemists-supplier-comes-for-b-too", () => InQueue("retired_chemist") && TradeVisits.SeatFor("retired_chemist") == Specs.TakenBy("DRUG_DEALER") && FirstLine("retired_chemist").Contains(Members.NameOf(Specs.TakenBy("DRUG_DEALER"))) && !FirstLine("retired_chemist").Contains("again"), () => "in the queue " + InQueue("retired_chemist") + ", for seat " + TradeVisits.SeatFor("retired_chemist") + ", referral bought " + NetworkUpgrade.IsRetiredChemistUnlocked() + "; his first line: '" + FirstLine("retired_chemist") + "'");
		Check("the-shop-is-what-it-was", () => DayNightBotBase.Ps.startType != NewGameData.StartType.DRUG_DEALER && StoreStation.Instance.dayCounter == day0 + 1, () => Where + "; day counter " + StoreStation.Instance.dayCounter);
		Do("my-queue-next-day", () => Customers.DigestText());
		WaitFor("i-hear-b-ring-the-bell", 60f, () => BellShare.Heard > 0, () => "rings heard " + BellShare.Heard);
		// B asks for a resync and waits out the 30 s throttle.
		Delay("b-compares-the-queue-and-reloads", 150f);
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
		Check("the-night-start-was-compared", () => NightCheck.Checks >= 1, () => NightCheck.LastSummary);
		Check("the-shop-was-the-same-at-night", () => NightCheck.Checks >= 1 && NightCheck.Differ + NightCheck.OnlyServer + NightCheck.OnlyHere == 0, () => NightCheck.LastSummary).Soft();
		Check("the-first-night-block-ran-here", () => NightBlocks.Runs >= 1, () => NightBlocks.LastSummary);
		Check("the-first-night-block-came-out-the-same", () => NightBlocks.Runs >= 1 && NightBlocks.Differ + NightBlocks.Missing + NightBlocks.Extra == 0, () => NightBlocks.LastSummary).Soft();
		WaitFor("opened-next-day", 120f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		Delay("the-opening-lands", 2f);
		Check("the-cartels-man-is-in-my-queue-for-me", () => InQueue("cartelIntro") && TradeVisits.SeatFor("cartelIntro") == Members.LocalSeat && KnowsCartel, () => Where);
		Check("he-says-he-came-for-me", () => FirstLine("cartelIntro").Contains(Members.NameOf(Members.LocalSeat)) && TradeVisits.Openers.ContainsKey("cartelIntro"), () => "his first line here: '" + FirstLine("cartelIntro") + "'");
		Check("the-chemists-supplier-asks-for-me", () => InQueue("retired_chemist") && TradeVisits.SeatFor("retired_chemist") == Members.LocalSeat && FirstLine("retired_chemist").Contains(Members.NameOf(Members.LocalSeat)), () => "his first line here: '" + FirstLine("retired_chemist") + "'");
		WaitServer("the-hosts-queue", 20f, delegate(IReadOnlyDictionary<string, string> v)
		{
			theirs = DayNightBotBase.V(v, "cust.text");
			return theirs != "";
		}, "cust.text");
		Check("same-customers-as-the-host", () => Customers.DigestText() == theirs, () => (Customers.DigestText() == theirs) ? "same" : ("here " + Customers.DigestText() + " / host " + theirs));
		Check("no-reload-at-the-openings", () => DayNight.Mismatches == 0, () => "day/night mismatches " + DayNight.Mismatches + "; reloads asked " + Digest.ReloadsAsked);
		// A machine button whose caption is translated on this PC only still works.
		Do("press-my-blender-in-another-language", delegate
		{
			GameItem blender = Items.All().FirstOrDefault((GameItem i) => i != null && i.identifier == "blender" && Specs.OwnerOfType("blender") == Members.LocalSeat);
			if (blender == null)
			{
				throw new InvalidOperationException("no blender of mine in the shop");
			}
			ItemMouseDoubleClickHandler.current.OpenContentAction(blender, UnityEngine.Vector2.zero);
			MachineryButton btn = Machines.Widgets(blender).OfType<MachineryButton>().First();
			HarmonyLib.AccessTools.Property(typeof(MachineryButton), "text").SetValue(btn, "블렌드 시작 (번역)");
			pressApplied = Machines.AppliedCount;
			pressRejected = Machines.RejectCount;
			MachinesLab.Press(btn);
			return "pressed '" + btn.text + "' on blender #" + Ids.Of(blender) + "; sent " + Machines.SentCount;
		});
		WaitFor("the-server-took-the-press", 20f, () => Machines.AppliedCount > pressApplied || Machines.RejectCount > pressRejected, () => "applied " + Machines.AppliedCount + ", refused " + Machines.RejectCount);
		Check("not-refused-for-its-caption", () => Machines.RejectCount == pressRejected, () => "last refusal: " + Machines.LastReject);
		Do("ring-the-bell", delegate
		{
			BellShare.Rang();
			return "bell.ring sent";
		});
		// A moved window reopens in the same place after the reload.
		Do("open-and-move-my-bay", delegate
		{
			GameItem bay = Bays.BayOf(Members.LocalSeat);
			ItemMouseDoubleClickHandler.current.OpenContentAction(bay, UnityEngine.Vector2.zero);
			bay.contentWindow.rectTransform.anchoredPosition = new UnityEngine.Vector2(137f, -61f);
			bay.contentWindow.FixBounds();
			bayPos = bay.contentWindow.rectTransform.anchoredPosition;
			return "bay #" + bay.GetUniqueID() + " open: " + bay.contentWindow.IsVisible() + " at " + bayPos;
		});
		Do("ask-a-resync", delegate
		{
			loadsBefore = Snapshot.Loads;
			asksBefore = Snapshot.AskCount;
			Snapshot.RequestResync("rig: reload the morning with a supplier in the queue");
			return "loads " + loadsBefore + ", asks " + asksBefore;
		});
		WaitFor("the-morning-reloaded", 90f, () => Snapshot.Loads > loadsBefore && Client.State == ClientState.Ready, () => "loads " + Snapshot.Loads + ", state " + Client.State);
		Delay("the-replay-lands", 3f);
		Check("the-replay-agrees-with-the-server", () => Snapshot.LastReplay != null && Snapshot.LastReplay.ChecksBad == 0, () => (Snapshot.LastReplay == null) ? "no replay" : Snapshot.LastReplay.ToString());
		Check("my-bay-is-open-again-where-i-left-it", delegate
		{
			GameItem bay = Bays.BayOf(Members.LocalSeat);
			return bay?.contentWindow != null && bay.contentWindow.IsVisible() && (bay.contentWindow.rectTransform.anchoredPosition - bayPos).magnitude < 1f;
		}, delegate
		{
			GameItem bay = Bays.BayOf(Members.LocalSeat);
			return "open " + (bay?.contentWindow?.IsVisible() ?? false) + " at " + bay?.contentWindow?.rectTransform.anchoredPosition + " (was " + bayPos + "); reopened " + ProbablyTogether.UI.WindowMemory.Reopened + ", not: " + ProbablyTogether.UI.WindowMemory.LastSkipped;
		});
		Check("the-supplier-still-asks-for-me", () => InQueue("retired_chemist") && TradeVisits.SeatFor("retired_chemist") == Members.LocalSeat && FirstLine("retired_chemist").Contains(Members.NameOf(Members.LocalSeat)), () => "in the queue " + InQueue("retired_chemist") + ", for seat " + TradeVisits.SeatFor("retired_chemist") + "; his first line here: '" + FirstLine("retired_chemist") + "'");
		Delay("past-the-resync-throttle", 35f);
		Check("no-second-resync", () => Snapshot.AskCount == asksBefore + 1 && Snapshot.Loads == loadsBefore + 1, () => "asks " + Snapshot.AskCount + " (before " + asksBefore + "), loads " + Snapshot.Loads + " (before " + loadsBefore + ")");
	}

	private UnityEngine.Vector2 bayPos;

	private int pressApplied;

	private int pressRejected;

	private int loadsBefore;

	private int asksBefore;
}
