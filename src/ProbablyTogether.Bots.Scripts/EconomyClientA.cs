using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class EconomyClientA : EconomyScript
{
	private int cashBefore;

	private int favorBefore;

	private int costBefore;

	private int stampsBefore;

	private int rentBefore;

	private int dayLeft;

	private string ownedBefore;

	private int dlg0;

	public override string Name => "econ-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 1800f;

	protected override void Build()
	{
		Joined();
		Same("join", "econ.hash", "cash", "econ.requests", "econ.services");
		Do("wild-phone", delegate
		{
			Vars["wild0"] = ((object)EconomyLab.WildPhone()).ToString();
			EconomyLab.Op("phone", "8376", "Regular");
			return "Wild's phone was " + Vars["wild0"] + " -> Regular (lab op)";
		});
		WaitFor("wild-phone-set", 20f, () => (int)EconomyLab.WildPhone() == 4, () => "Wild's phone " + ((object)EconomyLab.WildPhone()).ToString());
		Step("browse", 60f, EconomyLab.PhoneBrowsePoll());
		WaitFor("browsed", 20f, () => Game.Store.daySinceLastBrowseUpgrade == 0, () => "days " + Game.Store.daySinceLastBrowseUpgrade);
		ServerSays("window-open-there", 15f, "econ.windows", "1|0");
		Same("browse", "econ.days", "econ.hash");
		Act("buy-brewer", delegate
		{
			cashBefore = Game.Store.playerCash;
			costBefore = EconomyLab.Up("BREWER").GetCost();
			return EconomyLab.ClickBuy("BREWER");
		}, () => EconomyLab.Up("BREWER").IsUnlocked());
		Check("brewer-paid", delegate
		{
			if (Game.Store.playerCash == cashBefore - costBefore && Owned("card_brewer") == 1)
			{
				StorePhoneClient brewerPhoneClient = StorePhoneClient.GetBrewerPhoneClient();
				if (brewerPhoneClient == null)
				{
					return false;
				}
				return (int)brewerPhoneClient.phoneState == 3;
			}
			return false;
		}, () => "cash " + cashBefore + " -> " + Game.Store.playerCash + " (cost " + costBefore + "), card_brewer x" + Owned("card_brewer") + ", phone " + (StorePhoneClient.GetBrewerPhoneClient()?.phoneState).ToString());
		Same("brewer", "econ.hash", "econ.digest", "cash", "econ.up.BREWER");
		Do("favor-fixture", delegate
		{
			int cost = EconomyLab.Up("SHOWCASE_I").GetCost();
			EconomyLab.Op("favor", cost.ToString(CultureInfo.InvariantCulture));
			return "favor -> " + cost;
		});
		WaitFor("favor-set", 20f, () => Game.Store.wildFavor == EconomyLab.Up("SHOWCASE_I").GetCost(), () => "favor " + Game.Store.wildFavor);
		Act("buy-showcase", delegate
		{
			cashBefore = Game.Store.playerCash;
			favorBefore = Game.Store.wildFavor;
			return EconomyLab.ClickBuy("SHOWCASE_I");
		}, () => EconomyLab.Up("SHOWCASE_I").IsUnlocked());
		Check("showcase-paid-by-favor", () => Game.Store.playerCash == cashBefore && Game.Store.wildFavor == 0, () => "cash " + cashBefore + " -> " + Game.Store.playerCash + ", favor " + favorBefore + " -> " + Game.Store.wildFavor);
		Same("showcase", "econ.hash", "econ.digest", "favor", "econ.up.SHOWCASE_I");
		Act("buy-sec-grace", delegate
		{
			costBefore = EconomyLab.Up("SEC_GRACE").cost;
			return EconomyLab.ClickBuy("SEC_GRACE");
		}, () => EconomyLab.Up("SEC_GRACE").cost == costBefore + 100);
		Check("sec-grace-held", () => EconomyLab.Up("SEC_GRACE").state == 2, () => "state " + EconomyLab.Up("SEC_GRACE").state + ", cost " + costBefore + " -> " + EconomyLab.Up("SEC_GRACE").cost);
		Same("sec-grace", "econ.hash", "cash", "econ.up.SEC_GRACE");
		Act("buy-marketplace", () => EconomyLab.ClickBuy("MARKETPLACE_REQUESTS"), () => EconomyLab.Up("MARKETPLACE_REQUESTS").IsUnlocked());
		Same("marketplace", "econ.hash", "econ.requests", "econ.digest");
		Do("broke", delegate
		{
			EconomyLab.Op("cash", "0");
			EconomyLab.Op("favor", "0");
			return "cash and favor -> 0";
		});
		WaitFor("broke-set", 20f, () => Game.Store.playerCash == 0 && Game.Store.wildFavor == 0);
		Act("forged-buy", delegate
		{
			Economy.RequestBuy("DOSSIER_UPGRADE");
			return "asked for DOSSIER_UPGRADE with no money";
		}, () => Answered());
		Check("forged-refused", () => !EconomyLab.Up("DOSSIER_UPGRADE").IsUnlocked() && Economy.LastRefusal.Contains("DOSSIER_UPGRADE"), () => "refusal '" + Economy.LastRefusal + "', state " + EconomyLab.Up("DOSSIER_UPGRADE").state);
		Do("cash-back", delegate
		{
			EconomyLab.Op("cash", "5000");
			return "cash -> 5000";
		});
		WaitFor("cash-back-set", 20f, () => Game.Store.playerCash == 5000);
		Do("close-window", delegate
		{
			WildNetworkUI.Close();
			return "closed the network window (open " + WildNetworkUI.IsOpen + ")";
		});
		ServerSays("window-closed-there", 15f, "econ.windows", "0|0");
		Act("buy-window-closed", delegate
		{
			Vars["closedId"] = EconomyLab.FreshUpgrade("BREWER", "SHOWCASE_I", "SEC_GRACE", "MARKETPLACE_REQUESTS", "DOSSIER_UPGRADE") ?? "DOSSIER_UPGRADE";
			Economy.RequestBuy(Vars["closedId"]);
			return "asked for " + Vars["closedId"] + " with cash " + Game.Store.playerCash + " and no window open";
		}, () => Answered());
		Check("closed-window-refused", () => !EconomyLab.Up(Vars["closedId"]).IsUnlocked() && Economy.LastRefusal.EndsWith("the network window is not open", StringComparison.Ordinal) && Game.Store.playerCash == 5000, () => "refusal '" + Economy.LastRefusal + "', " + Vars["closedId"] + " state " + EconomyLab.Up(Vars["closedId"]).state + ", cash " + Game.Store.playerCash);
		Do("wild-phone-back", delegate
		{
			EconomyLab.Op("phone", "8376", Vars["wild0"]);
			return "Wild's phone -> " + Vars["wild0"];
		});
		WaitFor("wild-phone-back-set", 20f, () => ((object)EconomyLab.WildPhone()).ToString() == Vars["wild0"], () => "Wild's phone " + ((object)EconomyLab.WildPhone()).ToString());
		Act("janitorial-off", () => EconomyLab.ClickService("JANITORIAL_SERVICE", on: false), () => !EconomyLab.Svc("JANITORIAL_SERVICE").isUsing);
		Same("janitorial-off", "econ.services", "econ.hash");
		Act("janitorial-on", () => EconomyLab.ClickService("JANITORIAL_SERVICE", on: true), () => EconomyLab.Svc("JANITORIAL_SERVICE").isUsing);
		Act("cartel-off-refused", () => EconomyLab.ClickService("CARTEL_PROTECTION", on: false), () => Answered());
		Check("cartel-refused", () => Economy.LastRefusal.Contains("CARTEL_PROTECTION"), () => "refusal '" + Economy.LastRefusal + "'");
		Act("criminal-on-refused", () => EconomyLab.ClickService("CRIMINEL_NETWORK", on: true), () => Answered());
		Check("criminal-refused", () => Economy.LastRefusal.Contains("CRIMINEL_NETWORK") && !EconomyLab.Svc("CRIMINEL_NETWORK").isUsing, () => "refusal '" + Economy.LastRefusal + "'");
		Do("evening", delegate
		{
			EconomyLab.Op("use", "CRIMINEL_NETWORK", "1");
			EconomyLab.Op("use", "CARTEL_PROTECTION", "0");
			EconomyLab.Op("evening");
			return "cooldown service forced on, mandatory off, then the evening update";
		});
		WaitFor("evening-normalized", 20f, () => !EconomyLab.Svc("CRIMINEL_NETWORK").isUsing && EconomyLab.Svc("CARTEL_PROTECTION").isUsing, () => BotProbes.Read("econ.services"));
		Same("evening", "econ.services", "econ.hash");
		Act("commissary-away", delegate
		{
			costBefore = Game.Store.commissaryData.redeemedCredit;
			Game.Store.commissaryData.BuyItem("nutrifruit");
			return "tried to buy nutrifruit from the shop with " + costBefore + " tokens";
		}, () => Answered());
		Check("commissary-away-refused", () => Game.Store.commissaryData.redeemedCredit == costBefore && Economy.LastRefusal.StartsWith("commissary buy", StringComparison.Ordinal) && Economy.LastRefusal.Contains("not at the commissary"), () => "refusal '" + Economy.LastRefusal + "', tokens " + costBefore + " -> " + Game.Store.commissaryData.redeemedCredit);
		Same("commissary", "econ.tokens", "econ.hash");
		Act("fill-request", delegate
		{
			cashBefore = Game.Store.playerCash;
			EconomyLab.Req().FillRequest();
			return "filling br_test with " + Owned("processed_meat") + " processed_meat";
		}, () => EconomyLab.Req() == null);
		Check("request-paid", () => Game.Store.playerCash == cashBefore + 77 && Owned("processed_meat") == 0, () => "cash " + cashBefore + " -> " + Game.Store.playerCash + ", processed_meat x" + Owned("processed_meat"));
		Same("request", "econ.requests", "econ.digest", "cash", "econ.hash");
		Do("guard", delegate
		{
			cashBefore = Game.Store.playerCash;
			Game.Store.ModCash(1000, true);
			return "ModCash(1000) called locally";
		});
		Check("guard-blocked", () => Game.Store.playerCash == cashBefore && Economy.Blocked > 0, () => "cash " + cashBefore + " -> " + Game.Store.playerCash + ", blocked " + Economy.Blocked + " (" + Economy.LastBlocked + ")");
		Do("lottery-fixture", delegate
		{
			EconomyLab.Op("lottery", "50");
			return "a winning ticket worth 50";
		});
		WaitFor("lottery-arrived", 20f, () => Owned("lottery_ticket") == 1);
		Do("wild-phone-2", delegate
		{
			EconomyLab.Op("phone", "8376", "Regular");
			return "Wild's phone -> Regular (lab op)";
		});
		WaitFor("wild-phone-2-set", 20f, () => (int)EconomyLab.WildPhone() == 4, () => "Wild's phone " + ((object)EconomyLab.WildPhone()).ToString());
		Step("browse-2", 60f, EconomyLab.PhoneBrowsePoll());
		ServerSays("window-open-there-2", 15f, "econ.windows", "1|0");
		MarkServer("a-ready");
		WaitFor("race-armed", 240f, () => EconomyLab.RaceId != null && Game.Store.playerCash > 0, () => "race " + EconomyLab.RaceId + ", cash " + Game.Store.playerCash);
		Do("race-buy", delegate
		{
			Economy.RequestBuy(EconomyLab.RaceId);
			return "asked for " + EconomyLab.RaceId + " (both clients ask before either hears back)";
		});
		WaitFor("race-settled", 30f, () => EconomyLab.Up(EconomyLab.RaceId).IsUnlocked(), () => BotProbes.Read("econ.race"));
		Delay("race-wait", 3f);
		MarkServer("a-raced");
		Same("race", "econ.hash", "cash", "econ.race", "econ.digest");
		Do("close-window-2", delegate
		{
			WildNetworkUI.Close();
			return "closed the network window (open " + WildNetworkUI.IsOpen + ")";
		});
		ServerSays("window-closed-there-2", 15f, "econ.windows", "0|0");
		Do("wild-phone-back-2", delegate
		{
			EconomyLab.Op("phone", "8376", Vars["wild0"]);
			return "Wild's phone -> " + Vars["wild0"];
		});
		WaitFor("wild-phone-back-2-set", 20f, () => ((object)EconomyLab.WildPhone()).ToString() == Vars["wild0"], () => "Wild's phone " + ((object)EconomyLab.WildPhone()).ToString());
		Dialogues();
		Outing();
		WaitMark("b-resynced", "b-resynced", 240f);
		Agree("same-final", 25f, "econ.hash", "econ.dpart", "cash", "favor", "econ.rent", "econ.georgio", "econ.permit", "econ.intro", "econ.owned", "econ.tokens");
		Check("no-mismatch", () => Economy.Mismatches == 0 && DialogueSync.MismatchCount == 0 && Economy.GameErrors == 0, () => BotProbes.Read("econ.counts") + "; dialogue stepDone mismatches " + DialogueSync.MismatchCount);
		WaitMark("b-done", "b-done", 240f);
		MarkServer("a-done");
		RunOver();
	}

	private void Dialogues()
	{
		WaitMark("b-watching", "b-watching", 120f);
		Do("options", delegate
		{
			EmporiumOption instance = EmporiumOption.Instance;
			if ((Object)(object)instance != (Object)null)
			{
				instance.autoCallNext = false;
			}
			return "autoCallNext off (a visit's end must not call a generated customer)";
		});
		Do("cash-5000", delegate
		{
			EconomyLab.Op("cash", "5000");
			return "cash -> 5000 (rent, loan and permit need it)";
		});
		WaitFor("cash-5000-set", 20f, () => Game.Store.playerCash == 5000);
		Do("open-shutter", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "clicked the shutter (state " + Game.Store.storeState.ToString() + ")";
		});
		WaitFor("open", 30f, () => (int)Game.Store.storeState == 1 && Game.Store.lockShutterButtonFlag == 0, () => "state " + Game.Store.storeState.ToString() + ", shutter flag " + Game.Store.lockShutterButtonFlag);
		WaitFor("counter-empty", 30f, () => Game.Store.currentClientInstance == null, EconomyScript.Dlg);
		Visit("wild2", "intro");
		Do("note-intro", delegate
		{
			favorBefore = Game.Store.wildFavor;
			ownedBefore = BotProbes.Read("econ.owned");
			return "favor " + favorBefore + ", owned " + ownedBefore;
		});
		Talk("talk-intro", 90f, (Dialogue _) => 0, () => WildNetworkUI.IsOpen && DialogueSystem.Counter.Current == null && Game.Store.isNetworkIntroduced);
		Check("intro-window-here", () => Economy.IntroBrowsing && Game.Store.wildFavor > favorBefore && Economy.WindowsSkipped == 0, () => "window open, intro browsing " + Economy.IntroBrowsing + ", favor " + favorBefore + " -> " + Game.Store.wildFavor + ", windows skipped here " + Economy.WindowsSkipped);
		Agree("same-intro", 20f, "econ.intro", "favor", "econ.hash", "econ.dpart");
		MarkServer("a-intro-open");
		WaitMark("b-intro-checked", "b-intro-checked", 60f);
		Do("close-intro-window", delegate
		{
			WildNetworkUI.Close();
			return "closed the network window (window open " + WildNetworkUI.IsOpen + ")";
		});
		Talk("talk-wild-continue", 90f, (Dialogue _) => 0, () => EconomyLab.CounterId() == "none" && DialogueSystem.Counter.Current == null && !Economy.IntroBrowsing);
		Check("wild-continued", delegate
		{
			StorePhoneClient wildPhoneClient = StorePhoneClient.GetWildPhoneClient();
			return wildPhoneClient != null && (int)wildPhoneClient.phoneState == 4 && Owned("joe_card") >= 1 && !WildNetworkUI.IsOpen;
		}, () => "Wild's phone " + (StorePhoneClient.GetWildPhoneClient()?.phoneState).ToString() + ", owned " + ownedBefore + " -> " + BotProbes.Read("econ.owned") + ", window " + WildNetworkUI.IsOpen);
		Agree("same-wild-continue", 20f, "econ.intro", "econ.owned", "econ.counter", "econ.hash", "econ.dpart", "econ.digest");
		Do("days-4", delegate
		{
			EconomyLab.Op("days", "4");
			return "daySinceLastBrowseUpgrade -> 4";
		});
		WaitFor("days-4-set", 20f, () => Game.Store.daySinceLastBrowseUpgrade == 4);
		Visit("wild_upgrade_reminder", "reminder");
		Talk("talk-reminder", 90f, (Dialogue _) => 0, () => WildNetworkUI.IsOpen && DialogueSystem.Counter.Current == null);
		Check("reminder-window-here", () => Game.Store.daySinceLastBrowseUpgrade == 0 && EconomyLab.CounterId() == "wild_upgrade_reminder", () => "days " + Game.Store.daySinceLastBrowseUpgrade + ", at the counter " + EconomyLab.CounterId());
		Agree("same-reminder", 20f, "econ.days", "econ.counter", "econ.hash");
		Do("close-reminder-window", delegate
		{
			WildNetworkUI.Close();
			return "closed the network window";
		});
		WaitFor("joe-left", 30f, () => EconomyLab.CounterId() == "none", EconomyScript.Dlg);
		Agree("same-joe-left", 20f, "econ.counter", "econ.hash", "econ.dpart");
		Do("note-rent", delegate
		{
			cashBefore = Game.Store.playerCash;
			rentBefore = Game.Store.GetRentValue();
			return "cash " + cashBefore + ", rent " + Game.Store.rentValue + "+" + Game.Store.temporaryRent + ", days left " + Game.Store.dayUntilRent;
		});
		Visit("landlord", "rent");
		Talk("talk-rent", 90f, (Dialogue d) => 0, VisitOver);
		Check("rent-paid", () => Game.Store.playerCash == cashBefore - rentBefore && Game.Store.dayUntilRent == 7 && Game.Store.temporaryRent == 0 && Game.Store.rentValue >= rentBefore, () => "cash " + cashBefore + " -> " + Game.Store.playerCash + " (rent " + rentBefore + "), rent now " + BotProbes.Read("econ.rent"));
		Agree("same-rent", 20f, "cash", "econ.rent", "econ.hash", "econ.dpart", "econ.digest");
		Do("note-loan", delegate
		{
			cashBefore = Game.Store.playerCash;
			ownedBefore = BotProbes.Read("econ.owned");
			return "cash " + cashBefore + ", owned " + ownedBefore;
		});
		Visit("georgioLoan", "loan");
		Talk("talk-loan", 120f, (Dialogue d) => 0, VisitOver);
		Check("loan-lent", () => Game.Store.loanOfferedToGeorgio == cashBefore / 4 && Game.Store.playerCash == cashBefore - cashBefore / 4 && Owned("scav_token") >= 1 && Owned("handnote") >= 1, () => "cash " + cashBefore + " -> " + Game.Store.playerCash + ", loan " + BotProbes.Read("econ.georgio") + ", owned " + ownedBefore + " -> " + BotProbes.Read("econ.owned"));
		Agree("same-loan", 20f, "cash", "econ.georgio", "econ.owned", "econ.hash", "econ.dpart", "econ.digest");
		Do("note-permit", delegate
		{
			cashBefore = Game.Store.playerCash;
			costBefore = PlayerStore.GetBusinessPermitPrice();
			return "cash " + cashBefore + ", permit " + costBefore + ", permit state " + BotProbes.Read("econ.permit");
		});
		Visit("businessPermitOfficial", "permit");
		Talk("talk-permit", 120f, (Dialogue d) => d.choices.Count - 1, VisitOver);
		Check("permit-bought", () => Game.Store.hasBusinessPermit && Game.Store.playerCash == cashBefore - costBefore && Owned("business_permit") >= 1, () => "cash " + cashBefore + " -> " + Game.Store.playerCash + " (price " + costBefore + "), permit " + BotProbes.Read("econ.permit") + ", owned " + BotProbes.Read("econ.owned"));
		Agree("same-permit", 20f, "cash", "econ.permit", "econ.owned", "econ.hash", "econ.dpart", "econ.digest");
		Agree("same-core-digest", 25f, "core.digest");
		MarkServer("a-dialogues");
	}

	private bool VisitOver()
	{
		if (EconomyLab.CounterId() == "none" && DialogueSystem.Counter.Current == null)
		{
			return DialogueSync.Seq("counter") > dlg0;
		}
		return false;
	}

	private void Visit(string id, string tag)
	{
		WaitFor("free-" + tag, 30f, () => Game.Store.currentClientInstance == null && Game.Store.lockNextBellFlag == 0, EconomyScript.Dlg);
		Do("queue-" + tag, delegate
		{
			EconomyLab.Op("visitor", id);
			return "asked the server to put " + id + " first in the queue";
		});
		WaitFor("queued-" + tag, 20f, () => EconomyLab.QueueHead() == id, () => "head " + EconomyLab.QueueHead());
		Do("call-" + tag, delegate
		{
			dlg0 = DialogueSync.Seq("counter");
			Customers.RequestCall();
			return "rang the bell (dialogue step " + dlg0 + ")";
		});
		WaitFor("arrived-" + tag, 40f, () => EconomyLab.CounterId() == id && Game.Store.isClientArrived, EconomyScript.Dlg);
		Check("holds-counter-" + tag, () => Locks.HeldBy("counter") == Net.LocalPlayer, () => "counter holder " + Locks.HeldBy("counter") + ", me " + Net.LocalPlayer);
	}

	private BotStep Talk(string name, float timeout, Func<Dialogue, int> choose, Func<bool> done)
	{
		float lastNudge = 0f;
		int shown = 0;
		int advanced = 0;
		string picks = "";
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (done())
			{
				st.Detail = "dialogue steps " + dlg0 + " -> " + DialogueSync.Seq("counter") + " (shown " + shown + ", advance " + advanced + ((picks.Length > 0) ? (", chose " + picks) : "") + "), refused " + DialogueSync.RefusedCount + "; " + EconomyScript.Dlg() + " after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
				return StepStatus.Pass;
			}
			CounterChannel counter = DialogueSystem.Counter;
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (counter.Current == null || realtimeSinceStartup - lastNudge < 1.2f)
			{
				return StepStatus.Running;
			}
			lastNudge = realtimeSinceStartup;
			if (counter.LineUnfinished)
			{
				if (DialogueSync.RequestShown("counter"))
				{
					shown++;
				}
			}
			else if (counter.IsChoiceNow)
			{
				int index = choose(counter.Current);
				if (DialogueSync.RequestChoose("counter", index))
				{
					picks = picks + ((picks.Length > 0) ? "," : "") + index + "/" + counter.Current.choices.Count;
				}
			}
			else if (DialogueSync.RequestAdvance("counter"))
			{
				advanced++;
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "conversation not done: " + EconomyScript.Dlg() + "; shown " + shown + ", advance " + advanced + ", chose " + picks + ", refused " + DialogueSync.RefusedCount + " (" + DialogueSystem.LastRefused + ")";
		return botStep;
	}

	private void Outing()
	{
		Do("clear-queue", delegate
		{
			if (Game.Store.CanEndDay())
			{
				return "every waiting customer can be sent away";
			}
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "clearqueue"
			});
			return "a story customer is waiting: day-night's lab op clearqueue";
		});
		WaitFor("closable", 20f, () => Game.Store.CanEndDay() && Game.Store.currentClientInstance == null, () => "queue " + Game.Store.storeClientManager.clientStack.Count + ", " + EconomyScript.Dlg());
		Do("close-shop", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "closed the shutter";
		});
		WaitFor("closed", 30f, () => (int)Game.Store.storeState == 0, () => "state " + Game.Store.storeState.ToString());
		Delay("past-close-timers", 5f);
		Do("door", delegate
		{
			EmporiumEntry.Instance.selectedBackpack = null;
			MapUIManager.Instance.OpenUI();
			return "opened the door (dayNight.goOut)";
		});
		WaitFor("map-open", 20f, () => DayNightOuting.MapOpen, () => "map " + DayNightOuting.MapOpen + "; last day-night refusal '" + DayNight.LastRefusedWhy + "'");
		Do("travel", delegate
		{
			AfterhourLocation val = AfterhourLocation.Get("commissary");
			MapUIManager.Instance.Travel(val);
			return "to the commissary (discovered " + (val != null && val.IsDiscovered()) + ", available " + (val != null && val.IsAvailable()) + ")";
		});
		WaitFor("at-commissary", 20f, () => Economy.AtCommissary(), () => "at '" + DayNightOuting.LocationId + "'; last day-night refusal '" + DayNight.LastRefusedWhy + "'");
		Agree("same-at-commissary", 20f, "econ.loc", "econ.hash", "econ.lock");
		Do("trade-button", delegate
		{
			CommissaryUIManager.Instance.OpenUI();
			return "pressed Trade (window open " + Economy.CommissaryWindowOpen + ")";
		});
		WaitFor("trade-lock", 20f, () => Economy.CommissaryWindowOpen && Locks.HeldBy("outing.commissary") == Net.LocalPlayer, () => "window " + Economy.CommissaryWindowOpen + ", lock " + Locks.HeldBy("outing.commissary"));
		MarkServer("a-trading");
		WaitMark("b-blocked", "b-blocked", 90f);
		Act("trade", delegate
		{
			costBefore = Game.Store.commissaryData.redeemedCredit;
			Game.Store.commissaryData.BuyItem("nutrifruit");
			return "bought nutrifruit at the commissary with " + costBefore + " tokens";
		}, () => Game.Store.commissaryData.redeemedCredit == costBefore - 12, 20f, () => "tokens " + costBefore + " -> " + Game.Store.commissaryData.redeemedCredit + "; last refusal '" + Economy.LastRefusal + "'");
		Agree("same-trade", 20f, "econ.tokens", "econ.afterhour", "econ.hash", "econ.dpart");
		Act("stamps", delegate
		{
			stampsBefore = Owned("food_stamp");
			costBefore = Game.Store.commissaryData.redeemedCredit;
			CommissaryUIManager.Instance.OnRedeemAllButtonClick();
			return stampsBefore + " stamp(s), " + costBefore + " tokens";
		}, () => Answered());
		Check("stamps-redeemed", () => Owned("food_stamp") == 0 && Game.Store.commissaryData.redeemedCredit == costBefore + 80 && Economy.GameErrors == 0, () => "stamps " + stampsBefore + " -> " + Owned("food_stamp") + ", tokens " + costBefore + " -> " + Game.Store.commissaryData.redeemedCredit + ", game errors " + Economy.GameErrors + "; last refusal '" + Economy.LastRefusal + "'");
		Agree("same-stamps", 20f, "econ.tokens", "econ.digest", "econ.hash");
		Act("lottery", delegate
		{
			cashBefore = Game.Store.playerCash;
			CommissaryUIManager.Instance.OnRedeemAllLottery();
			return "redeeming with cash " + cashBefore;
		}, () => Game.Store.playerCash == cashBefore + 50 && Owned("lottery_ticket") == 0, 20f, () => "cash " + cashBefore + " -> " + Game.Store.playerCash + ", tickets " + Owned("lottery_ticket") + "; last refusal '" + Economy.LastRefusal + "'");
		Agree("same-lottery", 20f, "cash", "econ.digest", "econ.hash");
		Do("close-trade", delegate
		{
			CommissaryUIManager.Instance.CloseUI();
			return "closed the trade window";
		});
		WaitFor("trade-lock-free", 20f, () => Locks.HeldBy("outing.commissary") == -1 && !Economy.CommissaryWindowOpen, () => "lock " + Locks.HeldBy("outing.commissary"));
		Act("trade-closed", delegate
		{
			costBefore = Game.Store.commissaryData.redeemedCredit;
			Economy.RequestCommissary("buy", "nutrifruit");
			return "sent a commissary buy with the trade window closed";
		}, () => Answered());
		Check("trade-closed-refused", () => Game.Store.commissaryData.redeemedCredit == costBefore && Economy.LastRefusal.EndsWith("the commissary trade window is not open", StringComparison.Ordinal), () => "refusal '" + Economy.LastRefusal + "', tokens " + costBefore + " -> " + Game.Store.commissaryData.redeemedCredit);
		MarkServer("a-closed");
		WaitFor("home", 90f, () => !DayNightOuting.IsOut, () => "at '" + DayNightOuting.LocationId + "'");
		Agree("same-home", 20f, "econ.loc", "econ.lock", "econ.tokens", "econ.hash", "econ.dpart", "econ.digest");
		Agree("same-core-digest-home", 25f, "core.digest");
	}

	private void RunOver()
	{
		Do("ending", delegate
		{
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "gameover:rent"
			});
			return "asked day-night's lab for the 'rent' ending";
		});
		WaitFor("run-ended-here", 60f, () => DayNightRun.RunOver, () => "run " + BotProbes.Read("econ.runover"));
		Do("buy-after-end", delegate
		{
			DayNight.LastRefusedWhat = "";
			Economy.RequestBuy(EconomyLab.FreshUpgrade() ?? "DOSSIER_UPGRADE");
			return "asked to buy an upgrade after the ending";
		});
		WaitFor("buy-refused", 20f, () => DayNight.LastRefusedWhat == "econ.network.buy", () => "last refusal '" + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy + "'");
		Check("refused-as-ended", () => DayNight.LastRefusedWhy.StartsWith("The run has ended.", StringComparison.Ordinal), () => "'" + DayNight.LastRefusedWhy + "'");
	}

	private static int Owned(string id)
	{
		return EconomyLab.Owned(id);
	}
}
