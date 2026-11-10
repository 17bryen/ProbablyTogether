using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ProbablyTogether;

internal static class ItemsMoveRecorder
{
	private sealed class RefEq : IEqualityComparer<GameItem>
	{
		public bool Equals(GameItem a, GameItem b)
		{
			return a == b;
		}

		public int GetHashCode(GameItem o)
		{
			return RuntimeHelpers.GetHashCode(o);
		}
	}

	internal sealed class AcceptState
	{
		public int mode;

		public GameItem item;

		public GameItem target;

		public long id;

		public long onto;

		public ItemRef to;

		public GridShape shape;

		public GameInventory inv;

		public long tape = -1L;
	}

	internal sealed class StackState
	{
		public int mode;

		public long id;

		public long onto;

		public int n;

		public bool fresh;

		public long tape = -1L;
	}

	internal sealed class InvState
	{
		public int mode;

		public long id;

		public ItemRef inv;

		public int x;

		public int y;

		public int rot;

		public bool f;

		public bool fresh;

		public long tape = -1L;
	}

	private static List<ItemsOp> ops;

	private static int depth;

	private static readonly HashSet<GameItem> fresh = new HashSet<GameItem>(new RefEq());

	internal static bool On
	{
		get
		{
			if (ops != null)
			{
				return Net.IsServer;
			}
			return false;
		}
	}

	internal static bool AtTop
	{
		get
		{
			if (On)
			{
				return depth == 0;
			}
			return false;
		}
	}

	internal static void Begin(List<ItemsOp> list)
	{
		ops = list;
		depth = 0;
		fresh.Clear();
	}

	internal static void End()
	{
		ops = null;
		depth = 0;
		fresh.Clear();
	}

	internal static void Add(ItemsOp op)
	{
		if (ops != null)
		{
			ops.Add(op);
		}
	}

	internal static void OnCreated(GameItem it)
	{
		if (AtTop)
		{
			fresh.Add(it);
		}
	}

	internal static int Enter()
	{
		if (On)
		{
			if (depth++ != 0)
			{
				return 1;
			}
			return 2;
		}
		return 0;
	}

	internal static void Leave(int mode)
	{
		if (mode != 0 && depth > 0)
		{
			depth--;
		}
	}

	private static bool IsFresh(GameItem it)
	{
		if (it != null)
		{
			return fresh.Contains(it);
		}
		return false;
	}

	private static void Sub(GameItem placed, GameInventory inv)
	{
		fresh.Remove(placed);
		foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)placed, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
		{
			fresh.Remove(item);
		}
		Subtree subtree = Items.Encode(placed);
		if (subtree == null)
		{
			Log.Warn("items: could not encode new " + placed.identifier);
			return;
		}
		ops.Add(new ItemsOp
		{
			t = "sub",
			i = Ids.Of(placed),
			to = Items.RefOf((GraphNodeStorage)(object)(inv ?? placed.parentInventory)),
			sub = subtree
		});
	}

	private static void Pos(ItemsOp op, GridShape s)
	{
		if (s != null)
		{
			op.x = s.minX;
			op.y = s.minY;
			op.f = s.flipped;
			op.rot = s.orientation;
		}
	}

	internal static AcceptState BeforeAccept(SlotMarker m)
	{
		AcceptState acceptState = new AcceptState
		{
			mode = Enter()
		};
		if (acceptState.mode != 2)
		{
			return acceptState;
		}
		acceptState.tape = Items.TapeMark();
		acceptState.item = m.item;
		acceptState.target = m.targetItem;
		acceptState.inv = m.inventory;
		acceptState.shape = m.itemGridShape;
		acceptState.id = ((m.item != null && !IsFresh(m.item)) ? Ids.Of(m.item) : 0);
		acceptState.onto = ((m.targetItem != null) ? Ids.Of(m.targetItem) : 0);
		acceptState.to = Items.RefOf((GraphNodeStorage)(object)m.inventory);
		return acceptState;
	}

	internal static void AfterAccept(SlotMarker m, int result, AcceptState st)
	{
		if (st == null || st.mode != 2 || result <= 0 || st.item == null)
		{
			return;
		}
		if (st.target != null)
		{
			if (IsFresh(st.item))
			{
				Sub(st.target, st.target.parentInventory);
				Items.TapeRewind(st.tape);
				return;
			}
			ops.Add(new ItemsOp
			{
				t = "stack",
				i = st.id,
				onto = st.onto,
				n = result
			});
			return;
		}
		GameItem item = m.item;
		if (IsFresh(st.item) || IsFresh(item))
		{
			Sub(item, st.inv);
			Items.TapeRewind(st.tape);
			return;
		}
		ItemsOp itemsOp = new ItemsOp
		{
			t = "place",
			i = st.id,
			to = st.to,
			n = result,
			nw = ((item == st.item) ? 0 : Ids.Of(item))
		};
		Pos(itemsOp, st.shape);
		ops.Add(itemsOp);
	}

	internal static AcceptState BeforeReposition(SlotMarker m)
	{
		return BeforeAccept(m);
	}

	internal static void AfterReposition(SlotMarker m, int result, AcceptState st)
	{
		if (st != null && st.mode == 2 && result > 0 && st.item != null)
		{
			if (IsFresh(st.item))
			{
				Sub(st.item, st.inv);
				Items.TapeRewind(st.tape);
				return;
			}
			ItemsOp itemsOp = new ItemsOp
			{
				t = "rep",
				i = st.id,
				to = st.to
			};
			Pos(itemsOp, st.shape);
			ops.Add(itemsOp);
		}
	}

	internal static StackState BeforeStack(GameItem item, GameItem target, int amount)
	{
		StackState stackState = new StackState
		{
			mode = Enter()
		};
		if (stackState.mode != 2 || item == null || target == null)
		{
			return stackState;
		}
		stackState.tape = Items.TapeMark();
		stackState.fresh = IsFresh(item);
		stackState.id = (stackState.fresh ? 0 : Ids.Of(item));
		stackState.onto = Ids.Of(target);
		stackState.n = ((amount == -1) ? item.unitCount : amount);
		return stackState;
	}

	internal static void AfterStack(GameItem item, GameItem target, StackState st)
	{
		if (st != null && st.mode == 2 && target != null)
		{
			if (st.fresh)
			{
				Sub(target, target.parentInventory);
				Items.TapeRewind(st.tape);
				return;
			}
			ops.Add(new ItemsOp
			{
				t = "stack",
				i = st.id,
				onto = st.onto,
				n = st.n
			});
		}
	}

	internal static InvState BeforeInvAccept(GameInventory inv, GameItem item)
	{
		InvState invState = new InvState
		{
			mode = Enter()
		};
		if (invState.mode != 2 || item == null)
		{
			return invState;
		}
		invState.tape = Items.TapeMark();
		invState.fresh = IsFresh(item);
		invState.id = (invState.fresh ? 0 : Ids.Of(item));
		invState.inv = Items.RefOf((GraphNodeStorage)(object)inv);
		GridShape modifiedShape = item.modifiedShape;
		if (modifiedShape != null)
		{
			invState.x = modifiedShape.minX;
			invState.y = modifiedShape.minY;
			invState.f = modifiedShape.flipped;
			invState.rot = modifiedShape.orientation;
		}
		return invState;
	}

	internal static void AfterInvAccept(GameInventory inv, GameItem item, bool result, InvState st)
	{
		if (st != null && st.mode == 2 && result && item != null)
		{
			if (st.fresh)
			{
				Sub(item, inv);
				Items.TapeRewind(st.tape);
				return;
			}
			ops.Add(new ItemsOp
			{
				t = "accept",
				i = st.id,
				to = st.inv,
				x = st.x,
				y = st.y,
				f = st.f,
				rot = st.rot
			});
		}
	}

	internal static InvState BeforeExpel(GameInventory inv, GameItem item)
	{
		InvState invState = new InvState
		{
			mode = Enter()
		};
		if (invState.mode != 2 || item == null)
		{
			return invState;
		}
		invState.fresh = IsFresh(item);
		invState.id = (invState.fresh ? 0 : Ids.Of(item));
		invState.inv = Items.RefOf((GraphNodeStorage)(object)inv);
		return invState;
	}

	internal static void AfterExpel(GameItem item, bool result, InvState st)
	{
		if (st != null && st.mode == 2 && result && !st.fresh)
		{
			ops.Add(new ItemsOp
			{
				t = "expel",
				i = st.id,
				to = st.inv
			});
		}
	}

	internal static int BeforeDestroy(GameItemElement item)
	{
		int num = Enter();
		if (num != 2 || item == null || item.IsDestroyed())
		{
			return num;
		}
		if (IsFresh((GameItem)(object)item))
		{
			fresh.Remove((GameItem)(object)item);
			return num;
		}
		long num2 = Ids.Of((GameItem)(object)item);
		if (num2 != 0L)
		{
			ops.Add(new ItemsOp
			{
				t = "destroy",
				i = num2
			});
		}
		return num;
	}
}
