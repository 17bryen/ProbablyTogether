using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

public static class ItemsMove
{
	private sealed class Undo : IDisposable
	{
		private Action a;

		public Undo(Action x)
		{
			a = x;
		}

		public void Dispose()
		{
			Action action = a;
			a = null;
			action?.Invoke();
		}
	}

	public const string KMove = "items.move";

	public const string KStack = "items.stack";

	public const string KInsert = "items.insertInto";

	public const string KQuick = "items.quickMove";

	public const string KGroup = "items.groupMove";

	public const string KSort = "items.sort";

	public const string KSortUndo = "items.sortUndo";

	public const string KUnload = "items.unloadAll";

	public const string KPin = "items.pin";

	public const string KTransfer = "items.transferButton";

	public const string KTrash = "items.trash";

	public const string KOpened = "items.markOpened";

	public const string KOps = "items.ops";

	public const string KReject = "items.reject";

	private static readonly string[] requestKinds = new string[12]
	{
		"items.move", "items.stack", "items.insertInto", "items.quickMove", "items.groupMove", "items.sort", "items.sortUndo", "items.unloadAll", "items.pin", "items.transferButton",
		"items.trash", "items.markOpened"
	};

	private static int nextQ;

	private static readonly Dictionary<int, string> outcome = new Dictionary<int, string>();

	public static bool LabSkipClientCheck;

	private static int itemsDepth;

	public const string CounterLock = "counter";

	private static int seq;

	private static readonly Dictionary<long, int> lastMover = new Dictionary<long, int>();

	internal const string OwnMoveRefusal = "Your last move already put that item somewhere else.";

	private static bool counterTaken;

	private static string counterRefusal;

	internal static bool? SortRotateOverride;

	private static readonly Dictionary<int, string> rejectTexts = new Dictionary<int, string>();

	private static readonly FieldInfo undoField = AccessTools.Field(typeof(InventorySortHelper), "undoSnapshots");

	public static int SentCount { get; private set; }

	public static string LastSentKind { get; private set; }

	public static int LastSentQ { get; private set; }

	public static int RandomMismatches { get; private set; }

	public static string LastRandomCheck { get; private set; } = "";

	public static int NegoFixes { get; private set; }

	public static string LastNotice { get; private set; } = "";

	public static int AppliedCount { get; private set; }

	public static int RejectCount { get; private set; }

	public static int ApplyMismatches { get; private set; }

	public static ItemsReject LastReject { get; private set; }

	public static ItemsOps LastApplied { get; private set; }

	public static int ApplyingActor
	{
		get
		{
			if (itemsDepth <= 0)
			{
				return -1;
			}
			return Net.Actor;
		}
	}

	public static event Action<int, string, ItemsReq, string> Refused;

	internal static void Init()
	{
		string[] array = requestKinds;
		foreach (string text in array)
		{
			string kind = text;
			Net.On(kind, delegate(ItemsReq m, int from)
			{
				Serve(kind, m, from);
			}, null);
		}
		Net.On<ItemsOps>("items.ops", null, OnOps);
		Net.On<ItemsReject>("items.reject", null, OnReject);
		ServerMode.NewRunStarting += delegate
		{
			lastMover.Clear();
		};
	}

	public static int Move(GameItem item, GraphNodeStorage to, int x, int y, bool flipped, int orientation, int n = -1, bool rep = false, string via = "api")
	{
		return Send("items.move", Base(item, n, via, delegate(ItemsReq r)
		{
			r.to = Items.RefOf(to);
			r.x = x;
			r.y = y;
			r.f = flipped;
			r.rot = orientation;
			r.rep = rep;
		}));
	}

	public static int Stack(GameItem item, GameItem onto, int n = -1, string via = "api")
	{
		return Send("items.stack", Base(item, n, via, delegate(ItemsReq r)
		{
			r.onto = Ids.Of(onto);
		}));
	}

	public static int InsertInto(GameItem item, GameItem container, int n = -1, bool chain = false, string via = "api")
	{
		return Send("items.insertInto", Base(item, n, via, delegate(ItemsReq r)
		{
			r.onto = Ids.Of(container);
			r.chain = chain;
		}));
	}

	public static int QuickMove(GameItem item, GraphNodeStorage to, bool all, int n = -1, string via = "api")
	{
		return Send("items.quickMove", Base(item, n, via, delegate(ItemsReq r)
		{
			r.to = Items.RefOf(to);
			r.all = all;
		}));
	}

	public static int GroupMove(IList<GameItem> items, GameInventory to, int dx, int dy, string via = "api")
	{
		return Send("items.groupMove", new ItemsReq
		{
			ids = items.Select(Ids.Of).ToArray(),
			to = Items.RefOf((GraphNodeStorage)(object)to),
			dx = dx,
			dy = dy,
			via = via
		});
	}

	public static int Sort(GameGridInventory inv, bool bigFirst, bool fromEnd, bool autoRotate, string via = "api")
	{
		return Send("items.sort", new ItemsReq
		{
			to = Items.RefOf((GraphNodeStorage)(object)inv),
			big = bigFirst,
			end = fromEnd,
			autoRot = autoRotate,
			via = via
		});
	}

	public static int SortUndo(GameGridInventory inv, string via = "api")
	{
		return Send("items.sortUndo", new ItemsReq
		{
			to = Items.RefOf((GraphNodeStorage)(object)inv),
			via = via
		});
	}

	public static int UnloadAll(GameItem item, string via = "api")
	{
		return Send("items.unloadAll", Base(item, -1, via, null));
	}

	public static int Pin(GameItem item, bool pinned, string via = "api")
	{
		return Send("items.pin", Base(item, -1, via, delegate(ItemsReq r)
		{
			r.on = pinned;
		}));
	}

	public static int Trash(GameItem item, string via = "api")
	{
		return Send("items.trash", Base(item, -1, via, null));
	}

	public static int MarkOpened(GameItem item, string via = "api")
	{
		return Send("items.markOpened", Base(item, -1, via, null));
	}

	public static int TransferButton(bool frontVisible, string via = "api")
	{
		return Send("items.transferButton", new ItemsReq
		{
			frontVisible = frontVisible,
			via = via
		});
	}

	public static string Outcome(int q)
	{
		if (q == 0 || !outcome.TryGetValue(q, out var value))
		{
			return "";
		}
		return value;
	}

	private static ItemsReq Base(GameItem item, int n, string via, Action<ItemsReq> more)
	{
		ItemsReq itemsReq = new ItemsReq
		{
			i = Ids.Of(item),
			n = n,
			via = via
		};
		if (item != null && item.parentInventory != null)
		{
			itemsReq.src = Items.RefOf((GraphNodeStorage)(object)item.parentInventory);
			GridShape modifiedShape = item.modifiedShape;
			itemsReq.sx = ((modifiedShape != null) ? modifiedShape.minX : 0);
			GridShape modifiedShape2 = item.modifiedShape;
			itemsReq.sy = ((modifiedShape2 != null) ? modifiedShape2.minY : 0);
		}
		more?.Invoke(itemsReq);
		return itemsReq;
	}

	internal static int Send(string kind, ItemsReq r)
	{
		if (r == null)
		{
			return 0;
		}
		if (Net.IsServer)
		{
			r.q = ++nextQ;
			outcome[r.q] = "pending";
			Serve(kind, r, Net.IsHost ? Net.LocalPlayer : 0);
			if (outcome[r.q] == "pending")
			{
				outcome[r.q] = "applied";
			}
			return r.q;
		}
		if (!Net.IsClient)
		{
			return 0;
		}
		if (!Net.IsLive)
		{
			Notify("Wait until the shop has loaded.");
			Log.Info("items: " + kind + " not sent (not live: " + Net.ClientState.ToString() + ")");
			return 0;
		}
		string text = (LabSkipClientCheck ? null : CounterBlockedHere(r));
		if (text != null)
		{
			Notify(text);
			Log.Info("items: " + kind + " not sent: " + text);
			return 0;
		}
		r.q = ++nextQ;
		outcome[r.q] = "pending";
		if (outcome.Count > 2000)
		{
			outcome.Remove(outcome.Keys.Min());
		}
		Net.ToServer(kind, r);
		SentCount++;
		LastSentKind = kind;
		LastSentQ = r.q;
		return r.q;
	}

	private static string CounterText(string holderName)
	{
		if (string.IsNullOrEmpty(holderName))
		{
			return "Someone else is at the counter.";
		}
		return holderName + " is at the counter.";
	}

	private static string CounterBlockedHere(ItemsReq r)
	{
		string text = Customers.LocalCounterRefusal();
		if (text == null)
		{
			return null;
		}
		bool flag = false;
		GameItem val = ((r.i != 0L) ? Items.Find(r.i) : null);
		if (val != null && Items.IsCounterArea((GraphNodeStorage)(object)val.parentInventory))
		{
			flag = true;
		}
		GraphNodeStorage val2 = Items.Resolve(r.to);
		if (val2 != null && Items.IsCounterArea(val2))
		{
			flag = true;
		}
		GameItem val3 = ((r.onto != 0L) ? Items.Find(r.onto) : null);
		if (val3 != null && Items.IsCounterArea((GraphNodeStorage)(object)val3))
		{
			flag = true;
		}
		if (r.ids != null)
		{
			long[] ids = r.ids;
			for (int i = 0; i < ids.Length; i++)
			{
				GameItem val4 = Items.Find(ids[i]);
				if (val4 != null && Items.IsCounterArea((GraphNodeStorage)(object)val4.parentInventory))
				{
					flag = true;
				}
			}
		}
		if (r.frontVisible || r.via == "button")
		{
			flag = true;
		}
		if (!flag)
		{
			return null;
		}
		return text;
	}

	private static void Serve(string kind, ItemsReq m, int from)
	{
		if (m == null)
		{
			return;
		}
		if (!Items.Ready)
		{
			Reject(from, m, kind, "starting", "The shop is not open yet.");
			return;
		}
		ItemRef at = null;
		counterTaken = false;
		counterRefusal = null;
		string text;
		Action run;
		try
		{
			GameItem val = ((m.i != 0L) ? Items.Find(m.i) : null);
			GameItem val2 = ((m.onto != 0L) ? Items.Find(m.onto) : null);
			string text2 = Systems.Bays.Refusal(from, (GraphNodeStorage)(object)((val != null) ? val.parentInventory : null), (GraphNodeStorage)(object)val2);
			if (text2 != null && kind != "items.pin" && kind != "items.markOpened")
			{
				counterRefusal = text2;
				Reject(from, m, kind, "counter", counterRefusal);
				return;
			}
			switch (kind)
			{
			case "items.move":
				text = PlanMove(m, from, out run);
				break;
			case "items.stack":
				text = PlanStack(m, from, out run);
				break;
			case "items.insertInto":
				text = PlanInsert(m, from, out run);
				break;
			case "items.quickMove":
				text = PlanQuick(m, from, out run);
				break;
			case "items.trash":
				text = PlanTrash(m, from, out run);
				break;
			case "items.groupMove":
				text = PlanGroup(m, from, out run);
				break;
			case "items.sort":
				text = PlanSort(m, from, undo: false, out run);
				at = m.to;
				break;
			case "items.sortUndo":
				text = PlanSort(m, from, undo: true, out run);
				at = m.to;
				break;
			case "items.unloadAll":
				text = PlanUnload(m, from, out run);
				break;
			case "items.pin":
				text = PlanPin(m, from, out run);
				break;
			case "items.markOpened":
				text = PlanOpened(m, from, out run);
				break;
			case "items.transferButton":
				text = PlanTransfer(m, from, out run);
				break;
			default:
				text = "bad";
				run = null;
				break;
			}
		}
		catch (Exception ex)
		{
			Log.Warn("items: checking " + kind + " from " + Net.PlayerName(from) + " threw: " + ex.Message);
			text = "bad";
			run = null;
		}
		bool took = counterTaken;
		counterTaken = false;
		if (text != null)
		{
			Customers.GiveBack(from, took);
			Reject(from, m, kind, text, (text == "counter") ? counterRefusal : ((text == "moved") ? OwnMoveText(from, m) : null));
		}
		else if (!Execute(kind, from, m.q, run, at, Describe(kind, m)))
		{
			Customers.GiveBack(from, took);
			Reject(from, m, kind, "nothing", null);
		}
	}

	private static string OwnMoveText(int from, ItemsReq m)
	{
		if (from <= 0 || m.i == 0L || !lastMover.TryGetValue(m.i, out var value) || value != from)
		{
			return null;
		}
		return "Your last move already put that item somewhere else.";
	}

	private static void NoteMovers(int actor, List<ItemsOp> ops)
	{
		if (lastMover.Count > 20000)
		{
			lastMover.Clear();
		}
		foreach (ItemsOp op in ops)
		{
			if (op.i != 0L)
			{
				lastMover[op.i] = actor;
			}
			if (op.nw != 0L)
			{
				lastMover[op.nw] = actor;
			}
		}
	}

	private static string Describe(string kind, ItemsReq m)
	{
		GameItem val = ((m.i != 0L) ? Items.Find(m.i) : null);
		return kind + " " + ((val != null) ? (val.identifier + "#" + m.i) : ((m.ids != null) ? (m.ids.Length + " items") : "")) + ((m.to != null) ? (" -> " + m.to) : ((m.onto != 0L) ? (" -> item#" + m.onto) : "")) + ((m.via != null) ? (" (" + m.via + ")") : "");
	}

	private static bool Execute(string kind, int actor, int q, Action run, ItemRef at, string what)
	{
		ItemsOps itemsOps = new ItemsOps
		{
			actor = actor,
			kind = kind,
			q = q,
			at = at,
			ops = new List<ItemsOp>()
		};
		Exception ex = null;
		GameItem val = NegotiatedItem();
		Seed seed;
		using (Replay.Server(out seed, actor))
		{
			itemsOps.seed = seed;
			Tape tape;
			using (Items.Record(out tape))
			{
				itemsOps.tape = tape;
				ItemsMoveRecorder.Begin(itemsOps.ops);
				itemsDepth++;
				try
				{
					run();
				}
				catch (Exception ex2)
				{
					ex = ex2;
				}
				finally
				{
					ItemsMoveRecorder.End();
					itemsDepth--;
				}
			}
		}
		itemsOps.ra = (itemsOps.ops.Exists((ItemsOp o) => o.t == "sub") ? 0 : RandomHash());
		itemsOps.dg = ActionDigest();
		if (ex != null)
		{
			Log.Error("items: " + what + " threw on the server after " + itemsOps.ops.Count + " step(s) (sent as far as it got): " + ex);
		}
		if (itemsOps.ops.Count == 0)
		{
			return false;
		}
		bool flag = TouchesCounter(itemsOps.ops);
		GameItem val2 = NegotiatedItem();
		if (flag || val != val2)
		{
			itemsOps.nego = true;
			itemsOps.negoId = ((val2 != null) ? Ids.Of(val2) : 0);
		}
		if (actor > 0 && flag)
		{
			Locks.Touch(Customers.CounterLockNow, actor);
		}
		itemsOps.seq = ++seq;
		NoteMovers(actor, itemsOps.ops);
		Systems.Standing.NoteMoves(actor, itemsOps.ops);
		Net.ToAll("items.ops", itemsOps);
		Log.Info("items: #" + itemsOps.seq + " " + Net.PlayerName(actor) + " " + what + ": " + Summary(itemsOps.ops));
		return true;
	}

	internal static bool RunServerAction(string what, Action body, int actor)
	{
		if (!Items.Ready)
		{
			Log.Warn("items: server action " + what + " before the shop loaded");
			return false;
		}
		return Execute(what, actor, 0, body, null, what);
	}

	/// Server: its own item steps (the night's changes) go to every copy as one action.
	internal static void BroadcastOps(string kind, List<ItemsOp> ops)
	{
		if (!Items.Ready || ops == null || ops.Count == 0)
		{
			return;
		}
		ItemsOps itemsOps = new ItemsOps
		{
			actor = 0,
			kind = kind,
			ops = ops,
			seed = new Seed(),
			tape = new Tape
			{
				after = (Game.Store?.currentUniqueId ?? 0)
			}
		};
		itemsOps.seq = ++seq;
		Net.ToAll("items.ops", itemsOps);
		Log.Info("items: #" + itemsOps.seq + " " + kind + ": " + Summary(ops));
	}

	internal static void BroadcastSubtree(GameItem item, string why)
	{
		if (Items.Ready && !Items.IsGone(item))
		{
			Subtree subtree = Items.Encode(item);
			if (subtree != null)
			{
				ItemsOp itemsOp = new ItemsOp
				{
					t = "sub",
					i = Ids.Of(item),
					to = Items.RefOf((GraphNodeStorage)(object)item.parentInventory),
					sub = subtree
				};
				ItemsOps itemsOps = new ItemsOps
				{
					actor = 0,
					kind = "items.subtree:" + why,
					ops = new List<ItemsOp> { itemsOp },
					seed = new Seed(),
					tape = new Tape
					{
						after = (Game.Store?.currentUniqueId ?? 0)
					}
				};
				itemsOps.seq = ++seq;
				Net.ToAll("items.ops", itemsOps);
				string[] obj = new string[11]
				{
					"items: #",
					itemsOps.seq.ToString(),
					" subtree of ",
					item.identifier,
					"#",
					itemsOp.i.ToString(),
					" sent (",
					why,
					"): ",
					null,
					null
				};
				long[] ids = subtree.ids;
				obj[9] = ((ids != null) ? ids.Length : 0).ToString();
				obj[10] = " item(s)";
				Log.Info(string.Concat(obj));
			}
		}
	}

	private static bool TouchesCounter(List<ItemsOp> ops)
	{
		foreach (ItemsOp op in ops)
		{
			if (op.to != null && (op.to.r == "front" || op.to.r == "back" || op.to.r == "front.w" || op.to.r == "back.w"))
			{
				return true;
			}
			GameItem val = ((op.i != 0L) ? Items.Find(op.i) : null);
			if (val != null && Items.IsCounterArea((GraphNodeStorage)(object)val.parentInventory))
			{
				return true;
			}
		}
		return false;
	}

	private static string Summary(List<ItemsOp> ops)
	{
		List<string> list = new List<string>();
		foreach (ItemsOp item in ops.Take(6))
		{
			list.Add(item.t + " " + ((item.i != 0L) ? ("#" + item.i) : "") + ((item.to != null) ? ("->" + item.to?.ToString() + "@" + item.x + "," + item.y + ((item.rot != 0) ? ("r" + item.rot) : "")) : "") + ((item.nw != 0L) ? (" new#" + item.nw) : ""));
		}
		if (ops.Count > 6)
		{
			list.Add("+" + (ops.Count - 6) + " more");
		}
		return string.Join("; ", list);
	}

	private static void Reject(int player, ItemsReq m, string kind, string why, string text)
	{
		text = text ?? RejectText(why);
		Log.Info("items: refused " + Net.PlayerName(player) + "'s " + Describe(kind, m) + ": " + why);
		if (player > 0)
		{
			Net.ToPlayer(player, "items.reject", new ItemsReject
			{
				q = m.q,
				kind = kind,
				why = why,
				text = text
			});
		}
		else if (m.q != 0)
		{
			outcome[m.q] = "rejected:" + why;
		}
		if (player > 0 && ItemsMove.Refused != null)
		{
			try
			{
				ItemsMove.Refused(player, kind, m, why);
			}
			catch (Exception ex)
			{
				Log.Warn("items: a Refused listener threw: " + ex.Message);
			}
		}
	}

	private static string RejectText(string why)
	{
		switch (why)
		{
		case "gone":
			return "That item is gone.";
		case "moved":
			return "Someone else moved that item first.";
		case "locked":
			return "That item can't be moved right now.";
		case "target":
			return "That spot is gone.";
		case "noroom":
			return "It doesn't fit there.";
		case "counter":
		{
			string counterLockNow = Customers.CounterLockNow;
			int num = Locks.Holder(counterLockNow);
			if (num <= 0)
			{
				return CounterText(null);
			}
			return Customers.HeldText(num, counterLockNow);
		}
		case "starting":
			return "The shop is not open yet.";
		default:
			return "That didn't work.";
		}
	}

	private static string CheckItem(ItemsReq m, out GameItem item, bool needRemove = true)
	{
		item = ((m.i != 0L) ? Items.Find(m.i) : null);
		if (item == null)
		{
			return "gone";
		}
		if (m.src != null)
		{
			ItemRef itemRef = Items.RefOf((GraphNodeStorage)(object)item.parentInventory);
			if (itemRef == null || !itemRef.Same(m.src))
			{
				return "moved";
			}
			if (item.modifiedShape != null && (item.modifiedShape.minX != m.sx || item.modifiedShape.minY != m.sy))
			{
				return "moved";
			}
		}
		if (item.parentInventory == null)
		{
			return "locked";
		}
		if (needRemove && !item.MayRemove())
		{
			return "locked";
		}
		return null;
	}

	private static int Units(ItemsReq m, GameItem item, out string why)
	{
		why = null;
		int num = item.MaxNumRemove();
		int num2 = ((m.n <= 0) ? num : m.n);
		if (num < 1 || num2 < 1 || num2 > num)
		{
			why = "amount";
		}
		return num2;
	}

	private static string Counter(int player, params GraphNodeStorage[] nodes)
	{
		if (player == 0)
		{
			return null;
		}
		string text = Systems.Bays.Refusal(player, nodes);
		if (text != null)
		{
			counterRefusal = text;
			return "counter";
		}
		if (!Items.CustomerPresent)
		{
			return null;
		}
		bool flag = false;
		foreach (GraphNodeStorage val in nodes)
		{
			if (val != null && Items.IsCounterArea(val))
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			return null;
		}
		if (!Customers.TryCounter(player, out var refusal, out var took))
		{
			counterRefusal = refusal;
			return "counter";
		}
		if (took)
		{
			counterTaken = true;
		}
		return null;
	}

	private static string PlanMove(ItemsReq m, int from, out Action run)
	{
		run = null;
		string why = CheckItem(m, out var item);
		if (why != null)
		{
			return why;
		}
		int num = Units(m, item, out why);
		if (why != null)
		{
			return why;
		}
		GameInventory val = Items.ResolveInventory(m.to);
		if (val == null)
		{
			return "target";
		}
		if (!GeneralHelper.MayPlayerInsertInto(val))
		{
			return "notallowed";
		}
		string own1 = Systems.Bays.MoveRefusal(from, item, (GraphNodeStorage)val);
		if (own1 != null)
		{
			counterRefusal = own1;
			return "counter";
		}
		why = Counter(from, (GraphNodeStorage)item.parentInventory, (GraphNodeStorage)val);
		if (why != null)
		{
			return why;
		}
		if (m.rot < 0 || m.rot > 3)
		{
			return "bad";
		}
		GridShape val2 = Items.Shape(item, m.x, m.y, m.f, m.rot);
		SlotMarker slot = val.TryInventorySlot(item, num, Vector2.negativeInfinity, val2, item.modifiedState);
		if (slot == null || slot.numTransfer < 1)
		{
			return "noroom";
		}
		if (m.rep)
		{
			GameGridScrollableInventory val3 = (GameGridScrollableInventory)(object)((val is GameGridScrollableInventory) ? val : null);
			if (val3 == null || !val3.alwaysStack || item.parentInventory != val)
			{
				return "bad";
			}
			run = delegate
			{
				slot.AcceptUnchecked();
			};
		}
		else
		{
			run = delegate
			{
				slot.TryAcceptOnce(-1);
				DestroyIfOrphan(item);
			};
		}
		return null;
	}

	private static string PlanStack(ItemsReq m, int from, out Action run)
	{
		run = null;
		string why = CheckItem(m, out var item);
		if (why != null)
		{
			return why;
		}
		int n = Units(m, item, out why);
		if (why != null)
		{
			return why;
		}
		GameItem onto = Items.Find(m.onto);
		if (onto == null || onto == item || onto.parentInventory == null)
		{
			return "target";
		}
		if (!onto.MayStack(item))
		{
			return "notallowed";
		}
		why = Counter(from, (GraphNodeStorage)item.parentInventory, (GraphNodeStorage)onto);
		if (why != null)
		{
			return why;
		}
		SlotMarker stackSlot = onto.GetStackSlot(item);
		if (stackSlot == null || stackSlot.numTransfer < 1)
		{
			return "noroom";
		}
		run = delegate
		{
			SlotMarker stackSlot2 = onto.GetStackSlot(item);
			if (stackSlot2 != null)
			{
				stackSlot2.TryAcceptOnce(n);
			}
			DestroyIfOrphan(item);
		};
		return null;
	}

	private static string PlanInsert(ItemsReq m, int from, out Action run)
	{
		run = null;
		string why = CheckItem(m, out var item);
		if (why != null)
		{
			return why;
		}
		int n = Units(m, item, out why);
		if (why != null)
		{
			return why;
		}
		GameItem into = Items.Find(m.onto);
		if (into == null || into == item)
		{
			return "target";
		}
		if (!GeneralHelper.MayPlayerInsertInto(into) || !into.MayHaveValidInventorySlot(item))
		{
			return "notallowed";
		}
		why = Counter(from, (GraphNodeStorage)item.parentInventory, (GraphNodeStorage)into);
		if (why != null)
		{
			return why;
		}
		bool chain = m.chain;
		run = delegate
		{
			int num = n;
			for (int i = 0; i < 40; i++)
			{
				if (num < 1)
				{
					break;
				}
				SlotMarker val = into.TryFindOneValidInventorySlot(item);
				if (val == null || val.numTransfer < 1)
				{
					break;
				}
				num -= val.TryAcceptOnce(num);
			}
			if (chain && num >= 1 && into.MayStack(item))
			{
				SlotMarker stackSlot = into.GetStackSlot(item);
				if (stackSlot != null)
				{
					stackSlot.TryAcceptOnce(num);
				}
			}
			DestroyIfOrphan(item);
		};
		return null;
	}

	private static string PlanQuick(ItemsReq m, int from, out Action run)
	{
		run = null;
		string why = CheckItem(m, out var item);
		if (why != null)
		{
			return why;
		}
		int n = Units(m, item, out why);
		if (why != null)
		{
			return why;
		}
		GraphNodeStorage node = Items.Resolve(m.to);
		if (node == null || (object)node == item)
		{
			return "target";
		}
		GraphNodeStorage obj = node;
		GameInventory val = (GameInventory)(object)((obj is GameInventory) ? obj : null);
		if (val != null && !GeneralHelper.MayPlayerInsertInto(val))
		{
			return "notallowed";
		}
		GraphNodeStorage obj2 = node;
		GameItem val2 = (GameItem)(object)((obj2 is GameItem) ? obj2 : null);
		if (val2 != null && !GeneralHelper.MayPlayerInsertInto(val2))
		{
			return "notallowed";
		}
		string own2 = Systems.Bays.MoveRefusal(from, item, node);
		if (own2 != null)
		{
			counterRefusal = own2;
			return "counter";
		}
		why = Counter(from, (GraphNodeStorage)item.parentInventory, node);
		if (why != null)
		{
			return why;
		}
		SlotMarker val3 = node.TryFindOneValidInventorySlot(item);
		if (val3 == null || val3.numTransfer < 1)
		{
			return "noroom";
		}
		bool all = m.all;
		run = delegate
		{
			if (all)
			{
				GraphUtils.TryAcceptAll(node, item, n);
			}
			else
			{
				GraphUtils.TryAcceptOnce(node, item, n);
			}
			DestroyIfOrphan(item);
		};
		return null;
	}

	private static string PlanTrash(ItemsReq m, int from, out Action run)
	{
		run = null;
		string text = CheckItem(m, out var item);
		if (text != null)
		{
			return text;
		}
		EmporiumEntry instance = EmporiumEntry.Instance;
		if (((instance != null) ? instance.trashcan : null) != null)
		{
			PixelWindow contentWindow = instance.trashcan.contentWindow;
			GraphNodeStorage val = ((contentWindow != null) ? contentWindow.child : null);
			GameInventory trash = (GameInventory)(object)((val is GameInventory) ? val : null);
			if (trash != null)
			{
				if (!GeneralHelper.IsItemOwned(item) || item == instance.trashcan || item.MaxNumRemove() < 1 || item.parentInventory == trash)
				{
					return "notallowed";
				}
				if ((Object)(object)MapUIManager.Instance != (Object)null && MapUIManager.Instance.isOutside)
				{
					return "notallowed";
				}
				string own3 = Systems.Bays.MoveRefusal(from, item, (GraphNodeStorage)(object)trash);
				if (own3 != null)
				{
					counterRefusal = own3;
					return "counter";
				}
				text = Counter(from, (GraphNodeStorage)item.parentInventory);
				if (text != null)
				{
					return text;
				}
				run = delegate
				{
					GraphUtils.TryAcceptAll((GraphNodeStorage)(object)trash, item, -1);
				};
				return null;
			}
		}
		return "target";
	}

	private static string PlanGroup(ItemsReq m, int from, out Action run)
	{
		run = null;
		if (m.ids == null || m.ids.Length == 0 || m.ids.Length > 400)
		{
			return "bad";
		}
		List<GameItem> items = new List<GameItem>();
		long[] ids = m.ids;
		for (int i = 0; i < ids.Length; i++)
		{
			GameItem val = Items.Find(ids[i]);
			if (val == null)
			{
				return "gone";
			}
			if (!items.Contains(val))
			{
				items.Add(val);
			}
		}
		GameInventory inv = Items.ResolveInventory(m.to);
		if (inv == null)
		{
			return "target";
		}
		string text = FormationProblem(items, inv, m.dx, m.dy);
		if (text != null)
		{
			return text;
		}
		List<GraphNodeStorage> list = new List<GraphNodeStorage> { (GraphNodeStorage)(object)inv };
		foreach (GameItem item in items)
		{
			list.Add((GraphNodeStorage)(object)item.parentInventory);
			string own4 = Systems.Bays.MoveRefusal(from, item, (GraphNodeStorage)(object)inv);
			if (own4 != null)
			{
				counterRefusal = own4;
				return "counter";
			}
		}
		text = Counter(from, list.ToArray());
		if (text != null)
		{
			return text;
		}
		int dx = m.dx;
		int dy = m.dy;
		run = delegate
		{
			foreach (GameItem item2 in items)
			{
				if (!Items.IsGone(item2))
				{
					GameInventory parentInventory = item2.parentInventory;
					if (parentInventory != null)
					{
						parentInventory.Expel(item2);
					}
				}
			}
			foreach (GameItem item3 in items)
			{
				if (!Items.IsGone(item3))
				{
					GridShape modifiedShape = item3.modifiedShape;
					item3.modifiedShape = (GridShape)(object)new GridShapeBuilder(modifiedShape).SetTransform(modifiedShape.minX + dx, modifiedShape.minY + dy, modifiedShape.flipped, modifiedShape.orientation);
					inv.UncheckedAccept(item3);
				}
			}
		};
		return null;
	}

	public static string FormationProblem(IList<GameItem> items, GameInventory inv, int dx, int dy)
	{
		if (items == null || items.Count == 0 || inv == null)
		{
			return "bad";
		}
		if (inv.IsInsertLocked() || !inv.SupportsGroupPlacement)
		{
			return "notallowed";
		}
		GameInventory val = null;
		foreach (GameItem item in items)
		{
			if (val == null)
			{
				val = item.parentInventory;
			}
			else if (val != item.parentInventory)
			{
				val = null;
				break;
			}
		}
		bool flag = inv != val;
		if (flag && !GeneralHelper.MayPlayerInsertInto(inv))
		{
			return "notallowed";
		}
		List<GameItem> list = new List<GameItem>(items);
		foreach (GameItem item2 in items)
		{
			if (Items.IsGone(item2) || item2.parentInventory == null)
			{
				return "gone";
			}
			if (!item2.MayRemove() || item2.MaxNumRemove() < item2.unitCount)
			{
				return "locked";
			}
			if (flag && !inv.MayHaveValidInventorySlot(item2))
			{
				return "notallowed";
			}
			GridShape modifiedShape = item2.modifiedShape;
			GridShape val2 = new GridShapeBuilder(modifiedShape).SetPosition(modifiedShape.minX + dx, modifiedShape.minY + dy).Build();
			if (!inv.CheckShapePlacement(item2, val2, list))
			{
				return "noroom";
			}
		}
		return null;
	}

	private static string PlanSort(ItemsReq m, int from, bool undo, out Action run)
	{
		run = null;
		GameInventory val = Items.ResolveInventory(m.to);
		GameGridInventory inv = (GameGridInventory)(object)((val is GameGridInventory) ? val : null);
		if (inv == null)
		{
			return "target";
		}
		if (undo ? (!InventorySortHelper.CanUndo(inv)) : (!InventorySortHelper.CanSort(inv)))
		{
			return "notallowed";
		}
		string text = Counter(from, (GraphNodeStorage)inv);
		if (text != null)
		{
			return text;
		}
		bool big = m.big;
		bool end = m.end;
		bool rot = m.autoRot;
		if (undo)
		{
			run = delegate
			{
				InventorySortHelper.Undo(inv);
			};
		}
		else
		{
			run = delegate
			{
				using (SortRotate(rot))
				{
					InventorySortHelper.Sort(inv, big, end);
				}
			};
		}
		return null;
	}

	private static string PlanUnload(ItemsReq m, int from, out Action run)
	{
		run = null;
		string text = CheckItem(m, out var item, needRemove: false);
		if (text != null)
		{
			return text;
		}
		if (item.contentWindow == null || item.IsTag("IS_UNLOAD_DISABLED_TAG"))
		{
			return "notallowed";
		}
		text = Counter(from, (GraphNodeStorage)item.parentInventory, (GraphNodeStorage)(object)item);
		if (text != null)
		{
			return text;
		}
		run = delegate
		{
			item.TryUnloadAll(true);
		};
		return null;
	}

	private static string PlanPin(ItemsReq m, int from, out Action run)
	{
		run = null;
		GameItem item;
		string text = CheckItem(m, out item, needRemove: false);
		if (text != null)
		{
			return text;
		}
		if (!ItemPinHelper.MayPin(item))
		{
			return "notallowed";
		}
		if (ItemPinHelper.IsPinned(item) == m.on)
		{
			return "nothing";
		}
		bool on = m.on;
		long id = m.i;
		run = delegate
		{
			ItemPinHelper.SetPinned(item, on);
			ItemsMoveRecorder.Add(new ItemsOp
			{
				t = "pin",
				i = id,
				on = on
			});
		};
		return null;
	}

	private static string PlanOpened(ItemsReq m, int from, out Action run)
	{
		run = null;
		GameItem item = Items.Find(m.i);
		if (item == null)
		{
			return "gone";
		}
		if (!item.IsTag("TRACK_OPENED_TAG") || item.IsTag("OPENED_TAG"))
		{
			return "nothing";
		}
		long id = m.i;
		run = delegate
		{
			item.EnableTag("OPENED_TAG", false);
			ItemsMoveRecorder.Add(new ItemsOp
			{
				t = "tag",
				i = id,
				tag = "OPENED_TAG",
				on = true
			});
		};
		return null;
	}

	private static string PlanTransfer(ItemsReq m, int from, out Action run)
	{
		run = null;
		EmporiumEntry e = EmporiumEntry.Instance;
		if ((Object)(object)e == (Object)null)
		{
			return "target";
		}
		// While the host is out on its own, "the counters" are the ones the players at home see (HostTrip clones).
		GraphNodeStorage frontNode = Systems.HostTrip.Front(e);
		GraphNodeStorage backNode = Systems.HostTrip.Back(e);
		string text = Counter(from, frontNode, backNode);
		if (text != null)
		{
			return text;
		}
		bool front = m.frontVisible;
		run = delegate
		{
			List<GameItem> onFront = GraphUtils.FindAllChildrenType<GameItem>(frontNode, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).FindAll((GameItem i) => (object)i.parentInventory == frontNode);
			List<GameItem> onBack = GraphUtils.FindAllChildrenType<GameItem>(backNode, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).FindAll((GameItem i) => (object)i.parentInventory == backNode);
			if (front && onFront.Count != 0)
			{
				foreach (GameItem item in onFront)
				{
					GraphUtils.TryAcceptAll(backNode, item, -1);
				}
				return;
			}
			// Same as the game's TransferOwnedItemBackToInv / TransferOwnedItemFrontToInv, on the counters chosen above.
			foreach (GameItem item2 in onBack)
			{
				if (GeneralHelper.IsItemOwned(item2))
				{
					GraphUtils.TryAcceptAll((GraphNodeStorage)(object)e.invElement, item2, -1);
				}
			}
			if (front)
			{
				foreach (GameItem item3 in onFront)
				{
					if (GeneralHelper.IsItemOwned(item3))
					{
						GraphUtils.TryAcceptAll((GraphNodeStorage)(object)e.invElement, item3, -1);
					}
				}
			}
		};
		return null;
	}

	private static void DestroyIfOrphan(GameItem item)
	{
		if (!Items.IsGone(item) && item.parentInventory == null)
		{
			item.Destroy();
		}
	}

	private static IDisposable SortRotate(bool v)
	{
		bool? prev = SortRotateOverride;
		SortRotateOverride = v;
		return new Undo(delegate
		{
			SortRotateOverride = prev;
		});
	}

	public static string RejectTextOf(int q)
	{
		if (q == 0 || !rejectTexts.TryGetValue(q, out var value))
		{
			return null;
		}
		return value;
	}

	private static void OnReject(ItemsReject r)
	{
		if (r != null)
		{
			RejectCount++;
			LastReject = r;
			if (r.q != 0)
			{
				outcome[r.q] = "rejected:" + r.why;
				rejectTexts[r.q] = r.text;
			}
			Log.Info("items: the server refused request " + r.q + " (" + r.kind + "): " + r.why);
			if (!string.IsNullOrEmpty(r.text) && r.why != "nothing")
			{
				Notify(r.text);
			}
		}
	}

	private static void OnOps(ItemsOps m)
	{
		if (m?.ops == null)
		{
			return;
		}
		if (!Items.Ready)
		{
			Log.Warn("items: #" + m.seq + " arrived with no shop loaded; skipped");
			return;
		}
		CancelLocalGestures(m.ops);
		string text = null;
		int num = 0;
		itemsDepth++;
		try
		{
			using (Replay.Client(m.seed ?? new Seed(), m.actor))
			{
				using (Items.Play(m.tape))
				{
					if (m.kind == "items.sort")
					{
						RememberSortUndo(m.at);
					}
					foreach (ItemsOp op in m.ops)
					{
						string text2;
						try
						{
							text2 = Apply(op);
						}
						catch (Exception ex)
						{
							text2 = op.t + " threw " + ex.GetType().Name + ": " + ex.Message;
						}
						if (text2 != null)
						{
							text = text ?? text2;
						}
						else
						{
							num++;
						}
					}
				}
			}
		}
		finally
		{
			itemsDepth--;
		}
		Systems.Standing.NoteMoves(m.actor, m.ops);
		long num2 = RandomHash();
		LastRandomCheck = ((m.ra == 0L || num2 == 0L) ? "not compared" : ((num2 == m.ra) ? "same" : "differs"));
		if (m.ra != 0L && num2 != 0L && num2 != m.ra)
		{
			RandomMismatches++;
			Log.Warn("items: #" + m.seq + " (" + m.kind + ") ended with a different random state here: the replay rolled a different number of times than the server (" + RandomMismatches + " so far)");
		}
		if (m.nego)
		{
			SetNegotiatedItem(m.negoId, m.seq);
		}
		if (m.kind == "items.sortUndo")
		{
			ForgetSortUndo(m.at);
		}
		long num3 = ActionDigest();
		if (text == null && m.dg != 0L && num3 != 0L && num3 != m.dg)
		{
			text = "item digest after the action differs (here " + ProbablyTogether.Digest.Compute().Describe() + ")";
		}
		AppliedCount++;
		LastApplied = m;
		if (m.actor == Net.LocalPlayer && m.q != 0)
		{
			outcome[m.q] = ((text == null) ? "applied" : "applied-mismatch");
		}
		if (text != null)
		{
			ApplyMismatches++;
			LastApplyProblem = "#" + m.seq + " " + m.kind + ": " + text;
			Log.Warn("items: #" + m.seq + " (" + m.kind + " by " + Net.PlayerName(m.actor) + ") applied " + num + "/" + m.ops.Count + " step(s); first problem: " + text);
			if (!DayLog.Replaying)
			{
				Snapshot.RequestResync("items #" + m.seq + " " + m.kind + ": " + text);
			}
		}
	}

	/// Which game rule blocks taking an item out, for the log. A shape-changing item (a silencer) may only leave
	/// if every container around it still fits where it sits without it.
	internal static string WhyRefused(GameItem item)
	{
		try
		{
			GameInventory from = item?.parentInventory;
			if (from == null)
			{
				return "why: the item is in no inventory";
			}
			string text = "why: may leave " + item.MaxNumRemove() + " of " + item.unitCount + (item.MayParentShapeModifier() ? ", changes the shape of what holds it" : "");
			foreach (GameItem box in GraphUtils.FindParentsType<GameItem>((GraphNodeStorage)(object)from, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				GameInventory inv = box?.parentInventory;
				if (inv == null)
				{
					continue;
				}
				(GridShape, int, int, TagSystem) st = box.ComputeModifiedShapeState(null, null, null, null, item);
				GridShape shape = st.Item1;
				SlotMarker slot = inv.TryInventorySlot(box, shape, st.Item4);
				text = text + "; around it #" + Ids.Of(box) + " " + box.identifier + " in " + Items.RefOf((GraphNodeStorage)(object)inv) + "@" + shape.minX + "," + shape.minY + " " + shape.width + "x" + shape.height + ": fits " + (slot?.numTransfer ?? (-1)) + ", may go in " + inv.MayHaveValidInventorySlot(box) + ", may leave " + box.MaxNumRemove();
				if (inv is GameGridInventory grid)
				{
					bool inside = shape.FullyIntersects(grid.inventoryShape, (byte b, byte bo) => b != 1 || bo == 0);
					List<string> over = (from o in grid.childItems
						where o != null && o != box && shape.Intersects(o.modifiedShape)
						select "#" + Ids.Of(o) + " " + o.identifier + "@" + o.modifiedShape.minX + "," + o.modifiedShape.minY).ToList();
					text = text + ", grid " + grid.inventoryShape.width + "x" + grid.inventoryShape.height + ", inside it " + inside + ", overlaps " + ((over.Count == 0) ? "nothing" : string.Join(" ", over));
				}
			}
			return text;
		}
		catch (Exception ex)
		{
			return "why: could not tell (" + ex.Message + ")";
		}
	}

	/// The last remote step that applied differently here.
	public static string LastApplyProblem;

	/// Server-made moves the game refused here and the mod placed anyway.
	public static int PlacedAnyway;

	/// A whole item to a cell of an inventory with none of the game's "may it leave" questions.
	private static bool PlaceAnyway(GameItem item, GameInventory to, GridShape shape)
	{
		try
		{
			if (item == null || to == null || shape == null)
			{
				return false;
			}
			GameInventory from = item.parentInventory;
			if (from != null && !from.Expel(item))
			{
				return false;
			}
			if (item.parentInventory != null)
			{
				return false;
			}
			item.modifiedShape = new GridShapeBuilder(item.modifiedShape).SetTransform(shape);
			return to.UncheckedAccept(item);
		}
		catch (Exception ex)
		{
			Log.Warn("items: placing #" + Ids.Of(item) + " anyway threw: " + ex.Message);
			return false;
		}
	}

	private static string Apply(ItemsOp op)
	{
		GameItem val = ((op.i != 0L) ? Items.Find(op.i) : null);
		switch (op.t)
		{
		case "place":
		{
			if (val == null)
			{
				return "place: item #" + op.i + " not found";
			}
			GameInventory val4 = Items.ResolveInventory(op.to);
			if (val4 == null)
			{
				return "place: inventory " + op.to?.ToString() + " not found";
			}
			SlotMarker val5 = new SlotMarker
			{
				item = val,
				inventory = val4,
				itemGridShape = Items.Shape(val, op.x, op.y, op.f, op.rot),
				numTransfer = op.n
			};
			int num = val5.TryAcceptOnce(op.n);
			if (num != op.n && num == 0 && op.nw == 0L && op.n >= val.unitCount && PlaceAnyway(val, val4, val5.itemGridShape))
			{
				// A server-made move refused by a game rule on this copy only: place it anyway; the action's digest still checks agreement.
				PlacedAnyway++;
				if (Log.Allow("items|placeAnyway", 10000L, out var skippedPlace))
				{
					Log.Warn("items: the game refused #" + op.i + " -> " + op.to?.ToString() + " here although the server made the move; placed anyway" + Log.More(skippedPlace));
				}
				num = op.n;
			}
			if (num != op.n)
			{
				return "place: moved " + num + " of " + op.n + " (#" + op.i + " -> " + op.to?.ToString() + "); " + WhyRefused(val);
			}
			if (op.nw != 0L)
			{
				if (val5.item == val)
				{
					return "place: expected a split-off item #" + op.nw;
				}
				if (Ids.Of(val5.item) != op.nw)
				{
					Ids.Bind(val5.item, op.nw);
				}
			}
			return null;
		}
		case "rep":
		{
			if (val == null)
			{
				return "rep: item #" + op.i + " not found";
			}
			GameInventory val9 = Items.ResolveInventory(op.to);
			if (val9 == null)
			{
				return "rep: inventory " + op.to?.ToString() + " not found";
			}
			if (new SlotMarker
			{
				item = val,
				inventory = val9,
				itemGridShape = Items.Shape(val, op.x, op.y, op.f, op.rot),
				numTransfer = val.unitCount
			}.AcceptUnchecked() <= 0)
			{
				return "rep: refused";
			}
			return null;
		}
		case "stack":
		{
			GameItem val7 = Items.Find(op.onto);
			if (val == null || val7 == null)
			{
				return "stack: item #" + op.i + " or #" + op.onto + " not found";
			}
			SlotMarker.StackItemUnchecked(val, val7, op.n);
			return null;
		}
		case "expel":
		{
			if (val == null)
			{
				return "expel: item #" + op.i + " not found";
			}
			GameInventory parentInventory = val.parentInventory;
			if (parentInventory == null)
			{
				return "expel: #" + op.i + " is in no inventory";
			}
			GraphNodeStorage val8 = ((op.to != null) ? Items.Resolve(op.to) : null);
			if (val8 != null && (object)val8 != parentInventory)
			{
				return "expel: #" + op.i + " is in " + Items.RefOf((GraphNodeStorage)(object)parentInventory)?.ToString() + ", the server had " + op.to;
			}
			if (!parentInventory.Expel(val))
			{
				return "expel: refused";
			}
			return null;
		}
		case "accept":
		{
			if (val == null)
			{
				return "accept: item #" + op.i + " not found";
			}
			GameInventory val6 = Items.ResolveInventory(op.to);
			if (val6 == null)
			{
				return "accept: inventory " + op.to?.ToString() + " not found";
			}
			if (val.parentInventory != null)
			{
				return "accept: #" + op.i + " is still in " + Items.RefOf((GraphNodeStorage)(object)val.parentInventory);
			}
			val.modifiedShape = (GridShape)(object)new GridShapeBuilder(val.modifiedShape).SetTransform(op.x, op.y, op.f, op.rot);
			if (!val6.UncheckedAccept(val))
			{
				return "accept: " + op.to?.ToString() + " refused #" + op.i;
			}
			return null;
		}
		case "destroy":
			if (val == null)
			{
				return null;
			}
			val.Destroy();
			return null;
		case "pin":
			if (val == null)
			{
				return "pin: item #" + op.i + " not found";
			}
			ItemPinHelper.SetPinned(val, op.on);
			return null;
		case "tag":
			if (val == null || string.IsNullOrEmpty(op.tag))
			{
				return "tag: item #" + op.i + " not found";
			}
			val.SetEnableDisableTag(op.tag, op.on);
			return null;
		case "sub":
		{
			GameInventory val2 = ((op.to != null) ? Items.ResolveInventory(op.to) : null);
			GameItem val3 = Items.ReplaceSubtree(op.sub, val2);
			if (val3 == null)
			{
				return "sub: could not build #" + op.i;
			}
			if (op.to != null && val3.parentInventory == null)
			{
				return "sub: #" + op.i + " not placed in " + op.to;
			}
			Systems.Bays.OpenTree(val3);
			return null;
		}
		default:
			return "unknown step '" + op.t + "'";
		}
	}

	private static void CancelLocalGestures(List<ItemsOp> ops)
	{
		try
		{
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			ItemMultiSelectHandler current2 = ItemMultiSelectHandler.current;
			ItemSelectHandler current3 = ItemSelectHandler.current;
			GameItem val = (((Object)(object)current != (Object)null && current.IsDraggingItem) ? current.GetCurrentItem() : null);
			GameItem val2 = (((Object)(object)current3 != (Object)null) ? current3.GetEquippedItem() : null);
			bool flag = (Object)(object)current2 != (Object)null && current2.IsGroupDragging;
			if (val == null && val2 == null && !flag)
			{
				return;
			}
			foreach (ItemsOp op in ops)
			{
				GameItem val3 = ((op.i != 0L) ? Items.Find(op.i) : null);
				GameItem val4 = ((op.onto != 0L) ? Items.Find(op.onto) : null);
				GameItem[] array = (GameItem[])(object)new GameItem[2] { val3, val4 };
				foreach (GameItem val5 in array)
				{
					if (val5 != null)
					{
						if (val != null && val5 == val)
						{
							current.Cancel();
							val = null;
						}
						if (flag && current2.IsGroupDraggingItem(val5))
						{
							current2.Cancel();
							flag = false;
						}
						if (val2 != null && val5 == val2)
						{
							current3.EndSelect(false);
							val2 = null;
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("items: cancelling a local drag: " + ex.Message);
		}
	}

	private static void RememberSortUndo(ItemRef at)
	{
		try
		{
			GameInventory obj = Items.ResolveInventory(at);
			GameGridInventory val = (GameGridInventory)(object)((obj is GameGridInventory) ? obj : null);
			if (val == null || !(undoField?.GetValue(null) is Dictionary<GameGridInventory, List<(GameItem, GridShape)>> dictionary))
			{
				return;
			}
			List<(GameItem, GridShape)> list = new List<(GameItem, GridShape)>();
			foreach (GameItem childItem in ((GameInventory)val).childItems)
			{
				if (!ItemPinHelper.IsPinned(childItem) && !DebrisHelper.IsDebris(childItem))
				{
					list.Add((childItem, childItem.modifiedShape.Clone()));
				}
			}
			dictionary[val] = list;
		}
		catch (Exception ex)
		{
			Log.Warn("items: sort undo snapshot: " + ex.Message);
		}
	}

	private static void ForgetSortUndo(ItemRef at)
	{
		try
		{
			GameInventory obj = Items.ResolveInventory(at);
			GameGridInventory val = (GameGridInventory)(object)((obj is GameGridInventory) ? obj : null);
			if (val != null && undoField?.GetValue(null) is Dictionary<GameGridInventory, List<(GameItem, GridShape)>> dictionary)
			{
				dictionary.Remove(val);
			}
		}
		catch
		{
		}
	}

	internal static void Notify(string text)
	{
		if (string.IsNullOrEmpty(text) || !Net.IsClient)
		{
			return;
		}
		LastNotice = text;
		try
		{
			StoreUIManager instance = StoreUIManager.Instance;
			if (instance != null)
			{
				instance.Notify(Kit.Safe(Lang.Tr(text)), "default");
			}
		}
		catch
		{
		}
	}

	private static GameItem NegotiatedItem()
	{
		try
		{
			return PlayerStore.HasInstance ? PlayerStore.Instance.currentNegociatedItem : null;
		}
		catch
		{
			return null;
		}
	}

	private static void SetNegotiatedItem(long id, int seq)
	{
		if (!PlayerStore.HasInstance)
		{
			return;
		}
		GameItem val = ((id != 0L) ? Items.Find(id) : null);
		if (id != 0L && val == null)
		{
			Log.Warn("items: #" + seq + " negotiated item #" + id + " not found here");
			return;
		}
		PlayerStore instance = PlayerStore.Instance;
		if (instance.currentNegociatedItem != val)
		{
			NegoFixes++;
			if (NegoFixes <= 5 || NegoFixes % 50 == 0)
			{
				Log.Info("items: #" + seq + " negotiated item here was " + ((instance.currentNegociatedItem != null) ? (instance.currentNegociatedItem.identifier + "#" + Ids.Of(instance.currentNegociatedItem)) : "none") + ", the server's is " + ((val != null) ? (val.identifier + "#" + id) : "none") + "; set (" + NegoFixes + " so far)");
			}
			instance.currentNegociatedItem = val;
		}
	}

	public static long RandomHash()
	{
		try
		{
			string text = JsonUtility.ToJson((object)UnityEngine.Random.state);
			ulong num = 14695981039346656037uL;
			string text2 = text;
			foreach (char c in text2)
			{
				num ^= c;
				num *= 1099511628211L;
			}
			return (long)(num | 1);
		}
		catch
		{
			return 0L;
		}
	}

	public static long ActionDigest()
	{
		try
		{
			if (!Items.Ready)
			{
				return 0L;
			}
			MsgDigest msgDigest = ProbablyTogether.Digest.Compute();
			ulong result;
			return (long)(((ulong.TryParse(msgDigest.hash, NumberStyles.HexNumber, null, out result) ? result : 0) ^ (ulong)(msgDigest.items * -7046029254386353131L)) | 1);
		}
		catch
		{
			return 0L;
		}
	}

	public static string Digest()
	{
		if (!Items.Ready)
		{
			return "";
		}
		ulong num = 0uL;
		int num2 = 0;
		foreach (GameItem item in Items.All())
		{
			ulong h = 1469598103934665603uL;
			Mix(ref h, Ids.Of(item).ToString());
			Mix(ref h, item.identifier);
			Mix(ref h, item.unitCount.ToString());
			Mix(ref h, Items.RefOf((GraphNodeStorage)(object)item.parentInventory)?.ToString() ?? "-");
			GridShape modifiedShape = item.modifiedShape;
			if (modifiedShape != null)
			{
				Mix(ref h, modifiedShape.minX + "," + modifiedShape.minY + "," + modifiedShape.orientation + "," + (modifiedShape.flipped ? 1 : 0));
			}
			Mix(ref h, (ItemPinHelper.IsPinned(item) ? "P" : "") + (item.IsTag("OPENED_TAG") ? "O" : ""));
			num += h;
			num2++;
		}
		return num2 + ":" + num.ToString("x16");
	}

	private static void Mix(ref ulong h, string s)
	{
		if (s == null)
		{
			s = "\0";
		}
		string text = s;
		foreach (char c in text)
		{
			h ^= c;
			h *= 1099511628211uL;
		}
		h ^= 255uL;
		h *= 1099511628211uL;
	}
}
