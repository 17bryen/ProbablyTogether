using System;
using System.Collections.Generic;

namespace ProbablyTogether;

internal static class ItemsCapture
{
	internal sealed class Ctx
	{
		public string kind;

		public GameItem dragItem;

		public GameItem dragOnto;

		public bool insertPending;

		public bool insertSent;
	}

	private static readonly Stack<Ctx> stack = new Stack<Ctx>();

	internal static bool ClientActive
	{
		get
		{
			if (Net.IsClient)
			{
				return !Net.Applying;
			}
			return false;
		}
	}

	internal static Ctx Current
	{
		get
		{
			if (stack.Count <= 0)
			{
				return null;
			}
			return stack.Peek();
		}
	}

	internal static bool Enter(string kind, out Ctx c)
	{
		c = null;
		if (!ClientActive)
		{
			return false;
		}
		c = new Ctx
		{
			kind = kind
		};
		stack.Push(c);
		return true;
	}

	internal static void Exit(bool entered)
	{
		if (entered && stack.Count > 0)
		{
			stack.Pop();
		}
	}

	internal static int OnTryAcceptOnce(SlotMarker m, int amount)
	{
		Ctx current = Current;
		GameItem item = m.item;
		if (item == null || current == null)
		{
			return 0;
		}
		int val = ((amount < 0) ? m.numTransfer : amount);
		val = Math.Min(val, m.numTransfer);
		val = Math.Min(val, item.MaxNumRemove());
		if (val <= 0)
		{
			return 0;
		}
		if (current.kind == "drag" && current.insertPending && m.targetItem == null && current.dragOnto != null)
		{
			current.insertPending = false;
			current.insertSent = true;
			int num = ((amount < 0) ? val : amount);
			ItemsMove.InsertInto(item, current.dragOnto, num, chain: true, "drag");
			return num;
		}
		if (current.insertSent || current.kind == "button")
		{
			return 0;
		}
		if (m.targetItem != null)
		{
			ItemsMove.Stack(item, m.targetItem, val, current.kind);
			return val;
		}
		GridShape itemGridShape = m.itemGridShape;
		if (m.inventory == null || itemGridShape == null)
		{
			return 0;
		}
		ItemsMove.Move(item, (GraphNodeStorage)(object)m.inventory, itemGridShape.minX, itemGridShape.minY, itemGridShape.flipped, itemGridShape.orientation, val, rep: false, current.kind);
		return val;
	}

	internal static int OnAcceptUnchecked(SlotMarker m)
	{
		Ctx current = Current;
		GameItem item = m.item;
		GridShape itemGridShape = m.itemGridShape;
		if (current == null || item == null || m.inventory == null || itemGridShape == null)
		{
			return 0;
		}
		ItemsMove.Move(item, (GraphNodeStorage)(object)m.inventory, itemGridShape.minX, itemGridShape.minY, itemGridShape.flipped, itemGridShape.orientation, item.unitCount, rep: true, current.kind);
		return item.unitCount;
	}

	internal static int OnGraphAcceptOnce(GraphNodeStorage node, GameItem item, int amount)
	{
		Ctx current = Current;
		if (current == null || item == null)
		{
			return 0;
		}
		int num = ((amount < 0) ? item.unitCount : amount);
		if (num <= 0)
		{
			return 0;
		}
		SlotMarker val = ((node != null) ? node.TryFindOneValidInventorySlot(item) : null);
		if (val == null || val.numTransfer <= 0)
		{
			return num;
		}
		int num2 = Math.Min(Math.Min(num, val.numTransfer), item.MaxNumRemove());
		if (num2 <= 0)
		{
			return num;
		}
		if (val.targetItem != null)
		{
			ItemsMove.Stack(item, val.targetItem, num2, current.kind);
		}
		else
		{
			GridShape itemGridShape = val.itemGridShape;
			if (val.inventory == null || itemGridShape == null)
			{
				return num;
			}
			ItemsMove.Move(item, (GraphNodeStorage)(object)val.inventory, itemGridShape.minX, itemGridShape.minY, itemGridShape.flipped, itemGridShape.orientation, num2, rep: false, current.kind);
		}
		return num - num2;
	}

	internal static int OnGraphAcceptAll(GraphNodeStorage node, GameItem item, int amount)
	{
		Ctx current = Current;
		if (current == null || item == null || node == null)
		{
			return 0;
		}
		int num = ((amount < 0) ? item.unitCount : amount);
		if (num <= 0)
		{
			return 0;
		}
		if (current.kind == "button")
		{
			return 0;
		}
		if (current.kind == "trash")
		{
			ItemsMove.Trash(item, "trash");
		}
		else
		{
			GameItem val = (GameItem)(object)((node is GameItem) ? node : null);
			if (val != null)
			{
				ItemsMove.InsertInto(item, val, num, chain: false, current.kind);
			}
			else
			{
				ItemsMove.QuickMove(item, node, all: true, num, current.kind);
			}
		}
		return 0;
	}
}
