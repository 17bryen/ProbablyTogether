using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProbablyTogether.Systems;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

/// Every perk in a shop of two: both players pick through Perks.LabPick, then each copy checks the perk's effects.
public abstract class PerkSuiteBase : DayNightBotBase
{
	protected const string Sec = "FACTION_SECURITY";

	protected const string Ul = "FACTION_UPPER_LEVEL";

	protected const string Rev = "FACTION_REVOLUTION";

	protected const string Bm = "FACTION_BLACK_MARKET";

	protected const string Ll = "FACTION_LOWER_LEVEL";

	protected static readonly string[] Factions = new string[5] { Sec, Ul, Rev, Bm, Ll };

	protected int cash0;

	protected int rent0;

	protected int luck0;

	protected int cust0;

	protected int wild0;

	protected int day0;

	protected readonly Dictionary<string, double> rep0 = new Dictionary<string, double>();

	protected static int Me => Members.LocalSeat;

	protected static int Other => Net.Players.Keys.FirstOrDefault((int p) => p != Net.LocalPlayer);

	protected static int OtherSeat => Members.SeatOf(Other);

	protected static PlayerStore S => DayNightBotBase.Ps;

	protected static HealthData H => DayNightBotBase.Ps.healthData;

	protected static int Customers => S.storeClientManager.GetNormalCustomerCount();

	protected static int WildCost => NetworkUpgrade.GetUpgradeById("EVIDENCE_REMOVAL")?.GetCost() ?? (-1);

	protected static int Width => EmporiumEntry.Instance.invElement.inventoryShape.width;

	protected static double Rep(string f)
	{
		return Standing.RepNow(f);
	}

	protected static int Count(params string[] ids)
	{
		return Items.All().Count((GameItem i) => i != null && ids.Contains(i.identifier) && GeneralHelper.IsItemOwned(i));
	}

	protected static List<string> InBay(int seat)
	{
		GameItem val = Bays.BayOf(seat);
		return (val == null) ? new List<string>() : (from i in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null)
			select i.identifier).ToList();
	}

	protected static int Mod(ItemFeature f)
	{
		return (f == null) ? 0 : ((int)AccessTools.Field(typeof(ItemFeature), "preExposeValueModifier").GetValue(f));
	}

	protected static bool Near(double a, double b)
	{
		return Math.Abs(a - b) < 0.01;
	}

	protected static bool Active(params string[] ids)
	{
		return ids.All((string i) => StartingPerk.IsPerkActive(i));
	}

	protected static bool Listed(string id)
	{
		return S.startingPerks != null && S.startingPerks.Count((StartingPerk p) => p != null && p.id == id) == 1;
	}

	protected static bool HasAll(int seat, string[] ids)
	{
		return seat > 0 && ids.All((string i) => Perks.Has(seat, i));
	}

	protected static string State => "record {" + Perks.Describe() + "}; shop's list [" + string.Join(",", (S.startingPerks ?? new List<StartingPerk>()).Select((StartingPerk p) => p.id)) + "]; cash " + S.playerCash + ", rent " + S.rentValue + ", luck " + H.luck + ", customers " + Customers + ", penalty " + H.GetTotalAddictionPenalty() + ", width " + Width + "; standing " + string.Join(" ", Factions.Select((string f) => f.Replace("FACTION_", "") + "=" + Rep(f).ToString("0.#"))) + "; day " + DayNightBotBase.Day + " " + DayNightBotBase.State;

	protected static void Door()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		GameItem selectedBackpack = ((IEnumerable<GameItem>)instance.GetAllOwnedItemsByTag("BACKPACK_TAG")).FirstOrDefault((Func<GameItem, bool>)delegate(GameItem b)
		{
			GameItem bay;
			int num = Bays.OwnerAround((GraphNodeStorage)(object)b.parentInventory, out bay);
			return num == 0 || num == Members.LocalSeat;
		});
		instance.selectedBackpack = selectedBackpack;
		MapUIManager.Instance.OpenUI();
	}

	protected void Before()
	{
		Do("before", delegate
		{
			cash0 = S.playerCash;
			rent0 = S.rentValue;
			luck0 = H.luck;
			cust0 = Customers;
			wild0 = WildCost;
			day0 = DayNightBotBase.Day;
			foreach (string faction in Factions)
			{
				rep0[faction] = Rep(faction);
			}
			return "cash " + cash0 + ", rent " + rent0 + ", luck " + luck0 + ", customers " + cust0 + ", an upgrade at Joe Wild's " + wild0 + "; " + State;
		});
	}

	/// Founder: waits for the other player, takes the baseline, picks, waits for both picks.
	protected void HostStart(string[] mine, string[] theirs)
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && DayNightBotBase.Live && Me == 1 && Bays.BayOf(1) != null, () => "role " + Net.Role + ", live " + Net.IsLive);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2 && OtherSeat > 1 && Bays.BayOf(OtherSeat) != null, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		Before();
		Delay("the-other-player-takes-their-baseline", 12f);
		Do("pick", delegate
		{
			Perks.LabPick(mine);
			return string.Join(",", mine);
		});
		WaitFor("i-have-mine", 30f, () => HasAll(1, mine), () => State);
		WaitFor("they-have-theirs", 120f, () => HasAll(OtherSeat, theirs), () => State);
		Delay("settle", 2f);
	}

	protected void ClientStart(string[] mine, string[] theirs, Action beforeMyPick = null)
	{
		JoinSteps();
		WaitFor("my-bay", 60f, () => Me > 1 && Bays.BayOf(Me) != null, () => State);
		Before();
		WaitFor("the-founder-has-theirs", 120f, () => HasAll(1, theirs), () => State);
		beforeMyPick?.Invoke();
		Do("pick", delegate
		{
			Perks.LabPick(mine);
			return string.Join(",", mine);
		});
		WaitFor("i-have-mine", 30f, () => HasAll(Me, mine), () => State);
		Delay("settle", 2f);
	}

	protected void End(float wait = 22f)
	{
		Delay("comparisons-with-the-server", wait);
		Agree("same-shop", 60f, "core.digest", "items.digest", "econ.hash");
		Check("no-reload", () => Digest.ReloadsAsked == 0 && DayNight.Mismatches == 0, () => "reloads asked " + Digest.ReloadsAsked + "; day/night mismatches " + DayNight.Mismatches + "; " + State);
	}

	protected void OpenAndClose()
	{
		Do("open-click", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened", 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		WaitFor("shutter-settled", 20f, () => S.lockShutterButtonFlag == 0, () => "flag " + S.lockShutterButtonFlag);
	}

	protected void Close()
	{
		Do("queue", delegate
		{
			if (S.CanEndDay())
			{
				return "every waiting customer can be sent away";
			}
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "clearqueue"
			});
			return "lab op clearqueue";
		});
		WaitFor("closable", 15f, () => S.CanEndDay(), () => "queue " + S.storeClientManager.clientStack.Count);
		Do("close", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "sent";
		});
		WaitFor("closed", 20f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
	}
}

public abstract class PerkStackBase : PerkSuiteBase
{
	protected static readonly string[] Founder = new string[8] { "saving", "bad_lease", "prime_real_estate", "awful_negotiator", "lucky", "handy", "nicotine_addiction", "alcohol_addiction" };

	protected static readonly string[] Friend = new string[8] { "saving", "bad_lease", "prime_real_estate", "awful_negotiator", "lucky", "handy", "nicotine_addiction", "narcotic_addiction" };

	protected void Shared()
	{
		Check("savings-twice-is-300", () => S.playerCash == cash0 + 300, () => "cash " + cash0 + " -> " + S.playerCash);
		Check("bad-lease-twice-and-prime-real-estate-twice-is-230-more-rent", () => S.rentValue == rent0 + 230, () => "rent " + rent0 + " -> " + S.rentValue);
		Check("prime-real-estate-twice-is-two-more-customers", () => Customers == Mathf.Clamp(Mathf.Clamp(cust0 + 1, 0, 6) + 1, 0, 8), () => "customers " + cust0 + " -> " + Customers);
		Check("lucky-twice-is-30", () => H.luck == luck0 + 30, () => "luck " + luck0 + " -> " + H.luck);
		Check("handy-twice-is-two-toolboxes", () => Count("toolbox") == 2, () => "toolboxes " + Count("toolbox"));
		Check("awful-negotiator-twice-is-ten-in-a-hundred-and-nobody-bargains", () => StartingPerk.IsAwfulNegotiator() && Mod(ItemFeatureList.AwfulNegotiatorBuyPenalty()) == 10 && Mod(ItemFeatureList.AwfulNegotiatorSellPenalty()) == -10, () => "buy " + Mod(ItemFeatureList.AwfulNegotiatorBuyPenalty()) + ", sell " + Mod(ItemFeatureList.AwfulNegotiatorSellPenalty()));
		Check("four-withdrawals-are-32-in-a-hundred", () => H.GetTotalAddictionPenalty() == 32, () => State);
		Check("each-is-in-the-shop's-list-once", () => Founder.Concat(Friend).Distinct().All(Listed), () => State);
		Check("what-does-not-go-with-them-is-locked-for-everybody", () => Perks.LockReason(1, "unlucky") != null && Perks.LockReason(2, "unlucky") != null && Perks.LockReason(1, "side_street") != null && Perks.LockReason(2, "side_street") != null, () => "unlucky: " + Perks.LockReason(2, "unlucky") + "; side street: " + Perks.LockReason(2, "side_street"));
	}
}

public sealed class PerkStackH : PerkStackBase
{
	public override string Name => "pc1-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		HostStart(Founder, Friend);
		Shared();
		Check("i-am-the-one-on-alcohol", () => HealthData.IsNicotineAddicted() && HealthData.IsAlcoholAddicted() && !HealthData.IsNarcoticAddicted(), () => "nicotine " + HealthData.IsNicotineAddicted() + ", alcohol " + HealthData.IsAlcoholAddicted() + ", narcotic " + HealthData.IsNarcoticAddicted());
		Do("i-have-had-my-nicotine", delegate
		{
			Perks.LabClock(1, 0, 0);
			return "my nicotine clock is at 0";
		});
		WaitFor("three-withdrawals-are-24", 20f, () => H.GetTotalAddictionPenalty() == 24, () => State);
		End();
	}
}

public sealed class PerkStackB : PerkStackBase
{
	public override string Name => "pc1-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		ClientStart(Friend, Founder);
		Shared();
		Check("i-am-the-one-on-narcotics", () => HealthData.IsNicotineAddicted() && !HealthData.IsAlcoholAddicted() && HealthData.IsNarcoticAddicted(), () => "nicotine " + HealthData.IsNicotineAddicted() + ", alcohol " + HealthData.IsAlcoholAddicted() + ", narcotic " + HealthData.IsNarcoticAddicted());
		Check("the-first-doses-are-in-my-bay", () => InBay(Me).Contains("packet_red_cigarette") && InBay(Me).Contains("pink_injector"), () => "my bay [" + string.Join(",", InBay(Me)) + "]");
		WaitFor("the-founder's-dose-helps-the-shop-here-too", 60f, () => H.GetTotalAddictionPenalty() == 24, () => State);
		End();
	}
}

public abstract class PerkStackNegBase : PerkSuiteBase
{
	protected static readonly string[] Both = new string[2] { "side_street", "unlucky" };

	protected void Shared()
	{
		Check("side-street-twice-is-two-customers-fewer", () => Customers == Mathf.Clamp(Mathf.Clamp(cust0 - 1, 0, 6) - 1, 0, 8), () => "customers " + cust0 + " -> " + Customers);
		Check("unlucky-twice-is-minus-30", () => H.luck == luck0 - 30, () => "luck " + luck0 + " -> " + H.luck);
		Check("each-is-in-the-shop's-list-once", () => Both.All(Listed), () => State);
		Check("their-opposites-are-locked-for-everybody", () => Perks.LockReason(1, "prime_real_estate") != null && Perks.LockReason(2, "prime_real_estate") != null && Perks.LockReason(1, "lucky") != null && Perks.LockReason(2, "lucky") != null, () => "prime real estate: " + Perks.LockReason(2, "prime_real_estate") + "; lucky: " + Perks.LockReason(2, "lucky"));
	}
}

public sealed class PerkStackNegH : PerkStackNegBase
{
	public override string Name => "pc2-h";

	public override BotRole Role => BotRole.Any;

	protected override void Build()
	{
		HostStart(Both, Both);
		Shared();
		End();
	}
}

public sealed class PerkStackNegB : PerkStackNegBase
{
	public override string Name => "pc2-b";

	public override BotRole Role => BotRole.Client;

	protected override void Build()
	{
		ClientStart(Both, Both);
		Shared();
		End();
	}
}

public abstract class PerkUniqueABase : PerkSuiteBase
{
	protected static readonly string[] Founder = new string[13]
	{
		"convict", "well_connected", "sycophant", "alert", "sawyer_crew", "diplomatic", "intimidating", "minimalist", "makeshift_reactor", "tapehead",
		"wild_favorite", "vending", "working_pipes"
	};

	protected static readonly string[] None = new string[0];

	protected static int Cassettes => GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)EmporiumEntry.Instance.cassettePlayer, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).Count((GameItem i) => i != null && new string[7] { "cassette1", "cassette2", "cassette3", "cassette4", "cassette5", "cassette6", "cassette7" }.Contains(i.identifier));

	protected void Shared()
	{
		Check("all-thirteen-act-on-this-game-and-are-in-the-shop's-list", () => Active(Founder) && Founder.All(Listed), () => "not active here: " + string.Join(",", Founder.Where((string i) => !StartingPerk.IsPerkActive(i))) + "; " + State);
		Check("convict-and-well-connected-security-minus-15", () => Near(Rep(Sec), rep0[Sec] - 30.0 + 15.0), () => "security " + rep0[Sec] + " -> " + Rep(Sec));
		Check("sycophant-and-well-connected-upper-levels-plus-30", () => Near(Rep(Ul), rep0[Ul] + 30.0), () => "upper levels " + rep0[Ul] + " -> " + Rep(Ul));
		Check("well-connected-the-rest-plus-15", () => Near(Rep(Rev), rep0[Rev] + 15.0) && Near(Rep(Bm), rep0[Bm] + 15.0) && Near(Rep(Ll), rep0[Ll] + 15.0), () => State);
		Check("that-standing-is-in-the-founder's-row", () => Near(Standing.RepOf(1, Sec), -15.0) && Near(Standing.RepOf(1, Ul), 30.0) && Near(Standing.RepOf(1, Rev), 15.0) && Near(Standing.RepOf(2, Sec), 0.0), () => "founder's row: security " + Standing.RepOf(1, Sec) + ", upper levels " + Standing.RepOf(1, Ul) + ", revolution " + Standing.RepOf(1, Rev) + "; the other's security " + Standing.RepOf(2, Sec));
		Check("convict-a-report-pays-three-quarters", () => StartingPerkContent.HandleReportRewardConvict(100) == 75, () => "100 -> " + StartingPerkContent.HandleReportRewardConvict(100));
		Check("minimalist-the-shop's-storage-is-20-wide", () => Width == 20, () => "width " + Width);
		Check("the-makeshift-reactor-is-in-the-shop", () => Count("makeshift_reactor") == 1, () => "reactors " + Count("makeshift_reactor"));
		Check("tapehead-all-seven-cassettes", () => Cassettes >= 7, () => "cassettes 1 to 7 in the player: " + Cassettes);
		Check("wild's-favorite-his-upgrades-cost-a-tenth-less", () => wild0 > 0 && WildCost < wild0 && Math.Abs((double)WildCost - (double)wild0 * 0.9) <= 1.0, () => "evidence removal " + wild0 + " -> " + WildCost);
		Check("the-vending-machine-is-unlocked", () => S.isVendingMachineUnlocked, () => "unlocked " + S.isVendingMachineUnlocked);
		Check("working-pipes-both-taps", () => NetworkUpgrade.GetUpgradeById("GUTTERFLOW_TAP").state == 2 && NetworkUpgrade.GetUpgradeById("RUSTWATER_TAP").state == 2, () => "gutterflow " + NetworkUpgrade.GetUpgradeById("GUTTERFLOW_TAP").state + ", rustwater " + NetworkUpgrade.GetUpgradeById("RUSTWATER_TAP").state);
		Check("each-is-locked-for-the-other-player-with-the-founder's-name", () => Founder.All((string i) => (Perks.LockReason(2, i) ?? "").Contains(Members.NameOf(1))), () => "not locked for seat 2: " + string.Join(",", Founder.Where((string i) => !(Perks.LockReason(2, i) ?? "").Contains(Members.NameOf(1)))));
		Check("so-is-what-does-not-go-with-them", () => new string[4] { "model_citizen", "public_enemy", "ascetic", "wild_bad_side" }.All((string i) => Perks.LockReason(2, i) != null), () => string.Join("; ", new string[4] { "model_citizen", "public_enemy", "ascetic", "wild_bad_side" }.Select((string i) => i + ": " + (Perks.LockReason(2, i) ?? "FREE"))));
	}
}

public sealed class PerkUniqueAH : PerkUniqueABase
{
	public override string Name => "pc3-h";

	public override BotRole Role => BotRole.Any;

	protected override void Build()
	{
		HostStart(Founder, None);
		Shared();
		End(40f);
	}
}

public sealed class PerkUniqueAB : PerkUniqueABase
{
	public override string Name => "pc3-b";

	public override BotRole Role => BotRole.Client;

	protected override void Build()
	{
		ClientStart(None, Founder, delegate
		{
			Delay("the-founder's-pick-has-settled", 2f);
			Do("i-ask-for-vigilant-all-the-same", delegate
			{
				Perks.Pick(new string[1] { "alert" });
				return "asked";
			});
			WaitFor("the-shop-says-who-has-it", 20f, () => Perks.LastRefusal.Contains(Members.NameOf(1)) && !Perks.Has(Me, "alert") && !Perks.Picked(Me), () => "last refusal '" + Perks.LastRefusal + "'; " + State);
		});
		Shared();
		End();
	}
}

public abstract class PerkUniqueBBase : PerkSuiteBase
{
	protected static readonly string[] Founder = new string[10] { "model_citizen", "public_enemy", "blackmail", "blabbermouth", "ascetic", "hoarder", "revolution_sympathizer", "wild_bad_side", "aug_menace", "known_snitch" };

	protected static readonly string[] None = new string[0];

	protected void Shared()
	{
		Check("all-ten-act-on-this-game-and-are-in-the-shop's-list", () => Active(Founder) && Founder.All(Listed), () => "not active here: " + string.Join(",", Founder.Where((string i) => !StartingPerk.IsPerkActive(i))) + "; " + State);
		Check("model-citizen-and-public-enemy-security-plus-15", () => Near(Rep(Sec), rep0[Sec] + 30.0 - 15.0), () => "security " + rep0[Sec] + " -> " + Rep(Sec));
		Check("public-enemy-sympathizer-and-snitch-revolution-level", () => Near(Rep(Rev), rep0[Rev] - 15.0 + 25.0 - 10.0), () => "revolution " + rep0[Rev] + " -> " + Rep(Rev));
		Check("public-enemy-and-snitch-black-market-minus-25", () => Near(Rep(Bm), rep0[Bm] - 25.0), () => "black market " + rep0[Bm] + " -> " + Rep(Bm));
		Check("public-enemy-the-rest-minus-15", () => Near(Rep(Ul), rep0[Ul] - 15.0) && Near(Rep(Ll), rep0[Ll] - 15.0), () => State);
		Check("that-standing-is-in-the-founder's-row", () => Near(Standing.RepOf(1, Sec), 15.0) && Near(Standing.RepOf(1, Bm), -25.0) && Near(Standing.RepOf(1, Rev), 0.0) && Near(Standing.RepOf(2, Bm), 0.0), () => "founder's row: security " + Standing.RepOf(1, Sec) + ", black market " + Standing.RepOf(1, Bm) + ", revolution " + Standing.RepOf(1, Rev));
		Check("model-citizen-a-report-pays-half-again", () => StartingPerkContent.HandleReportRewardModelCitizen(100) == 150, () => "100 -> " + StartingPerkContent.HandleReportRewardModelCitizen(100));
		Check("ascetic-the-shop's-storage-is-16-wide", () => Width == 16, () => "width " + Width);
		Check("hoarder-three-piles-of-debris", () => Items.All().Count((GameItem i) => i != null && (i.identifier ?? "").StartsWith("debris_pile", StringComparison.Ordinal)) == 3, () => "debris piles " + Items.All().Count((GameItem i) => i != null && (i.identifier ?? "").StartsWith("debris_pile", StringComparison.Ordinal)));
		Check("on-wild's-bad-side-his-upgrades-cost-a-tenth-more", () => wild0 > 0 && WildCost > wild0 && Math.Abs((double)WildCost - (double)wild0 * 1.1) <= 1.0, () => "evidence removal " + wild0 + " -> " + WildCost);
		Check("aug-menace-the-augs-are-here-from-day-one", () => S.isAugIntroduced && PlayerStore.GetAugIntroducedDay() == 1, () => "introduced " + S.isAugIntroduced + ", day " + PlayerStore.GetAugIntroducedDay());
		Check("each-is-locked-for-the-other-player-with-the-founder's-name", () => Founder.All((string i) => (Perks.LockReason(2, i) ?? "").Contains(Members.NameOf(1))), () => "not locked for seat 2: " + string.Join(",", Founder.Where((string i) => !(Perks.LockReason(2, i) ?? "").Contains(Members.NameOf(1)))));
		Check("so-is-what-does-not-go-with-them", () => new string[4] { "convict", "well_connected", "minimalist", "wild_favorite" }.All((string i) => Perks.LockReason(2, i) != null), () => string.Join("; ", new string[4] { "convict", "well_connected", "minimalist", "wild_favorite" }.Select((string i) => i + ": " + (Perks.LockReason(2, i) ?? "FREE"))));
	}

	protected void Blackmailer()
	{
		Check("blackmail-the-blackmailer-is-in-today's-queue", () => S.storeClientManager.clientStack.Any((StoreClient c) => c != null && (c.identifier ?? "").IndexOf("blackmail", StringComparison.OrdinalIgnoreCase) >= 0) || (S.currentClientInstance?.GetClientBlueprint()?.identifier ?? "").IndexOf("blackmail", StringComparison.OrdinalIgnoreCase) >= 0, () => "queue [" + string.Join(",", S.storeClientManager.clientStack.Select((StoreClient c) => c?.identifier)) + "], at the counter " + (S.currentClientInstance?.GetClientBlueprint()?.identifier ?? "-"));
		Check("known-snitch-nobody-from-the-wanted-poster-is", () => !S.storeClientManager.clientStack.Any((StoreClient c) => c != null && StoreClientManager.IsClientBlockedByKnownSnitch(c)) && S.storeClientManager.GetRandomValidWantedClient() == null, () => "queue [" + string.Join(",", S.storeClientManager.clientStack.Select((StoreClient c) => c?.identifier + (c != null && c.isWanted ? "(wanted)" : ""))) + "]");
	}
}

public sealed class PerkUniqueBH : PerkUniqueBBase
{
	public override string Name => "pc4-h";

	public override BotRole Role => BotRole.Any;

	protected override void Build()
	{
		HostStart(Founder, None);
		Shared();
		OpenAndClose();
		Blackmailer();
		End(30f);
	}
}

public sealed class PerkUniqueBB : PerkUniqueBBase
{
	public override string Name => "pc4-b";

	public override BotRole Role => BotRole.Client;

	protected override void Build()
	{
		ClientStart(None, Founder);
		Shared();
		WaitFor("opened-by-the-founder", 120f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		Delay("the-queue-arrives", 2f);
		Blackmailer();
		End();
	}
}

public abstract class PerkPersonalBase : PerkSuiteBase
{
	protected static readonly string[] Founder = new string[2] { "careful_scavenger", "banned_commissary" };

	protected static readonly string[] Friend = new string[10] { "illiterate", "tinkerer", "proficient_scavenger", "short_sleeper", "agoraphobia", "welfare_program", "gambling_addiction", "secret_admirer", "colorful_character", "degenerative_osteoarthritis" };

	protected static int Stamps => Items.All().Count((GameItem i) => i != null && i.identifier == "food_stamp");

	protected void Shared()
	{
		Check("none-of-the-personal-ones-is-in-the-shop's-list", () => !Founder.Concat(Friend).Any((string i) => S.startingPerks.Any((StartingPerk p) => p.id == i)), () => State);
		Check("the-commissary-is-open-to-the-one-on-welfare-and-closed-to-the-banned-one", () => Perks.CommissaryOpenFor(2) && !Perks.CommissaryOpenFor(1), () => "seat 1 " + Perks.CommissaryOpenFor(1) + ", seat 2 " + Perks.CommissaryOpenFor(2) + "; the shop knows stamps " + S.isStampIntroduced);
		Check("the-lottery-is-unlocked-for-the-shop", () => LotteryHelper.IsLotteryUnlocked(), () => "unlocked here " + LotteryHelper.IsLotteryUnlocked());
		Check("colorful-character-is-three-more-slots", () => Perks.Slots(new string[1] { "colorful_character" }) == 7 && Perks.Slots(new string[1] { "saving" }) == 4, () => "slots with it " + Perks.Slots(new string[1] { "colorful_character" }));
	}
}

public sealed class PerkPersonalH : PerkPersonalBase
{
	private int before;

	public override string Name => "pc5-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		HostStart(Founder, Friend);
		Shared();
		Check("i-can-read-and-do-not-tinker", () => HealthData.CanRead() && !StartingPerk.IsIlliterate() && !StartingPerk.IsTinkerer() && !StartingPerk.IsShortSleeper() && !StartingPerk.IsAgoraphobia(), () => "can read " + HealthData.CanRead() + ", tinkerer " + StartingPerk.IsTinkerer());
		Check("careful-scavenger-three-searches-and-no-wounds-for-me", () => ScavHelper.GetMaxScavAttempts() == 3 && ScavHelper.GetMinorWoundChance() == 0f && StartingPerk.IsCarefulScavenger() && !StartingPerk.IsProficientScavenger(), () => "my searches " + ScavHelper.GetMaxScavAttempts() + ", minor wound chance " + ScavHelper.GetMinorWoundChance());
		Check("i-am-the-banned-one", () => StartingPerk.IsBannedCommissary() && !StartingPerk.IsWelfareProgram(), () => "banned here " + StartingPerk.IsBannedCommissary());
		OpenAndClose();
		Check("welfare-one-holder-one-food-stamp-on-monday", () => Stamps == 1, () => "food stamps in the shop " + Stamps);
		Close();
		WaitFor("they-went-to-the-dump-and-came-home", 240f, () => Perks.LastOuting(OtherSeat) == day0 && !Outings.AnyOut, () => "their last outing " + Perks.LastOuting(OtherSeat) + "; anybody out " + Outings.AnyOut);
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Check("agoraphobia-is-theirs-i-could-go-out", () => !StartingPerkContent.IsAgoraphobiaResting(), () => "resting here " + StartingPerkContent.IsAgoraphobiaResting());
		Do("short-sleeper-the-shop-misses-nobody-for-their-trip", delegate
		{
			before = Customers;
			return "customers this morning " + before + " (they went out last night: " + H.wentToAfterhoursLastNight + ")";
		});
		Check("somebody-did-go-out", () => H.wentToAfterhoursLastNight, () => "went out last night " + H.wentToAfterhoursLastNight);
		Do("had-i-gone-out-too", delegate
		{
			Perks.LabClock(1, 6, day0);
			return "my last outing is set to day " + day0;
		});
		WaitFor("the-shop-would-miss-one-customer-for-mine", 20f, () => before > 0 && Customers == before - 1, () => "customers " + before + " -> " + Customers);
		Delay("the-other-player-has-the-morning", 10f);
		OpenAndClose();
		WaitFor("the-secret-admirer's-first-gift-is-in-their-bay", 30f, () => InBay(OtherSeat).Contains("galaxy_blend"), () => "their bay [" + string.Join(",", InBay(OtherSeat)) + "]; on the counter [" + string.Join(",", ((GameInventory)EmporiumEntry.Instance.frontInvinvElement).childItems.Select((GameItem i) => i.identifier)) + "]");
		End(30f);
	}
}

public sealed class PerkPersonalB : PerkPersonalBase
{
	public override string Name => "pc5-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		ClientStart(Friend, Founder);
		Shared();
		Check("i-cannot-read-and-i-tinker", () => !HealthData.CanRead() && StartingPerk.IsIlliterate() && StartingPerk.IsTinkerer() && StartingPerk.IsShortSleeper() && StartingPerk.IsAgoraphobia() && StartingPerk.IsSecretAdmirer(), () => "can read " + HealthData.CanRead() + ", tinkerer " + StartingPerk.IsTinkerer());
		Check("proficient-scavenger-seven-searches-for-me", () => ScavHelper.GetMaxScavAttempts() == 7 && StartingPerk.IsProficientScavenger() && !StartingPerk.IsCarefulScavenger(), () => "my searches " + ScavHelper.GetMaxScavAttempts());
		Check("i-am-on-welfare-and-not-banned", () => StartingPerk.IsWelfareProgram() && !StartingPerk.IsBannedCommissary(), () => "welfare " + StartingPerk.IsWelfareProgram() + ", banned " + StartingPerk.IsBannedCommissary());
		Check("what-my-perks-hand-out-is-in-my-bay", () => InBay(Me).Contains("scratcher") && InBay(Me).Contains("gamblers_coin") && InBay(Me).Count((string i) => i == "topical_bandage_item") >= 2 && InBay(Me).Contains("stamp_guide"), () => "my bay [" + string.Join(",", InBay(Me)) + "]");
		WaitFor("opened-by-the-founder", 120f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		Delay("the-morning's-things-arrive", 2f);
		Check("welfare-one-holder-one-food-stamp-on-monday", () => Stamps == 1, () => "food stamps in the shop " + Stamps);
		WaitFor("closed-by-the-founder", 300f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		Delay("past-close-timers", 8f);
		Do("door", delegate
		{
			Door();
			return "asked to go out";
		});
		WaitFor("private", 20f, () => Outings.Private && TransitUI.IsOpen, () => "private " + Outings.Private + ", refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Do("travel", delegate
		{
			MapUIManager.Instance.Travel(AfterhourLocation.Get("dumping_ground"));
			return "travelling";
		});
		WaitFor("at-the-dump", 20f, () => DayNightOuting.LocationId == "dumping_ground", () => "at " + DayNightOuting.LocationId);
		Check("at-the-dump-i-have-seven-searches-left", () => ScavHelper.GetScavTimeLeft() == 7 && ScavHelper.GetMaxScavAttempts() == 7, () => "searches left " + ScavHelper.GetScavTimeLeft() + " of " + ScavHelper.GetMaxScavAttempts());
		Do("return", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "coming home";
		});
		WaitFor("home", 60f, () => !Outings.Private && DayNightBotBase.Live && DayNightOuting.LocationId == "", () => "private " + Outings.Private + ", at " + DayNightOuting.LocationId);
		WaitFor("my-outing-is-on-my-record", 20f, () => Perks.LastOuting(Me) == day0, () => "my last outing " + Perks.LastOuting(Me));
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1, () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Check("agoraphobia-i-rest-for-two-nights", () => StartingPerkContent.IsAgoraphobiaResting() && StartingPerkContent.GetAgoraphobiaReadyDay() == day0 + 3, () => "resting " + StartingPerkContent.IsAgoraphobiaResting() + ", ready on day " + StartingPerkContent.GetAgoraphobiaReadyDay());
		WaitFor("opened-by-the-founder-again", 240f, () => DayNightBotBase.State == "OPEN" && DayNightBotBase.Day == day0 + 1, () => "state " + DayNightBotBase.State);
		WaitFor("the-secret-admirer's-first-gift-is-in-my-bay", 30f, () => InBay(Me).Contains("galaxy_blend"), () => "my bay [" + string.Join(",", InBay(Me)) + "]");
		End(20f);
	}
}

public abstract class PerkDeathBase : PerkSuiteBase
{
	protected static readonly string[] Friend = new string[2] { "maimed", "aug_menace" };

	protected static readonly string[] None = new string[0];
}

public sealed class PerkDeathH : PerkDeathBase
{
	public override string Name => "pc6-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 700f;

	protected override void Build()
	{
		HostStart(None, Friend);
		Check("maimed-is-theirs-i-have-five-searches", () => ScavHelper.GetMaxScavAttempts() == 5 && !StartingPerk.IsMaimed(), () => "my searches " + ScavHelper.GetMaxScavAttempts());
		Check("the-aug-menace-is-the-shop's", () => StartingPerk.IsAugMenace() && Listed("aug_menace") && S.isAugIntroduced, () => State);
		OpenAndClose();
		Close();
		Do("too-many-augs-got-away", delegate
		{
			using (Net.Apply())
			{
				S.augPresence = 100;
			}
			return "aug presence " + S.augPresence + "; this difficulty ends a run for that by itself: " + Difficulty.Rules.augGameOverAtFullPresence;
		});
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State + "; run over " + DayNightRun.RunOver);
		Check("the-run-goes-on", () => !DayNightRun.RunOver, () => "run over " + DayNightRun.RunOver);
		WaitFor("only-the-holder-does-not-survive-and-starts-over", 60f, () => Perks.Deaths == 1 && !Perks.Picked(OtherSeat) && Members.BySeat(OtherSeat) != null && !Listed("aug_menace") && !StartingPerk.IsAugMenace(), () => "deaths " + Perks.Deaths + "; " + State);
		End(30f);
	}
}

public sealed class PerkDeathB : PerkDeathBase
{
	public override string Name => "pc6-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 700f;

	protected override void Build()
	{
		ClientStart(Friend, None);
		Check("maimed-i-cannot-search-and-the-dump-is-not-on-my-map", () => ScavHelper.GetMaxScavAttempts() == 0 && StartingPerk.IsMaimed() && !AfterhourLocation.Get("dumping_ground").IsDiscovered(), () => "my searches " + ScavHelper.GetMaxScavAttempts() + ", the dump on my map " + AfterhourLocation.Get("dumping_ground").IsDiscovered());
		Check("the-aug-menace-is-the-shop's", () => StartingPerk.IsAugMenace() && Listed("aug_menace"), () => State);
		WaitFor("closed-by-the-founder", 300f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		WaitFor("i-start-over-without-perks", 60f, () => !Perks.Picked(Me) && Members.LocalSeat > 1 && !StartingPerk.IsMaimed() && !StartingPerk.IsAugMenace() && ScavHelper.GetMaxScavAttempts() == 5, () => State);
		End(30f);
	}
}
