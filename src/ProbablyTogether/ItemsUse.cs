using System;
using System.Collections.Generic;
using System.Reflection;
using ProbablyTogether.UI;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

public static class ItemsUse
{
	private sealed class QuietScope : IDisposable
	{
		private bool done;

		public QuietScope()
		{
			EnterQuiet();
		}

		public void Dispose()
		{
			if (!done)
			{
				done = true;
				LeaveQuiet();
			}
		}
	}

	public sealed class MsgRequest
	{
		public long src;

		public long dst;

		public int n;
	}

	public sealed class MsgConfirm
	{
		public long of;

		public int index;

		public bool yes;
	}

	public sealed class MsgRenameTab
	{
		public long bay;

		public int index;

		public string name;

		public int n;
	}

	public sealed class MsgScratch
	{
		public long item;

		public int slot;

		public int n;
	}

	public sealed class MsgPerk
	{
		public long item;

		public string perk;

		public int n;
	}

	public sealed class MsgRefused
	{
		public int n;

		public int op;

		public string reason;

		public bool notice;
	}

	public sealed class MsgUse
	{
		public long seq;

		public int op;

		public int actor;

		public long src;

		public long dst;

		public string srcKind;

		public string dstKind;

		public bool bm;

		public bool ran;

		public bool run = true;

		public int index;

		public string text;

		public long of;

		public bool yes;

		public Seed seed;

		public int uidAfter;

		public long digest;
	}

	private sealed class PendingConfirm
	{
		public Action Yes;

		public Action No;

		public int Actor;

		public UseOp Op;

		public GameItem Source;

		public GameItem Target;

		public long SourceId;

		public long TargetId;

		public long Ms;
	}

	private static readonly List<KeyValuePair<string, Func<UseContext, string>>> validators = new List<KeyValuePair<string, Func<UseContext, string>>>();

	private static readonly List<KeyValuePair<string, Func<UseContext, UseReplay?>>> modes = new List<KeyValuePair<string, Func<UseContext, UseReplay?>>>();

	private static UseContext scope;

	private static int quietNest;

	private static long seq;

	private static int requestN;

	private static bool inited;

	private static readonly Dictionary<long, PendingConfirm> pending = new Dictionary<long, PendingConfirm>();

	private static readonly Queue<long> pendingOrder = new Queue<long>();

	private const int MaxPending = 64;

	private static readonly HashSet<long> scratchStartSent = new HashSet<long>();

	private static readonly FieldInfo selectCurrent = AccessTools.Field(typeof(ItemSelectHandler), "currentItem");

	private static AudioSource[] quietSources;

	private static bool[] quietWasPlaying;

	private static readonly MethodInfo perkRebuild = AccessTools.Method(typeof(MindReprogrammerHelper), "RebuildList", (Type[])null, (Type[])null);

	public static UseContext Current => scope;

	public static bool QuietUi => quietNest > 0;

	public static int SentCount { get; private set; }

	public static int QuietSoundsBlocked { get; internal set; }

	public static int QuietSoundsStopped { get; private set; }

	public static int SoundsPlayed { get; internal set; }

	public static int QuietNoticesBlocked { get; internal set; }

	public static int NoticesShown { get; internal set; }

	public static int RefusedCount { get; private set; }

	public static string LastRefusal { get; private set; } = "";

	public static int AppliedCount { get; private set; }

	public static int MismatchCount { get; private set; }

	public static event Action<UseContext> Performed;

	public static void RegisterValidator(Func<UseContext, string> check)
	{
		Add(validators, null, check?.Method.DeclaringType?.Name, check);
	}

	public static void RegisterValidator(string owner, Func<UseContext, string> check)
	{
		Add(validators, owner, owner, check);
	}

	public static void RegisterReplayMode(string owner, Func<UseContext, UseReplay?> pick)
	{
		Add(modes, owner, owner, pick);
	}

	public static bool Unregister(string owner)
	{
		if (string.IsNullOrEmpty(owner))
		{
			return false;
		}
		bool flag = false;
		lock (validators)
		{
			flag |= validators.RemoveAll((KeyValuePair<string, Func<UseContext, string>> kv) => kv.Key == owner) > 0;
		}
		lock (modes)
		{
			return flag | (modes.RemoveAll((KeyValuePair<string, Func<UseContext, UseReplay?>> kv) => kv.Key == owner) > 0);
		}
	}

	public static string Registered()
	{
		string text;
		lock (validators)
		{
			text = string.Join(", ", validators.ConvertAll((KeyValuePair<string, Func<UseContext, string>> kv) => kv.Key));
		}
		string text2;
		lock (modes)
		{
			text2 = string.Join(", ", modes.ConvertAll((KeyValuePair<string, Func<UseContext, UseReplay?>> kv) => kv.Key));
		}
		return "validators: " + ((text.Length > 0) ? text : "none") + "; modes: " + ((text2.Length > 0) ? text2 : "none");
	}

	private static void Add<T>(List<KeyValuePair<string, T>> list, string replaceOwner, string name, T fn) where T : class
	{
		if (fn == null)
		{
			return;
		}
		lock (list)
		{
			if (!string.IsNullOrEmpty(replaceOwner))
			{
				int num = list.FindIndex((KeyValuePair<string, T> kv) => kv.Key == replaceOwner);
				if (num >= 0)
				{
					list[num] = new KeyValuePair<string, T>(replaceOwner, fn);
					return;
				}
			}
			list.Add(new KeyValuePair<string, T>(string.IsNullOrEmpty(name) ? "?" : name, fn));
		}
	}

	public static IDisposable Quiet()
	{
		return new QuietScope();
	}

	public static bool RequestTarget(GameItem source, GameItem target)
	{
		return Request(UseOp.Target, source, target);
	}

	public static bool RequestActivate(GameItem item)
	{
		return Request(UseOp.Activate, item, null);
	}

	public static bool RequestToggle(GameItem item)
	{
		return Request(UseOp.Toggle, item, null);
	}

	private static long Key(long useSeq, int index)
	{
		return useSeq * 64 + Math.Min(index, 63);
	}

	[SystemInit]
	internal static void Init()
	{
		if (!inited)
		{
			inited = true;
			On("items.target", delegate(MsgRequest m, int p)
			{
				OnRequest(UseOp.Target, m, p);
			}, null);
			On("items.activate", delegate(MsgRequest m, int p)
			{
				OnRequest(UseOp.Activate, m, p);
			}, null);
			On("items.toggle", delegate(MsgRequest m, int p)
			{
				OnRequest(UseOp.Toggle, m, p);
			}, null);
			On<MsgConfirm>("items.use.confirm", OnConfirmRequest, null);
			On<MsgRenameTab>("storage.renameTab", OnRenameRequest, null);
			On<MsgScratch>("items.scratch", OnScratchRequest, null);
			On<MsgPerk>("items.perk", OnPerkRequest, null);
			On<MsgUse>("items.use", null, OnUse);
			On<MsgRefused>("items.use.refused", null, OnRefused);
			Net.PlayerLeft += delegate(PlayerInfo p)
			{
				DropPendingOf(p.Id);
			};
			Digest.AddPart("items-use", DigestPart);
		}
	}

	private static void On<T>(string kind, Action<T, int> onServer, Action<T> onClient) where T : class
	{
		try
		{
			Net.On(kind, onServer, onClient);
		}
		catch (Exception ex)
		{
			Log.Error("items-use: cannot register " + kind + ": " + ex.Message);
		}
	}

	internal static bool Intercept(UseOp op, GameItem item, GameItem target)
	{
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (IsChoiceTile(item))
		{
			return true;
		}
		if (!Net.IsClient)
		{
			return !ServerOriginated(op, item, target, 0, null);
		}
		Request(op, item, target);
		return false;
	}

	internal static bool IsChoiceTile(GameItem item)
	{
		GameInventory val = ((item != null) ? item.parentInventory : null);
		EmporiumEntry instance = EmporiumEntry.Instance;
		if (val != null && (Object)(object)instance != (Object)null)
		{
			if ((object)val != instance.responseInventory)
			{
				return (object)val == instance.responseInventoryClosable;
			}
			return true;
		}
		return false;
	}

	internal static bool InterceptRename(GameItem bay, int index, string name)
	{
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (!Net.IsClient)
		{
			return !ServerOriginated(UseOp.RenameTab, bay, null, index, name);
		}
		if (!ReadyToSend("storage.renameTab"))
		{
			return false;
		}
		long num = Ids.Of(bay);
		if (num == 0L)
		{
			Unknown("rename a tab of", bay);
			return false;
		}
		Net.ToServer("storage.renameTab", new MsgRenameTab
		{
			bay = num,
			index = index,
			name = Clip(name, 64),
			n = ++requestN
		});
		SentCount++;
		return false;
	}

	internal static bool InterceptScratch(GameItem ticket, int slot)
	{
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (!Net.IsClient)
		{
			return !ServerOriginated(UseOp.Scratch, ticket, null, slot, null);
		}
		if (!ReadyToSend("items.scratch"))
		{
			return false;
		}
		long num = Ids.Of(ticket);
		if (num == 0L)
		{
			Unknown("scratch", ticket);
			return false;
		}
		if (slot == 0 && !scratchStartSent.Add(num))
		{
			return false;
		}
		Net.ToServer("items.scratch", new MsgScratch
		{
			item = num,
			slot = slot,
			n = ++requestN
		});
		SentCount++;
		return false;
	}

	internal static bool InterceptPerk(GameItem item, StartingPerk perk)
	{
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		string text = perk?.id;
		if (!Net.IsClient)
		{
			return !ServerOriginated(UseOp.Perk, item, null, 0, text);
		}
		if (!ReadyToSend("items.perk"))
		{
			return false;
		}
		long num = Ids.Of(item);
		if (num == 0L)
		{
			Unknown("reprogram with", item);
			return false;
		}
		if (string.IsNullOrEmpty(text))
		{
			Log.Warn("items-use: a perk with no id: not sent");
			return false;
		}
		Net.ToServer("items.perk", new MsgPerk
		{
			item = num,
			perk = Clip(text, 64),
			n = ++requestN
		});
		SentCount++;
		return false;
	}

	internal static bool OnConfirmPanel(ref Action onConfirm, ref Action onCancel, bool single)
	{
		UseContext useContext = scope;
		if (useContext == null || !Net.IsOnline)
		{
			return true;
		}
		int index = useContext.ConfirmCount++;
		long num = Key(useContext.Seq, index);
		PendingConfirm value = new PendingConfirm
		{
			Yes = onConfirm,
			No = (single ? null : onCancel),
			Actor = useContext.Player,
			Op = (useContext.ConfirmCallback ? useContext.OriginalOp : useContext.Op),
			Source = useContext.Source,
			Target = useContext.Target,
			SourceId = useContext.SourceId,
			TargetId = useContext.TargetId,
			Ms = Clock.Ms
		};
		pending[num] = value;
		pendingOrder.Enqueue(num);
		while (pendingOrder.Count > 64)
		{
			pending.Remove(pendingOrder.Dequeue());
		}
		if (useContext.Quiet)
		{
			return false;
		}
		long of = useContext.Seq;
		onConfirm = delegate
		{
			Net.ToServer("items.use.confirm", new MsgConfirm
			{
				of = of,
				index = index,
				yes = true
			});
			SentCount++;
		};
		if (!single)
		{
			onCancel = delegate
			{
				Net.ToServer("items.use.confirm", new MsgConfirm
				{
					of = of,
					index = index,
					yes = false
				});
				SentCount++;
			};
		}
		return true;
	}

	internal static bool MayClosePanel()
	{
		if (scope != null)
		{
			if (!scope.Quiet)
			{
				return !scope.ConfirmCallback;
			}
			return false;
		}
		return true;
	}

	private static bool Request(UseOp op, GameItem item, GameItem target)
	{
		if (!Net.IsClient)
		{
			return false;
		}
		string kind = op switch
		{
			UseOp.Activate => "items.activate", 
			UseOp.Target => "items.target", 
			_ => "items.toggle", 
		};
		if (!ReadyToSend(kind))
		{
			return false;
		}
		if (item == null || (op == UseOp.Target && target == null))
		{
			Log.Warn("items-use: " + op.ToString() + " with no " + ((item == null) ? "item" : "target") + ": not sent");
			return false;
		}
		long num = Ids.Of(item);
		long num2 = ((target != null) ? Ids.Of(target) : 0);
		if (num == 0L)
		{
			Unknown(op.ToString().ToLowerInvariant(), item);
			return false;
		}
		if (op == UseOp.Target && num2 == 0L)
		{
			Unknown("target", target);
			return false;
		}
		Net.ToServer(kind, new MsgRequest
		{
			src = num,
			dst = num2,
			n = ++requestN
		});
		SentCount++;
		return true;
	}

	private static bool ReadyToSend(string kind)
	{
		if (Net.IsLive)
		{
			return true;
		}
		Log.Warn("items-use: " + kind + " while the shop is still loading: not sent");
		return false;
	}

	private static void Unknown(string what, GameItem item)
	{
		Log.Warn("items-use: cannot " + what + " " + Describe(item) + ": the server never named this item; asking for a resync");
		Snapshot.RequestResync("items-use: cannot " + what + " " + Describe(item) + ": no server id");
	}

	private static void OnRefused(MsgRefused m)
	{
		RefusedCount++;
		UseOp op = (UseOp)m.op;
		LastRefusal = op.ToString() + ": " + m.reason;
		Log.Info("items-use: the server refused " + LastRefusal);
		if (!m.notice || string.IsNullOrEmpty(m.reason))
		{
			return;
		}
		try
		{
			StoreUIManager instance = StoreUIManager.Instance;
			if (instance != null)
			{
				instance.Notify(Kit.Safe(Lang.Tr(m.reason)), "default");
			}
		}
		catch
		{
		}
	}

	private static void OnRequest(UseOp op, MsgRequest m, int player)
	{
		if (!Net.IsLive)
		{
			Refuse(player, m.n, op, "The shop is not open yet.", notice: false);
			return;
		}
		GameItem val = Ids.Find(m.src);
		GameItem val2 = ((op == UseOp.Target) ? Ids.Find(m.dst) : null);
		string text = Possible(op, val, val2);
		if (text != null)
		{
			Refuse(player, m.n, op, text, notice: false);
			return;
		}
		UseContext ctx = new UseContext
		{
			Op = op,
			Source = val,
			Target = val2,
			SourceId = m.src,
			TargetId = ((op == UseOp.Target) ? m.dst : 0),
			Player = player
		};
		text = Validate(ctx);
		if (text != null)
		{
			Refuse(player, m.n, op, text, notice: true);
		}
		else
		{
			ServerPerform(ctx, bm: true, forced: false, 0L);
		}
	}

	private static void OnRenameRequest(MsgRenameTab m, int player)
	{
		if (!Net.IsLive)
		{
			Refuse(player, m.n, UseOp.RenameTab, "The shop is not open yet.", notice: false);
			return;
		}
		GameItem val = Ids.Find(m.bay);
		string text = null;
		if (Dead(val))
		{
			text = "that storage bay is gone";
		}
		else if (!GeneralHelper.IsItemOwned(val))
		{
			text = "not our storage bay";
		}
		else if (m.index < 0 || m.index >= 1 + StorageExpansionHelper.GetExtraTabs(val))
		{
			text = "no tab " + m.index;
		}
		if (text != null)
		{
			Refuse(player, m.n, UseOp.RenameTab, text, notice: false);
			return;
		}
		UseContext ctx = new UseContext
		{
			Op = UseOp.RenameTab,
			Source = val,
			SourceId = m.bay,
			Player = player,
			Index = m.index,
			Text = Clip(m.name, 64)
		};
		text = Validate(ctx);
		if (text != null)
		{
			Refuse(player, m.n, UseOp.RenameTab, text, notice: true);
		}
		else
		{
			ServerPerform(ctx, bm: false, forced: false, 0L);
		}
	}

	private static void OnScratchRequest(MsgScratch m, int player)
	{
		if (!Net.IsLive)
		{
			Refuse(player, m.n, UseOp.Scratch, "The shop is not open yet.", notice: false);
			return;
		}
		GameItem val = Ids.Find(m.item);
		string text = null;
		if (Dead(val))
		{
			text = "that ticket is gone";
		}
		else if (!val.IsTag("SCRATCHER_TAG"))
		{
			text = "not a scratch ticket";
		}
		else if (!GeneralHelper.IsItemOwned(val))
		{
			text = "not our ticket";
		}
		else if (m.slot < 0 || m.slot > 3)
		{
			text = "no symbol " + m.slot;
		}
		else if (m.slot == 0 && !val.IsTag("SCRATCHER_NEW_TAG"))
		{
			text = "already started";
		}
		else if (m.slot > 0 && val.GetTagReadonly("SCRATCHER_STATE" + m.slot).GetInt() == 1)
		{
			text = "already scratched";
		}
		if (text != null)
		{
			Refuse(player, m.n, UseOp.Scratch, text, notice: false);
			return;
		}
		UseContext ctx = new UseContext
		{
			Op = UseOp.Scratch,
			Source = val,
			SourceId = m.item,
			Player = player,
			Index = m.slot
		};
		text = Validate(ctx);
		if (text != null)
		{
			Refuse(player, m.n, UseOp.Scratch, text, notice: true);
		}
		else
		{
			ServerPerform(ctx, bm: false, forced: false, 0L);
		}
	}

	private static void OnPerkRequest(MsgPerk m, int player)
	{
		if (!Net.IsLive)
		{
			Refuse(player, m.n, UseOp.Perk, "The shop is not open yet.", notice: false);
			return;
		}
		GameItem val = Ids.Find(m.item);
		StartingPerk val2 = EditablePerk(m.perk);
		string text = null;
		try
		{
			if (Dead(val))
			{
				text = "that mind reprogrammer is gone";
			}
			else if (!val.IsTag("MIND_REPROGRAMMER_TAG"))
			{
				text = Describe(val) + " is not a mind reprogrammer";
			}
			else if (val.parentInventory == null)
			{
				text = Describe(val) + " is not in the shop";
			}
			else if (!GeneralHelper.IsItemOwned(val))
			{
				text = "not our mind reprogrammer";
			}
			else if (val2 == null)
			{
				text = "no editable perk '" + Clip(m.perk, 40) + "'";
			}
			else if (!MindReprogrammerHelper.CanApply(val, val2))
			{
				text = Describe(val) + " cannot " + (StartingPerk.IsPerkActive(val2.id) ? "remove " : "add ") + val2.id + " now";
			}
		}
		catch (Exception ex)
		{
			text = "check failed (" + ex.GetType().Name + ": " + ex.Message + ")";
		}
		if (text != null)
		{
			Refuse(player, m.n, UseOp.Perk, text, notice: false);
			return;
		}
		UseContext ctx = new UseContext
		{
			Op = UseOp.Perk,
			Source = val,
			SourceId = m.item,
			Player = player,
			Text = val2.id
		};
		text = Validate(ctx);
		if (text != null)
		{
			Refuse(player, m.n, UseOp.Perk, text, notice: true);
		}
		else
		{
			ServerPerform(ctx, bm: false, forced: false, 0L);
		}
	}

	private static StartingPerk EditablePerk(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return null;
		}
		foreach (StartingPerk editablePerk in MindReprogrammerHelper.GetEditablePerks())
		{
			if (editablePerk != null && editablePerk.id == id)
			{
				return editablePerk;
			}
		}
		return null;
	}

	private static void OnConfirmRequest(MsgConfirm m, int player)
	{
		if (m == null || m.index < 0 || m.index > 63)
		{
			Refuse(player, 0, UseOp.Confirm, "no such question (index " + (m?.index ?? (-1)) + ")", notice: false);
			return;
		}
		long key = Key(m.of, m.index);
		if (!pending.TryGetValue(key, out var value))
		{
			Refuse(player, 0, UseOp.Confirm, "that question is no longer open", notice: false);
			return;
		}
		if (value.Actor != player)
		{
			Refuse(player, 0, UseOp.Confirm, "only " + Net.PlayerName(value.Actor) + " can answer that", notice: false);
			return;
		}
		pending.Remove(key);
		UseContext ctx = new UseContext
		{
			Op = UseOp.Confirm,
			OriginalOp = value.Op,
			Source = value.Source,
			Target = value.Target,
			SourceId = value.SourceId,
			TargetId = value.TargetId,
			Player = player,
			Index = m.index,
			Yes = m.yes,
			ConfirmCallback = true
		};
		string text = Validate(ctx);
		if (text != null)
		{
			Refuse(player, 0, UseOp.Confirm, text, notice: true);
		}
		else
		{
			ServerPerform(ctx, bm: false, forced: false, m.of, m.yes ? value.Yes : value.No);
		}
	}

	private static void Refuse(int player, int n, UseOp op, string reason, bool notice)
	{
		Log.Info("items-use: refused " + op.ToString() + " from " + Net.PlayerName(player) + ": " + reason);
		if (player > 0)
		{
			Net.ToPlayer(player, "items.use.refused", new MsgRefused
			{
				n = n,
				op = (int)op,
				reason = reason,
				notice = notice
			});
		}
	}

	private static string Possible(UseOp op, GameItem src, GameItem dst)
	{
		try
		{
			if (Dead(src))
			{
				return "that item is gone";
			}
			if (src.parentInventory == null)
			{
				return Describe(src) + " is not in the shop";
			}
			switch (op)
			{
			case UseOp.Target:
				if (Dead(dst))
				{
					return "the target is gone";
				}
				if (src == dst)
				{
					return "an item cannot be used on itself";
				}
				if (dst.parentInventory == null)
				{
					return Describe(dst) + " is not in the shop";
				}
				if (!src.MayTarget(dst) || !src.CanTarget(dst))
				{
					return Describe(src) + " cannot be used on " + Describe(dst) + " now";
				}
				return null;
			case UseOp.Activate:
				return (src.MayActivateSlotItem() && src.CanActivateSlotItem()) ? null : (Describe(src) + " cannot be used now");
			case UseOp.Toggle:
				return (src.MayToggleSlotItem() && src.CanToggleSlotItem()) ? null : (Describe(src) + " cannot be switched now");
			default:
				return "unknown use";
			}
		}
		catch (Exception ex)
		{
			return "check failed (" + ex.GetType().Name + ": " + ex.Message + ")";
		}
	}

	private static string Validate(UseContext ctx)
	{
		KeyValuePair<string, Func<UseContext, string>>[] array;
		lock (validators)
		{
			array = validators.ToArray();
		}
		KeyValuePair<string, Func<UseContext, string>>[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			KeyValuePair<string, Func<UseContext, string>> keyValuePair = array2[i];
			try
			{
				string text = keyValuePair.Value(ctx);
				if (!string.IsNullOrEmpty(text))
				{
					return text;
				}
			}
			catch (Exception ex)
			{
				Log.Error("items-use: validator " + keyValuePair.Key + " threw on " + ctx?.ToString() + ": " + ex);
				return "That cannot be done right now.";
			}
		}
		return null;
	}

	private static UseReplay ModeFor(UseContext ctx)
	{
		if (ctx.Op != UseOp.Target && ctx.Op != UseOp.Activate && ctx.Op != UseOp.Toggle)
		{
			return UseReplay.Replay;
		}
		KeyValuePair<string, Func<UseContext, UseReplay?>>[] array;
		lock (modes)
		{
			array = modes.ToArray();
		}
		KeyValuePair<string, Func<UseContext, UseReplay?>>[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			KeyValuePair<string, Func<UseContext, UseReplay?>> keyValuePair = array2[i];
			try
			{
				UseReplay? useReplay = keyValuePair.Value(ctx);
				if (useReplay.HasValue)
				{
					return useReplay.Value;
				}
			}
			catch (Exception ex)
			{
				Log.Error("items-use: replay mode of " + keyValuePair.Key + " threw on " + ctx?.ToString() + ": " + ex);
			}
		}
		return UseReplay.Replay;
	}

	private static bool ServerOriginated(UseOp op, GameItem item, GameItem target, int index, string text)
	{
		if (!Net.IsLive || Snapshot.Capturing || SaveManager.IsDecoding)
		{
			return false;
		}
		ServerPerform(new UseContext
		{
			Op = op,
			Source = item,
			Target = target,
			SourceId = Ids.Of(item),
			TargetId = ((target != null) ? Ids.Of(target) : 0),
			Player = 0,
			Index = index,
			Text = text
		}, bm: false, forced: true, 0L);
		return true;
	}

	private static void ServerPerform(UseContext ctx, bool bm, bool forced = false, long of = 0L, Action callback = null)
	{
		ctx.Seq = ++seq;
		ctx.OnServer = true;
		ctx.Quiet = !(Net.IsHost && ctx.Player == Net.LocalPlayer);
		UseReplay useReplay = ModeFor(ctx);
		bool flag = ctx.Op != UseOp.RenameTab;
		Seed seed = null;
		bool ran = false;
		UseContext useContext = scope;
		IDisposable disposable = null;
		bool flag2 = false;
		try
		{
			disposable = ((!flag) ? Net.Apply() : Replay.Server(out seed));
			scope = ctx;
			if (ctx.Quiet)
			{
				EnterQuiet();
				flag2 = true;
			}
			ran = RunGame(ctx, bm, forced, callback);
		}
		catch (Exception ex)
		{
			Log.Warn("items-use: server run of " + ctx?.ToString() + " threw (broadcast anyway; the digest catches a difference): " + ex);
		}
		finally
		{
			if (flag2)
			{
				LeaveQuiet();
			}
			scope = useContext;
			disposable?.Dispose();
		}
		AppliedCount++;
		MsgUse obj = new MsgUse
		{
			seq = ctx.Seq,
			op = (int)ctx.Op,
			actor = ctx.Player,
			src = ctx.SourceId,
			dst = ctx.TargetId
		};
		GameItem source = ctx.Source;
		obj.srcKind = ((source != null) ? source.identifier : null);
		GameItem target = ctx.Target;
		obj.dstKind = ((target != null) ? target.identifier : null);
		obj.bm = bm;
		obj.ran = ran;
		obj.run = useReplay == UseReplay.Replay;
		obj.index = ctx.Index;
		obj.text = ctx.Text;
		obj.of = of;
		obj.yes = ctx.Yes;
		obj.seed = seed;
		obj.uidAfter = Uid();
		obj.digest = SafeDigest();
		MsgUse msg = obj;
		Net.ToAll("items.use", msg);
		RaisePerformed(ctx);
	}

	private static bool RunGame(UseContext ctx, bool bm, bool forced, Action callback, bool clientRan = true)
	{
		GameItem source = ctx.Source;
		switch (ctx.Op)
		{
		case UseOp.Target:
		{
			GameItem target = ctx.Target;
			if (bm && (Object)(object)ItemBehaviourManager.current != (Object)null)
			{
				ItemBehaviourManager.current.Target(source, target);
			}
			if (ctx.OnServer)
			{
				if (!forced && (Dead(source) || Dead(target) || !source.MayTarget(target) || !source.CanTarget(target)))
				{
					return false;
				}
			}
			else if (!clientRan)
			{
				return false;
			}
			source.Target(target);
			return true;
		}
		case UseOp.Activate:
			source.ActivateSlotItem();
			return true;
		case UseOp.Toggle:
			source.ToggleSlotItem();
			return true;
		case UseOp.RenameTab:
			StorageExpansionHelper.SetTabName(source, ctx.Index, ctx.Text);
			return true;
		case UseOp.Scratch:
			if (ctx.Index == 0)
			{
				StartScratch(source);
			}
			else
			{
				ScratcherHelper.MarkSymbolScratched(source, ctx.Index);
			}
			return true;
		case UseOp.Confirm:
			callback?.Invoke();
			return callback != null;
		case UseOp.Perk:
		{
			StartingPerk val = EditablePerk(ctx.Text);
			if (val == null)
			{
				return false;
			}
			MindReprogrammerHelper.Apply(source, val);
			return true;
		}
		default:
			return false;
		}
	}

	private static void StartScratch(GameItem t)
	{
		if (t.IsTag("SCRATCHER_NEW_TAG"))
		{
			t.DisableTag("SCRATCHER_NEW_TAG", false);
			t.unitValue = 0L;
			t.RemoveGameItemType("LOTTERY");
			LotteryHelper.OnScratcherStarted();
		}
	}

	private static void OnUse(MsgUse m)
	{
		UseOp op = (UseOp)m.op;
		UseContext useContext = new UseContext
		{
			Op = op,
			Seq = m.seq,
			Player = m.actor,
			SourceId = m.src,
			TargetId = m.dst,
			Index = m.index,
			Text = m.text,
			Yes = m.yes,
			OnServer = false,
			Quiet = (m.actor != Net.LocalPlayer || DayLog.Replaying)
		};
		if (op == UseOp.Scratch && m.index == 0)
		{
			scratchStartSent.Remove(m.src);
		}
		Action callback = null;
		if (op == UseOp.Confirm)
		{
			useContext.ConfirmCallback = true;
			long key = Key(m.of, m.index);
			if (!pending.TryGetValue(key, out var value))
			{
				if (m.run)
				{
					Mismatch("confirm " + m.of + "/" + m.index + " answered on the server but never opened here");
				}
				return;
			}
			pending.Remove(key);
			useContext.OriginalOp = value.Op;
			useContext.Source = value.Source;
			useContext.Target = value.Target;
			callback = (m.yes ? value.Yes : value.No);
		}
		else
		{
			useContext.Source = ((m.src != 0L) ? Ids.Find(m.src) : null);
			useContext.Target = ((m.dst != 0L) ? Ids.Find(m.dst) : null);
		}
		if (m.run)
		{
			string text = ((op == UseOp.Confirm) ? null : ((useContext.Source == null) ? ("item " + m.src + " (" + m.srcKind + ")") : ((op == UseOp.Target && useContext.Target == null) ? ("target " + m.dst + " (" + m.dstKind + ")") : ((m.srcKind != null && useContext.Source.identifier != m.srcKind) ? ("item " + m.src + " is " + useContext.Source.identifier + " here, " + m.srcKind + " on the server") : null))));
			if (text != null)
			{
				Mismatch("use #" + m.seq + " " + op.ToString() + ": " + text + " not found");
				return;
			}
			UseContext useContext2 = scope;
			IDisposable disposable = null;
			bool flag = false;
			try
			{
				disposable = ((m.seed != null) ? Replay.Client(m.seed) : Net.Apply());
				scope = useContext;
				if (useContext.Quiet)
				{
					EnterQuiet();
					flag = true;
				}
				RunGame(useContext, m.bm, forced: false, callback, m.ran);
				useContext.Replayed = true;
			}
			catch (Exception ex)
			{
				Log.Warn("items-use: replay of " + useContext?.ToString() + " threw: " + ex);
			}
			finally
			{
				if (flag)
				{
					LeaveQuiet();
				}
				scope = useContext2;
				disposable?.Dispose();
			}
			AppliedCount++;
			Verify(m, useContext);
			if (op == UseOp.Perk)
			{
				RefreshPerkWindow();
			}
		}
		CancelLocalGestures(useContext.Source, useContext.Target);
		RaisePerformed(useContext);
	}

	private static void Verify(MsgUse m, UseContext ctx)
	{
		if (m.seed != null && m.seed.UidBase != 0)
		{
			int num = Uid();
			if (num != m.uidAfter)
			{
				Mismatch(ctx?.ToString() + ": item id counter " + num + " here, " + m.uidAfter + " on the server");
				return;
			}
		}
		if (m.digest != 0L && SafeDigest() != m.digest)
		{
			Mismatch(ctx?.ToString() + ": action digest differs");
		}
	}

	private static void Mismatch(string what)
	{
		MismatchCount++;
		Log.Warn("items-use: " + what + "; asking for a resync");
		if (!DayLog.Replaying)
		{
			Snapshot.RequestResync("items-use: " + what);
		}
	}

	private static void CancelLocalGestures(GameItem a, GameItem b)
	{
		if (!Net.IsClient || (!Dead(a) && !Dead(b)))
		{
			return;
		}
		try
		{
			ItemSelectHandler current = ItemSelectHandler.current;
			if ((Object)(object)current != (Object)null && selectCurrent != null)
			{
				object? value = selectCurrent.GetValue(current);
				GameItem val = (GameItem)((value is GameItem) ? value : null);
				if (val != null && Dead(val))
				{
					current.EndSelect(false);
				}
			}
			ItemMouseDragHandler current2 = ItemMouseDragHandler.current;
			GameItem val2 = (((Object)(object)current2 != (Object)null) ? current2.GetCurrentItem() : null);
			if (val2 != null && Dead(val2))
			{
				current2.Cancel();
			}
			ItemMultiSelectHandler current3 = ItemMultiSelectHandler.current;
			if ((Object)(object)current3 != (Object)null && ((Dead(a) && a != null && current3.IsGroupDraggingItem(a)) || (Dead(b) && b != null && current3.IsGroupDraggingItem(b))))
			{
				current3.CustomCancel(false);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("items-use: cancelling a local gesture: " + ex.Message);
		}
	}

	private static void EnterQuiet()
	{
		if (quietNest++ > 0)
		{
			return;
		}
		try
		{
			quietSources = EmporiumSources();
			if (quietSources != null)
			{
				if (quietWasPlaying == null || quietWasPlaying.Length != quietSources.Length)
				{
					quietWasPlaying = new bool[quietSources.Length];
				}
				for (int i = 0; i < quietSources.Length; i++)
				{
					quietWasPlaying[i] = (Object)(object)quietSources[i] != (Object)null && quietSources[i].isPlaying;
				}
			}
		}
		catch
		{
			quietSources = null;
		}
	}

	private static void LeaveQuiet()
	{
		if (quietNest <= 0)
		{
			quietNest = 0;
		}
		else
		{
			if (--quietNest > 0)
			{
				return;
			}
			AudioSource[] array = quietSources;
			quietSources = null;
			if (array == null)
			{
				return;
			}
			try
			{
				for (int i = 0; i < array.Length; i++)
				{
					if ((Object)(object)array[i] != (Object)null && !quietWasPlaying[i] && array[i].isPlaying)
					{
						array[i].Stop();
						QuietSoundsStopped++;
					}
				}
			}
			catch
			{
			}
		}
	}

	private static AudioSource[] EmporiumSources()
	{
		EmporiumAudioPlayer instance = EmporiumAudioPlayer.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return null;
		}
		return (AudioSource[])(object)new AudioSource[27]
		{
			instance.audioButton1, instance.audioButton2, instance.audioButton3, instance.audioButton4, instance.audioBeatVictory, instance.audioRegister, instance.audioStep1, instance.audioStep2, instance.audioClickClick, instance.audioNegativeTwoBeep,
			instance.audioExposeScam, instance.audioLabeler, instance.audioFail, instance.audioBedroll, instance.audioLampClick, instance.audioLampSnap, instance.audioWriting, instance.flip1, instance.alarm2, instance.airlock_ext_open,
			instance.airlock_ext_close, instance.cross, instance.dumpWater, instance.wirecutter, instance.ratchet, instance.ratchet2, instance.welder
		};
	}

	private static void RefreshPerkWindow()
	{
		try
		{
			CustomUIWindow val = (((Object)(object)CustomUIManager.Instance != (Object)null) ? CustomUIManager.Instance.GetWindow("mind_reprogrammer") : null);
			if (val != null && val.IsAlive && !(perkRebuild == null))
			{
				perkRebuild.Invoke(null, null);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("items-use: refreshing the perk window: " + ex.Message);
		}
	}

	internal static string DigestPart()
	{
		if (!Game.ShopLoaded)
		{
			return "";
		}
		PlayerStore store = Game.Store;
		ulong h = 14695981039346656037uL;
		List<string> list = new List<string>();
		if (store.startingPerks != null)
		{
			foreach (StartingPerk startingPerk in store.startingPerks)
			{
				if (startingPerk != null)
				{
					list.Add(startingPerk.id ?? "");
				}
			}
		}
		list.Sort(StringComparer.Ordinal);
		foreach (string item in list)
		{
			Mix(ref h, item);
		}
		int num = 0;
		List<GraphNodeStorage> list2 = Game.Roots();
		for (int i = 0; i < list2.Count; i++)
		{
			if (list2[i] == null)
			{
				continue;
			}
			foreach (GameItem item2 in GraphUtils.FindAllChildrenType<GameItem>(list2[i], (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				if (item2 == null)
				{
					continue;
				}
				bool flag = false;
				if (item2.IsTag("TOOLBOX_TAG"))
				{
					Mix(ref h, 1L);
					Mix(ref h, ToolboxHelper.GetUnlockedSlots(item2));
					flag = true;
				}
				if (item2.IsTag("STORAGE_EXPANSION_TABS_INT"))
				{
					int extraTabs = StorageExpansionHelper.GetExtraTabs(item2);
					Mix(ref h, 2L);
					Mix(ref h, extraTabs);
					for (int j = 0; j <= extraTabs; j++)
					{
						Mix(ref h, StorageExpansionHelper.GetTabName(item2, j));
					}
					flag = true;
				}
				if (item2.IsTag("SCRATCHER_TAG"))
				{
					Mix(ref h, 3L);
					Mix(ref h, item2.IsTag("SCRATCHER_NEW_TAG") ? 1 : 0);
					for (int k = 1; k <= 3; k++)
					{
						Mix(ref h, item2.GetTagReadonly("SCRATCHER_STATE" + k).GetInt());
					}
					flag = true;
				}
				if (item2.IsTag("MIND_REPROGRAMMER_TAG"))
				{
					Mix(ref h, 4L);
					Mix(ref h, MindReprogrammerHelper.GetCharge(item2));
					Mix(ref h, MindReprogrammerHelper.GetCooldown(item2));
					flag = true;
				}
				if (flag)
				{
					Mix(ref h, item2.uniqueId);
					num++;
				}
			}
		}
		return num + ":" + list.Count + ":" + h.ToString("x16");
	}

	private static void Mix(ref ulong h, long v)
	{
		for (int i = 0; i < 8; i++)
		{
			h ^= (byte)(v >> i * 8);
			h *= 1099511628211uL;
		}
	}

	private static void Mix(ref ulong h, string s)
	{
		if (s == null)
		{
			Mix(ref h, -7L);
			return;
		}
		foreach (char c in s)
		{
			h ^= c;
			h *= 1099511628211uL;
		}
		Mix(ref h, s.Length);
	}

	private static void DropPendingOf(int player)
	{
		if (!Net.IsServer)
		{
			return;
		}
		List<long> list = new List<long>();
		foreach (KeyValuePair<long, PendingConfirm> item in pending)
		{
			if (item.Value.Actor == player)
			{
				list.Add(item.Key);
			}
		}
		foreach (long item2 in list)
		{
			pending.Remove(item2);
		}
	}

	private static void RaisePerformed(UseContext ctx)
	{
		Action<UseContext> performed = ItemsUse.Performed;
		if (performed == null)
		{
			return;
		}
		Delegate[] invocationList = performed.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			Action<UseContext> action = (Action<UseContext>)invocationList[i];
			try
			{
				action(ctx);
			}
			catch (Exception ex)
			{
				Log.Error("items-use: Performed handler " + action.Method.DeclaringType?.Name + "." + action.Method.Name + " threw on " + ctx?.ToString() + ": " + ex);
			}
		}
	}

	internal static bool Dead(GameItem item)
	{
		if (item != null)
		{
			GameItemElement val = (GameItemElement)(object)((item is GameItemElement) ? item : null);
			if (val != null)
			{
				return val.IsDestroyed();
			}
			return false;
		}
		return true;
	}

	internal static bool InCounter(GameItem item)
	{
		if (item == null)
		{
			return false;
		}
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return false;
		}
		try
		{
			return (object)item.parentInventory == instance.frontInvinvElement || (object)item.parentInventory == instance.backInvinvElement || GraphUtils.FindParentEquals((GraphNodeStorage)(object)item, (GraphNodeStorage)(object)instance.frontInvinvElement) || GraphUtils.FindParentEquals((GraphNodeStorage)(object)item, (GraphNodeStorage)(object)instance.backInvinvElement);
		}
		catch
		{
			return false;
		}
	}

	internal static string Describe(GameItem item)
	{
		if (item == null)
		{
			return "nothing";
		}
		return (item.identifier ?? "?") + "#" + item.uniqueId;
	}

	private static int Uid()
	{
		if (!PlayerStore.HasInstance)
		{
			return 0;
		}
		return PlayerStore.Instance.currentUniqueId;
	}

	private static long SafeDigest()
	{
		try
		{
			return Replay.Digest();
		}
		catch (Exception ex)
		{
			Log.Warn("items-use: Replay.Digest threw: " + ex.Message);
			return 0L;
		}
	}

	private static string Clip(string s, int max)
	{
		if (s != null)
		{
			if (s.Length <= max)
			{
				return s;
			}
			return s.Substring(0, max);
		}
		return "";
	}
}
