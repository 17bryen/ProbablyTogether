using System;
using System.Reflection;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PicsFeatLead : PicsFeatBase
{
	private static readonly FieldInfo fCur = AccessTools.Field(typeof(ItemMouseDragHandler), "currentItem");

	private static readonly FieldInfo fLastItem = AccessTools.Field(typeof(ItemMouseDragHandler), "lastItem");

	private static readonly FieldInfo fLastInv = AccessTools.Field(typeof(ItemMouseDragHandler), "lastInventory");

	private static readonly FieldInfo fDragging = AccessTools.Field(typeof(ItemMouseDragHandler), "isDragging");

	private static readonly MethodInfo mStart = AccessTools.Method(typeof(ItemMouseDragHandler), "StartDrag", new Type[0], (Type[])null);

	private static readonly MethodInfo mEnd = BotDrag.EndDrag;

	private long pointed;

	private long carried;

	public override string Name => "pics-feat-lead";

	protected override void Build()
	{
		Join();
		WaitMark("w-fshop", "pics-fshop-seen", 120f);
		Do("talk", delegate
		{
			Voice.FakeToneHz = 440f;
			Voice.FakeTalk = true;
			return "talk switch held (lab tone 440 Hz)";
		});
		WaitFor("talking", 15f, () => Voice.Talking, () => "talking " + Voice.Talking + ", server voice " + Voice.ServerOn + ", capture '" + Voice.CaptureProblem + "'");
		WaitMark("w-talk", "pics-talk-seen", 90f);
		Do("stop-talk", delegate
		{
			Voice.FakeTalk = false;
			return "talk switch released after " + Voice.SentMessages + " voice message(s)";
		});
		WaitMark("w-options", "pics-options-seen", 90f);
		Do("point-item", delegate
		{
			GameItem val = PicsFeatBase.StorageItem(0L);
			if (val == null)
			{
				throw new Exception("no item of ours drawn in the main storage");
			}
			pointed = val.uniqueId;
			string text = Pointing.PointAt(pointed, PicsFeatBase.CentreOf(val));
			if (text != null)
			{
				throw new Exception("refused: " + text);
			}
			return "pointed at " + val.identifier + "#" + pointed + " (" + Pointing.ItemName(pointed) + ")";
		});
		WaitMark("w-ping-item", "pics-ping-item-seen", 60f);
		Delay("ping-gap", 2f);
		Do("point-spot", delegate
		{
			string text = Pointing.PointAt(0L, PicsFeatBase.SpotAt);
			if (text != null)
			{
				throw new Exception("refused: " + text);
			}
			return "pointed at the spot " + ((object)PicsFeatBase.SpotAt).ToString();
		});
		WaitMark("w-ping-spot", "pics-ping-spot-seen", 60f);
		Do("pick-up", delegate
		{
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			GameItem val = PicsFeatBase.StorageItem(pointed) ?? PicsFeatBase.StorageItem(0L);
			if ((Object)(object)current == (Object)null || val == null)
			{
				throw new Exception("no drag handler or no item");
			}
			carried = val.uniqueId;
			Vector2 val2 = PicsFeatBase.CentreOf(val);
			Presence.FakePointer = new Vector2(Mathf.Clamp01(val2.x + 0.03f), Mathf.Clamp01(val2.y + 0.06f));
			fCur.SetValue(current, val);
			mStart.Invoke(current, null);
			if (!current.IsDraggingItem)
			{
				throw new Exception("the game's StartDrag did not start a drag on " + val.identifier);
			}
			return val.identifier + "#" + carried + " picked up; my pointer at " + ((object)Presence.FakePointer.Value).ToString() + "; carry " + Carry.LocalItem;
		});
		WaitMark("w-carry", "pics-carry-seen", 90f);
		Do("drop", delegate
		{
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			fLastItem.SetValue(current, null);
			fLastInv.SetValue(current, null);
			try
			{
				BotDrag.End(current);
			}
			catch
			{
			}
			if ((bool)fDragging.GetValue(current))
			{
				fDragging.SetValue(current, false);
			}
			fCur.SetValue(current, null);
			return "dropped on nothing; carry " + Carry.LocalItem;
		});
		Delay("end", 5f);
	}
}
