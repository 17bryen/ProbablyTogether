using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

/// A silencer moved inside a gun storage in the founder's bay must replay on another player's load
/// without the placed-anyway fallback.
public abstract class BayNestBotBase : DayNightBotBase
{
	protected static int Me => Members.LocalSeat;

	protected static GameItem Storage => Items.All().FirstOrDefault((GameItem i) => i != null && i.identifier == "storage_bay_gun" && GeneralHelper.IsItemOwned(i));

	protected static GameItem Silencer => (from i in Items.All()
		where i != null && i.identifier == "silencer" && GeneralHelper.IsItemOwned(i)
		orderby Ids.Of(i)
		select i).FirstOrDefault();

	protected static GameInventory InvOf(GameItem box)
	{
		PixelWindow w = box?.contentWindow;
		return (w != null) ? (w.child as GameInventory) : null;
	}

	protected static GameItem BoxOf(GameItem item)
	{
		return (item?.parentInventory != null) ? GraphUtils.FindParentType<GameItem>((GraphNodeStorage)(object)item.parentInventory) : null;
	}

	protected static string Where()
	{
		GameItem s = Storage;
		GameItem x = Silencer;
		return "gun storage " + ((s == null) ? "none" : ("#" + Ids.Of(s) + " in " + Items.RefOf((GraphNodeStorage)(object)s.parentInventory) + "@" + s.modifiedShape.minX + "," + s.modifiedShape.minY))
			+ "; silencer " + ((x == null) ? "none" : ("#" + Ids.Of(x) + " in " + Items.RefOf((GraphNodeStorage)(object)x.parentInventory) + "@" + x.modifiedShape.minX + "," + x.modifiedShape.minY))
			+ "; the gun storage's box " + ((BoxOf(s) == null) ? "none" : ("#" + Ids.Of(BoxOf(s)) + " of seat " + Bays.TagOwner(BoxOf(s))));
	}

	/// Gun storage in the founder's bay, the silencer in it, moved once.
	protected static bool Arranged()
	{
		GameItem s = Storage;
		GameItem x = Silencer;
		return s != null && x != null && BoxOf(s) != null && Bays.TagOwner(BoxOf(s)) == 1 && BoxOf(x) == s && x.modifiedShape.minY >= 5;
	}
}

public sealed class BayNestH : BayNestBotBase
{
	private float asked = -100f;

	private int q;

	public override string Name => "bn-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 420f;

	private StepStatus MoveStep(BotStep st, Func<bool> done, Func<GameItem> what, Func<GameInventory> to, Func<GameInventory, GameItem, int[]> cell)
	{
		st.Detail = Where() + "; request " + q + " " + ((q != 0) ? ItemsMove.Outcome(q) : "-");
		if (done())
		{
			q = 0;
			asked = -100f;
			return StepStatus.Pass;
		}
		if (Time.realtimeSinceStartup - asked < 4f)
		{
			return StepStatus.Running;
		}
		GameItem item = what();
		GameInventory inv = to();
		if (item == null || inv == null)
		{
			return StepStatus.Running;
		}
		int[] at = cell(inv, item);
		if (at == null)
		{
			return StepStatus.Running;
		}
		asked = Time.realtimeSinceStartup;
		q = ItemsMove.Move(item, (GraphNodeStorage)(object)inv, at[0], at[1], item.modifiedShape.flipped, item.modifiedShape.orientation, -1, rep: false, "bot");
		return StepStatus.Running;
	}

	private static int[] Free(GameInventory inv, GameItem item, int fromY)
	{
		if (!(inv is GameGridInventory grid))
		{
			return null;
		}
		for (int y = fromY; y < grid.inventoryShape.height; y++)
		{
			for (int x = 0; x < grid.inventoryShape.width; x++)
			{
				GridShape shape = Items.Shape(item, x, y, item.modifiedShape.flipped, item.modifiedShape.orientation);
				if (!shape.FullyIntersects(grid.inventoryShape, (byte b, byte bo) => b != 1 || bo == 0))
				{
					continue;
				}
				if (grid.childItems.All((GameItem o) => o == item || !shape.Intersects(o.modifiedShape)))
				{
					return new int[2] { x, y };
				}
			}
		}
		return null;
	}

	protected override void Build()
	{
		WaitFor("shop", 240f, () => Net.IsLive && Items.Ready && Me == 1, () => "state " + Net.ClientState + ", seat " + Me);
		WaitFor("i-am-the-gunsmith-with-a-bay", 90f, () => Specs.Of(Me) == "GUNSMITH" && Bays.BayOf(1) != null && Storage != null && Silencer != null, () => "my trade [" + (Specs.Of(Me) ?? "-") + "]; " + Where());
		WaitFor("the-other-player-is-in", 240f, () => Net.Players.Count >= 2, () => "players " + Net.Players.Count);
		Delay("kit-settles", 5f);
		Step("gun-storage-into-my-bay", 60f, (BotStep st) => MoveStep(st, () => Storage != null && BoxOf(Storage) == Bays.BayOf(1), () => Storage, () => InvOf(Bays.BayOf(1)), (GameInventory inv, GameItem it) => Free(inv, it, 0)));
		Step("silencer-into-the-gun-storage", 60f, (BotStep st) => MoveStep(st, () => Silencer != null && BoxOf(Silencer) == Storage, () => Silencer, () => InvOf(Storage), (GameInventory inv, GameItem it) => Free(inv, it, 0)));
		Delay("a-look-over-passes", 2f);
		Step("silencer-to-another-cell-of-it", 60f, (BotStep st) => MoveStep(st, Arranged, () => Silencer, () => InvOf(Storage), (GameInventory inv, GameItem it) => Free(inv, it, 5)));
		Delay("the-other-player-loads-again", 45f);
		Agree("same-shop", 60f, "core.digest", "items.digest");
		Check("still-arranged", Arranged, Where);
	}
}

public sealed class BayNestB : BayNestBotBase
{
	private DayLog.ReplayStats before;

	private int mismatchesBefore;

	public override string Name => "bn-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 420f;

	protected override void Build()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role + ", player " + Net.LocalPlayer);
		WaitFor("live", 240f, () => Net.IsLive && Items.Ready, () => "state " + Net.ClientState);
		WaitFor("the-founder-has-arranged-the-gun-storage", 240f, Arranged, Where);
		Check("seen-live-by-the-game's-own-rules", () => ItemsMove.PlacedAnyway == 0 && ItemsMove.ApplyMismatches == 0, () => "placed anyway " + ItemsMove.PlacedAnyway + "; apply mismatches " + ItemsMove.ApplyMismatches + "; last problem " + (ItemsMove.LastApplyProblem ?? "-"));
		Delay("settle", 3f);
		Do("load-the-shop-again", delegate
		{
			before = DayLog.Last;
			mismatchesBefore = ItemsMove.ApplyMismatches;
			Snapshot.RequestResync("rig: load the day again");
			return "asked; " + Where();
		});
		WaitFor("loaded-again", 120f, () => DayLog.Last != null && DayLog.Last != before && Net.IsLive && Items.Ready, () => "last replay " + (DayLog.Last?.ToString() ?? "none"));
		Check("every-step-of-the-day-replayed", () => DayLog.Last.ChecksBad == 0 && DayLog.Last.Replayed == DayLog.Last.Expected, () => DayLog.Last.ToString() + "; last problem " + (ItemsMove.LastApplyProblem ?? "-"));
		Check("the-silencer-is-where-the-founder-put-it", Arranged, Where);
		Check("by-the-game's-own-rules", () => ItemsMove.PlacedAnyway == 0 && ItemsMove.ApplyMismatches == mismatchesBefore, () => "placed anyway " + ItemsMove.PlacedAnyway + "; apply mismatches " + mismatchesBefore + " -> " + ItemsMove.ApplyMismatches);
		Delay("two-comparisons-with-the-server", 22f);
		Agree("same-shop", 30f, "core.digest", "items.digest");
		Check("one-reload-only", () => Digest.ReloadsAsked <= 1, () => "reloads asked " + Digest.ReloadsAsked);
	}
}
