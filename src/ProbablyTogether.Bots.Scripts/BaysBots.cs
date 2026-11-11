using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class BaysProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("bay.grants", () => (!Net.IsServer) ? "" : BotProbeSet.Str(Bays.Grants));
		BotProbes.Add("bay.refused", () => (!Net.IsServer) ? "" : BotProbeSet.Str(Bays.Refusals));
		BotProbes.Add("bay.owners", delegate
		{
			List<string> list = new List<string>();
			foreach (GameItem item in Items.All())
			{
				int num = Bays.OwnerOf(item);
				if (num != 0)
				{
					list.Add(num + ":" + Ids.Of(item) + ":" + GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)item, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).Count);
				}
			}
			list.Sort(StringComparer.Ordinal);
			return string.Join(" ", list);
		});
	}
}

public abstract class BaysBotBase : DayNightBotBase
{
	protected static EmporiumEntry E => EmporiumEntry.Instance;

	protected static GameItem MyBay => Bays.BayOf(Members.LocalSeat);

	protected static GameInventory Inside(GameItem bay)
	{
		PixelWindow obj = ((bay != null) ? bay.contentWindow : null);
		GraphNodeStorage val = ((obj != null) ? obj.child : null);
		return (GameInventory)(object)((val is GameInventory) ? val : null);
	}

	protected static int CountIn(GameItem bay)
	{
		return (bay == null) ? (-1) : GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)bay, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).Count;
	}

	/// Loose things in the shop's storage that are nobody's bay, oldest first.
	protected static List<GameItem> Loose()
	{
		return (from i in ItemsBotBase.Movables((GameInventory)(object)E.invElement)
			where Bays.OwnerOf(i) == 0 && i.contentWindow == null
			orderby Ids.Of(i)
			select i).ToList();
	}

	/// One request to the server: passes when the server's answer is the expected one (carried out, or refused).
	protected BotStep Ask(string name, bool expectRefusal, Func<string> what, Func<int> send, Func<bool> after = null)
	{
		int q = 0;
		string text = null;
		BotStep botStep = Step(name, 25f, delegate(BotStep st)
		{
			if (q == 0)
			{
				text = what();
				if (text != null && text.StartsWith("SKIP", StringComparison.Ordinal))
				{
					st.Detail = text;
					return StepStatus.Fail;
				}
				q = send();
				if (q == 0)
				{
					st.Detail = text + ": the request was not sent";
					return StepStatus.Fail;
				}
				return StepStatus.Running;
			}
			string text2 = ItemsMove.Outcome(q);
			if (text2.StartsWith("rejected", StringComparison.Ordinal))
			{
				st.Detail = text + " -> " + text2 + (expectRefusal ? "" : " (should have been carried out)");
				return (!expectRefusal) ? StepStatus.Fail : (((after == null || after()) ? true : false) ? StepStatus.Pass : StepStatus.Fail);
			}
			if (text2 == "applied" || text2 == "applied-mismatch")
			{
				if (expectRefusal)
				{
					st.Detail = text + " -> " + text2 + " (should have been refused)";
					return StepStatus.Fail;
				}
				if (after != null && !after())
				{
					return StepStatus.Running;
				}
				st.Detail = text + " -> " + text2;
				return (text2 == "applied") ? StepStatus.Pass : StepStatus.Fail;
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => text + ": no answer (" + ItemsMove.Outcome(q) + ")";
		return botStep;
	}
}

/// The dedicated server of the storage bay test: two players, a bay each, then a save.
public sealed class BaysServer : BaysBotBase
{
	public override string Name => "bays-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 500f;

	protected override void Build()
	{
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop && Game.ShopLoaded && ProbablyTogether.Server.ShopReady, () => "scene " + BotProbes.Scene);
		WaitFor("two-bays", 240f, () => Bays.Grants == 2 && Bays.BayOf(1) != null && Bays.BayOf(2) != null, () => Bays.Grants + " bay(s) handed out; " + Members.Describe());
		Check("one-each", () => Items.All().Count((GameItem i) => Bays.OwnerOf(i) != 0) == 2 && Members.Fact(1, "bay") != null && Members.Fact(2, "bay") != null, () => "bay of seat 1 #" + Members.Fact(1, "bay") + ", of seat 2 #" + Members.Fact(2, "bay"));
		WaitFor("b-stocked-and-a-was-refused", 240f, () => CountIn(Bays.BayOf(2)) >= 1 && CountIn(Bays.BayOf(1)) >= 1 && Bays.Refusals >= 6, () => "seat 1's bay holds " + CountIn(Bays.BayOf(1)) + ", seat 2's " + CountIn(Bays.BayOf(2)) + "; refusals " + Bays.Refusals);
		Check("save", delegate
		{
			LabKit.Do("save");
			return LabKit.Last.StartsWith("saved", StringComparison.Ordinal);
		}, () => LabKit.Last);
		WaitFor("b-gone-a-refused-again", 240f, () => ProbablyTogether.Server.PlayerCount == 1 && Bays.Refusals >= 7, () => ProbablyTogether.Server.PlayerCount + " connected; refusals " + Bays.Refusals);
		Check("nothing-moved", () => CountIn(Bays.BayOf(2)) == 1 && CountIn(Bays.BayOf(1)) == 1, () => "seat 1's bay holds " + CountIn(Bays.BayOf(1)) + ", seat 2's " + CountIn(Bays.BayOf(2)));
		Delay("clients-finish", 8f);
	}
}

/// Seat 1: uses its own bay, and tries everything it should not be able to do with seat 2's.
public class BaysA : BaysBotBase
{
	private long xId;

	private long zId;

	private long yId;

	private bool second;

	public override string Name => (!second) ? "bays-a" : "bays-a2";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 500f;

	private GameItem Theirs => Bays.BayOf(2);

	public BaysA()
	{
	}

	protected BaysA(bool afterRestart)
	{
		second = afterRestart;
	}

	protected override void Build()
	{
		JoinSteps();
		Check("seat-1", () => Members.LocalSeat == 1, () => "my seat " + Members.LocalSeat);
		WaitFor("both-bays-here", 120f, () => MyBay != null && Theirs != null, () => "mine " + ((MyBay != null) ? ("#" + Ids.Of(MyBay)) : "none") + ", theirs " + ((Theirs != null) ? ("#" + Ids.Of(Theirs)) : "none"));
		Delay("locks-settle", 2f);
		Check("mine-opens-theirs-is-locked", () => !MyBay.contentWindow.isLocked && Theirs.contentWindow.isLocked, () => "mine locked " + MyBay.contentWindow.isLocked + " (" + MyBay.name + "), theirs locked " + Theirs.contentWindow.isLocked + " (" + Theirs.name + ")");
		if (!second)
		{
			Ask("into-my-bay", expectRefusal: false, delegate
			{
				GameItem val = Loose().FirstOrDefault();
				if (val == null)
				{
					return "SKIP nothing loose in the shop";
				}
				xId = Ids.Of(val);
				return val.identifier + "#" + xId + " into my bay";
			}, () => ItemsMove.InsertInto(Ids.Find(xId), MyBay, -1, chain: true, "bot"), () => GraphUtils.FindParentEquals((GraphNodeStorage)(object)Ids.Find(xId), (GraphNodeStorage)(object)MyBay));
			WaitFor("b-stocked-its-bay", 120f, () => CountIn(Theirs) >= 1, () => "their bay holds " + CountIn(Theirs));
		}
		else
		{
			Check("my-things-kept", () => CountIn(MyBay) == 1 && CountIn(Theirs) == 1, () => "my bay holds " + CountIn(MyBay) + ", theirs " + CountIn(Theirs) + " after the restart");
		}
		Ask("into-their-bay", expectRefusal: true, delegate
		{
			GameItem val = Loose().FirstOrDefault();
			if (val == null)
			{
				return "SKIP nothing loose in the shop";
			}
			zId = Ids.Find(Ids.Of(val)) != null ? Ids.Of(val) : 0;
			return val.identifier + "#" + zId + " into seat 2's bay";
		}, () => ItemsMove.InsertInto(Ids.Find(zId), Theirs, -1, chain: true, "bot"), () => !GraphUtils.FindParentEquals((GraphNodeStorage)(object)Ids.Find(zId), (GraphNodeStorage)(object)Theirs));
		if (!second)
		{
			Ask("out-of-their-bay", expectRefusal: true, delegate
			{
				GameItem val = GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)Theirs, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).FirstOrDefault();
				if (val == null)
				{
					return "SKIP their bay is empty";
				}
				yId = Ids.Of(val);
				return "take " + val.identifier + "#" + yId + " out of seat 2's bay";
			}, () => ItemsMove.QuickMove(Ids.Find(yId), (GraphNodeStorage)(object)E.invWindow, all: false, -1, "bot"), () => GraphUtils.FindParentEquals((GraphNodeStorage)(object)Ids.Find(yId), (GraphNodeStorage)(object)Theirs));
			Ask("unload-their-bay", expectRefusal: true, () => "unload all of seat 2's bay", () => ItemsMove.UnloadAll(Theirs, "bot"), () => CountIn(Theirs) >= 1);
			Ask("bin-their-bay", expectRefusal: true, () => "seat 2's bay into the bin", () => ItemsMove.Trash(Theirs, "bot"), () => Theirs != null);
			Ask("their-bay-to-the-counter", expectRefusal: true, delegate
			{
				int x;
				int y;
				return ItemsBotBase.Spot((GameInventory)(object)E.backInvinvElement, Theirs, 1, Theirs.modifiedShape.orientation, out x, out y) ? "seat 2's bay onto the back counter" : "seat 2's bay onto the back counter (no free spot: any answer but 'applied' passes)";
			}, delegate
			{
				ItemsBotBase.Spot((GameInventory)(object)E.backInvinvElement, Theirs, 1, Theirs.modifiedShape.orientation, out var x, out var y);
				return ItemsMove.Move(Theirs, (GraphNodeStorage)(object)E.backInvinvElement, x, y, Theirs.modifiedShape.flipped, Theirs.modifiedShape.orientation, -1, rep: false, "bot");
			}, () => (object)Theirs.parentInventory != E.backInvinvElement);
			Ask("sort-their-bay", expectRefusal: true, () => "sort the inside of seat 2's bay", () => (!(Inside(Theirs) is GameGridInventory inv)) ? 0 : ItemsMove.Sort(inv, bigFirst: true, fromEnd: false, autoRotate: false, "bot"));
			Ask("their-bay-around-the-storage", expectRefusal: false, delegate
			{
				int x;
				int y;
				return ItemsBotBase.Spot((GameInventory)(object)E.invElement, Theirs, 1, Theirs.modifiedShape.orientation, out x, out y) ? ("seat 2's bay to " + x + "," + y + " of the shop's storage") : "SKIP no free spot in the shop's storage";
			}, delegate
			{
				ItemsBotBase.Spot((GameInventory)(object)E.invElement, Theirs, 1, Theirs.modifiedShape.orientation, out var x, out var y);
				return ItemsMove.Move(Theirs, (GraphNodeStorage)(object)E.invElement, x, y, Theirs.modifiedShape.flipped, Theirs.modifiedShape.orientation, -1, rep: false, "bot");
			});
			Agree("same-with-b-here", 30f, "core.digest", "items.digest", "bay.owners", "mem.count");
			WaitFor("b-left", 240f, () => Net.Players.Count == 1, () => Net.Players.Count + " connected");
			Check("still-locked-with-b-away", () => Theirs.contentWindow.isLocked && Bays.OwnerOf(Theirs) == 2, () => "seat 2's bay locked " + Theirs.contentWindow.isLocked + " while seat 2 is offline");
			Ask("into-their-bay-while-away", expectRefusal: true, () => "into seat 2's bay while seat 2 is offline", () => ItemsMove.InsertInto(Ids.Find(zId), Theirs, -1, chain: true, "bot"), () => !GraphUtils.FindParentEquals((GraphNodeStorage)(object)Ids.Find(zId), (GraphNodeStorage)(object)Theirs));
		}
		Agree("same-at-the-end", 30f, "core.digest", "items.digest", "bay.owners", "mem.count");
	}
}

/// Seat 1 again after the shop was restarted from its save, alone: seat 2's bay is still seat 2's.
public sealed class BaysA2 : BaysA
{
	public BaysA2()
		: base(afterRestart: true)
	{
	}
}

/// Seat 2: stocks its own bay, waits while seat 1 tries the door, leaves.
public sealed class BaysB : BaysBotBase
{
	private long yId;

	public override string Name => "bays-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 400f;

	protected override void Build()
	{
		JoinSteps();
		Check("seat-2", () => Members.LocalSeat == 2, () => "my seat " + Members.LocalSeat);
		WaitFor("both-bays-here", 120f, () => MyBay != null && Bays.BayOf(1) != null, () => "mine " + ((MyBay != null) ? ("#" + Ids.Of(MyBay)) : "none"));
		Delay("locks-settle", 2f);
		Check("mine-opens-theirs-is-locked", () => !MyBay.contentWindow.isLocked && Bays.BayOf(1).contentWindow.isLocked, () => "mine locked " + MyBay.contentWindow.isLocked + ", seat 1's locked " + Bays.BayOf(1).contentWindow.isLocked);
		Ask("into-my-bay", expectRefusal: false, delegate
		{
			GameItem val = Loose().LastOrDefault();
			if (val == null)
			{
				return "SKIP nothing loose in the shop";
			}
			yId = Ids.Of(val);
			return val.identifier + "#" + yId + " into my bay";
		}, () => ItemsMove.InsertInto(Ids.Find(yId), MyBay, -1, chain: true, "bot"), () => GraphUtils.FindParentEquals((GraphNodeStorage)(object)Ids.Find(yId), (GraphNodeStorage)(object)MyBay));
		WaitServer("a-was-refused", 180f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "bay.refused") >= 6, "bay.refused", "bay.owners");
		Check("my-things-untouched", () => CountIn(MyBay) == 1 && GraphUtils.FindParentEquals((GraphNodeStorage)(object)Ids.Find(yId), (GraphNodeStorage)(object)MyBay), () => "my bay holds " + CountIn(MyBay) + " item(s)");
		Agree("same-as-server", 30f, "core.digest", "items.digest", "bay.owners");
		WaitServer("server-saved", 90f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "lab.applied") >= 1, "lab.applied");
	}
}
