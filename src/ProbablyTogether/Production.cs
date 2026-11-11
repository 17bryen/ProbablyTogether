using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace ProbablyTogether;

public static class Production
{
	public sealed class MsgReq
	{
		public int n;

		public int op;

		public long m;

		public ProdTag[] tags;
	}

	public sealed class MsgDone
	{
		public long seq;

		public int op;

		public int actor;

		public int n;

		public long m;

		public long x;

		public int charge;

		public ProdTag[] tags;

		public Seed seed;

		public int uidAfter;

		public long mh;

		public long digest;

		public bool ran;
	}

	public sealed class MsgRefused
	{
		public int n;

		public int op;

		public string reason;
	}

	private struct SettingSpec
	{
		public string Owner;

		public bool IsBool;

		public int Min;

		public int Max;
	}

	private sealed class AutoState
	{
		public GameItem Machine;

		public long Id;

		public Seed Seed;

		public IDisposable Scope;
	}

	public const string KReq = "production.req";

	public const string KDone = "production.done";

	public const string KRefused = "production.refused";

	private static long seq;

	private static int nextN;

	private static bool inited;

	private static readonly Dictionary<string, SettingSpec> settings = BuildSettings();

	private static readonly Dictionary<long, Dictionary<string, ProdTag>> pendingSets = new Dictionary<long, Dictionary<string, ProdTag>>();

	private static readonly Dictionary<long, int> lastSentSet = new Dictionary<long, int>();

	private static readonly Dictionary<long, int> lastSeenSet = new Dictionary<long, int>();

	private static float lastFlush;

	private static readonly HashSet<string> rollWarned = new HashSet<string>();

	private static readonly List<string> keyBuf = new List<string>();

	private static readonly HashSet<string> warned = new HashSet<string>();

	public static int SentCount { get; private set; }

	public static int AppliedCount { get; private set; }

	public static int RefusedCount { get; private set; }

	public static int MismatchCount { get; private set; }

	public static int DigestDiffers { get; private set; }

	public static int BlockedAuto { get; private set; }

	public static int BlockedNight { get; private set; }

	public static int UnseededClient { get; private set; }

	public static int UnseededServerDay { get; private set; }

	public static int ServerOriginatedCount { get; private set; }

	public static int UnlockMachineCount { get; private set; }

	public static int UnlockFountainCount { get; private set; }

	public static string LastRefusal { get; private set; } = "";

	public static string LastMismatch { get; private set; } = "";

	public static string LastDone { get; private set; } = "";

	internal static bool ServerDay
	{
		get
		{
			if (Net.IsServer && Net.IsLive && Game.ShopLoaded && !Snapshot.Capturing && !SaveManager.IsDecoding)
			{
				return !CurrentConverterHelper.isNightCycle;
			}
			return false;
		}
	}

	[SystemInit]
	internal static void Init()
	{
		if (!inited)
		{
			inited = true;
			Net.On<MsgReq>("production.req", OnRequest, null);
			Net.On<MsgDone>("production.done", null, OnDone);
			Net.On<MsgRefused>("production.refused", null, OnRefused);
			Digest.AddPart("production", DigestPart);
		}
	}

	[SystemTick]
	internal static void Tick()
	{
		if (pendingSets.Count > 0)
		{
			FlushSets(force: false);
		}
	}

	private static Dictionary<string, SettingSpec> BuildSettings()
	{
		Dictionary<string, SettingSpec> d = new Dictionary<string, SettingSpec>(StringComparer.Ordinal);
		I("FEED_DISPENSER_TAG", "FEED_DISPENSER_TARGET_PERCENT_INT", 0, 100);
		B("FEED_DISPENSER_TAG", "FEED_DISPENSER_AUTO_BLEND_BOOL");
		string[] array = new string[2] { "CAGE", "TANK" };
		foreach (string text in array)
		{
			string text2 = "AUTO_BUTCHER_" + text + "_";
			I("AUTO_BUTCHER_TAG", text2 + "HARVEST_INT", 0, 3);
			I("AUTO_BUTCHER_TAG", text2 + "WEIGHT_MIN_INT", 0, 100000);
			I("AUTO_BUTCHER_TAG", text2 + "WEIGHT_MAX_INT", 0, 100000);
			I("AUTO_BUTCHER_TAG", text2 + "AGE_MIN_INT", 0, 100000);
			I("AUTO_BUTCHER_TAG", text2 + "AGE_MAX_INT", 0, 100000);
			I("AUTO_BUTCHER_TAG", text2 + "KEEP_FEMALES_INT", 0, 100);
			I("AUTO_BUTCHER_TAG", text2 + "KEEP_MALES_INT", 0, 100);
			B("AUTO_BUTCHER_TAG", text2 + "COUNT_PROTECTED_BOOL");
		}
		array = new string[7] { "CAGE_SPARE_PREGNANT", "CAGE_CULL_SICK", "CAGE_CULL_PRION", "TANK_SPARE_EGG_LAYERS", "TANK_FILLET_CARCASSES", "TANK_CUTS_TO_DISPENSER", "TANK_COLLECT_ROE" };
		foreach (string text3 in array)
		{
			B("AUTO_BUTCHER_TAG", "AUTO_BUTCHER_" + text3 + "_BOOL");
		}
		B("AUTO_BUTCHER_TAG", "AUTO_BUTCHER_DROP_TO_BUTCHER_BOOL");
		B("AUTO_BUTCHER_TAG", "AUTO_BUTCHER_KILL_STORE_ONLY_BOOL");
		return d;
		void B(string owner, string t)
		{
			d[t] = new SettingSpec
			{
				Owner = owner,
				IsBool = true
			};
		}
		void I(string owner, string t, int lo, int hi)
		{
			d[t] = new SettingSpec
			{
				Owner = owner,
				Min = lo,
				Max = hi
			};
		}
	}

	public static bool OwnsSettings(GameItem machine)
	{
		if (machine != null)
		{
			if (!machine.IsTag("FEED_DISPENSER_TAG"))
			{
				return machine.IsTag("AUTO_BUTCHER_TAG");
			}
			return true;
		}
		return false;
	}

	private static ProdTag ReadTag(GameItem item, string tag)
	{
		ProdTag prodTag = new ProdTag
		{
			t = tag
		};
		object obj;
		if (item == null)
		{
			obj = null;
		}
		else
		{
			TagSystem state = item.state;
			obj = ((state != null) ? state.dict : null);
		}
		Dictionary<string, TagState> dictionary = (Dictionary<string, TagState>)obj;
		if (dictionary != null && dictionary.TryGetValue(tag, out var value) && value != null)
		{
			prodTag.e = value.valueEnabled;
			prodTag.i = value.valueInt;
			prodTag.b = value.valueBool;
		}
		return prodTag;
	}

	private static bool SameTag(ProdTag a, ProdTag b)
	{
		if (a != null && b != null && a.e == b.e && a.i == b.i)
		{
			return a.b == b.b;
		}
		return false;
	}

	internal static ProdTag BeforeTag(GameItem item, string tag)
	{
		if (item == null || tag == null || Net.Applying || DayLog.Replaying || Snapshot.IsLoading || SaveManager.IsDecoding)
		{
			return null;
		}
		if (!settings.TryGetValue(tag, out var value) || !item.IsTag(value.Owner))
		{
			return null;
		}
		return ReadTag(item, tag);
	}

	internal static void AfterTag(GameItem item, string tag, ProdTag before)
	{
		ProdTag prodTag = ReadTag(item, tag);
		if (SameTag(before, prodTag))
		{
			return;
		}
		long num = item.uniqueId;
		if (num != 0L)
		{
			if (!pendingSets.TryGetValue(num, out var value))
			{
				value = (pendingSets[num] = new Dictionary<string, ProdTag>(StringComparer.Ordinal));
			}
			value[tag] = prodTag;
		}
	}

	internal static void FlushSets(bool force)
	{
		if (pendingSets.Count == 0 || (!force && Time.realtimeSinceStartup - lastFlush < 0.1f))
		{
			return;
		}
		lastFlush = Time.realtimeSinceStartup;
		if (!Net.IsClient || !Net.IsLive)
		{
			pendingSets.Clear();
			return;
		}
		foreach (KeyValuePair<long, Dictionary<string, ProdTag>> pendingSet in pendingSets)
		{
			List<ProdTag> list = new List<ProdTag>(pendingSet.Value.Values);
			int num = ++nextN;
			lastSentSet[pendingSet.Key] = num;
			Net.ToServer("production.req", new MsgReq
			{
				n = num,
				op = 6,
				m = pendingSet.Key,
				tags = list.ToArray()
			});
			SentCount++;
		}
		pendingSets.Clear();
	}

	private static bool HasNewerLocalSettings(long m, int confirmedN)
	{
		if (!pendingSets.ContainsKey(m))
		{
			if (lastSentSet.TryGetValue(m, out var value))
			{
				return value > confirmedN;
			}
			return false;
		}
		return true;
	}

	internal static bool Let(ProdOp op, GameItem machine)
	{
		if (!Net.IsClient || Net.Applying)
		{
			return true;
		}
		if (DayLog.Replaying || Snapshot.IsLoading)
		{
			return false;
		}
		if (!Net.IsLive)
		{
			WarnOnce("notlive:" + op, "production: " + op.ToString() + " while the shop is not live here: ignored");
			return false;
		}
		long num = ((machine != null) ? machine.uniqueId : 0);
		if (num == 0L)
		{
			WarnOnce("noid:" + op, "production: " + op.ToString() + " on a machine with no id: ignored");
			return false;
		}
		if (IsLabelMatch(op) && ItemsUse.InCounter(machine))
		{
			string text = Customers.LocalCounterRefusal();
			if (text != null)
			{
				RefusedCount++;
				LastRefusal = op.ToString() + ": " + text;
				Log.Info("production: " + op.ToString() + " ignored: " + text);
				try
				{
					if ((Object)(object)StoreUIManager.Instance != (Object)null)
					{
						StoreUIManager.Instance.Notify(Kit.Safe(Lang.Tr(text)), "default");
					}
				}
				catch
				{
				}
				return false;
			}
		}
		FlushSets(force: true);
		int n = ++nextN;
		Net.ToServer("production.req", new MsgReq
		{
			n = n,
			op = (int)op,
			m = num
		});
		SentCount++;
		return false;
	}

	internal static bool LetNightRun()
	{
		if (!Net.IsClient || Net.Applying)
		{
			return true;
		}
		BlockedNight++;
		WarnOnce("nightrun", "production: the auto butcher's nightly run was called on a client: stopped (the night runs on the server)");
		return false;
	}

	private static void OnRequest(MsgReq r, int player)
	{
		if (!Net.IsServer || r == null)
		{
			return;
		}
		ProdOp op = (ProdOp)r.op;
		GameItem m = null;
		string refusal = Validate(r, op, ref m);
		if (refusal == null)
		{
			refusal = Systems.Bays.Refusal(player, (GraphNodeStorage)(object)m);
		}
		if (refusal != null)
		{
			RefusedCount++;
			LastRefusal = op.ToString() + ": " + refusal;
			Log.Info("production: refused " + op.ToString() + " #" + r.n + " from " + Net.PlayerName(player) + ": " + refusal);
			Net.ToPlayer(player, "production.refused", new MsgRefused
			{
				n = r.n,
				op = r.op,
				reason = refusal
			});
			return;
		}
		bool took = false;
		if (IsLabelMatch(op) && ItemsUse.InCounter(m) && !Customers.TryCounter(player, out refusal, out took))
		{
			RefusedCount++;
			LastRefusal = op.ToString() + ": " + refusal;
			Log.Info("production: refused " + op.ToString() + " #" + r.n + " from " + Net.PlayerName(player) + ": " + refusal);
			Net.ToPlayer(player, "production.refused", new MsgRefused
			{
				n = r.n,
				op = r.op,
				reason = refusal
			});
		}
		else if (!Perform(op, m, player, r.n, (op == ProdOp.Set) ? r.tags : null, null, 0))
		{
			Customers.GiveBack(player, took);
		}
	}

	private static bool IsLabelMatch(ProdOp op)
	{
		if (op != ProdOp.WaterMatch)
		{
			return op == ProdOp.ChemMatch;
		}
		return true;
	}

	private static string Validate(MsgReq r, ProdOp op, ref GameItem m)
	{
		if (!ServerDay)
		{
			return "The shop is closed for the night.";
		}
		if ((uint)(op - 1) > 5u && (uint)(op - 10) > 1u)
		{
			return "That cannot be done.";
		}
		m = FindItem(r.m);
		if (m == null || IsGone(m))
		{
			return "That machine is gone.";
		}
		if (!GeneralHelper.IsItemOwned(m))
		{
			return "That machine is not yours.";
		}
		switch (op)
		{
		case ProdOp.Blend:
			if (!m.IsTag("FEED_DISPENSER_TAG") || MachineFeedDispenser.GetStorageGrid(m) == null)
			{
				return "That is not a feed dispenser.";
			}
			break;
		case ProdOp.FeedEmpty:
			if (!m.IsTag("FEED_DISPENSER_TAG"))
			{
				return "That is not a feed dispenser.";
			}
			break;
		case ProdOp.ButcherRun:
		case ProdOp.ButcherContent:
			if (!m.IsTag("AUTO_BUTCHER_TAG") || MachineAutoButcher.GetOutputGrid(m) == null)
			{
				return "That is not an auto butcher.";
			}
			break;
		case ProdOp.Cashout:
			if (!IsVending(m))
			{
				return "That is not a vending machine.";
			}
			break;
		case ProdOp.WaterMatch:
		{
			ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(m);
			if (waterFeature == null || m.IsTag("WINE_CONTAINER_TAG"))
			{
				return "That holds no water.";
			}
			if (WaterHelper.IsEmpty(m))
			{
				return "That container is empty.";
			}
			if (waterFeature.IsExposed() || waterFeature.IsFeatureMatch())
			{
				return "That label already matches.";
			}
			break;
		}
		case ProdOp.ChemMatch:
		{
			ItemFeature chemicalFeature = ChemicalProductHelper.GetChemicalFeature(m);
			if (chemicalFeature == null || chemicalFeature.realCondition == null || chemicalFeature.fakeCondition == null)
			{
				return "That has no chemical label.";
			}
			if (chemicalFeature.realCondition.identifier == chemicalFeature.fakeCondition.identifier)
			{
				return "That label already matches.";
			}
			break;
		}
		case ProdOp.Set:
		{
			if (r.tags == null || r.tags.Length == 0 || r.tags.Length > 48)
			{
				return "No settings.";
			}
			ProdTag[] tags = r.tags;
			foreach (ProdTag prodTag in tags)
			{
				if (prodTag == null || prodTag.t == null || !settings.TryGetValue(prodTag.t, out var value) || !m.IsTag(value.Owner))
				{
					return "That machine has no such setting.";
				}
				if (!value.IsBool && (prodTag.i < value.Min || prodTag.i > value.Max))
				{
					return "That setting is out of range.";
				}
			}
			break;
		}
		}
		return null;
	}

	private static bool IsVending(GameItem m)
	{
		if (!(m.identifier == "vending_machine") && !(m.identifier == "vending_fountain"))
		{
			TagSystem state = m.state;
			if (((state != null) ? state.dict : null) != null && m.state.dict.TryGetValue("VENDING_CASH", out var value) && value != null && value.valueEnabled)
			{
				return (m.identifier ?? "").StartsWith("vending", StringComparison.Ordinal);
			}
			return false;
		}
		return true;
	}

	private static bool Perform(ProdOp op, GameItem m, int actor, int n, ProdTag[] tags, GameItem except, int charge)
	{
		long mid = ((m != null) ? Ids.Of(m) : 0);
		long xid = ((except != null) ? Ids.Of(except) : 0);
		Seed seed = null;
		bool flag = false;
		IDisposable disposable = ((op == ProdOp.Set) ? Net.Apply() : Replay.Server(out seed));
		try
		{
			flag = RunGame(op, m, except, tags, charge);
		}
		catch (Exception ex)
		{
			Log.Warn("production: server run of " + op.ToString() + " threw (broadcast anyway; the clients' hash check catches a difference): " + ex);
		}
		finally
		{
			disposable.Dispose();
		}
		if (op == ProdOp.Set)
		{
			RefreshWindow(m);
		}
		Broadcast(op, m, mid, xid, actor, n, tags, seed, charge, flag);
		return flag;
	}

	private static void Broadcast(ProdOp op, GameItem m, long mid, long xid, int actor, int n, ProdTag[] tags, Seed seed, int charge, bool ran)
	{
		PlayerStore store = Game.Store;
		if (op == ProdOp.UnlockMachine || op == ProdOp.UnlockFountain)
		{
			m = UnlockedMachine(op);
		}
		MsgDone msg = new MsgDone
		{
			seq = ++seq,
			op = (int)op,
			actor = actor,
			n = n,
			m = mid,
			x = xid,
			charge = charge,
			tags = tags,
			seed = seed,
			uidAfter = (store?.currentUniqueId ?? 0),
			mh = ActionHash(op, m),
			digest = SafeDigest(),
			ran = ran
		};
		AppliedCount++;
		LastDone = op.ToString() + " " + Describe(m) + " by " + Net.PlayerName(actor) + (ran ? "" : " (nothing to do)");
		Net.ToAll("production.done", msg);
	}

	private static bool RunGame(ProdOp op, GameItem m, GameItem except, ProdTag[] tags, int charge)
	{
		switch (op)
		{
		case ProdOp.Blend:
		{
			GameGridInventory storageGrid2 = MachineFeedDispenser.GetStorageGrid(m);
			if (storageGrid2 == null)
			{
				return false;
			}
			int num = StoredKcal(m);
			MachineFeedDispenser.Blend(m, storageGrid2);
			return StoredKcal(m) != num;
		}
		case ProdOp.AutoBlend:
		{
			GameGridInventory storageGrid = MachineFeedDispenser.GetStorageGrid(m);
			if (storageGrid == null)
			{
				return false;
			}
			MachineFeedDispenser.Blend(m, storageGrid);
			return true;
		}
		case ProdOp.FeedEmpty:
			MachineFeedDispenser.EmptyStorage(m);
			return true;
		case ProdOp.ButcherRun:
			MachineAutoButcher.Run(m, true);
			return m.GetTagReadonly("AUTO_BUTCHER_LAST_COUNT_INT").GetInt() > 0;
		case ProdOp.ButcherContent:
			return MachineAutoButcher.ButcherContent(m);
		case ProdOp.Cashout:
		{
			int num2 = m.GetTagReadonly("VENDING_CASH").GetInt();
			VendingMachineHelper.TransferCashToPlayer(m);
			return num2 > 0;
		}
		case ProdOp.Set:
			if (tags == null)
			{
				return false;
			}
			foreach (ProdTag prodTag in tags)
			{
				if (prodTag == null || prodTag.t == null || !settings.TryGetValue(prodTag.t, out var spec))
				{
					continue;
				}
				ProdTag tt = prodTag;
				m.ModifyTag(tt.t, (Action<TagState>)delegate(TagState s)
				{
					if (tt.e)
					{
						s.Enable();
					}
					else
					{
						s.Disable();
					}
					if (spec.IsBool)
					{
						s.SetBool(tt.b);
					}
					else
					{
						s.SetInt(tt.i);
					}
				}, false);
			}
			return true;
		case ProdOp.WaterMatch:
		{
			ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(m);
			if (waterFeature == null)
			{
				return false;
			}
			waterFeature.ExposeFeature(true, false);
			WaterFeatureHelper.OnWaterLabelUpdated(m);
			return true;
		}
		case ProdOp.ChemMatch:
		{
			ItemFeature chemicalFeature = ChemicalProductHelper.GetChemicalFeature(m);
			if (chemicalFeature == null)
			{
				return false;
			}
			chemicalFeature.ExposeFeature(true, false);
			ChemicalProductHelper.GradeProduct(m);
			return true;
		}
		case ProdOp.UnlockMachine:
			VendingMachineHelper.UnlockVendingMachine(charge);
			return true;
		case ProdOp.UnlockFountain:
			VendingFountainHelper.UnlockVendingFountain();
			return true;
		default:
			return false;
		}
	}

	internal static object BeginServerAutoBlend(GameItem machine)
	{
		AutoState obj = new AutoState
		{
			Machine = machine,
			Id = Ids.Of(machine)
		};
		obj.Scope = Replay.Server(out obj.Seed);
		return obj;
	}

	internal static void EndServerAutoBlend(object state, bool result, Exception error)
	{
		if (state is AutoState autoState)
		{
			autoState.Scope?.Dispose();
			if (error != null)
			{
				Log.Warn("production: the server's auto-blend pass threw (broadcast anyway; the clients' hash check catches a difference): " + error.Message);
			}
			ServerOriginatedCount++;
			Broadcast(ProdOp.AutoBlend, autoState.Machine, autoState.Id, 0L, 0, 0, null, autoState.Seed, 0, result);
		}
	}

	internal static void ServerUnlock(ProdOp op, int charge)
	{
		ServerOriginatedCount++;
		if (op == ProdOp.UnlockMachine)
		{
			UnlockMachineCount++;
		}
		else
		{
			UnlockFountainCount++;
		}
		Log.Info("production: " + op.ToString() + " by server game code outside a replicated action: broadcast as a production action");
		Perform(op, null, 0, 0, null, null, charge);
	}

	internal static bool ClientBlockAutoBlend()
	{
		BlockedAuto++;
		return false;
	}

	internal static bool ClientBlockUnlock(string what)
	{
		BlockedNight++;
		WarnOnce("unlock:" + what, "production: " + what + " on a client outside a replicated action: stopped (the server unlocks and broadcasts)");
		return false;
	}

	private static void OnDone(MsgDone d)
	{
		if (d == null || Net.IsServer)
		{
			return;
		}
		ProdOp op = (ProdOp)d.op;
		bool flag = op == ProdOp.UnlockMachine || op == ProdOp.UnlockFountain;
		GameItem val = (flag ? null : FindItem(d.m));
		if (!flag && (val == null || IsGone(val)))
		{
			Mismatch("production: " + op.ToString() + " #" + d.seq + " for item " + d.m + " which this copy does not have");
			return;
		}
		if (op == ProdOp.Set && d.actor == Net.LocalPlayer && d.n > 0)
		{
			lastSeenSet[d.m] = Math.Max(lastSeenSet.TryGetValue(d.m, out var value) ? value : 0, d.n);
		}
		GameItem except = ((d.x != 0L) ? FindItem(d.x) : null);
		IDisposable disposable = ((d.seed != null) ? Replay.Client(d.seed) : Net.Apply());
		try
		{
			RunGame(op, val, except, d.tags, d.charge);
		}
		catch (Exception ex)
		{
			Log.Warn("production: replay of " + op.ToString() + " #" + d.seq + " threw: " + ex);
		}
		finally
		{
			disposable.Dispose();
		}
		AppliedCount++;
		if (flag)
		{
			val = UnlockedMachine(op);
		}
		LastDone = op.ToString() + " " + Describe(val) + " by " + Net.PlayerName(d.actor);
		PlayerStore store = Game.Store;
		if (d.seed != null && d.uidAfter > 0 && store != null && store.currentUniqueId != d.uidAfter)
		{
			Log.Warn("production: after " + op.ToString() + " #" + d.seq + " the uid counter is " + store.currentUniqueId + ", the server's " + d.uidAfter + "; taking the server's");
			store.currentUniqueId = d.uidAfter;
		}
		int value2;
		bool flag2 = d.m != 0L && HasNewerLocalSettings(d.m, lastSeenSet.TryGetValue(d.m, out value2) ? value2 : 0);
		long num = ActionHash(op, val);
		if (!flag2 && d.mh != 0L && num != d.mh)
		{
			Mismatch("production: after " + op.ToString() + " #" + d.seq + " on " + Describe(val) + " this copy's subtree differs from the server's (" + num.ToString("x") + " vs " + d.mh.ToString("x") + ")");
		}
		if (d.digest != 0L && SafeDigest() != d.digest)
		{
			DigestDiffers++;
		}
		if (op == ProdOp.Set && !(d.actor == Net.LocalPlayer && flag2))
		{
			RefreshWindow(val);
		}
		if (op == ProdOp.Cashout && !DayLog.Replaying)
		{
			RefreshVendingLabel(val);
		}
		if ((op == ProdOp.WaterMatch || op == ProdOp.ChemMatch) && d.actor == Net.LocalPlayer && !DayLog.Replaying)
		{
			RefreshLabelPanels(val);
		}
	}

	private static void OnRefused(MsgRefused r)
	{
		if (r == null)
		{
			return;
		}
		RefusedCount++;
		ProdOp op = (ProdOp)r.op;
		LastRefusal = op.ToString() + ": " + r.reason;
		string[] obj = new string[6] { "production: the server refused ", null, null, null, null, null };
		op = (ProdOp)r.op;
		obj[1] = op.ToString();
		obj[2] = " #";
		obj[3] = r.n.ToString();
		obj[4] = ": ";
		obj[5] = r.reason;
		Log.Info(string.Concat(obj));
		try
		{
			if ((Object)(object)StoreUIManager.Instance != (Object)null && !string.IsNullOrEmpty(r.reason))
			{
				StoreUIManager.Instance.Notify(Kit.Safe(Lang.Tr(r.reason)), "default");
			}
		}
		catch
		{
		}
	}

	private static void Mismatch(string text)
	{
		MismatchCount++;
		LastMismatch = text;
		Log.Warn(text + (DayLog.Replaying ? " (during the day-log replay)" : "; asking for a resync"));
		if (!DayLog.Replaying)
		{
			Snapshot.RequestResync(text);
		}
	}

	private static void RefreshWindow(GameItem m)
	{
		if (m == null || !OwnsSettings(m))
		{
			return;
		}
		try
		{
			using (Net.Apply())
			{
				((GameItemFunc)m).onLoaded?.Invoke(m);
			}
		}
		catch (Exception ex)
		{
			WarnOnce("refresh:" + m.identifier, "production: window refresh of " + m.identifier + " threw: " + ex.Message);
		}
	}

	private static void RefreshLabelPanels(GameItem m)
	{
		try
		{
			WaterStripUIManager instance = WaterStripUIManager.Instance;
			if ((Object)(object)instance != (Object)null && (Object)(object)instance.panel != (Object)null && instance.panel.activeSelf && instance.currentWaterContainer == m)
			{
				ShowLabelled(instance.matchButton, instance.labelTMP, instance.testedTMP);
			}
			WaterScannerUIManager instance2 = WaterScannerUIManager.Instance;
			if ((Object)(object)instance2 != (Object)null && (Object)(object)instance2.panel != (Object)null && instance2.panel.activeSelf && instance2.currentWaterContainer == m)
			{
				ShowLabelled(instance2.matchButton, instance2.labelTMP, instance2.testedTMP);
			}
			ChemScannerUIManager instance3 = ChemScannerUIManager.Instance;
			if ((Object)(object)instance3 != (Object)null && (Object)(object)instance3.panel != (Object)null && instance3.panel.activeSelf && instance3.currentScannedItem == m)
			{
				ShowLabelled(instance3.matchButton, instance3.labelTMP, instance3.testedTMP);
			}
		}
		catch (Exception ex)
		{
			WarnOnce("labelpanel", "production: label panel refresh threw: " + ex.Message);
		}
	}

	private static void ShowLabelled(Button match, TextMeshProUGUI label, TextMeshProUGUI tested)
	{
		if ((Object)(object)match != (Object)null)
		{
			((Component)match).gameObject.SetActive(false);
		}
		if ((Object)(object)label != (Object)null && (Object)(object)tested != (Object)null)
		{
			((TMP_Text)label).text = Regex.Replace(((TMP_Text)tested).text, "\\[[^\\]]*\\]", LocHelper.GetLocalizedMechanic("mech_tooltip_labeled", Array.Empty<object>()));
		}
	}

	private static void RefreshVendingLabel(GameItem m)
	{
		try
		{
			if (m != null)
			{
				PixelWindow contentWindow = m.contentWindow;
				if (contentWindow != null)
				{
					contentWindow.openCallback?.Invoke();
				}
			}
		}
		catch
		{
		}
	}

	internal static void NoteRoll(string site)
	{
		if (!Net.IsOnline || Net.Applying)
		{
			return;
		}
		if (Net.IsClient)
		{
			if (!Snapshot.IsLoading && !SaveManager.IsDecoding && !DayLog.Replaying)
			{
				UnseededClient++;
				if (rollWarned.Add("c:" + site))
				{
					Log.Warn("production: " + site + " rolled on a client outside a seeded replay (its result will differ from the server's)");
				}
			}
		}
		else if (ServerDay)
		{
			UnseededServerDay++;
			if (rollWarned.Add("s:" + site))
			{
				Log.Info("production: " + site + " rolled on the server during the day outside a replicated action (whoever called it must broadcast the result)");
			}
		}
	}

	private static GameItem Scope(ProdOp op, GameItem m)
	{
		if (m == null)
		{
			return null;
		}
		GameItem val = null;
		try
		{
			GameInventory parentInventory = m.parentInventory;
			val = ((parentInventory != null) ? parentInventory.GetParentItem() : null);
		}
		catch
		{
		}
		if (val != null && (val.IsTag("CAGE_TAG") || val.IsTag("RAS_TAG")))
		{
			return val;
		}
		return m;
	}

	private static long ActionHash(ProdOp op, GameItem m)
	{
		long num = Hash(Scope(op, m));
		if (op == ProdOp.Cashout && Game.Store != null)
		{
			num = num * 31 + Game.Store.playerCash;
		}
		return num;
	}

	public static long Hash(GameItem root)
	{
		if (root == null || IsGone(root))
		{
			return 0L;
		}
		ulong num = ItemHash(root);
		foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)root, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
		{
			if (!IsGone(item))
			{
				num += ItemHash(item);
			}
		}
		return (long)num;
	}

	internal static string DigestPart()
	{
		if (!Game.ShopLoaded)
		{
			return "";
		}
		ulong num = 0uL;
		int num2 = 0;
		List<GraphNodeStorage> list = new List<GraphNodeStorage>(Game.Roots());
		GameGridInventory val = EmporiumEntry.Instance?.hirelingInv;
		if (val != null && !list.Contains((GraphNodeStorage)(object)val))
		{
			list.Add((GraphNodeStorage)(object)val);
		}
		foreach (GraphNodeStorage item in list)
		{
			if (item == null)
			{
				continue;
			}
			foreach (GameItem item2 in GraphUtils.FindAllChildrenType<GameItem>(item, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				if (item2 != null && !IsGone(item2) && IsProductionMachine(item2))
				{
					num += (ulong)(Hash(item2) * 1099511628211L + item2.uniqueId);
					num2++;
				}
			}
		}
		return num2.ToString(CultureInfo.InvariantCulture) + ":" + num.ToString("x16");
	}

	private static bool IsProductionMachine(GameItem it)
	{
		if (!it.IsTag("FEED_DISPENSER_TAG") && !it.IsTag("AUTO_BUTCHER_TAG") && !it.IsTag("CAGE_TAG") && !it.IsTag("RAS_TAG") && !(it.identifier == "vending_machine"))
		{
			return it.identifier == "vending_fountain";
		}
		return true;
	}

	private static ulong ItemHash(GameItem it)
	{
		ulong h = 14695981039346656037uL;
		Mix(ref h, it.uniqueId);
		Mix(ref h, it.identifier);
		Mix(ref h, it.unitCount);
		GameItem val = null;
		try
		{
			GameInventory parentInventory = it.parentInventory;
			val = ((parentInventory != null) ? parentInventory.GetParentItem() : null);
		}
		catch
		{
		}
		Mix(ref h, (val != null) ? val.uniqueId : (-1));
		GridShape modifiedShape = it.modifiedShape;
		if (modifiedShape != null)
		{
			Mix(ref h, modifiedShape.minX);
			Mix(ref h, modifiedShape.minY);
			Mix(ref h, modifiedShape.orientation);
			Mix(ref h, modifiedShape.flipped ? 1 : 0);
		}
		TagSystem state = it.state;
		Dictionary<string, TagState> dictionary = ((state != null) ? state.dict : null);
		if (dictionary != null)
		{
			keyBuf.Clear();
			foreach (KeyValuePair<string, TagState> item in dictionary)
			{
				if (item.Value != null && item.Value.valueEnabled && !Volatile(item.Key))
				{
					keyBuf.Add(item.Key);
				}
			}
			keyBuf.Sort(StringComparer.Ordinal);
			foreach (string item2 in keyBuf)
			{
				TagState val2 = dictionary[item2];
				Mix(ref h, item2);
				Mix(ref h, val2.valueInt);
				Mix(ref h, val2.valueBool ? 1 : 0);
				Mix(ref h, val2.valueLong);
				Mix(ref h, val2.valueString);
				Mix(ref h, (long)Math.Round((double)val2.valueFloat * 100.0));
				Mix(ref h, (long)Math.Round(val2.valueDouble * 100.0));
			}
		}
		return h;
	}

	private static bool Volatile(string tag)
	{
		if (tag.IndexOf("TEMP", StringComparison.Ordinal) < 0)
		{
			return tag == "HEATING_TAG";
		}
		return true;
	}

	public static string TagText(GameItem it)
	{
		object obj;
		if (it == null)
		{
			obj = null;
		}
		else
		{
			TagSystem state = it.state;
			obj = ((state != null) ? state.dict : null);
		}
		Dictionary<string, TagState> dictionary = (Dictionary<string, TagState>)obj;
		if (dictionary == null)
		{
			return "";
		}
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, TagState> item in dictionary)
		{
			if (item.Value != null && item.Value.valueEnabled)
			{
				list.Add(item.Key + "=" + item.Value.valueInt + "/" + (item.Value.valueBool ? 1 : 0) + "/" + item.Value.valueLong + "/" + item.Value.valueString + "/" + Math.Round(item.Value.valueFloat, 2).ToString(CultureInfo.InvariantCulture) + "/" + Math.Round(item.Value.valueDouble, 2).ToString(CultureInfo.InvariantCulture));
			}
		}
		list.Sort(StringComparer.Ordinal);
		return string.Join(";", list);
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

	private static GameItem UnlockedMachine(ProdOp op)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		GameSlotInventory obj = (((Object)(object)instance == (Object)null) ? null : ((op == ProdOp.UnlockMachine) ? instance.vendingMachineElement : instance.vendingFountainElement));
		if (obj == null)
		{
			return null;
		}
		return obj.childItem;
	}

	private static int StoredKcal(GameItem m)
	{
		return m.GetTagReadonly("STORED_KCAL_TAG").GetInt();
	}

	internal static bool IsGone(GameItem it)
	{
		if (it != null)
		{
			GameItemElement val = (GameItemElement)(object)((it is GameItemElement) ? it : null);
			if (val != null)
			{
				return val.IsDestroyed();
			}
			return false;
		}
		return true;
	}

	internal static GameItem FindItem(long id)
	{
		if (id != 0L)
		{
			return Items.Find(id);
		}
		return null;
	}

	internal static string Describe(GameItem it)
	{
		if (it != null)
		{
			return (it.identifier ?? "?") + "#" + it.uniqueId;
		}
		return "-";
	}

	private static long SafeDigest()
	{
		try
		{
			return Replay.Digest();
		}
		catch
		{
			return 0L;
		}
	}

	private static void WarnOnce(string key, string text)
	{
		if (warned.Add(key))
		{
			Log.Warn(text);
		}
	}
}
