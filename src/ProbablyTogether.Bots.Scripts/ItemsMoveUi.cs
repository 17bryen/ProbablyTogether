using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ItemsMoveUi : ItemsBotBase
{
	private static readonly FieldInfo fCur = AccessTools.Field(typeof(ItemMouseDragHandler), "currentItem");

	private static readonly FieldInfo fLastItem = AccessTools.Field(typeof(ItemMouseDragHandler), "lastItem");

	private static readonly FieldInfo fLastInv = AccessTools.Field(typeof(ItemMouseDragHandler), "lastInventory");

	private static readonly FieldInfo fRemove = AccessTools.Field(typeof(ItemMouseDragHandler), "currentRemove");

	private static readonly FieldInfo fDragging = AccessTools.Field(typeof(ItemMouseDragHandler), "isDragging");

	private static readonly MethodInfo mEndDrag = AccessTools.Method(typeof(ItemMouseDragHandler), "EndDrag", (Type[])null, (Type[])null);

	private static readonly FieldInfo fSelected = AccessTools.Field(typeof(ItemMultiSelectHandler), "selectedItems");

	private static readonly MethodInfo mPrecise = AccessTools.Method(typeof(ItemMultiSelectHandler), "CommitPrecisePlacement", (Type[])null, (Type[])null);

	private long yId;

	private long zId;

	private long wId;

	private long xId;

	private long vId;

	private long cId;

	private long bagId;

	private long boxId;

	private List<long> pair = new List<long>();

	private int pdx;

	private int pdy;

	private Dictionary<long, (int, int)> pairFrom = new Dictionary<long, (int, int)>();

	public override string Name => "items-move-ui";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	private GameItem I(long id)
	{
		return Items.Find(id);
	}

	private BotStep Ui(string name, string kind, Func<string> act, Func<bool> applied)
	{
		int before = 0;
		int q = 0;
		int sent = 0;
		string detail = "";
		BotStep botStep = Step(name, 20f, delegate(BotStep st)
		{
			if (detail.StartsWith("SKIP", StringComparison.Ordinal) || detail.StartsWith("FAIL", StringComparison.Ordinal))
			{
				st.Detail = detail;
				return StepStatus.Fail;
			}
			if (sent != 1)
			{
				st.Detail = detail + "; " + sent + " request(s) sent (want 1 " + kind + ", last " + ItemsMove.LastSentKind + ")";
				return StepStatus.Fail;
			}
			string text = ItemsMove.Outcome(q);
			if (text.StartsWith("rejected", StringComparison.Ordinal))
			{
				st.Detail = detail + "; server " + text;
				return StepStatus.Fail;
			}
			if (text != "applied" || !applied())
			{
				return StepStatus.Running;
			}
			st.Detail = detail + "; one " + kind + " (request " + q + ") applied in " + st.Elapsed.ToString("F2", CultureInfo.InvariantCulture) + " s";
			return StepStatus.Pass;
		}, delegate
		{
			before = ItemsMove.SentCount;
			try
			{
				detail = act() ?? "";
			}
			catch (Exception ex)
			{
				Exception ex2 = ((ex is TargetInvocationException { InnerException: not null } ex3) ? ex3.InnerException : ex);
				detail = "game code threw after the hook: " + ex2.GetType().Name + ": " + ex2.Message;
			}
			sent = ItemsMove.SentCount - before;
			q = ItemsMove.LastSentQ;
			if (sent == 1 && ItemsMove.LastSentKind != kind)
			{
				sent = -1;
			}
		});
		botStep.TimeoutDetail = () => detail + "; request " + q + " outcome '" + ItemsMove.Outcome(q) + "'";
		return botStep;
	}

	private static string Same(GameItem it, GraphNodeStorage parentBefore)
	{
		if ((object)it.parentInventory != parentBefore)
		{
			return "FAIL moved locally before the server answered";
		}
		return "unchanged here before the answer";
	}

	protected override void Build()
	{
		JoinSteps();
		DigestSame("join-digest");
		Cmd("ask-spawn", "spawn");
		WaitFor("spawned", 30f, () => ItemsBotBase.Tagged("BR_BOT_BAG") != null && ItemsBotBase.Tagged("BR_BOT_BOX") != null, () => "bag " + (ItemsBotBase.Tagged("BR_BOT_BAG") != null));
		Do("spawn-ids", delegate
		{
			bagId = Ids.Of(ItemsBotBase.Tagged("BR_BOT_BAG"));
			boxId = Ids.Of(ItemsBotBase.Tagged("BR_BOT_BOX"));
			return "bag #" + bagId + ", box #" + boxId;
		});
		Ui("ui-drag-insert", "items.insertInto", delegate
		{
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			GameItem bag = I(bagId);
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => i is GameItemElement && bag.MayHaveValidInventorySlot(i) && bag.TryFindOneValidInventorySlot(i) != null));
			if ((Object)(object)current == (Object)null || val == null)
			{
				return "SKIP no drag handler or nothing fits the bag";
			}
			yId = Ids.Of(val);
			GameInventory parentInventory = val.parentInventory;
			fCur.SetValue(current, val);
			fLastItem.SetValue(current, bag);
			fLastInv.SetValue(current, null);
			fRemove.SetValue(current, val.unitCount);
			fDragging.SetValue(current, true);
			string text = "";
			try
			{
				mEndDrag.Invoke(current, null);
			}
			catch (Exception ex)
			{
				text = "; EndDrag's clean-up threw " + (ex.InnerException ?? ex).GetType().Name;
			}
			fDragging.SetValue(current, false);
			fCur.SetValue(current, null);
			fLastItem.SetValue(current, null);
			return val.identifier + "#" + yId + " dropped on bag#" + bagId + " by EndDrag: " + Same(val, (GraphNodeStorage)(object)parentInventory) + text;
		}, delegate
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
		DigestSame("drag-digest");
		Ui("ui-quick", "items.move", delegate
		{
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)delegate(GameItem i)
			{
				SlotMarker obj = ItemsBotBase.E.showcaseWindow.TryFindOneValidInventorySlot(i);
				return obj != null && obj.IsValid();
			});
			if (val == null)
			{
				return "SKIP nothing fits the display case";
			}
			zId = Ids.Of(val);
			GameInventory parentInventory = val.parentInventory;
			ItemsCapture.Ctx c;
			bool entered = ItemsCapture.Enter("quick", out c);
			int num;
			try
			{
				num = GraphUtils.TryAcceptOnce((GraphNodeStorage)(object)ItemsBotBase.E.showcaseWindow, val, -1);
			}
			finally
			{
				ItemsCapture.Exit(entered);
			}
			return val.identifier + "#" + zId + " quick -> showcase.w (left " + num + "): " + Same(val, (GraphNodeStorage)(object)parentInventory);
		}, delegate
		{
			GameItem obj = I(zId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.showcaseElement;
		});
		DigestSame("quick-digest");
		Ui("ui-group-all", "items.quickMove", delegate
		{
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => ItemsBotBase.E.hiddenElement.TryFindOneValidInventorySlot(i, false) != null));
			if (val == null)
			{
				return "SKIP nothing fits the stash";
			}
			wId = Ids.Of(val);
			GameInventory parentInventory = val.parentInventory;
			ItemsCapture.Ctx c;
			bool entered = ItemsCapture.Enter("group", out c);
			try
			{
				GraphUtils.TryAcceptAll((GraphNodeStorage)(object)ItemsBotBase.E.hiddenElement, val, -1);
			}
			finally
			{
				ItemsCapture.Exit(entered);
			}
			return val.identifier + "#" + wId + " group -> hidden: " + Same(val, (GraphNodeStorage)(object)parentInventory);
		}, delegate
		{
			GameItem obj = I(wId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.hiddenElement;
		});
		DigestSame("group-all-digest");
		Ui("ui-group-precise", "items.groupMove", delegate
		{
			ItemMultiSelectHandler current = ItemMultiSelectHandler.current;
			if ((Object)(object)current == (Object)null)
			{
				return "SKIP no multi-select handler";
			}
			List<GameItem> list = ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement, (GameItem i) => i is GameItemElement);
			for (int num = 0; num < list.Count; num++)
			{
				for (int num2 = num + 1; num2 < list.Count; num2++)
				{
					(int, int)[] array = new(int, int)[6]
					{
						(1, 0),
						(-1, 0),
						(0, 1),
						(0, -1),
						(2, 0),
						(0, 2)
					};
					for (int num3 = 0; num3 < array.Length; num3++)
					{
						(int, int) tuple = array[num3];
						List<GameItem> list2 = new List<GameItem>
						{
							list[num],
							list[num2]
						};
						if (ItemsMove.FormationProblem(list2, (GameInventory)(object)ItemsBotBase.E.invElement, tuple.Item1, tuple.Item2) == null)
						{
							pair = list2.Select(Ids.Of).ToList();
							pdx = tuple.Item1;
							pdy = tuple.Item2;
							pairFrom = list2.ToDictionary(Ids.Of, (GameItem i) => (minX: i.modifiedShape.minX, minY: i.modifiedShape.minY));
							List<GameItemElement> list3 = (List<GameItemElement>)fSelected.GetValue(current);
							list3.Clear();
							list3.Add((GameItemElement)list[num]);
							list3.Add((GameItemElement)list[num2]);
							try
							{
								mPrecise.Invoke(current, new object[3]
								{
									ItemsBotBase.E.invElement,
									pdx,
									pdy
								});
							}
							finally
							{
								list3.Clear();
							}
							bool flag = list2.All((GameItem i) => i.modifiedShape.minX == pairFrom[Ids.Of(i)].Item1 && i.modifiedShape.minY == pairFrom[Ids.Of(i)].Item2);
							return string.Join("+", list2.Select((GameItem i) => i.identifier + "#" + Ids.Of(i))) + " by " + pdx + "," + pdy + ": " + (flag ? "unchanged here before the answer" : "FAIL moved locally");
						}
					}
				}
			}
			return "SKIP no pair can shift";
		}, () => pair.All((long id) => I(id) != null && I(id).modifiedShape.minX == pairFrom[id].Item1 + pdx && I(id).modifiedShape.minY == pairFrom[id].Item2 + pdy));
		DigestSame("group-precise-digest");
		Ui("ui-sort", "items.sort", delegate
		{
			string text = ItemsMove.Digest();
			return "Sort returned " + InventorySortHelper.Sort(ItemsBotBase.E.invElement, false, true) + ": " + ((ItemsMove.Digest() == text) ? "unchanged here before the answer" : "FAIL sorted locally");
		}, () => true);
		DigestSame("sort-digest");
		Ui("ui-undo", "items.sortUndo", delegate
		{
			string text = ItemsMove.Digest();
			return "Undo returned " + InventorySortHelper.Undo(ItemsBotBase.E.invElement) + ": " + ((ItemsMove.Digest() == text) ? "unchanged here before the answer" : "FAIL undone locally");
		}, () => true);
		DigestSame("undo-digest");
		Ui("ui-unload", "items.unloadAll", delegate
		{
			GameItem val = I(yId);
			GameInventory parentBefore = ((val != null) ? val.parentInventory : null);
			ItemsCapture.Ctx c;
			bool entered = ItemsCapture.Enter("menu", out c);
			try
			{
				I(bagId).TryUnloadAll(true);
			}
			finally
			{
				ItemsCapture.Exit(entered);
			}
			return "bag#" + bagId + " unload: " + ((val != null) ? Same(val, (GraphNodeStorage)(object)parentBefore) : "?");
		}, delegate
		{
			if (I(yId) != null)
			{
				GameInventory parentInventory = I(yId).parentInventory;
				return ((parentInventory != null) ? parentInventory.GetParentItem() : null) != I(bagId);
			}
			return false;
		});
		DigestSame("unload-digest");
		Ui("ui-pin", "items.pin", delegate
		{
			GameItem val = ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement).FirstOrDefault();
			if (val == null)
			{
				return "SKIP nothing to pin";
			}
			xId = Ids.Of(val);
			bool flag = ItemPinHelper.TogglePinned(val);
			return val.identifier + "#" + xId + " TogglePinned returned " + flag + ": " + (ItemPinHelper.IsPinned(val) ? "FAIL pinned locally" : "unchanged here before the answer");
		}, () => ItemPinHelper.IsPinned(I(xId)));
		DigestSame("pin-digest");
		Ui("ui-trash", "items.trash", delegate
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
			GameInventory trash = (GameInventory)((obj is GameInventory) ? obj : null);
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)delegate(GameItem i)
			{
				GameInventory obj2 = trash;
				return ((obj2 != null) ? obj2.TryFindOneValidInventorySlot(i) : null) != null;
			});
			if (val == null)
			{
				return "SKIP nothing fits the trash can";
			}
			vId = Ids.Of(val);
			GameInventory parentInventory = val.parentInventory;
			ItemsCapture.Ctx c;
			bool entered = ItemsCapture.Enter("trash", out c);
			int num;
			try
			{
				num = GraphUtils.TryAcceptAll((GraphNodeStorage)(object)trash, val, -1);
			}
			finally
			{
				ItemsCapture.Exit(entered);
			}
			return val.identifier + "#" + vId + " trash (left " + num + "): " + Same(val, (GraphNodeStorage)(object)parentInventory);
		}, delegate
		{
			GameItem obj = I(vId);
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
		Ui("ui-open", "items.markOpened", delegate
		{
			ItemMouseDoubleClickHandler current = ItemMouseDoubleClickHandler.current;
			GameItem val = I(boxId);
			if ((Object)(object)current == (Object)null || val == null)
			{
				return "SKIP no double-click handler or box";
			}
			current.OpenContentAction(val, Vector2.zero);
			try
			{
				val.CloseContentWindow(true);
			}
			catch
			{
			}
			return "box#" + boxId + " opened by OpenContentAction";
		}, delegate
		{
			GameItem obj = I(boxId);
			return obj != null && obj.IsTag("OPENED_TAG");
		});
		DigestSame("open-digest");
		Act("ui-button-setup", 20f, delegate
		{
			int x;
			int y;
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.backInvinvElement, i, i.unitCount, i.modifiedShape.orientation, out x, out y)));
			if (val == null)
			{
				return "SKIP nothing fits the back counter";
			}
			cId = Ids.Of(val);
			return val.identifier + "#" + cId + " -> back counter";
		}, delegate
		{
			GameItem val = I(cId);
			ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.backInvinvElement, val, val.unitCount, val.modifiedShape.orientation, out var x, out var y);
			return ItemsMove.Move(val, (GraphNodeStorage)(object)ItemsBotBase.E.backInvinvElement, x, y, val.modifiedShape.flipped, val.modifiedShape.orientation, -1, rep: false, "bot");
		}, delegate
		{
			GameItem obj = I(cId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.backInvinvElement;
		});
		Ui("ui-button", "items.transferButton", delegate
		{
			StoreTransferButton val = Object.FindObjectOfType<StoreTransferButton>();
			if ((Object)(object)val == (Object)null)
			{
				return "SKIP no transfer button in the scene";
			}
			GameInventory parentInventory = I(cId).parentInventory;
			val.OnClick();
			return "StoreTransferButton.OnClick: " + Same(I(cId), (GraphNodeStorage)(object)parentInventory);
		}, delegate
		{
			GameItem obj = I(cId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.invElement;
		});
		DigestSame("button-digest");
		Check("no-mismatch", () => ItemsMove.ApplyMismatches == 0 && Items.TapeMismatches == 0, () => "sent " + ItemsMove.SentCount + ", applied " + ItemsMove.AppliedCount + ", mismatches " + ItemsMove.ApplyMismatches + ", tape mismatches " + Items.TapeMismatches + ", rejects " + ItemsMove.RejectCount);
		Cmd("tell-done", "done");
	}
}
