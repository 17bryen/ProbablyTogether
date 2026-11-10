using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ItemsMove3B : ItemsBot3Base
{
	private static readonly FieldInfo fCur = AccessTools.Field(typeof(ItemMouseDragHandler), "currentItem");

	private static readonly FieldInfo fRemove = AccessTools.Field(typeof(ItemMouseDragHandler), "currentRemove");

	private static readonly FieldInfo fDragging = AccessTools.Field(typeof(ItemMouseDragHandler), "isDragging");

	private static readonly MethodInfo mStart = AccessTools.Method(typeof(ItemMouseDragHandler), "StartDrag", (Type[])null, (Type[])null);

	private long dId;

	private long bId;

	private long kId;

	private GameInventory dFrom;

	private int sentBefore;

	private int a;

	private int tx;

	private int ty;

	private string aName = "";

	public override string Name => "items-move3-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 1400f;

	private GameItem I(long id)
	{
		return Items.Find(id);
	}

	protected override void Build()
	{
		JoinSteps();
		Quiet();
		DigestSame("join-digest");
		WaitServer("a-roots-done", 180f, (Dictionary<string, string> s) => ItemsBot3Base.Int(s["items3.marks"]) >= 1, "items3.marks");
		Do("start-drag", delegate
		{
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			int x;
			int y;
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement, (GameItem i) => i is GameItemElement)).LastOrDefault((Func<GameItem, bool>)((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.showcaseElement, i, i.unitCount, i.modifiedShape.orientation, out x, out y) || ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.hiddenElement, i, i.unitCount, i.modifiedShape.orientation, out y, out x)));
			if ((Object)(object)current == (Object)null || val == null)
			{
				return "FAIL no drag handler or no item";
			}
			dId = Ids.Of(val);
			dFrom = val.parentInventory;
			string text = "StartDrag";
			fCur.SetValue(current, val);
			try
			{
				mStart.Invoke(current, null);
			}
			catch (Exception ex)
			{
				text = "StartDrag threw " + (ex.InnerException ?? ex).GetType().Name;
			}
			if (!current.IsDraggingItem)
			{
				fRemove.SetValue(current, val.unitCount);
				fDragging.SetValue(current, true);
				text += "; drag state set by hand";
			}
			sentBefore = ItemsMove.SentCount;
			Net.ToServer("items.bot3", new ItemsBot3Cmd
			{
				cmd = "drag",
				id = dId
			});
			return val.identifier + "#" + dId + " dragging here (" + text + "): IsDraggingItem " + current.IsDraggingItem;
		});
		Check("dragging", () => (Object)(object)ItemMouseDragHandler.current != (Object)null && ItemMouseDragHandler.current.IsDraggingItem && ItemMouseDragHandler.current.GetCurrentItem() == I(dId), delegate
		{
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			return "IsDraggingItem " + ((current != null) ? new bool?(current.IsDraggingItem) : ((bool?)null));
		});
		WaitFor("moved-by-a", 60f, () => I(dId) != null && I(dId).parentInventory != dFrom, delegate
		{
			string text = dId.ToString();
			GameItem obj = I(dId);
			return "#" + text + " still in " + Items.RefOf((GraphNodeStorage)(object)((obj != null) ? obj.parentInventory : null));
		});
		Check("drag-cancelled", () => !ItemMouseDragHandler.current.IsDraggingItem, delegate
		{
			string text = ItemMouseDragHandler.current.IsDraggingItem.ToString();
			GameItem obj = I(dId);
			return "IsDraggingItem " + text + "; item now in " + Items.RefOf((GraphNodeStorage)(object)((obj != null) ? obj.parentInventory : null));
		});
		Check("no-stray-request", () => ItemsMove.SentCount == sentBefore, delegate
		{
			string[] obj = new string[6]
			{
				"requests sent while the drag was cancelled: ",
				(ItemsMove.SentCount - sentBefore).ToString(),
				" (last ",
				ItemsMove.LastSentKind,
				"); item now in ",
				null
			};
			GameItem obj2 = I(dId);
			obj[5] = Items.RefOf((GraphNodeStorage)(object)((obj2 != null) ? obj2.parentInventory : null))?.ToString();
			return string.Concat(obj);
		});
		DigestSame("drag-digest");
		WaitServer("a-holds-counter", 300f, (Dictionary<string, string> s) => ItemsBot3Base.Int(s["items3.marks"]) >= 2, "items3.marks", "cust.current", "cust.holder");
		WaitFor("holder-here", 15f, () => Locks.HeldBy("counter") > 0 && Locks.HeldBy("counter") != Net.LocalPlayer && ItemsBot3Base.Cur() != "none", () => "holder " + Locks.HeldBy("counter") + ", me " + Net.LocalPlayer + ", customer " + ItemsBot3Base.Cur());
		Do("note", delegate
		{
			a = Locks.HeldBy("counter");
			aName = Net.PlayerName(a);
			return "A = player " + a + " (" + aName + "), customer " + ItemsBot3Base.Cur();
		});
		Step("refused-here", 5f, delegate(BotStep st)
		{
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.backInvinvElement, i, i.unitCount, i.modifiedShape.orientation, out tx, out ty)));
			if (val == null)
			{
				st.Detail = "no item fits the back counter";
				return StepStatus.Fail;
			}
			kId = Ids.Of(val);
			int sentCount = ItemsMove.SentCount;
			int num = ItemsMove.Move(val, (GraphNodeStorage)(object)ItemsBotBase.E.backInvinvElement, tx, ty, val.modifiedShape.flipped, val.modifiedShape.orientation, -1, rep: false, "bot");
			string text = aName + " is at the counter.";
			st.Detail = val.identifier + "#" + kId + " -> back counter: request " + num + ", sent " + (ItemsMove.SentCount - sentCount) + ", notice '" + ItemsMove.LastNotice + "'";
			return (num == 0 && ItemsMove.SentCount == sentCount && ItemsMove.LastNotice == text) ? StepStatus.Pass : StepStatus.Fail;
		});
		Do("skip-own-check", delegate
		{
			ItemsMove.LabSkipClientCheck = true;
			return "lab: this copy's counter check off";
		});
		ActRejected("refused-by-server", 20f, () => "#" + kId + " -> back counter " + tx + "," + ty + " sent anyway", () => ItemsMove.Move(I(kId), (GraphNodeStorage)(object)ItemsBotBase.E.backInvinvElement, tx, ty, I(kId).modifiedShape.flipped, I(kId).modifiedShape.orientation, -1, rep: false, "bot"), "counter");
		Do("own-check-on", delegate
		{
			ItemsMove.LabSkipClientCheck = false;
			return "this copy's counter check on again";
		});
		Check("server-notice", () => ItemsMove.LastReject?.text == aName + " is at the counter." && ItemsMove.LastNotice == aName + " is at the counter.", () => "reject '" + ItemsMove.LastReject?.why + "' text '" + ItemsMove.LastReject?.text + "', notice '" + ItemsMove.LastNotice + "'");
		Check("not-moved", delegate
		{
			GameItem obj = I(kId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.invElement;
		}, delegate
		{
			string text = kId.ToString();
			GameItem obj = I(kId);
			return "#" + text + " in " + Items.RefOf((GraphNodeStorage)(object)((obj != null) ? obj.parentInventory : null));
		});
		Act("backroom-free", 20f, delegate
		{
			int x;
			int y;
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.hiddenElement, i, i.unitCount, i.modifiedShape.orientation, out x, out y))) ?? ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.showcaseElement, i, i.unitCount, i.modifiedShape.orientation, out x, out y)));
			if (val == null)
			{
				return "SKIP nothing to move in the back room";
			}
			bId = Ids.Of(val);
			return val.identifier + "#" + bId + " -> stash or display case while " + aName + " holds the counter";
		}, delegate
		{
			GameItem val = I(bId);
			int x;
			int y;
			GameInventory val2 = (GameInventory)(object)(ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.hiddenElement, val, val.unitCount, val.modifiedShape.orientation, out x, out y) ? ItemsBotBase.E.hiddenElement : ItemsBotBase.E.showcaseElement);
			if ((object)val2 == ItemsBotBase.E.showcaseElement)
			{
				ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.showcaseElement, val, val.unitCount, val.modifiedShape.orientation, out x, out y);
			}
			return ItemsMove.Move(val, (GraphNodeStorage)(object)val2, x, y, val.modifiedShape.flipped, val.modifiedShape.orientation, -1, rep: false, "bot");
		}, delegate
		{
			GameItem obj = I(bId);
			if ((object)((obj != null) ? obj.parentInventory : null) != ItemsBotBase.E.hiddenElement)
			{
				GameItem obj2 = I(bId);
				return (object)((obj2 != null) ? obj2.parentInventory : null) == ItemsBotBase.E.showcaseElement;
			}
			return true;
		});
		DigestSame("refused-digest");
		Cmd3("mark-3", "mark", 0L);
		WaitServer("a-done", 900f, (Dictionary<string, string> s) => ItemsBot3Base.Int(s["items3.done"]) >= 1, "items3.done");
		DigestSame("final-digest", 30f);
		Same("final-same", 20f, "items3.sec", "items3.nego", "cust.current", "cust.digest");
		Cmd3("tell-done", "done", 0L);
		Same("final-dialogue", 20f, "items3.dlg").Soft();
		NoMismatch("no-mismatch");
		int randBefore = 0;
		int appliedBefore = 0;
		Do("resync", delegate
		{
			randBefore = ItemsMove.RandomMismatches;
			appliedBefore = ItemsMove.AppliedCount;
			Snapshot.RequestResync();
			return "asked for the morning snapshot + today's log (items actions applied so far " + appliedBefore + ")";
		});
		WaitFor("reloading", 30f, () => Net.ClientState != ClientState.Ready, () => "state " + Net.ClientState);
		WaitFor("reloaded", 150f, () => Net.IsLive && Items.Ready, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
		Delay("replay-settle", 2f);
		DigestSame("replay-digest", 30f);
		Same("replay-heat", 20f, "items3.heat", "items3.nego");
		Same("replay-customer", 20f, "cust.current", "cust.digest", "items3.sec").Soft();
		Check("replay-rolls", () => ItemsMove.RandomMismatches == randBefore && ItemsMove.ApplyMismatches == 0 && Items.TapeMismatches == 0, () => "items actions replayed " + (ItemsMove.AppliedCount - appliedBefore) + "; random mismatches " + randBefore + " -> " + ItemsMove.RandomMismatches + ", apply mismatches " + ItemsMove.ApplyMismatches + ", tape " + Items.TapeMismatches + "; heat " + ItemsBot3Base.Mine("items3.heat"));
	}
}
