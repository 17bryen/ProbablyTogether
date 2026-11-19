using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

/// The shop's standing with every faction, exact; its permits; whether it holds the Commissary's guides.
public sealed class RepProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("rep.all", () => RepBot.All());
	}
}

public sealed class RepBot : DayNightBotBase
{
	private string start = "";

	private bool startOnly;

	public override string Name => "rep-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 400f;

	internal static string All()
	{
		if (!PlayerStore.HasInstance || PlayerStore.Instance.storeReputations == null)
		{
			return "";
		}
		return string.Join(" ", from r in PlayerStore.Instance.storeReputations
			where r != null
			select r.factionId.Replace("FACTION_", "") + "=" + Math.Round(r.GetReputationExact(), 4).ToString("R", CultureInfo.InvariantCulture));
	}

	private static double Rep(string faction)
	{
		StoreReputation storeReputation = PlayerStore.Instance.storeReputations.FirstOrDefault((StoreReputation r) => r != null && r.factionId == faction);
		return (storeReputation == null) ? double.NaN : storeReputation.GetReputationExact();
	}

	/// What the game's own start gives a new shop of this trade with the founder's perks.
	private static Dictionary<string, double> Expected(string scenario)
	{
		double gain = Difficulty.Rules.reputationGainMultiplier;
		double loss = Difficulty.Rules.reputationLossMultiplier;
		Dictionary<string, double> dictionary = new Dictionary<string, double>(StringComparer.Ordinal);
		switch (scenario)
		{
		case "DRUG_DEALER":
			dictionary["FACTION_SECURITY"] = -15.0 * loss;
			break;
		case "GUNSMITH":
			dictionary["FACTION_SECURITY"] = 25.0 * gain;
			break;
		}
		return dictionary;
	}

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive + ", day " + DayNightBotBase.Day);
		Check("starting-standing-is-the-games", delegate
		{
			Dictionary<string, double> dictionary = Expected(DayNightBotBase.Ps.startType.ToString());
			return dictionary.Count > 0 && dictionary.All((KeyValuePair<string, double> kv) => Math.Abs(Rep(kv.Key) - kv.Value) < 0.0001);
		}, () => DayNightBotBase.Ps.startType.ToString() + " on " + Difficulty.Current.id + ": expected " + string.Join(", ", Expected(DayNightBotBase.Ps.startType.ToString()).Select((KeyValuePair<string, double> kv) => kv.Key + " " + kv.Value.ToString("R", CultureInfo.InvariantCulture))) + "; the shop has " + All());
		Check("nothing-of-the-start-blocked", () => Economy.Blocked == 0, () => "blocked " + Economy.Blocked + " (last: " + Economy.LastBlocked + ")");
		Do("start", delegate
		{
			start = All();
			startOnly = DayNightBotBase.Ps.startType.ToString() == "DRUG_DEALER";
			return start + (startOnly ? " (a Chemist's shop: the start only)" : "");
		});
		WaitFor("b-here", 240f, () => startOnly || ProbablyTogether.Server.PlayerCount >= 2, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		WaitFor("seat-2-is-the-chemist", 240f, () => startOnly || Specs.Of(2) == "DRUG_DEALER", () => "seat 2: " + (Specs.Of(2) ?? "none"));
		Delay("kit-settles", 3f);
		Check("a-trade-pick-leaves-the-standing", () => All() == start, () => "now " + All() + "; at the start " + start);
		Delay("b-finishes", 10f);
	}
}

public sealed class RepBotB : DayNightBotBase
{
	public override string Name => "rep-hb";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 300f;

	protected override void Build()
	{
		JoinSteps();
		WaitFor("seat-1s-trade", 120f, () => Specs.Of(1) != null && Specs.Of(1) != "DRUG_DEALER", () => "seat 1: " + (Specs.Of(1) ?? "none"));
		Agree("same-standing-before", 30f, "rep.all");
		Do("pick", delegate
		{
			ProbablyTogether.UI.Chat.Send("/trade chemist");
			return "typed '/trade chemist' in chat";
		});
		WaitFor("i-am-the-chemist", 30f, () => Specs.Of(2) == "DRUG_DEALER", () => "mine: " + (Specs.Of(2) ?? "none") + "; refusal '" + Specs.LastRefusal + "'");
		Delay("kit-settles", 3f);
		Agree("same-standing-after", 30f, "rep.all");
	}
}
