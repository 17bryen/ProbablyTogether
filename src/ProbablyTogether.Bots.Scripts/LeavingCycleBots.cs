using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

/// Picks each trade in turn; after each "/retrade" the shop must match its state before the pick.
public abstract class LeavingCycleBotBase : LeavingBotBase
{
	protected static readonly Dictionary<string, string[]> Entries = new Dictionary<string, string[]>
	{
		{ "WATER_MERCHANT", new string[1] { "WATER_TRADER" } },
		{ "SCAV", new string[1] { "RUINED_MACHINE_UNLOCK" } },
		{ "FARMER", new string[1] { "RETIRED_FARMER" } },
		{ "GUNSMITH", new string[2] { "RETIRED_GUNSMITH", "GUN_PERMIT_I" } },
		{ "DRUG_DEALER", new string[1] { "RETIRED_CHEMIST" } },
		{ "RANCHER", new string[1] { "RETIRED_RANCHER" } },
		{ "AQUAFARMER", new string[1] { "AQUACULTURE" } }
	};

	protected static int Permits => DayNightBotBase.Ps?.ActivePermits?.Count ?? (-1);

	protected static List<string> Free()
	{
		return (from c in Specs.Choices()
			select c.Key into k
			where k != "ROCK_BOTTOM"
			orderby k
			select k).ToList();
	}
}

public sealed class LeavingCycleH : LeavingCycleBotBase
{
	private int seat;

	private string name = "";

	private List<string> todo = new List<string>();

	private readonly List<string> done = new List<string>();

	private readonly List<string> bad = new List<string>();

	private int phase;

	private float since;

	private string cur = "";

	private string[] kinds = new string[0];

	private int baseItems;

	private int baseCash;

	private int basePermits;

	private int doneBefore;

	public override string Name => "lc-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 900f;

	private void Expect(bool ok, string what)
	{
		if (!ok)
		{
			bad.Add(cur + ": " + what);
		}
	}

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive);
		WaitFor("b-here-with-a-bay", 240f, delegate
		{
			seat = Members.State.list.Where((Member m) => m.s != 1).Select((Member m) => m.s).FirstOrDefault();
			name = Members.NameOf(seat);
			return ProbablyTogether.Server.PlayerCount >= 2 && seat > 1 && Bays.BayOf(seat) != null;
		}, () => Where(seat));
		Do("the-shop-before", delegate
		{
			day0 = DayNightBotBase.Day;
			todo = Free();
			baseItems = Items.All().Count;
			baseCash = DayNightBotBase.Ps.playerCash;
			basePermits = Permits;
			return todo.Count + " trade(s) on offer: " + string.Join(", ", todo) + "; " + baseItems + " item(s), cash " + baseCash + ", " + basePermits + " permit(s); the shop itself is " + DayNightBotBase.Ps.startType;
		});
		Step("every-trade-in-and-out", 700f, delegate(BotStep st)
		{
			st.Detail = done.Count + "/" + todo.Count + " done (" + string.Join(", ", done) + "); now " + cur + " phase " + phase + "; wrong so far: " + ((bad.Count == 0) ? "nothing" : string.Join(" | ", bad));
			if (phase == 0)
			{
				if (done.Count >= todo.Count)
				{
					return StepStatus.Pass;
				}
				string text = Specs.Of(seat);
				if (text == null || !(Members.Fact(seat, Specs.KitFact) ?? "").StartsWith(text + ":", StringComparison.Ordinal))
				{
					since = Time.realtimeSinceStartup;
					return StepStatus.Running;
				}
				if (Time.realtimeSinceStartup - since < 2.5f)
				{
					return StepStatus.Running;
				}
				cur = text;
				kinds = (Members.Fact(seat, "mach") ?? "").Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				Expect(Items.All().Count > baseItems, "the kit added nothing (" + Items.All().Count + " item(s), " + baseItems + " before)");
				if (Entries.TryGetValue(cur, out var value))
				{
					Expect(value.All((string e) => Entry(e) == 2), "Joe Wild's entries not held after the pick (" + string.Join(",", value.Select((string e) => e + "=" + Entry(e))) + ")");
				}
				Expect(kinds.All((string k) => Specs.OwnerOfType(k) == seat), "a machine kind is not theirs after the pick");
				doneBefore = Leaving.Done;
				Say("/retrade " + name);
				since = Time.realtimeSinceStartup;
				phase = 1;
				return StepStatus.Running;
			}
			if (Leaving.Done != doneBefore + 1)
			{
				if (Time.realtimeSinceStartup - since > 20f)
				{
					Expect(false, "/retrade did nothing in 20 s (last refusal '" + Leaving.LastRefusal + "')");
					done.Add(cur + "?");
					phase = 0;
				}
				return StepStatus.Running;
			}
			if (Time.realtimeSinceStartup - since < 1.5f)
			{
				return StepStatus.Running;
			}
			Expect(Items.All().Count == baseItems, Items.All().Count + " item(s) left, " + baseItems + " before the pick [" + Leaving.LastResult + "]");
			Expect(DayNightBotBase.Ps.playerCash == baseCash, "cash " + DayNightBotBase.Ps.playerCash + ", " + baseCash + " before the pick");
			if (Entries.TryGetValue(cur, out var value2))
			{
				Expect(value2.All((string e) => Entry(e) == 1), "Joe Wild's entries still held (" + string.Join(",", value2.Select((string e) => e + "=" + Entry(e))) + ")");
			}
			Expect(kinds.All((string k) => Count(k) == 0 && Specs.OwnerOfType(k) == 0), "machines left or still owned (" + string.Join(",", kinds.Select((string k) => k + "=" + Count(k))) + ")");
			Expect(Permits == basePermits, Permits + " permit(s), " + basePermits + " before");
			Expect(!KnowsCartel && !Protection, "the cartel is still in the shop (standing " + KnowsCartel + ", protection " + Protection + ")");
			Expect(Members.Fact(seat, Specs.KitFact) == null && Members.Fact(seat, "mach") == null && Members.Fact(seat, TradeVisits.DayFact) == null, "the member record still has the trade");
			Expect(Specs.TakenBy(cur) == 0 && Free().Contains(cur), "the trade is not on offer again");
			done.Add(cur);
			phase = 0;
			return StepStatus.Running;
		});
		Check("every-trade-left-the-shop-as-it-found-it", () => bad.Count == 0 && done.Count == todo.Count && todo.Count >= 7, () => done.Count + "/" + todo.Count + " (" + string.Join(", ", done) + "); wrong: " + ((bad.Count == 0) ? "nothing" : string.Join(" | ", bad)));
		WaitFor("b-is-the-gunsmith", 120f, () => Specs.Of(seat) == "GUNSMITH" && (Members.Fact(seat, Specs.KitFact) ?? "").StartsWith("GUNSMITH:", StringComparison.Ordinal), () => Where(seat));
		OpenAndClose("1", delegate
		{
			Check("the-gunsmiths-buyer-comes-for-them", () => TradeVisits.SeatFor("llGunBuyer") == seat || TradeVisits.Added >= 1, () => Where(seat) + "; visitors added for trades " + TradeVisits.Added + " (" + TradeVisits.LastAdded + "); queue " + string.Join(",", DayNightBotBase.Ps.storeClientManager.clientStack.Select((StoreClient c) => c.identifier)));
			Do("how-many-came", delegate
			{
				doneBefore = TradeVisits.Added;
				return "visitors added for trades so far: " + doneBefore + " (" + TradeVisits.LastAdded + ")";
			});
		});
		Do("retrade-at-night", delegate
		{
			Say("/retrade " + name);
			return "typed /retrade " + name + " at " + DayNightBotBase.State;
		});
		WaitFor("the-gunsmith-left", 15f, () => Specs.Of(seat) == null && Permits == basePermits, () => Where(seat) + "; permits " + Permits);
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => Where(seat));
		OpenAndClose("2", delegate
		{
			Check("nobody-comes-for-a-gunsmith-any-more", () => TradeVisits.Added == doneBefore && !InQueue("llGunSeller") && !InQueue("llGunBuyer") && !InQueue("retired_gunsmith"), () => Where(seat) + "; visitors added for trades " + TradeVisits.Added + " (was " + doneBefore + "; last " + TradeVisits.LastAdded + "); queue " + string.Join(",", DayNightBotBase.Ps.storeClientManager.clientStack.Select((StoreClient c) => c.identifier)));
			Delay("b-looks", 5f);
		});
		Delay("b-checks", 40f);
	}
}

public sealed class LeavingCycleB : LeavingCycleBotBase
{
	private static int Me => Members.LocalSeat;

	private List<string> todo;

	private int index;

	private bool picked;

	private float asked = -100f;

	public override string Name => "lc-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		JoinSteps();
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return Where(Me);
		});
		WaitFor("my-bay", 60f, () => Me > 1 && Bays.BayOf(Me) != null, () => Where(Me));
		Delay("the-founder-counts-the-shop", 5f);
		Step("pick-every-trade-in-turn", 700f, delegate(BotStep st)
		{
			if (todo == null)
			{
				todo = Free();
			}
			st.Detail = index + "/" + todo.Count + " (" + string.Join(", ", todo) + "); my trade [" + (Specs.Of(Me) ?? "-") + "]; last refusal '" + Specs.LastRefusal + "'";
			if (index >= todo.Count)
			{
				return StepStatus.Pass;
			}
			string text = Specs.Of(Me);
			if (text == todo[index])
			{
				picked = true;
			}
			else if (text == null && picked)
			{
				// Taken back; the next pick waits while the founder counts the shop.
				picked = false;
				index++;
				asked = Time.realtimeSinceStartup;
			}
			else if (text == null && Time.realtimeSinceStartup - asked > 8f)
			{
				asked = Time.realtimeSinceStartup;
				Specs.Pick(todo[index]);
			}
			return StepStatus.Running;
		});
		Delay("three-comparisons-with-the-server", 32f);
		Agree("same-shop-after-every-trade", 30f, "core.digest", "items.digest", "econ.hash");
		Check("no-reload-so-far", () => Digest.ReloadsAsked == 0 && DayNight.Mismatches == 0, () => "reloads asked " + Digest.ReloadsAsked + "; day/night mismatches " + DayNight.Mismatches);
		Step("pick-gunsmith", 60f, delegate(BotStep st)
		{
			if (Specs.Of(Me) == "GUNSMITH")
			{
				return StepStatus.Pass;
			}
			if (Specs.Of(Me) == null && st.Detail != "asked")
			{
				Specs.Pick("GUNSMITH");
				st.Detail = "asked";
			}
			return StepStatus.Running;
		});
		WaitFor("closed-by-host", 300f, () => DayNightBotBase.State == "CLOSED", () => Where(Me));
		WaitFor("the-founder-took-the-gunsmith", 60f, () => Specs.Of(Me) == null, () => Where(Me));
		Delay("past-close-timers", 6f);
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1, () => Where(Me));
		WaitFor("opened-by-host", 120f, () => DayNightBotBase.State == "OPEN", () => Where(Me));
		Check("nobody-comes-for-a-gunsmith-here-either", () => !InQueue("llGunSeller") && !InQueue("llGunBuyer") && !InQueue("retired_gunsmith"), () => Where(Me) + "; queue " + string.Join(",", DayNightBotBase.Ps.storeClientManager.clientStack.Select((StoreClient c) => c.identifier)));
		WaitFor("closed-again", 120f, () => DayNightBotBase.State == "CLOSED", () => Where(Me));
		Delay("two-comparisons-with-the-server", 22f);
		Agree("same-shop-at-the-end", 30f, "core.digest", "items.digest", "econ.hash");
		Check("no-reload", () => Digest.ReloadsAsked == 0 && DayNight.Mismatches == 0, () => "reloads asked " + Digest.ReloadsAsked + "; day/night mismatches " + DayNight.Mismatches + "; " + Where(Me));
	}
}
