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
		BotProbes.Add("rep.permits", () => RepBot.Permits());
		BotProbes.Add("rep.guides", () => RepBot.Guides());
	}
}

/// What a new shop starts with, and what a trade picked later does to it.
public class RepBot : DayNightBotBase
{
	private readonly string name;

	private readonly bool withB;

	private string start = "";

	public override string Name => name;

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 420f;

	public RepBot()
		: this("rep-h", withB: true)
	{
	}

	protected RepBot(string name, bool withB)
	{
		this.name = name;
		this.withB = withB;
	}

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

	internal static string Permits()
	{
		if (!PlayerStore.HasInstance || PlayerStore.Instance.ActivePermits == null)
		{
			return "";
		}
		string text = string.Join(",", from p in PlayerStore.Instance.ActivePermits
			orderby p.Item1, p.Item2
			select p.Item1 + p.Item2);
		return (text.Length > 0) ? text : "none";
	}

	internal static string Guides()
	{
		if (!PlayerStore.HasInstance || EmporiumEntry.Instance == null)
		{
			return "";
		}
		HashSet<string> hashSet = new HashSet<string>(EmporiumEntry.Instance.GetAllOwnedItems(false, true).Select((GameItem i) => i.identifier));
		return "stamp_guide=" + hashSet.Contains("stamp_guide") + " logo_checker=" + hashSet.Contains("logo_checker");
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
		Dictionary<string, double> d = new Dictionary<string, double>(StringComparer.Ordinal) { ["FACTION_SECURITY"] = 0.0 };
		switch (scenario)
		{
		case "DRUG_DEALER":
			d["FACTION_SECURITY"] += -15.0 * loss;
			break;
		case "GUNSMITH":
			d["FACTION_SECURITY"] += 25.0 * gain;
			break;
		}
		if (Perks.Has(1, "model_citizen"))
		{
			d["FACTION_SECURITY"] += 30.0;
		}
		if (Perks.Has(1, "convict"))
		{
			d["FACTION_SECURITY"] -= 30.0;
		}
		return d;
	}

	private static string Show(Dictionary<string, double> d)
	{
		return string.Join(", ", d.Select((KeyValuePair<string, double> kv) => kv.Key + " " + kv.Value.ToString("R", CultureInfo.InvariantCulture)));
	}

	private static bool HasPermit(string kind, int level)
	{
		return PlayerStore.Instance.ActivePermits != null && PlayerStore.Instance.ActivePermits.Any(((string, int) p) => p.Item1 == kind && p.Item2 == level);
	}

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive + ", day " + DayNightBotBase.Day);
		WaitFor("founder-set-up", 60f, () => Specs.Of(1) != null, () => "seat 1's trade: " + (Specs.Of(1) ?? "none"));
		Check("starting-standing-is-the-games", delegate
		{
			Dictionary<string, double> d = Expected(DayNightBotBase.Ps.startType.ToString());
			return d.All((KeyValuePair<string, double> kv) => Math.Abs(Rep(kv.Key) - kv.Value) < 0.0001);
		}, () => DayNightBotBase.Ps.startType.ToString() + " on " + Difficulty.Current.id + ", founder's perks [" + string.Join(",", Perks.Of(1).Ids) + "]: expected " + Show(Expected(DayNightBotBase.Ps.startType.ToString())) + "; the shop has " + All());
		Check("nothing-of-the-start-blocked", () => Economy.Blocked == 0, () => "blocked " + Economy.Blocked + " (last: " + Economy.LastBlocked + ")");
		Check("a-gunsmith-shop-has-its-permit", () => DayNightBotBase.Ps.startType.ToString() != "GUNSMITH" || (HasPermit("FIREARM", 1) && NetworkUpgrade.IsGunPermitIUnlocked()), () => "permits [" + Permits() + "], GUN_PERMIT_I unlocked " + NetworkUpgrade.IsGunPermitIUnlocked());
		Do("start", delegate
		{
			start = All();
			return start + "; permits [" + Permits() + "]; " + Guides() + "; skip intro " + DayNightBotBase.Ps.skipIntro;
		});
		if (!withB)
		{
			return;
		}
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		WaitFor("seat-2-picked-a-trade", 240f, () => Specs.Of(2) != null, () => "seat 2: none");
		Delay("kit-settles", 3f);
		Check("a-trade-pick-leaves-the-standing", () => All() == start, () => "seat 2 is the " + Specs.Of(2) + "; now " + All() + "; at the start " + start);
		Check("a-gunsmith-joiner-brings-the-permit", () => Specs.Of(2) != "GUNSMITH" || HasPermit("FIREARM", 1), () => "seat 2 is the " + Specs.Of(2) + "; permits [" + Permits() + "]");
		WaitFor("seat-2-picked-perks", 120f, () => Perks.Picked(2), () => "seat 2 has not picked");
		Delay("pick-settles", 3f);
		Check("guides-while-not-everybody-is-banned", delegate
		{
			bool flag = !DayNightBotBase.Ps.skipIntro || DayNightBotBase.Ps.IsRockBottomStart() || Members.State.list.All((Member m) => Perks.Has(m.s, "banned_commissary"));
			return flag || Guides() == "stamp_guide=True logo_checker=True";
		}, () => "skip intro " + DayNightBotBase.Ps.skipIntro + ", banned: " + string.Join(",", Members.State.list.Where((Member m) => Perks.Has(m.s, "banned_commissary")).Select((Member m) => m.s)) + "; " + Guides() + " (given " + Perks.GuidesGiven + ")");
		Delay("b-finishes", 12f);
	}
}

public sealed class RepBotStartOnly : RepBot
{
	public RepBotStartOnly()
		: base("rep-h1", withB: false)
	{
	}
}

public sealed class RepBotFlow : RepBot
{
	public RepBotFlow()
		: base("rep-hf", withB: true)
	{
	}
}

/// Player 2: picks a trade the founder does not have, and no perks; agrees with the host before and after.
public class RepBotB : DayNightBotBase
{
	private readonly string name;

	private readonly string trade;

	private readonly string word;

	public override string Name => name;

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 300f;

	public RepBotB()
		: this("rep-hb", "DRUG_DEALER", "chemist")
	{
	}

	protected RepBotB(string name, string trade, string word)
	{
		this.name = name;
		this.trade = trade;
		this.word = word;
	}

	protected override void Build()
	{
		JoinSteps();
		WaitFor("seat-1s-trade", 120f, () => Specs.Of(1) != null && Specs.Of(1) != trade, () => "seat 1: " + (Specs.Of(1) ?? "none"));
		Agree("same-before", 30f, "rep.all", "rep.permits", "rep.guides");
		Do("pick-trade", delegate
		{
			ProbablyTogether.UI.Chat.Send("/trade " + word);
			return "typed '/trade " + word + "' in chat";
		});
		WaitFor("my-trade", 30f, () => Specs.Of(2) == trade, () => "mine: " + (Specs.Of(2) ?? "none") + "; refusal '" + Specs.LastRefusal + "'");
		Delay("kit-settles", 3f);
		Agree("same-after-my-trade", 30f, "rep.all", "rep.permits");
		Do("pick-no-perks", delegate
		{
			Perks.Pick(new string[0]);
			return "picked none";
		});
		WaitFor("picked", 30f, () => Perks.Picked(2), () => "not picked; refusal '" + Perks.LastRefusal + "'");
		Delay("pick-settles", 3f);
		Agree("same-after-my-perks", 30f, "rep.all", "rep.permits", "rep.guides");
	}
}

public sealed class RepBotBGun : RepBotB
{
	public RepBotBGun()
		: base("rep-hb-gun", "GUNSMITH", "gunsmith")
	{
	}
}
