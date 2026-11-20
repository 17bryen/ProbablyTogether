using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CarryA : CarryBotBase
{
	private long id1;

	private long id2;

	private long id3;

	private long bagId;

	private long stackId;

	private int units1;

	private int units2;

	private int refusedQ;

	private int sentBefore;

	public override string Name => "carry-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	private string StartDrag(long id, out int units)
	{
		ItemMouseDragHandler current = ItemMouseDragHandler.current;
		GameItem val = CarryBotBase.I(id);
		if ((Object)(object)current == (Object)null || val == null)
		{
			throw new Exception("no drag handler or item #" + id + " gone");
		}
		if (current.IsDraggingItem)
		{
			throw new Exception("the handler already drags something");
		}
		int startsByHook = Carry.StartsByHook;
		CarryBotBase.fCur.SetValue(current, val);
		CarryBotBase.mStart.Invoke(current, null);
		if (!current.IsDraggingItem)
		{
			throw new Exception("the game's StartDrag did not start a drag on " + val.identifier + "#" + id);
		}
		units = ((CarryBotBase.fRemove.GetValue(current) is int num) ? num : 0);
		if (Carry.StartsByHook != startsByHook + 1 || Carry.LocalItem != id || Carry.LocalIsGroup)
		{
			throw new Exception("the start hook did not run (starts by hook " + Carry.StartsByHook + ", local item " + Carry.LocalItem + ")");
		}
		return val.identifier + "#" + id + " picked up by the game's StartDrag (" + units + " unit(s)); start hook ran, serial " + Carry.LocalSerial;
	}

	private string EndDrag(GameItem ontoItem, out int sent)
	{
		ItemMouseDragHandler current = ItemMouseDragHandler.current;
		int sentCount = ItemsMove.SentCount;
		int endsByHook = Carry.EndsByHook;
		CarryBotBase.fLastItem.SetValue(current, ontoItem);
		CarryBotBase.fLastInv.SetValue(current, null);
		string text = "";
		try
		{
			BotDrag.End(current);
		}
		catch (Exception ex)
		{
			text = "; EndDrag's clean-up threw " + (ex.InnerException ?? ex).GetType().Name;
		}
		if ((bool)CarryBotBase.fDragging.GetValue(current))
		{
			CarryBotBase.fDragging.SetValue(current, false);
		}
		CarryBotBase.fCur.SetValue(current, null);
		CarryBotBase.fLastItem.SetValue(current, null);
		sent = ItemsMove.SentCount - sentCount;
		return "EndDrag sent " + sent + " request(s) (" + ItemsMove.LastSentKind + "); drop hook ran " + (Carry.EndsByHook == endsByHook + 1) + ", local item now " + Carry.LocalItem + text;
	}

	private BotStep ServerRelays(string name, Func<long> id, bool want)
	{
		return WaitServer(name, 15f, delegate(IReadOnlyDictionary<string, string> v)
		{
			v.TryGetValue("carry.srv", out var value);
			string text = (value ?? "").Split(';').FirstOrDefault((string x) => x.StartsWith("p" + Net.LocalPlayer + ":", StringComparison.Ordinal)) ?? "";
			return (!want) ? text.Contains(" out=0/") : text.Contains(" out=" + id() + "/");
		}, "carry.srv");
	}

	protected override void Build()
	{
		JoinAndMeet();
		Do("ask-bag", delegate
		{
			Net.ToServer("carry.bot", new CarryBotMsg
			{
				op = "spawn"
			});
			return "asked the server for the test bag";
		});
		WaitFor("bag", 30f, () => CarryBotBase.Bag != null && ItemsBotBase.Tagged("BR_CARRY_STACK") != null, () => "bag " + (CarryBotBase.Bag != null) + ", stack " + (ItemsBotBase.Tagged("BR_CARRY_STACK") != null));
		Do("pick", delegate
		{
			GameItem bag = CarryBotBase.Bag;
			bagId = Ids.Of(bag);
			stackId = Ids.Of(ItemsBotBase.Tagged("BR_CARRY_STACK"));
			List<GameItem> list = ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement, (GameItem i) => i is GameItemElement && Ids.Of(i) > 0 && Ids.Of(i) != bagId && Ids.Of(i) != stackId).OrderBy(Ids.Of).ToList();
			GameItem val = ((IEnumerable<GameItem>)list).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => bag.MayHaveValidInventorySlot(i) && bag.TryFindOneValidInventorySlot(i) != null));
			if (val == null || list.Count < 2)
			{
				throw new Exception(list.Count + " movable item(s) in the main storage, " + ((val != null) ? "one" : "none") + " fits the bag");
			}
			id1 = Ids.Of(val);
			List<GameItem> list2 = list.Where((GameItem i) => Ids.Of(i) != id1).ToList();
			id2 = Ids.Of(list2[0]);
			id3 = ((list2.Count > 1) ? Ids.Of(list2[1]) : 0);
			return "bag #" + bagId + "; drag 1 " + val.identifier + "#" + id1 + " (fits the bag), drag 2-4 " + list2[0].identifier + "#" + id2 + ", group partner " + ((id3 != 0L) ? ("#" + id3) : "none");
		});
		Do("drag1-start", () => StartDrag(id1, out units1));
		ServerRelays("drag1-relayed", () => id1, want: true);
		Mark("drag1-mark", () => "a-drag1:" + id1 + ":" + units1);
		WaitMark("drag1-seen", 30f, "b-saw1");
		string value;
		Step("drag1-drop", 15f, delegate(BotStep st)
		{
			if (Vars.TryGetValue("drop1", out value) && value.StartsWith("FAIL", StringComparison.Ordinal))
			{
				st.Detail = value;
				return StepStatus.Fail;
			}
			string text = ItemsMove.Outcome(ItemsMove.LastSentQ);
			if (text.StartsWith("rejected", StringComparison.Ordinal))
			{
				st.Detail = Vars["drop1"] + "; server " + text;
				return StepStatus.Fail;
			}
			if (!(text != "applied"))
			{
				GameItem obj = CarryBotBase.I(id1);
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
				if (obj2 == CarryBotBase.I(bagId))
				{
					st.Detail = Vars["drop1"] + "; applied, #" + id1 + " is in the bag";
					return StepStatus.Pass;
				}
			}
			return StepStatus.Running;
		}, delegate
		{
			int sent;
			string text = EndDrag(CarryBotBase.I(bagId), out sent);
			Vars["drop1"] = ((sent == 1 && ItemsMove.LastSentKind == "items.insertInto" && Carry.LocalItem == 0L) ? text : ("FAIL " + text));
		});
		ServerRelays("drag1-cleared-srv", () => 0L, want: false);
		Mark("drop1-mark", () => "a-drop1:" + id1);
		WaitMark("drop1-seen", 30f, "b-clear1");
		Do("drag2-start", () => StartDrag(id2, out units2));
		ServerRelays("drag2-relayed", () => id2, want: true);
		Mark("drag2-mark", () => "a-drag2:" + id2 + ":" + units2);
		WaitMark("drag2-seen", 30f, "b-saw2");
		Step("drag2-refused", 15f, delegate(BotStep st)
		{
			string text = ItemsMove.Outcome(refusedQ);
			if (text == "pending" || text == "")
			{
				return StepStatus.Running;
			}
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			bool flag = (Object)(object)current != (Object)null && current.IsDraggingItem && Carry.LocalItem == id2;
			st.Detail = "stale move of #" + id2 + " (request " + refusedQ + "): " + text + "; still held here " + flag;
			return (text == "rejected:moved" && flag) ? StepStatus.Pass : StepStatus.Fail;
		}, delegate
		{
			GameItem val = CarryBotBase.I(id2);
			GridShape modifiedShape = val.modifiedShape;
			refusedQ = ItemsMove.Send("items.move", new ItemsReq
			{
				i = id2,
				n = 0,
				src = Items.RefOf((GraphNodeStorage)(object)val.parentInventory),
				sx = modifiedShape.minX + 50,
				sy = modifiedShape.minY,
				to = Items.RefOf((GraphNodeStorage)(object)val.parentInventory),
				x = modifiedShape.minX,
				y = modifiedShape.minY,
				f = modifiedShape.flipped,
				rot = modifiedShape.orientation,
				via = "bot"
			});
		});
		WaitServer("drag2-hidden-srv", 15f, (IReadOnlyDictionary<string, string> v) => v.TryGetValue("carry.srv", out value) && (value ?? "").Split(';').Any((string x) => x.StartsWith("p" + Net.LocalPlayer + ":", StringComparison.Ordinal) && x.Contains("in=" + id2 + "/") && x.Contains(" out=0/") && x.Contains("hide=" + id2)), "carry.srv");
		Mark("refused2-mark", () => "a-refused2:" + id2);
		WaitMark("refused2-seen", 40f, "b-clear2");
		Check("drag2-still-held", () => ItemMouseDragHandler.current.IsDraggingItem && Carry.LocalItem == id2, () => "held " + ItemMouseDragHandler.current.IsDraggingItem + ", local item " + Carry.LocalItem);
		Do("drag2-drop-nothing", delegate
		{
			int sent;
			string text = EndDrag(null, out sent);
			if (sent != 0 || Carry.LocalItem != 0L)
			{
				throw new Exception(text);
			}
			return text;
		});
		WaitServer("drag2-unhidden-srv", 15f, (IReadOnlyDictionary<string, string> v) => v.TryGetValue("carry.srv", out value) && (value ?? "").Split(';').Any((string x) => x.StartsWith("p" + Net.LocalPlayer + ":", StringComparison.Ordinal) && x.Contains("hide=0")), "carry.srv");
		Do("drag3-start", () => StartDrag(id2, out units2));
		ServerRelays("drag3-relayed", () => id2, want: true);
		Mark("drag3-mark", () => "a-drag3:" + id2 + ":" + units2);
		WaitMark("drag3-seen", 30f, "b-saw3");
		Do("drag3-cancel", delegate
		{
			sentBefore = ItemsMove.SentCount;
			int endsByHook = Carry.EndsByHook;
			bool flag = ItemMouseDragHandler.current.Cancel();
			if (ItemsMove.SentCount != sentBefore || Carry.LocalItem != 0L)
			{
				throw new Exception("Cancel " + flag + ", sent " + (ItemsMove.SentCount - sentBefore) + ", local item " + Carry.LocalItem);
			}
			return "the game's Cancel() " + flag + ": nothing sent, drop hook ran " + (Carry.EndsByHook == endsByHook + 1);
		});
		Mark("cancel3-mark", () => "a-cancel3:" + id2);
		WaitMark("cancel3-seen", 30f, "b-clear3");
		Do("drag4-start", () => StartDrag(id2, out units2));
		ServerRelays("drag4-relayed", () => id2, want: true);
		Mark("drag4-mark", () => "a-drag4:" + id2 + ":" + units2);
		WaitMark("drag4-seen", 30f, "b-saw4");
		int pollEnds = 0;
		Step("drag4-abort", 5f, delegate(BotStep st)
		{
			if (Carry.LocalItem != 0L)
			{
				return StepStatus.Running;
			}
			st.Detail = "AbortDrag ran; the cursor poll ended the carry after " + CarryBotBase.F(st.Elapsed) + " s (poll ends " + pollEnds + " -> " + Carry.EndsByPoll + "), sent " + (ItemsMove.SentCount - sentBefore);
			return (Carry.EndsByPoll == pollEnds + 1 && ItemsMove.SentCount == sentBefore) ? StepStatus.Pass : StepStatus.Fail;
		}, delegate
		{
			pollEnds = Carry.EndsByPoll;
			sentBefore = ItemsMove.SentCount;
			CarryBotBase.mAbort.Invoke(ItemMouseDragHandler.current, null);
		});
		Mark("abort4-mark", () => "a-abort4:" + id2);
		WaitMark("abort4-seen", 30f, "b-clear4");
		int stackUnits = 0;
		Do("drag5-start", delegate
		{
			string text = StartDrag(stackId, out stackUnits);
			if (stackUnits != 3)
			{
				throw new Exception(text + ": want 3 units in hand");
			}
			return text;
		});
		ServerRelays("drag5-relayed", () => stackId, want: true);
		Mark("drag5-mark", () => "a-drag5:" + stackId + ":3");
		WaitMark("drag5-seen", 30f, "b-saw5");
		Do("drag5-fewer", delegate
		{
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			CarryBotBase.fRemove.SetValue(current, 2);
			((GameItemElement)CarryBotBase.I(stackId)).UpdateFakeReal(2);
			return "2 of the 3 units in hand (as the mouse wheel sets it)";
		});
		Mark("fewer5-mark", () => "a-fewer5:" + stackId + ":2");
		WaitMark("fewer5-seen", 30f, "b-fewer5");
		Do("drag5-drop-nothing", delegate
		{
			int sent;
			string text = EndDrag(null, out sent);
			if (sent != 0 || Carry.LocalItem != 0L)
			{
				throw new Exception(text);
			}
			return text;
		});
		Mark("end5-mark", () => "a-end5:" + stackId);
		WaitMark("end5-seen", 30f, "b-clear5");
		Step("group-start", 10f, delegate(BotStep st)
		{
			if (Vars.TryGetValue("group", out value) && value.StartsWith("FAIL", StringComparison.Ordinal))
			{
				st.Detail = value;
				return StepStatus.Fail;
			}
			ItemMultiSelectHandler current = ItemMultiSelectHandler.current;
			if (Carry.LocalItem != 0L && current.IsGroupDragging)
			{
				return StepStatus.Running;
			}
			if (Carry.LocalItem != 0L)
			{
				current.Cancel();
			}
			st.Detail = Vars["group"] + "; ended after " + CarryBotBase.F(st.Elapsed) + " s (" + Carry.LastEnd + ")";
			return (Carry.LocalItem == 0L) ? StepStatus.Pass : StepStatus.Running;
		}, delegate
		{
			ItemMultiSelectHandler current = ItemMultiSelectHandler.current;
			if (id3 == 0L)
			{
				Vars["group"] = "FAIL only one movable item left: no group to drag";
			}
			else
			{
				List<GameItemElement> obj = (List<GameItemElement>)CarryBotBase.fSelected.GetValue(current);
				obj.Clear();
				obj.Add((GameItemElement)CarryBotBase.I(id2));
				obj.Add((GameItemElement)CarryBotBase.I(id3));
				int startsByHook = Carry.StartsByHook;
				CarryBotBase.mGroupStart.Invoke(current, null);
				MsgCursor msgCursor = new MsgCursor();
				Carry.FillLocal(msgCursor);
				bool flag = current.IsGroupDragging && Carry.StartsByHook == startsByHook + 1 && Carry.LocalIsGroup && msgCursor.d == id2 && msgCursor.g == 1;
				Vars["group"] = (flag ? "" : "FAIL ") + "StartGroupDrag on #" + id2 + "+#" + id3 + ": dragging " + current.IsGroupDragging + ", hook " + (Carry.StartsByHook - startsByHook) + ", frame d=" + msgCursor.d + " n=" + msgCursor.n + " g=" + msgCursor.g;
			}
		}).Soft();
		Do("done", delegate
		{
			Presence.FakePointer = null;
			return "starts by hook " + Carry.StartsByHook + ", by poll " + Carry.StartsByPoll + ", ends by hook " + Carry.EndsByHook + ", by poll " + Carry.EndsByPoll;
		});
		Mark("done-mark", () => "a-done");
	}
}
