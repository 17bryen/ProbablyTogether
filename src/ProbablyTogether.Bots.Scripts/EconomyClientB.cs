using System;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class EconomyClientB : EconomyScript
{
	private int loadsBefore;

	private int refusalsBefore;

	public override string Name => "econ-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 1800f;

	protected override void Build()
	{
		Joined(fixturesFresh: false);
		Same("join", "econ.hash", "cash", "econ.requests", "econ.tokens", "econ.services", "econ.digest");
		WaitFor("saw-a-buys", 300f, () => EconomyLab.Up("BREWER").IsUnlocked() && EconomyLab.Up("SHOWCASE_I").IsUnlocked() && EconomyLab.Up("MARKETPLACE_REQUESTS").IsUnlocked(), () => "BREWER " + EconomyLab.Up("BREWER")?.state + ", SHOWCASE_I " + EconomyLab.Up("SHOWCASE_I")?.state + ", MARKETPLACE " + EconomyLab.Up("MARKETPLACE_REQUESTS")?.state);
		Check("saw-brewer-card", () => EconomyLab.Owned("card_brewer") == 1, () => "card_brewer x" + EconomyLab.Owned("card_brewer"));
		WaitFor("saw-request-filled", 300f, () => EconomyLab.Req() == null && EconomyLab.Owned("processed_meat") == 0, () => "request " + (EconomyLab.Req() != null) + ", processed_meat x" + EconomyLab.Owned("processed_meat"));
		Same("after-a", "econ.hash", "econ.digest", "econ.requests", "econ.services", "econ.tokens");
		MarkServer("b-ready");
		WaitFor("race-armed", 240f, () => EconomyLab.RaceId != null && Game.Store.playerCash > 0, () => "race " + EconomyLab.RaceId + ", cash " + Game.Store.playerCash);
		Do("race-buy", delegate
		{
			Economy.RequestBuy(EconomyLab.RaceId);
			return "asked for " + EconomyLab.RaceId + " (both clients ask before either hears back)";
		});
		WaitFor("race-settled", 30f, () => EconomyLab.Up(EconomyLab.RaceId).IsUnlocked(), () => BotProbes.Read("econ.race"));
		Delay("race-wait", 3f);
		MarkServer("b-raced");
		Same("race", "econ.hash", "cash", "econ.race");
		MarkServer("b-watching");
		WaitMark("a-intro-open", "a-intro-open", 300f);
		Check("no-intro-window-here", () => !WildNetworkUI.IsOpen && Game.Store.isNetworkIntroduced && Economy.IntroBrowsing && Economy.WindowsSkipped >= 1, () => "window " + WildNetworkUI.IsOpen + ", intro " + BotProbes.Read("econ.intro") + ", windows skipped here " + Economy.WindowsSkipped);
		Agree("same-intro", 20f, "econ.intro", "favor", "econ.hash", "econ.dpart");
		MarkServer("b-intro-checked");
		WaitMark("a-dialogues", "a-dialogues", 400f);
		Check("no-windows-for-a", () => !WildNetworkUI.IsOpen && !EconomyLab.CommissaryOpen && Economy.WindowsSkipped >= 2, () => "network " + WildNetworkUI.IsOpen + ", commissary " + EconomyLab.CommissaryOpen + ", windows skipped here " + Economy.WindowsSkipped + " (intro + reminder)");
		Agree("same-dialogues", 25f, "cash", "favor", "econ.rent", "econ.georgio", "econ.permit", "econ.intro", "econ.owned", "econ.counter", "econ.hash", "econ.dpart", "econ.digest");
		Agree("same-core-digest", 25f, "core.digest");
		WaitMark("a-trading", "a-trading", 240f);
		Check("a-holds-trade-lock", () => Economy.AtCommissary() && Locks.HeldBy("outing.commissary") > 0 && Locks.HeldBy("outing.commissary") != Net.LocalPlayer, () => "at '" + DayNightOuting.LocationId + "', lock " + Locks.HeldBy("outing.commissary"));
		Do("trade-button", delegate
		{
			refusalsBefore = Economy.Refusals;
			CommissaryUIManager.Instance.OpenUI();
			return "pressed Trade";
		});
		Check("trade-window-refused", () => !Economy.CommissaryWindowOpen && Economy.Refusals == refusalsBefore + 1 && Economy.LastRefusal.EndsWith("is trading at the commissary.", StringComparison.Ordinal), () => "window " + Economy.CommissaryWindowOpen + ", refusal '" + Economy.LastRefusal + "'");
		Act("forged-trade", delegate
		{
			Economy.RequestCommissary("buy", "nutrifruit");
			return "sent a commissary buy around the window";
		}, () => Answered());
		Check("forged-trade-refused", () => Economy.LastRefusal.StartsWith("commissary buy", StringComparison.Ordinal) && Economy.LastRefusal.EndsWith("is trading at the commissary.", StringComparison.Ordinal), () => "refusal '" + Economy.LastRefusal + "'");
		Do("return-early", delegate
		{
			DayNight.LastRefusedWhat = "";
			MapUIManager.Instance.ReturnNow();
			return "asked to take the group home";
		});
		WaitFor("return-refused", 15f, () => DayNight.LastRefusedWhat == "return", () => "day-night refusal '" + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy + "'");
		Check("still-out", () => Economy.AtCommissary(), () => "at '" + DayNightOuting.LocationId + "' (" + DayNight.LastRefusedWhy + ")");
		MarkServer("b-blocked");
		WaitMark("a-closed", "a-closed", 120f);
		WaitFor("trade-lock-free", 20f, () => Locks.HeldBy("outing.commissary") == -1, () => "lock " + Locks.HeldBy("outing.commissary"));
		Do("return", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "asked to take the group home";
		});
		WaitFor("home", 20f, () => !DayNightOuting.IsOut, () => "at '" + DayNightOuting.LocationId + "'; day-night refusal '" + DayNight.LastRefusedWhy + "'");
		Agree("same-home", 20f, "econ.loc", "econ.lock", "econ.tokens", "econ.afterhour", "econ.hash", "econ.dpart", "econ.digest");
		Do("resync", delegate
		{
			loadsBefore = EconomyLab.Loads;
			Snapshot.RequestResync();
			return "asked for a resync";
		});
		WaitFor("reloaded", 120f, () => EconomyLab.Loads > loadsBefore && Net.IsLive && Game.ShopLoaded, () => "loads " + EconomyLab.Loads + ", state " + Net.ClientState);
		Delay("settle-2", 2f);
		Agree("same-after-replay", 25f, "econ.hash", "econ.dpart", "econ.digest", "cash", "favor", "econ.requests", "econ.services", "econ.tokens", "econ.afterhour", "econ.up.BREWER", "econ.up.SEC_GRACE", "econ.race", "econ.rent", "econ.georgio", "econ.permit", "econ.intro", "econ.owned", "econ.loc", "econ.lock");
		Agree("same-core-digest-after-replay", 25f, "core.digest");
		Check("no-windows-here", () => !WildNetworkUI.IsOpen && !EconomyLab.CommissaryOpen, () => "network " + WildNetworkUI.IsOpen + ", commissary " + EconomyLab.CommissaryOpen + " (this player never paced)");
		MarkServer("b-resynced");
		Check("no-mismatch", () => Economy.Mismatches == 0 && DialogueSync.MismatchCount == 0 && Economy.GameErrors == 0, () => BotProbes.Read("econ.counts") + "; dialogue stepDone mismatches " + DialogueSync.MismatchCount);
		MarkServer("b-done");
	}
}
