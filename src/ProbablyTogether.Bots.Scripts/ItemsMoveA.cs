using System;
using System.Collections.Generic;
using System.Linq;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ItemsMoveA : ItemsBotBase
{
	private long xId;

	private long shardId;

	private long bagId;

	private long boxId;

	private long yId;

	private long zId;

	private long wId;

	private long vId;

	private long cId;

	private int tx;

	private int ty;

	private int rot;

	private List<long> groupIds = new List<long>();

	private int gdx;

	private int gdy;

	private Dictionary<long, (int, int)> groupFrom = new Dictionary<long, (int, int)>();

	private int shardsBefore;

	private GameInventory target;

	private long s1;

	private long s2;

	public override string Name => "items-move-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 1200f;

	private GameItem I(long id)
	{
		return Items.Find(id);
	}

	protected override void Build()
	{
		JoinSteps();
		DigestSame("join-digest");
		Check("ids-unique", () => ItemsMoveProbes.IdCheck().Contains("dup=0 zero=0"), ItemsMoveProbes.IdCheck);
		Cmd("ask-spawn", "spawn");
		WaitFor("spawned", 30f, delegate
		{
			if (ItemsBotBase.Tagged("BR_BOT_BAG") != null && ItemsBotBase.Tagged("BR_BOT_BOX") != null)
			{
				GameItem obj = ItemsBotBase.Tagged("BR_BOT_SHARD");
				if (obj == null)
				{
					return false;
				}
				return obj.unitCount == 3;
			}
			return false;
		}, delegate
		{
			string[] obj = new string[6]
			{
				"bag ",
				(ItemsBotBase.Tagged("BR_BOT_BAG") != null).ToString(),
				", box ",
				(ItemsBotBase.Tagged("BR_BOT_BOX") != null).ToString(),
				", shard x",
				null
			};
			GameItem obj2 = ItemsBotBase.Tagged("BR_BOT_SHARD");
			obj[5] = ((obj2 != null) ? new int?(obj2.unitCount) : ((int?)null)).ToString();
			return string.Concat(obj);
		});
		Do("spawn-ids", delegate
		{
			bagId = Ids.Of(ItemsBotBase.Tagged("BR_BOT_BAG"));
			boxId = Ids.Of(ItemsBotBase.Tagged("BR_BOT_BOX"));
			shardId = Ids.Of(ItemsBotBase.Tagged("BR_BOT_SHARD"));
			return "bag #" + bagId + ", box #" + boxId + ", shard #" + shardId;
		});
		DigestSame("spawn-digest");
		Act("move", 20f, delegate
		{
			int x;
			int y;
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.showcaseElement, i, i.unitCount, i.modifiedShape.orientation, out x, out y))) ?? ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.hiddenElement, i, i.unitCount, i.modifiedShape.orientation, out x, out y)));
			if (val == null)
			{
				return "SKIP no movable item fits the display case or stash";
			}
			target = (GameInventory)(object)(ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.showcaseElement, val, val.unitCount, val.modifiedShape.orientation, out tx, out ty) ? ItemsBotBase.E.showcaseElement : ItemsBotBase.E.hiddenElement);
			if ((object)target == ItemsBotBase.E.hiddenElement)
			{
				ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.hiddenElement, val, val.unitCount, val.modifiedShape.orientation, out tx, out ty);
			}
			xId = Ids.Of(val);
			return val.identifier + "#" + xId + " -> " + Items.RefOf((GraphNodeStorage)(object)target)?.ToString() + " " + tx + "," + ty;
		}, () => ItemsMove.Move(I(xId), (GraphNodeStorage)(object)target, tx, ty, I(xId).modifiedShape.flipped, I(xId).modifiedShape.orientation, -1, rep: false, "bot"), delegate
		{
			GameItem obj = I(xId);
			return ((obj != null) ? obj.parentInventory : null) == target && I(xId).modifiedShape.minX == tx && I(xId).modifiedShape.minY == ty;
		});
		DigestSame("move-digest");
		Act("rotate", 20f, delegate
		{
			GameItem val = I(xId);
			rot = (val.modifiedShape.orientation + 1) % 4;
			return (!ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.invElement, val, val.unitCount, rot, out tx, out ty)) ? "SKIP no rotated spot" : (val.identifier + "#" + xId + " -> inv " + tx + "," + ty + " r" + rot);
		}, () => ItemsMove.Move(I(xId), (GraphNodeStorage)(object)ItemsBotBase.E.invElement, tx, ty, I(xId).modifiedShape.flipped, rot, -1, rep: false, "bot"), delegate
		{
			GameItem obj = I(xId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.invElement && I(xId).modifiedShape.orientation == rot && I(xId).modifiedShape.minX == tx;
		});
		DigestSame("rotate-digest");
		Act("split", 20f, delegate
		{
			GameItem sh = I(shardId);
			shardsBefore = Items.All().Count((GameItem i) => i.identifier == sh.identifier);
			return (!ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.invElement, sh, 1, sh.modifiedShape.orientation, out tx, out ty)) ? "SKIP no spot for a split" : ("shard#" + shardId + " x" + sh.unitCount + ": 1 -> inv " + tx + "," + ty);
		}, () => ItemsMove.Move(I(shardId), (GraphNodeStorage)(object)ItemsBotBase.E.invElement, tx, ty, I(shardId).modifiedShape.flipped, I(shardId).modifiedShape.orientation, 1, rep: false, "bot"), delegate
		{
			GameItem obj = I(shardId);
			return obj != null && obj.unitCount == 2 && Items.All().Count((GameItem i) => i.identifier == I(shardId).identifier) == shardsBefore + 1;
		}, () => "new piece #" + Ids.Of(((IEnumerable<GameItem>)Items.All()).FirstOrDefault((Func<GameItem, bool>)delegate(GameItem i)
		{
			string identifier = i.identifier;
			GameItem obj = I(shardId);
			return identifier == ((obj != null) ? obj.identifier : null) && i != I(shardId) && (object)i.parentInventory == ItemsBotBase.E.invElement && i.modifiedShape.minX == tx && i.modifiedShape.minY == ty;
		})));
		DigestSame("split-digest");
		AskServer("split-uid-ask");
		SameAsServer("split-uid", "items.uid");
		Act("insert", 20f, delegate
		{
			GameItem bag = I(bagId);
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement, delegate(GameItem i)
			{
				int uniqueId = i.uniqueId;
				GameItem obj = I(xId);
				return uniqueId != ((obj != null) ? new int?(obj.uniqueId) : ((int?)null));
			})).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => bag.MayHaveValidInventorySlot(i) && bag.TryFindOneValidInventorySlot(i) != null));
			if (val == null)
			{
				return "SKIP nothing fits the bag";
			}
			yId = Ids.Of(val);
			return val.identifier + "#" + yId + " -> bag#" + bagId;
		}, () => ItemsMove.InsertInto(I(yId), I(bagId), -1, chain: true, "bot"), delegate
		{
			GameItem obj = I(yId);
			object obj2;
			if (obj == null)
			{
				obj2 = null;
			}
			else
			{
				GameInventory parentInventory = obj.parentInventory;
				obj2 = ((parentInventory != null) ? parentInventory.GetParentItem() : null);
			}
			return obj2 == I(bagId);
		});
		DigestSame("insert-digest");
		Act("quick", 20f, delegate
		{
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement, (GameItem i) => Ids.Of(i) != xId)).FirstOrDefault((Func<GameItem, bool>)delegate(GameItem i)
			{
				SlotMarker obj = ItemsBotBase.E.showcaseWindow.TryFindOneValidInventorySlot(i);
				return obj != null && obj.IsValid();
			});
			if (val == null)
			{
				return "SKIP nothing fits the display case";
			}
			zId = Ids.Of(val);
			return val.identifier + "#" + zId + " -> showcase.w";
		}, () => ItemsMove.QuickMove(I(zId), (GraphNodeStorage)(object)ItemsBotBase.E.showcaseWindow, all: false, -1, "bot"), delegate
		{
			GameItem obj = I(zId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.showcaseElement;
		});
		DigestSame("quick-digest");
		Act("group", 20f, delegate
		{
			List<GameItem> list = ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement);
			for (int i = 0; i < list.Count; i++)
			{
				for (int j = i + 1; j < list.Count; j++)
				{
					(int, int)[] array = new(int, int)[8]
					{
						(1, 0),
						(-1, 0),
						(0, 1),
						(0, -1),
						(2, 0),
						(0, 2),
						(-2, 0),
						(0, -2)
					};
					for (int k = 0; k < array.Length; k++)
					{
						(int, int) tuple = array[k];
						List<GameItem> list2 = new List<GameItem>
						{
							list[i],
							list[j]
						};
						if (ItemsMove.FormationProblem(list2, (GameInventory)(object)ItemsBotBase.E.invElement, tuple.Item1, tuple.Item2) == null)
						{
							groupIds = list2.Select(Ids.Of).ToList();
							gdx = tuple.Item1;
							gdy = tuple.Item2;
							groupFrom = list2.ToDictionary(Ids.Of, (GameItem val) => (minX: val.modifiedShape.minX, minY: val.modifiedShape.minY));
							return string.Join("+", list2.Select((GameItem val) => val.identifier + "#" + Ids.Of(val))) + " by " + gdx + "," + gdy;
						}
					}
				}
			}
			return "SKIP no pair can shift";
		}, () => ItemsMove.GroupMove(groupIds.Select(I).ToList(), (GameInventory)(object)ItemsBotBase.E.invElement, gdx, gdy, "bot"), () => groupIds.All((long id) => I(id) != null && I(id).modifiedShape.minX == groupFrom[id].Item1 + gdx && I(id).modifiedShape.minY == groupFrom[id].Item2 + gdy));
		DigestSame("group-digest");
		Act("sort", 20f, () => (!InventorySortHelper.CanSort(ItemsBotBase.E.invElement)) ? "SKIP inv cannot be sorted" : "inv, big first", () => ItemsMove.Sort(ItemsBotBase.E.invElement, bigFirst: true, fromEnd: false, autoRotate: false, "bot"), () => true);
		DigestSame("sort-digest");
		Check("undo-offered", () => InventorySortHelper.MayUndo(ItemsBotBase.E.invElement), () => "MayUndo " + InventorySortHelper.MayUndo(ItemsBotBase.E.invElement) + ", CanUndo " + InventorySortHelper.CanUndo(ItemsBotBase.E.invElement));
		Act("sort-undo", 20f, null, () => ItemsMove.SortUndo(ItemsBotBase.E.invElement, "bot"), () => true);
		DigestSame("undo-digest");
		Act("unload", 20f, () => "bag#" + bagId, () => ItemsMove.UnloadAll(I(bagId), "bot"), delegate
		{
			if (I(yId) != null)
			{
				GameInventory parentInventory = I(yId).parentInventory;
				return ((parentInventory != null) ? parentInventory.GetParentItem() : null) != I(bagId);
			}
			return false;
		});
		DigestSame("unload-digest");
		Act("pin", 20f, delegate
		{
			GameItem obj = I(xId);
			return ((obj != null) ? obj.identifier : null) + "#" + xId;
		}, () => ItemsMove.Pin(I(xId), pinned: true, "bot"), () => ItemPinHelper.IsPinned(I(xId)));
		DigestSame("pin-digest");
		Act("trash", 20f, delegate
		{
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)delegate(GameItem i)
			{
				GameItem trashcan = ItemsBotBase.E.trashcan;
				object obj;
				if (trashcan == null)
				{
					obj = null;
				}
				else
				{
					PixelWindow contentWindow = trashcan.contentWindow;
					obj = ((contentWindow != null) ? contentWindow.child : null);
				}
				object obj2 = ((obj is GameInventory) ? obj : null);
				return ((obj2 != null) ? ((GameInventory)obj2).TryFindOneValidInventorySlot(i) : null) != null;
			});
			if (val == null)
			{
				return "SKIP nothing fits the trash can";
			}
			wId = Ids.Of(val);
			return val.identifier + "#" + wId;
		}, () => ItemsMove.Trash(I(wId), "bot"), delegate
		{
			GameItem obj = I(wId);
			GameInventory obj2 = ((obj != null) ? obj.parentInventory : null);
			GameItem trashcan = ItemsBotBase.E.trashcan;
			object obj3;
			if (trashcan == null)
			{
				obj3 = null;
			}
			else
			{
				PixelWindow contentWindow = trashcan.contentWindow;
				obj3 = ((contentWindow != null) ? contentWindow.child : null);
			}
			return obj2 == obj3;
		});
		DigestSame("trash-digest");
		Act("opened", 20f, () => "box#" + boxId, () => ItemsMove.MarkOpened(I(boxId), "bot"), delegate
		{
			GameItem obj = I(boxId);
			return obj != null && obj.IsTag("OPENED_TAG");
		});
		DigestSame("opened-digest");
		int q1 = 0;
		int q2 = 0;
		int ax = 0;
		int ay = 0;
		int bx = 0;
		int by = 0;
		string prep = "";
		Step("stale", 20f, delegate(BotStep st)
		{
			if (q1 == 0 || q2 == 0)
			{
				st.Detail = "not sent (" + prep + ")";
				return StepStatus.Fail;
			}
			string text = ItemsMove.Outcome(q1);
			string text2 = ItemsMove.Outcome(q2);
			if (text == "pending" || text2 == "pending")
			{
				return StepStatus.Running;
			}
			st.Detail = prep + "; first " + text + ", second " + text2;
			return (text == "applied" && text2 == "rejected:moved") ? StepStatus.Pass : StepStatus.Fail;
		}, delegate
		{
			int x;
			int y;
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.hiddenElement, i, i.unitCount, i.modifiedShape.orientation, out x, out y) && ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.showcaseElement, i, i.unitCount, i.modifiedShape.orientation, out y, out x)));
			if (val == null)
			{
				prep = "no item fits both the stash and the display case";
			}
			else
			{
				vId = Ids.Of(val);
				ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.hiddenElement, val, val.unitCount, val.modifiedShape.orientation, out ax, out ay);
				ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.showcaseElement, val, val.unitCount, val.modifiedShape.orientation, out bx, out by);
				prep = val.identifier + "#" + vId + " -> hidden and -> showcase in the same frame";
				q1 = ItemsMove.Move(val, (GraphNodeStorage)(object)ItemsBotBase.E.hiddenElement, ax, ay, val.modifiedShape.flipped, val.modifiedShape.orientation, -1, rep: false, "bot");
				q2 = ItemsMove.Move(val, (GraphNodeStorage)(object)ItemsBotBase.E.showcaseElement, bx, by, val.modifiedShape.flipped, val.modifiedShape.orientation, -1, rep: false, "bot");
			}
		});
		DigestSame("stale-digest");
		Act("counter-move", 20f, delegate
		{
			int x;
			int y;
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.backInvinvElement, i, i.unitCount, i.modifiedShape.orientation, out x, out y)));
			if (val == null)
			{
				return "SKIP no item fits the back counter";
			}
			cId = Ids.Of(val);
			ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.backInvinvElement, val, val.unitCount, val.modifiedShape.orientation, out tx, out ty);
			return val.identifier + "#" + cId + " -> back " + tx + "," + ty + " (no customer)";
		}, () => ItemsMove.Move(I(cId), (GraphNodeStorage)(object)ItemsBotBase.E.backInvinvElement, tx, ty, I(cId).modifiedShape.flipped, I(cId).modifiedShape.orientation, -1, rep: false, "bot"), delegate
		{
			GameItem obj = I(cId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.backInvinvElement;
		});
		DigestSame("counter-digest");
		Act("transfer", 20f, () => "back counter holds " + ((GameInventory)ItemsBotBase.E.backInvinvElement).childItems.Count + " item(s)", () => ItemsMove.TransferButton(frontVisible: false, "bot"), delegate
		{
			GameItem obj = I(cId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.invElement;
		});
		DigestSame("transfer-digest");
		Cmd("ask-replace", "replace");
		WaitFor("replaced", 20f, delegate
		{
			GameItem obj = I(bagId);
			return obj != null && obj.unitValue == 4321 && GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)I(bagId), (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).Any((GameItem i) => i.IsTag("BR_BOT_INNER"));
		}, delegate
		{
			GameItem obj = I(bagId);
			return "bag value " + ((obj != null) ? new long?(obj.unitValue) : ((long?)null)) + ", inner " + (I(bagId) != null && GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)I(bagId), (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).Any((GameItem i) => i.IsTag("BR_BOT_INNER")));
		});
		DigestSame("replace-digest");
		Cmd("ask-stackable", "stackable");
		WaitFor("stackable-here", 20f, () => ItemsBotBase.Tagged("BR_BOT_STK1") != null && ItemsBotBase.Tagged("BR_BOT_STK2") != null);
		Act("stack", 20f, delegate
		{
			s1 = Ids.Of(ItemsBotBase.Tagged("BR_BOT_STK1"));
			s2 = Ids.Of(ItemsBotBase.Tagged("BR_BOT_STK2"));
			return "shard#" + s1 + " x2 onto shard#" + s2 + " x3";
		}, () => ItemsMove.Stack(I(s1), I(s2), -1, "bot"), delegate
		{
			if (I(s1) == null)
			{
				GameItem obj = I(s2);
				if (obj == null)
				{
					return false;
				}
				return obj.unitCount == 5;
			}
			return false;
		}, delegate
		{
			string[] obj = new string[7]
			{
				"#",
				s2.ToString(),
				" now x",
				null,
				null,
				null,
				null
			};
			GameItem obj2 = I(s2);
			obj[3] = ((obj2 != null) ? new int?(obj2.unitCount) : ((int?)null)).ToString();
			obj[4] = ", #";
			obj[5] = s1.ToString();
			obj[6] = ((I(s1) == null) ? " gone" : " still here");
			return string.Concat(obj);
		});
		DigestSame("stack-digest");
		int ask = 0;
		float at = -10f;
		Step("race-wait-b", 300f, delegate(BotStep st)
		{
			if (ask == 0)
			{
				if (st.Elapsed - at < 2f)
				{
					return StepStatus.Running;
				}
				ask = BotProbes.AskServer();
				at = st.Elapsed;
				return StepStatus.Running;
			}
			Dictionary<string, string> dictionary = BotProbes.TakeReply(ask);
			if (dictionary == null)
			{
				return StepStatus.Running;
			}
			ask = 0;
			if (dictionary.TryGetValue("items.live", out var value) && int.TryParse(value, out var result) && result >= 2)
			{
				st.Detail = result + " players live on the server";
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		});
		Delay("race-b-polling", 4f);
		Cmd("race-arm", "race-arm");
		Delay("race-wait", 7f);
		Cmd("race-go", "race-go");
		int q3 = 0;
		long rid = 0L;
		int hx = 0;
		int hy = 0;
		Step("race", 30f, delegate(BotStep st)
		{
			if (rid == 0L)
			{
				GameItem val = ((IEnumerable<GameItem>)Items.All()).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => ItemPinHelper.IsPinned(i) && Ids.Of(i) != xId && (object)i.parentInventory == ItemsBotBase.E.invElement));
				if (val == null)
				{
					return StepStatus.Running;
				}
				rid = Ids.Of(val);
				if (!ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.invElement, val, val.unitCount, val.modifiedShape.orientation, out hx, out hy))
				{
					st.Detail = "no other spot in the main storage";
					return StepStatus.Fail;
				}
				q3 = ItemsMove.Move(val, (GraphNodeStorage)(object)ItemsBotBase.E.invElement, hx, hy, val.modifiedShape.flipped, val.modifiedShape.orientation, -1, rep: false, "bot-race");
				return StepStatus.Running;
			}
			string text = ItemsMove.Outcome(q3);
			if (text == "pending")
			{
				return StepStatus.Running;
			}
			GameItem val2 = Items.Find(rid);
			bool flag = val2 != null && (object)val2.parentInventory == ItemsBotBase.E.invElement && val2.modifiedShape.minX == hx && val2.modifiedShape.minY == hy;
			string text2 = ItemsMove.RejectTextOf(q3);
			st.Detail = "race item #" + rid + ": my move to inv " + hx + "," + hy + " " + text + ((text2 != null) ? (" (\"" + text2 + "\")") : "") + "; item now in " + Items.RefOf((GraphNodeStorage)(object)((val2 != null) ? val2.parentInventory : null))?.ToString() + (flag ? " (mine)" : "");
			if (text == "rejected:moved" && text2 != "Someone else moved that item first.")
			{
				return StepStatus.Fail;
			}
			return ((text == "applied" && flag) || (text.StartsWith("rejected", StringComparison.Ordinal) && !flag)) ? StepStatus.Pass : StepStatus.Fail;
		});
		DigestSame("race-digest", 30f);
		Check("no-mismatch", () => ItemsMove.ApplyMismatches == 0 && Items.TapeMismatches == 0, () => "applied " + ItemsMove.AppliedCount + ", mismatches " + ItemsMove.ApplyMismatches + ", tape mismatches " + Items.TapeMismatches + ", rejects " + ItemsMove.RejectCount);
		Cmd("tell-done", "done");
	}
}
