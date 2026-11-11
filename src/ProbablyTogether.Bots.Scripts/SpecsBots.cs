using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class SpecsProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("spec.kits", () => BotProbeSet.Str(Specs.Kits));
		BotProbes.Add("spec.list", delegate
		{
			List<string> list = new List<string>();
			foreach (Member item in Members.State.list)
			{
				list.Add(item.s + ":" + (Specs.Of(item.s) ?? "-") + ":" + (Members.Fact(item.s, "mach") ?? ""));
			}
			return string.Join(" ", list);
		});
	}
}

public abstract class SpecsBotBase : BaysBotBase
{
	protected static string ShopScenario => DayNightBotBase.Ps.startType.ToString();

	/// A machine of that seat's trade in the shop's storage, not inside a bay.
	protected static GameItem MachineInStorage(int seat)
	{
		return ((IEnumerable<GameItem>)((GameInventory)BaysBotBase.E.invElement).childItems).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => Bays.TagOwner(i) == 0 && Specs.OwnerOfType(i.identifier) == seat));
	}

	protected static GameItem MachineInBay(int seat)
	{
		GameItem val = Bays.BayOf(seat);
		if (val == null)
		{
			return null;
		}
		return GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).FirstOrDefault((GameItem i) => Specs.OwnerOfType(i.identifier) == seat && (object)i.parentInventory == BaysBotBase.Inside(val));
	}

	protected static string Kinds(int seat)
	{
		return Members.Fact(seat, "mach") ?? "";
	}
}

public sealed class SpecsServer : SpecsBotBase
{
	private int cash0;

	private int rent0;

	public override string Name => "specs-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 500f;

	protected override void Build()
	{
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop && Game.ShopLoaded && ProbablyTogether.Server.ShopReady, () => "scene " + BotProbes.Scene);
		Do("before", delegate
		{
			cash0 = DayNightBotBase.Ps.playerCash;
			rent0 = DayNightBotBase.Ps.rentValue;
			return "cash " + cash0 + ", base rent " + rent0 + ", shop scenario " + SpecsBotBase.ShopScenario;
		});
		WaitFor("seat-1-has-the-shops-trade", 240f, () => Specs.Of(1) == SpecsBotBase.ShopScenario && BaysBotBase.CountIn(Bays.BayOf(1)) > 0, () => "seat 1: " + (Specs.Of(1) ?? "none") + ", machine kinds [" + SpecsBotBase.Kinds(1) + "], bay holds " + BaysBotBase.CountIn(Bays.BayOf(1)));
		WaitFor("seat-2-picked", 240f, () => Specs.Of(2) == "DRUG_DEALER", () => "seat 2: " + (Specs.Of(2) ?? "none"));
		Check("kit-in-seat-2s-bay", () => BaysBotBase.CountIn(Bays.BayOf(2)) >= 5 && SpecsBotBase.Kinds(2).Length > 0, () => "seat 2's bay holds " + BaysBotBase.CountIn(Bays.BayOf(2)) + " item(s); machine kinds [" + SpecsBotBase.Kinds(2) + "]");
		Check("a-kit-costs-the-shop-nothing", () => DayNightBotBase.Ps.playerCash == cash0 && DayNightBotBase.Ps.rentValue == rent0, () => "cash " + DayNightBotBase.Ps.playerCash + " (was " + cash0 + "), base rent " + DayNightBotBase.Ps.rentValue + " (was " + rent0 + ")");
		Check("picks-are-single-and-final", () => Specs.Kits == 1 && Specs.TakenBy(SpecsBotBase.ShopScenario) == 1 && Specs.TakenBy("DRUG_DEALER") == 2, () => Specs.Kits + " kit(s) handed out; last refusal: " + Specs.LastRefusal);
		WaitFor("both-tried-the-others-machine", 240f, () => Bays.Refusals >= 3, () => "refusals " + Bays.Refusals);
		Delay("moves-settle", 3f);
		Check("save", delegate
		{
			LabKit.Do("save");
			return LabKit.Last.StartsWith("saved", StringComparison.Ordinal);
		}, () => LabKit.Last);
		Delay("clients-finish", 14f);
	}
}

/// Seat 1: has the shop's own trade. Takes one of its machines out of its bay, then tries seat 2's machine.
public class SpecsA : SpecsBotBase
{
	private readonly bool second;

	private long mineId;

	private long looseId;

	public override string Name => (!second) ? "specs-a" : "specs-a2";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 500f;

	public SpecsA()
	{
	}

	protected SpecsA(bool afterRestart)
	{
		second = afterRestart;
	}

	protected override void Build()
	{
		JoinSteps();
		Check("seat-1", () => Members.LocalSeat == 1, () => "my seat " + Members.LocalSeat);
		WaitFor("i-have-the-shops-trade", 120f, () => Specs.Of(1) == SpecsBotBase.ShopScenario && BaysBotBase.MyBay != null, () => "mine: " + (Specs.Of(1) ?? "none") + " (shop: " + SpecsBotBase.ShopScenario + "), machine kinds [" + SpecsBotBase.Kinds(1) + "]");
		if (!second)
		{
			Check("starting-kit-is-in-my-bay", () => BaysBotBase.CountIn(BaysBotBase.MyBay) > 0, () => "my bay holds " + BaysBotBase.CountIn(BaysBotBase.MyBay) + " item(s); " + ((GameInventory)BaysBotBase.E.invElement).childItems.Count + " thing(s) left loose in the shop's storage");
			Agree("same-after-my-kit", 30f, "core.digest", "items.digest", "spec.list", "cash");
			WaitFor("seat-2-picked", 240f, () => Specs.Of(2) == "DRUG_DEALER", () => "seat 2: " + (Specs.Of(2) ?? "none"));
			Delay("kit-settles", 2f);
			Agree("same-after-their-kit", 40f, "core.digest", "items.digest", "spec.list", "bay.owners", "cash");
			Step("a-machine-of-mine-in-the-storage", 25f, delegate(BotStep st)
			{
				GameItem val = SpecsBotBase.MachineInStorage(1);
				if (val != null)
				{
					st.Detail = val.identifier + "#" + Ids.Of(val) + " stands in the shop's storage" + ((mineId == 0L) ? " (it never fitted my bay)" : " (I took it out of my bay)");
					return StepStatus.Pass;
				}
				if (mineId == 0L)
				{
					GameItem val2 = SpecsBotBase.MachineInBay(1);
					if (val2 == null)
					{
						st.Detail = "none of my machine kinds [" + SpecsBotBase.Kinds(1) + "] is in my bay or in the storage";
						return StepStatus.Fail;
					}
					mineId = Ids.Of(val2);
					ItemsMove.QuickMove(val2, (GraphNodeStorage)(object)BaysBotBase.E.invWindow, all: false, -1, "bot");
				}
				return StepStatus.Running;
			});
		}
		else
		{
			Check("seat-2s-trade-remembered", () => Specs.Of(2) == "DRUG_DEALER" && SpecsBotBase.Kinds(2).Length > 0 && Net.Players.Count == 1, () => "seat 2: " + (Specs.Of(2) ?? "none") + " [" + SpecsBotBase.Kinds(2) + "] with " + Net.Players.Count + " connected");
		}
		WaitFor("their-machine-is-in-the-storage", 240f, () => SpecsBotBase.MachineInStorage(2) != null, () => "none of seat 2's kinds [" + SpecsBotBase.Kinds(2) + "] stands in the shop's storage yet");
		Delay("locks-settle", 2f);
		Check("their-machine-is-closed-to-me", delegate
		{
			GameItem val = SpecsBotBase.MachineInStorage(2);
			return Bays.OwnerOf(val) == 2 && (val.contentWindow == null || val.contentWindow.isLocked);
		}, delegate
		{
			GameItem val = SpecsBotBase.MachineInStorage(2);
			return val.identifier + "#" + Ids.Of(val) + " belongs to seat " + Bays.OwnerOf(val) + ((val.contentWindow != null) ? ("; its window is locked for me: " + val.contentWindow.isLocked) : "; it has no window");
		});
		Ask("something-into-their-machine", expectRefusal: true, delegate
		{
			GameItem val = BaysBotBase.Loose().FirstOrDefault();
			if (val == null)
			{
				return "SKIP nothing loose in the shop";
			}
			looseId = Ids.Of(val);
			return val.identifier + "#" + looseId + " into seat 2's " + SpecsBotBase.MachineInStorage(2).identifier;
		}, () => ItemsMove.InsertInto(Ids.Find(looseId), SpecsBotBase.MachineInStorage(2), -1, chain: true, "bot"));
		Ask("their-machine-into-the-bin", expectRefusal: true, () => "seat 2's " + SpecsBotBase.MachineInStorage(2).identifier + " into the bin", () => ItemsMove.Trash(SpecsBotBase.MachineInStorage(2), "bot"), () => SpecsBotBase.MachineInStorage(2) != null);
		Agree("same-at-the-end", 30f, "core.digest", "items.digest", "spec.list", "bay.owners", "cash");
	}
}

public sealed class SpecsA2 : SpecsA
{
	public SpecsA2()
		: base(afterRestart: true)
	{
	}
}

/// Seat 2: may not take the shop's trade, picks its own, may not pick twice; then tries seat 1's machine.
public sealed class SpecsB : SpecsBotBase
{
	private long mineId;

	private long looseId;

	public override string Name => "specs-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 400f;

	protected override void Build()
	{
		JoinSteps();
		Check("seat-2", () => Members.LocalSeat == 2, () => "my seat " + Members.LocalSeat);
		WaitFor("my-bay-and-seat-1s-trade", 120f, () => BaysBotBase.MyBay != null && Specs.Of(1) != null, () => "seat 1: " + (Specs.Of(1) ?? "none"));
		Do("ask-for-the-taken-one", delegate
		{
			Specs.Pick(Specs.Of(1));
			return "asked for " + Specs.Of(1);
		});
		WaitFor("refused-taken", 20f, () => Specs.LastRefusal.Contains("already has"), () => "refusal: '" + Specs.LastRefusal + "'");
		Do("pick", delegate
		{
			ProbablyTogether.UI.Chat.Send("/trade chemist");
			return "typed '/trade chemist' in chat";
		});
		WaitFor("i-am-the-chemist", 30f, () => Specs.Of(2) == "DRUG_DEALER" && Specs.Kits >= 1, () => "mine: " + (Specs.Of(2) ?? "none") + "; refusal '" + Specs.LastRefusal + "'");
		Check("kit-in-my-bay", () => BaysBotBase.CountIn(BaysBotBase.MyBay) >= 5, () => "my bay holds " + BaysBotBase.CountIn(BaysBotBase.MyBay) + " item(s): " + string.Join(",", from i in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)BaysBotBase.MyBay, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null)
			where (object)i.parentInventory == BaysBotBase.Inside(BaysBotBase.MyBay)
			select i.identifier) + "; machine kinds [" + SpecsBotBase.Kinds(2) + "]");
		Do("ask-for-a-second-one", delegate
		{
			Specs.Pick("FARMER");
			return "asked for FARMER";
		});
		WaitFor("refused-second", 20f, () => Specs.LastRefusal.Contains("already have"), () => "refusal: '" + Specs.LastRefusal + "'");
		Agree("same-after-my-kit", 40f, "core.digest", "items.digest", "spec.list", "bay.owners", "cash");
		Step("a-machine-of-mine-in-the-storage", 25f, delegate(BotStep st)
		{
			GameItem val = SpecsBotBase.MachineInStorage(2);
			if (val != null)
			{
				st.Detail = val.identifier + "#" + Ids.Of(val) + " stands in the shop's storage" + ((mineId == 0L) ? " (too big for my bay: the kit put it there)" : " (I took it out of my bay)");
				return StepStatus.Pass;
			}
			if (mineId == 0L)
			{
				GameItem val2 = SpecsBotBase.MachineInBay(2);
				if (val2 == null)
				{
					st.Detail = "none of my machine kinds [" + SpecsBotBase.Kinds(2) + "] is in my bay or in the storage";
					return StepStatus.Fail;
				}
				mineId = Ids.Of(val2);
				ItemsMove.QuickMove(val2, (GraphNodeStorage)(object)BaysBotBase.E.invWindow, all: false, -1, "bot");
			}
			return StepStatus.Running;
		});
		WaitFor("their-machine-is-in-the-storage", 240f, () => SpecsBotBase.MachineInStorage(1) != null, () => "none of seat 1's kinds [" + SpecsBotBase.Kinds(1) + "] stands in the shop's storage yet");
		Ask("something-into-their-machine", expectRefusal: true, delegate
		{
			GameItem val = BaysBotBase.Loose().LastOrDefault();
			if (val == null)
			{
				return "SKIP nothing loose in the shop";
			}
			looseId = Ids.Of(val);
			return val.identifier + "#" + looseId + " into seat 1's " + SpecsBotBase.MachineInStorage(1).identifier;
		}, () => ItemsMove.InsertInto(Ids.Find(looseId), SpecsBotBase.MachineInStorage(1), -1, chain: true, "bot"));
		Agree("same-as-server", 30f, "core.digest", "items.digest", "spec.list", "bay.owners", "cash");
		WaitServer("server-saved", 120f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "lab.applied") >= 1, "lab.applied");
	}
}
