using System;
using System.Globalization;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class EconomyServerBot : BotScript
{
	private int refusalsBefore;

	private string runOverId;

	public override string Name => "econ-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 1800f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop && Game.ShopLoaded, () => "scene " + BotProbes.Scene);
		Do("fixtures", delegate
		{
			EconomyLab.Op("cash", "5000");
			EconomyLab.Op("favor", "0");
			EconomyLab.Op("credit", "100");
			EconomyLab.Op("stock", "nutrifruit", "5");
			EconomyLab.Op("spawn", "food_stamp", "2");
			EconomyLab.Op("spawn", "processed_meat", "2");
			EconomyLab.Op("request", "processed_meat", "2", "77");
			EconomyLab.Op("service", "JANITORIAL_SERVICE", "1", "0");
			EconomyLab.Op("use", "JANITORIAL_SERVICE", "1");
			EconomyLab.Op("service", "CRIMINEL_NETWORK", "1", "2");
			EconomyLab.Op("service", "CARTEL_PROTECTION", "1", "0");
			EconomyLab.Op("days", "3");
			EconomyLab.Op("fixtures");
			return Economy.Describe() + "; day log " + DayLog.Count;
		});
		WaitFor("clients-ready", 420f, () => EconomyLab.Marks.Contains("a-ready") && EconomyLab.Marks.Contains("b-ready"), () => "marks " + string.Join(",", EconomyLab.Marks));
		Do("arm-race", delegate
		{
			string text = EconomyLab.FreshUpgrade("BREWER", "SHOWCASE_I", "SEC_GRACE", "MARKETPLACE_REQUESTS", "DOSSIER_UPGRADE");
			if (text == null)
			{
				throw new InvalidOperationException("no fresh upgrade to race for");
			}
			int cost = EconomyLab.Up(text).GetCost();
			refusalsBefore = Economy.Refusals;
			EconomyLab.Op("cash", cost.ToString(CultureInfo.InvariantCulture));
			EconomyLab.Op("favor", "0");
			EconomyLab.Op("arm", text);
			return "race for " + text + " at " + cost + " with cash " + cost;
		});
		WaitFor("race-over", 90f, () => EconomyLab.Marks.Contains("a-raced") && EconomyLab.Marks.Contains("b-raced"), () => "marks " + string.Join(",", EconomyLab.Marks));
		Check("race-one-winner", () => EconomyLab.Up(EconomyLab.RaceId).IsUnlocked() && Economy.Refusals == refusalsBefore + 1 && Game.Store.playerCash == 0, () => EconomyLab.RaceId + " state " + EconomyLab.Up(EconomyLab.RaceId).state + ", refusals " + (Economy.Refusals - refusalsBefore) + " (last '" + Economy.LastRefusal + "'), cash " + Game.Store.playerCash);
		WaitFor("trade-lock-seen", 900f, () => EconomyLab.Marks.Contains("b-blocked"), () => "marks " + string.Join(",", EconomyLab.Marks) + "; lock " + Locks.Holder("outing.commissary"));
		Check("trade-lock-held-by-a", () => Locks.Holder("outing.commissary") > 0, () => "lock outing.commissary holder " + Locks.Holder("outing.commissary") + ", last refusal '" + Economy.LastRefusal + "'");
		WaitFor("clients-done", 600f, () => EconomyLab.Marks.Contains("a-done") && EconomyLab.Marks.Contains("b-done"), () => "marks " + string.Join(",", EconomyLab.Marks));
		Do("state", () => Economy.Describe() + "; " + BotProbes.Read("econ.counts") + "; day log " + DayLog.Count);
		WaitFor("run-over", 120f, () => DayNightRun.RunOver, () => "run " + BotProbes.Read("econ.runover"));
		Do("pick", delegate
		{
			runOverId = EconomyLab.FreshUpgrade() ?? "DOSSIER_UPGRADE";
			return "would buy " + runOverId;
		});
		Check("run-over-buy", () => Economy.ServerBuy(runOverId, 0) == "The run has ended." && !EconomyLab.Up(runOverId).IsUnlocked(), () => "refusal '" + Economy.LastRefusal + "'");
		Check("run-over-service", () => Economy.ServerToggle("JANITORIAL_SERVICE", on: false, 0) == "The run has ended.", () => "refusal '" + Economy.LastRefusal + "'");
		Check("run-over-commissary", () => Economy.ServerCommissary("buy", "nutrifruit", 0) == "The run has ended.", () => "refusal '" + Economy.LastRefusal + "'");
		Check("run-over-request", () => Economy.ServerFill(0, "x", 0) == "The run has ended.", () => "refusal '" + Economy.LastRefusal + "'");
	}
}
