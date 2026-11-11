using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

public static class Machines
{
	private sealed class PendingWidget
	{
		public GameItem m;

		public int w;

		public string t;

		public float v;

		public float v2;

		public float since;

		public bool sent;
	}

	private sealed class RunScope : IDisposable
	{
		private readonly bool quiet;

		private readonly IDisposable inner;

		private bool done;

		public bool Quiet => quiet;

		public RunScope(bool quiet)
		{
			this.quiet = quiet;
			runNest++;
			if (!quiet)
			{
				LoudRuns++;
				return;
			}
			QuietRuns++;
			inner = ItemsUse.Quiet();
		}

		public void Dispose()
		{
			if (done)
			{
				return;
			}
			done = true;
			runNest--;
			try
			{
				inner?.Dispose();
			}
			catch
			{
			}
		}
	}

	public const string KButton = "machine.button";

	public const string KWidget = "machine.widget";

	public const string KTags = "machine.tags";

	public const string KHopper = "hopper.run";

	public const string KTrack = "machine.track";

	public const string KLock = "machine.lock";

	public const string KRepair = "machine.repair";

	public const string KDone = "machine.done";

	public const string KReject = "machine.reject";

	public const string KTick = "machine.tick";

	public const string KCheck = "machine.check";

	private static readonly Dictionary<long, string> lastOpOn = new Dictionary<long, string>();

	private static bool inited;

	private static long seq;

	private static int nextQ;

	private static readonly HashSet<string> warned = new HashSet<string>();

	private static readonly FieldInfo mbCallback = AccessTools.Field(typeof(MachineryButton), "callback");

	private static readonly FieldInfo beCallback = AccessTools.Field(typeof(ButtonElement), "callback");

	private static readonly FieldInfo sliderDragging = AccessTools.Field(typeof(MachinerySlider), "dragging");

	private static readonly FieldInfo rangeDragging = AccessTools.Field(typeof(MachineryRangeSlider), "dragging");

	private const int ClassNormal = 0;

	private const int ClassLocal = 1;

	private const int ClassCounter = 2;

	private static readonly Dictionary<MethodInfo, int> buttonClass = new Dictionary<MethodInfo, int>();

	private static HashSet<int> localTokens;

	private static HashSet<int> counterTokens;

	private static readonly Dictionary<GameItem, HashSet<string>> dirty = new Dictionary<GameItem, HashSet<string>>();

	[ThreadStatic]
	private static int captureOff;

	private static readonly Dictionary<object, PendingWidget> pendingWidgets = new Dictionary<object, PendingWidget>();

	private const string CassetteStateTag = "CASETTE_PLAYER_STATE_INT";

	private const string CassetteIndexTag = "CURRENT_PLAYING_CASSETTE_INDEX_TAG";

	private const string CassetteTrackTag = "CURRENT_PLAYING_TRACK_ID_TAG";

	private static readonly Dictionary<int, float> lastRepair = new Dictionary<int, float>();

	private const float RepairGapSeconds = 2f;

	private static readonly Dictionary<long, float> lastAsk = new Dictionary<long, float>();

	private static readonly List<KeyValuePair<long, string>> pendingRepairs = new List<KeyValuePair<long, string>>();

	private static float lastRepairSent = -100f;

	private const float RepairSendGap = 2.25f;

	private static float lastPush;

	private static float lastKeepAlive;

	private static readonly FieldInfo hopperOpen = AccessTools.Field(typeof(HopperHelper), "openItem");

	private static readonly FieldInfo coordOpen = AccessTools.Field(typeof(MachineStorageCoordinator), "openItem");

	private static GameItem heldModal;

	private static long heldModalId;

	private static long refusedModalId;

	private static float refusedAt;

	private const string TempTag = "CONTAINER_TEMP_TAG";

	private const string HeatTag = "HEATING_TAG";

	private static readonly Dictionary<long, long> pushed = new Dictionary<long, long>();

	private static readonly FieldInfo featureValueMod = AccessTools.Field(typeof(ItemFeature), "valueModifier");

	private static int runNest;

	private static readonly Dictionary<long, string> lastRunOn = new Dictionary<long, string>();

	public static int SentCount { get; private set; }

	public static string LastSentKind { get; private set; } = "";

	public static int AppliedCount { get; private set; }

	public static int RejectCount { get; private set; }

	public static int RefusedCount { get; private set; }

	public static string LastReject { get; private set; } = "";

	public static int MismatchCount { get; private set; }

	public static int RepairsAsked { get; private set; }

	public static int RepairsSent { get; private set; }

	public static int ChecksOk { get; private set; }

	public static int TicksApplied { get; private set; }

	public static int TicksSent { get; private set; }

	public static int SuppressedTicks { get; private set; }

	public static int SuppressedAuto { get; private set; }

	public static MachineDone LastApplied { get; private set; }

	public static int LoadedCount { get; private set; }

	public static int TrackStale { get; private set; }

	public static int RepairsThrottled { get; private set; }

	public static string LastRepairWhy { get; private set; } = "";

	public static int WaterLabelsApplied { get; private set; }

	public static int QuietRuns { get; private set; }

	public static int LoudRuns { get; private set; }

	public static string LockName(long machineId)
	{
		return "machine:" + machineId.ToString(CultureInfo.InvariantCulture);
	}

	public static string LastOpOn(long machineId)
	{
		if (!lastOpOn.TryGetValue(machineId, out var value))
		{
			return "";
		}
		return value;
	}

	[SystemInit]
	internal static void Init()
	{
		if (inited)
		{
			return;
		}
		inited = true;
		try
		{
			string[] array = new string[4] { "machine.button", "machine.widget", "machine.tags", "hopper.run" };
			foreach (string text in array)
			{
				string kind = text;
				On(kind, delegate(MachineReq m, int p)
				{
					Serve(kind, m, p);
				}, null);
			}
			On<MachineReq>("machine.track", OnTrackRequest, null);
			On<MachineReq>("machine.lock", OnLockRequest, null);
			On<MachineRepairReq>("machine.repair", OnRepairRequest, null);
			On<MachineDone>("machine.done", null, OnDone);
			On<MachineReject>("machine.reject", null, OnReject);
			On<MachineTick>("machine.tick", null, OnTick);
			On<MachineCheck>("machine.check", null, OnCheck);
			ItemsUse.RegisterValidator("machines", ValidateUse);
			ItemsUse.RegisterReplayMode("machines", PickUseMode);
			ItemsUse.Performed += OnUsePerformed;
			Snapshot.ClientReady += OnClientReady;
			Snapshot.Loaded += OnSnapshotLoaded;
			Net.PlayerLeft += OnPlayerLeftServer;
			ProbablyTogether.Digest.AddPart("machines", PartHash);
			Log.Info("machines: ready (quiet scope: ItemsUse.Quiet)");
		}
		catch (Exception ex)
		{
			Log.Error("machines: init failed: " + ex);
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
			Log.Error("machines: cannot register " + kind + ": " + ex.Message);
		}
	}

	internal static bool IsWidget(object el)
	{
		if (!(el is MachineryButton) && !(el is ButtonElement) && !(el is MachinerySlider) && !(el is MachineryRangeSlider))
		{
			return el is MachineryRadioButton;
		}
		return true;
	}

	public static List<object> Widgets(GameItem machine)
	{
		List<object> list = new List<object>();
		PixelWindow obj = ((machine != null) ? machine.contentWindow : null);
		object obj2;
		if (obj == null)
		{
			obj2 = null;
		}
		else
		{
			PixelElement childElement = obj.childElement;
			obj2 = ((childElement != null) ? childElement.handler : null);
		}
		TreeNodeRender val = (TreeNodeRender)obj2;
		if ((Object)(object)val == (Object)null)
		{
			return list;
		}
		TreeNodeRender[] componentsInChildren = ((Component)val).GetComponentsInChildren<TreeNodeRender>(true);
		foreach (TreeNodeRender val2 in componentsInChildren)
		{
			PixelElement element = val2.element;
			if (element != null && element.handler == val2 && IsWidget(element))
			{
				list.Add(element);
			}
		}
		return list;
	}

	public static object WidgetAt(GameItem machine, int index)
	{
		if (index < 0)
		{
			return null;
		}
		List<object> list = Widgets(machine);
		if (index >= list.Count)
		{
			return null;
		}
		return list[index];
	}

	public static bool OwnerOf(object widget, out GameItem machine, out int index)
	{
		machine = null;
		index = -1;
		PixelElement val = (PixelElement)((widget is PixelElement) ? widget : null);
		if (val == null)
		{
			return false;
		}
		TreeNodeRender handler = val.handler;
		if ((Object)(object)handler == (Object)null)
		{
			return false;
		}
		PixelWindow val2 = handler.FindParentWindow();
		List<GameItem> list = ((val2 != null) ? val2.parentItems : null);
		if (list == null)
		{
			return false;
		}
		foreach (GameItem item in list)
		{
			if (item != null && item.contentWindow == val2)
			{
				machine = item;
				break;
			}
		}
		if (machine == null)
		{
			return false;
		}
		index = Widgets(machine).IndexOf(widget);
		return index >= 0;
	}

	private static string Caption(object w)
	{
		MachineryButton val = (MachineryButton)((w is MachineryButton) ? w : null);
		if (val != null)
		{
			return val.text ?? "";
		}
		MachineryRadioButton val2 = (MachineryRadioButton)((w is MachineryRadioButton) ? w : null);
		if (val2 != null)
		{
			return val2.text ?? "";
		}
		return "";
	}

	internal static Action CallbackOf(object button)
	{
		FieldInfo fieldInfo = ((button is MachineryButton) ? mbCallback : ((button is ButtonElement) ? beCallback : null));
		try
		{
			return fieldInfo?.GetValue(button) as Action;
		}
		catch
		{
			return null;
		}
	}

	private static bool Dragging(object w)
	{
		try
		{
			if (w is MachinerySlider)
			{
				return sliderDragging != null && (bool)sliderDragging.GetValue(w);
			}
			if (w is MachineryRangeSlider)
			{
				return rangeDragging != null && (bool)rangeDragging.GetValue(w);
			}
		}
		catch
		{
		}
		return false;
	}

	private static void BuildTokens()
	{
		if (localTokens == null)
		{
			localTokens = new HashSet<int>();
			counterTokens = new HashSet<int>();
			Add(localTokens, AccessTools.Method(typeof(MachineAutoButcher), "CopySettings", (Type[])null, (Type[])null));
			Add(localTokens, AccessTools.Method(typeof(MachineAutoButcher), "PasteSettings", (Type[])null, (Type[])null));
			Add(localTokens, AccessTools.Method(typeof(MachineCassettePlayer), "ChangeVolumeClick", (Type[])null, (Type[])null));
			Add(localTokens, AccessTools.Method(typeof(GenericConfirmationUIManager), "OpenPanel", (Type[])null, (Type[])null));
			Add(localTokens, AccessTools.Method(typeof(GenericConfirmationUIManager), "OpenPanelSingleButton", (Type[])null, (Type[])null));
			Add(localTokens, AccessTools.Method(typeof(ChemFinisherUIManager), "OpenUI", (Type[])null, (Type[])null));
			Add(counterTokens, AccessTools.Method(typeof(MachineDealMaker), "TryActivate", (Type[])null, (Type[])null));
		}
		static void Add(HashSet<int> set, MethodInfo mi)
		{
			if (mi != null)
			{
				set.Add(mi.MetadataToken);
			}
		}
	}

	internal static int Classify(Action cb)
	{
		if (cb == null)
		{
			return 0;
		}
		MethodInfo method;
		try
		{
			method = cb.Method;
		}
		catch
		{
			return 0;
		}
		if (method == null)
		{
			return 0;
		}
		lock (buttonClass)
		{
			if (buttonClass.TryGetValue(method, out var value))
			{
				return value;
			}
			value = 0;
			try
			{
				BuildTokens();
				if (method.DeclaringType != null && method.DeclaringType.Assembly == typeof(GameItem).Assembly)
				{
					byte[] array = method.GetMethodBody()?.GetILAsByteArray();
					if (array != null)
					{
						value = (Calls(array, localTokens) ? 1 : (Calls(array, counterTokens) ? 2 : 0));
					}
				}
			}
			catch (Exception ex)
			{
				Log.Warn("machines: cannot read a button closure (" + ex.GetType().Name + "): sending it to the server");
			}
			buttonClass[method] = value;
			return value;
		}
	}

	private static bool Calls(byte[] il, HashSet<int> tokens)
	{
		if (tokens.Count == 0)
		{
			return false;
		}
		for (int i = 0; i + 4 < il.Length; i++)
		{
			byte b = il[i];
			if (b == 40 || b == 111)
			{
				int item = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
				if (tokens.Contains(item))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool ServerMayWrap()
	{
		if (Net.IsServer && Net.IsLive && Items.Ready && !CurrentConverterHelper.isNightCycle && !Snapshot.Capturing)
		{
			return !SaveManager.IsDecoding;
		}
		return false;
	}

	internal static bool OnButton(object button)
	{
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (!OwnerOf(button, out var machine, out var index))
		{
			return true;
		}
		int num = Classify(CallbackOf(button));
		if (Net.IsServer)
		{
			if (num == 1 || !ServerMayWrap())
			{
				return true;
			}
			ServerAct(new MachineDone
			{
				op = "button",
				w = index,
				t = button.GetType().Name
			}, machine, 0);
			Deselect(button);
			return false;
		}
		long num2 = Ids.Of(machine);
		if (num == 1)
		{
			string text = BusyText(num2);
			if (text == null)
			{
				return true;
			}
			Notify(text);
			Deselect(button);
			return false;
		}
		if (num2 == 0L)
		{
			Unknown(machine);
			Deselect(button);
			return false;
		}
		Send("machine.button", new MachineReq
		{
			m = num2,
			w = index,
			t = button.GetType().Name,
			x = Caption(button)
		});
		Deselect(button);
		return false;
	}

	private static void Deselect(object b)
	{
		try
		{
			object obj = ((b is SelectableElement) ? b : null);
			if (obj != null)
			{
				((SelectableElement)obj).Deselect();
			}
		}
		catch
		{
		}
	}

	internal static bool OnSlider(MachinerySlider s, float value, bool notify)
	{
		if (!notify || !Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (!OwnerOf(s, out var machine, out var index) || Production.OwnsSettings(machine))
		{
			return true;
		}
		if (Net.IsServer)
		{
			if (!ServerMayWrap())
			{
				return true;
			}
			ServerAct(new MachineDone
			{
				op = "slider",
				w = index,
				t = "MachinerySlider",
				v = value
			}, machine, 0);
			return false;
		}
		s.SetValue(value, false);
		Queue(s, machine, index, "MachinerySlider", s.value, 0f);
		return false;
	}

	internal static bool OnRange(MachineryRangeSlider r, float lo, float hi, bool notify)
	{
		if (!notify || !Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (!OwnerOf(r, out var machine, out var index) || Production.OwnsSettings(machine))
		{
			return true;
		}
		if (Net.IsServer)
		{
			if (!ServerMayWrap())
			{
				return true;
			}
			ServerAct(new MachineDone
			{
				op = "range",
				w = index,
				t = "MachineryRangeSlider",
				v = lo,
				v2 = hi
			}, machine, 0);
			return false;
		}
		r.SetValues(lo, hi, false);
		Queue(r, machine, index, "MachineryRangeSlider", r.low, r.high);
		return false;
	}

	internal static bool OnRadio(MachineryRadioButton rb, bool on, bool notify)
	{
		if (!notify || !Net.IsOnline || Net.Applying || rb.isOn == on)
		{
			return true;
		}
		if (!OwnerOf(rb, out var machine, out var index) || Production.OwnsSettings(machine))
		{
			return true;
		}
		if (Net.IsServer)
		{
			if (!ServerMayWrap())
			{
				return true;
			}
			ServerAct(new MachineDone
			{
				op = "radio",
				w = index,
				t = "MachineryRadioButton",
				on = on
			}, machine, 0);
			return false;
		}
		long num = Ids.Of(machine);
		if (num == 0L)
		{
			Unknown(machine);
			return false;
		}
		Send("machine.widget", new MachineReq
		{
			m = num,
			w = index,
			t = "MachineryRadioButton",
			x = Caption(rb),
			on = on
		});
		return false;
	}

	internal static bool OnHopperRun(GameItem intake, ref int result)
	{
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (Net.IsClient)
		{
			result = 0;
			long num = Ids.Of(intake);
			if (num == 0L)
			{
				Unknown(intake);
				return false;
			}
			Send("hopper.run", new MachineReq
			{
				m = num
			});
			return false;
		}
		if (!ServerMayWrap() || intake == null)
		{
			return true;
		}
		result = ServerAct(new MachineDone
		{
			op = "hopper"
		}, intake, 0);
		return false;
	}

	internal static bool OnHopperSchedule()
	{
		if (!Net.IsClient)
		{
			return true;
		}
		SuppressedAuto++;
		return false;
	}

	internal static bool OnTrackDone()
	{
		if (!Net.IsOnline || Net.Applying)
		{
			return true;
		}
		if (Net.IsServer)
		{
			return false;
		}
		if (DayLog.Replaying || !Net.IsLive)
		{
			return false;
		}
		GameItem val = (((Object)(object)EmporiumEntry.Instance != (Object)null) ? EmporiumEntry.Instance.cassettePlayer : null);
		if (val == null || val.uniqueId == 0)
		{
			return false;
		}
		Send("machine.track", new MachineReq
		{
			m = val.uniqueId,
			w = val.GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt(),
			x = (val.GetTagReadonly("CURRENT_PLAYING_TRACK_ID_TAG").GetString() ?? "")
		});
		return false;
	}

	/// The game's once-a-second work runs only on the copy that serves the shop (a host too, though Net.IsClient is true).
	internal static bool OnTickFunction()
	{
		if (!Net.IsClient || Net.Applying)
		{
			return true;
		}
		SuppressedTicks++;
		return false;
	}

	internal static void OnTagWrite(GameItem it, string key)
	{
		if (Net.IsClient && !Net.Applying && captureOff <= 0 && it != null && key != null && IsSettingKey(key) && SettingKeyFits(it, key) && !Snapshot.IsLoading && !SaveManager.IsDecoding && !DayLog.Replaying && it.uniqueId != 0)
		{
			if (!dirty.TryGetValue(it, out var value))
			{
				value = (dirty[it] = new HashSet<string>(StringComparer.Ordinal));
			}
			value.Add(key);
		}
	}

	private static bool IsSettingKey(string k)
	{
		if (k.Length < 8)
		{
			return false;
		}
		switch (k[0])
		{
		case 'H':
			return k.StartsWith("HOPPER_", StringComparison.Ordinal);
		case 'S':
			if (!(k == "STORAGE_COORDINATOR_PRESETS_STRING"))
			{
				return k == "SELECTED_CHEM_ID_TAG";
			}
			return true;
		default:
			return false;
		}
	}

	private static bool SettingKeyFits(GameItem it, string k)
	{
		switch (k[0])
		{
		case 'H':
			return it.IsTag("HOPPER_INTAKE_TAG");
		case 'S':
			if (k[1] != 'T')
			{
				return it.identifier == "chem_finisher";
			}
			return it.IsTag("STORAGE_COORDINATOR_TAG");
		default:
			return false;
		}
	}

	private static int Send(string kind, MachineReq r)
	{
		if (!Net.IsClient)
		{
			return 0;
		}
		if (!Net.IsLive)
		{
			WarnOnce("live:" + kind, "machines: " + kind + " while the shop is still loading: not sent");
			return 0;
		}
		r.q = ++nextQ;
		Net.ToServer(kind, r);
		SentCount++;
		LastSentKind = kind;
		return r.q;
	}

	private static void Queue(object widget, GameItem m, int idx, string type, float v, float v2)
	{
		if (!pendingWidgets.TryGetValue(widget, out var value))
		{
			Dictionary<object, PendingWidget> dictionary = pendingWidgets;
			PendingWidget obj = new PendingWidget
			{
				since = Time.realtimeSinceStartup
			};
			value = obj;
			dictionary[widget] = obj;
		}
		value.m = m;
		value.w = idx;
		value.t = type;
		value.v = v;
		value.v2 = v2;
		value.sent = false;
	}

	private static void FlushWidgets()
	{
		if (pendingWidgets.Count == 0)
		{
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		List<object> list = null;
		foreach (KeyValuePair<object, PendingWidget> pendingWidget in pendingWidgets)
		{
			PendingWidget value = pendingWidget.Value;
			bool flag = Dragging(pendingWidget.Key);
			if (!value.sent && (!flag || realtimeSinceStartup - value.since > 0.25f))
			{
				long num = Ids.Of(value.m);
				if (num != 0L)
				{
					Send("machine.widget", new MachineReq
					{
						m = num,
						w = value.w,
						t = value.t,
						v = value.v,
						v2 = value.v2
					});
				}
				value.sent = true;
				value.since = realtimeSinceStartup;
			}
			if (value.sent && !flag)
			{
				(list ?? (list = new List<object>())).Add(pendingWidget.Key);
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (object item in list)
		{
			pendingWidgets.Remove(item);
		}
	}

	private static void FlushTags()
	{
		if (dirty.Count == 0)
		{
			return;
		}
		List<KeyValuePair<GameItem, HashSet<string>>> list = dirty.ToList();
		dirty.Clear();
		foreach (KeyValuePair<GameItem, HashSet<string>> item in list)
		{
			GameItem it = item.Key;
			if (Items.IsGone(it))
			{
				continue;
			}
			long num = Ids.Of(it);
			if (num != 0L)
			{
				List<MachineTag> tags = (from k in item.Value.OrderBy<string, string>((string k) => k, StringComparer.Ordinal)
					select ToDto(k, it.state.GetTag(k))).ToList();
				Send("machine.tags", new MachineReq
				{
					m = num,
					tags = tags
				});
			}
		}
	}

	private static void Unknown(GameItem m)
	{
		Log.Warn("machines: " + Describe(m) + " has no id the server gave; asking for a resync");
		Snapshot.RequestResync("machines: " + Describe(m) + " has no server id");
	}

	private static void Serve(string kind, MachineReq r, int from)
	{
		if (r == null)
		{
			return;
		}
		if (!Net.IsLive || !Items.Ready)
		{
			Reject(from, r, kind, "starting", "The shop is not open yet.");
			return;
		}
		GameItem m = Find(r.m);
		string text = null;
		string text2 = CheckMachine(m, from, ref text);
		MachineDone d = null;
		if (text2 == null)
		{
			try
			{
				text2 = Plan(kind, r, m, from, out d, ref text);
			}
			catch (Exception ex)
			{
				text2 = "bad";
				Log.Warn("machines: checking " + kind + " from " + Net.PlayerName(from) + " threw: " + ex.Message);
			}
		}
		if (text2 != null)
		{
			Reject(from, r, kind, text2, text, (kind == "machine.tags") ? Current(m, r.tags) : null);
			return;
		}
		d.q = r.q;
		ServerAct(d, m, from);
	}

	private static string CheckMachine(GameItem m, int from, ref string text)
	{
		if (m == null || Items.IsGone(m))
		{
			return "gone";
		}
		string ownerOnly = Systems.Bays.Refusal(from, (GraphNodeStorage)(object)m);
		if (ownerOnly != null)
		{
			text = ownerOnly;
			return "owner";
		}
		if (!GeneralHelper.IsItemOwned(m) && (long)m.uniqueId != -103)
		{
			return "notours";
		}
		if (m.parentInventory == null || Items.RootOf((GraphNodeStorage)(object)m.parentInventory) == null)
		{
			return "notours";
		}
		string text2 = BusyFor(Ids.Of(m), from);
		if (text2 != null)
		{
			text = text2;
			return "locked";
		}
		return null;
	}

	private static string Plan(string kind, MachineReq r, GameItem m, int from, out MachineDone d, ref string text)
	{
		d = null;
		switch (kind)
		{
		case "machine.button":
		{
			object obj = WidgetAt(m, r.w);
			if (!(obj is MachineryButton) && !(obj is ButtonElement))
			{
				return "widget";
			}
			if (obj.GetType().Name != r.t)
			{
				return "widget";
			}
			string text2 = Caption(obj);
			if (text2 != "" && !string.IsNullOrEmpty(r.x) && text2 != r.x)
			{
				return "widget";
			}
			Action action = CallbackOf(obj);
			if (action == null)
			{
				return "nothing";
			}
			switch (Classify(action))
			{
			case 1:
				return "local";
			case 2:
				if ((text = CounterRefusal(from)) != null)
				{
					return "counter";
				}
				break;
			}
			d = new MachineDone
			{
				op = "button",
				w = r.w,
				t = r.t
			};
			return null;
		}
		case "machine.widget":
		{
			object obj2 = WidgetAt(m, r.w);
			if (obj2 == null || obj2.GetType().Name != r.t)
			{
				return "widget";
			}
			MachinerySlider val = (MachinerySlider)((obj2 is MachinerySlider) ? obj2 : null);
			if (val != null)
			{
				if (float.IsNaN(r.v) || r.v < val.min - 0.001f || r.v > val.max + 0.001f)
				{
					return "range";
				}
				d = new MachineDone
				{
					op = "slider",
					w = r.w,
					t = r.t,
					v = r.v
				};
				return null;
			}
			MachineryRangeSlider val2 = (MachineryRangeSlider)((obj2 is MachineryRangeSlider) ? obj2 : null);
			if (val2 != null)
			{
				if (float.IsNaN(r.v) || float.IsNaN(r.v2) || r.v > r.v2 || r.v < val2.min - 0.001f || r.v2 > val2.max + 0.001f)
				{
					return "range";
				}
				d = new MachineDone
				{
					op = "range",
					w = r.w,
					t = r.t,
					v = r.v,
					v2 = r.v2
				};
				return null;
			}
			MachineryRadioButton val3 = (MachineryRadioButton)((obj2 is MachineryRadioButton) ? obj2 : null);
			if (val3 != null)
			{
				string text3 = Caption(val3);
				if (text3 != "" && !string.IsNullOrEmpty(r.x) && text3 != r.x)
				{
					return "widget";
				}
				d = new MachineDone
				{
					op = "radio",
					w = r.w,
					t = r.t,
					on = r.on
				};
				return null;
			}
			return "widget";
		}
		case "machine.tags":
			if (r.tags == null || r.tags.Count == 0 || r.tags.Count > 64)
			{
				return "bad";
			}
			foreach (MachineTag tag in r.tags)
			{
				string text4 = TagRule(m, tag);
				if (text4 != null)
				{
					text = "That setting can't be changed: " + text4 + ".";
					return "setting";
				}
			}
			d = new MachineDone
			{
				op = "tags",
				tags = r.tags
			};
			return null;
		case "hopper.run":
			if (!HopperHelper.IsIntake(m))
			{
				return "notahopper";
			}
			d = new MachineDone
			{
				op = "hopper"
			};
			return null;
		default:
			return "bad";
		}
	}

	private static string TagRule(GameItem m, MachineTag t)
	{
		string text = t?.k;
		if (string.IsNullOrEmpty(text) || !IsSettingKey(text) || !SettingKeyFits(m, text))
		{
			return "not a setting of this machine (" + (text ?? "?") + ")";
		}
		if ((t.s?.Length ?? 0) > 20000)
		{
			return "too long";
		}
		switch (text)
		{
		case "HOPPER_ORDER_INT":
			if (t.i < 1 || t.i > 15)
			{
				return "order out of range";
			}
			return null;
		case "HOPPER_KEEP_INT":
			if (t.i < 0 || t.i > 20)
			{
				return "keep out of range";
			}
			return null;
		case "HOPPER_FILTER_MODE_INT":
			if (t.i < 0 || t.i > 2)
			{
				return "unknown filter mode";
			}
			return null;
		case "HOPPER_FILTER_STRING":
			if ((t.s?.Length ?? 0) > 4000)
			{
				return "too many filter items";
			}
			return null;
		case "HOPPER_ON_ADD_BOOL":
		case "HOPPER_ON_MOVE_BOOL":
			return null;
		case "HOPPER_LINK_INT":
			if (t.i != 0)
			{
				return "links are made by using one hopper on the other";
			}
			return null;
		case "STORAGE_COORDINATOR_PRESETS_STRING":
			return null;
		case "SELECTED_CHEM_ID_TAG":
			if (ChemFinisherRecipe.chemFinisherRecipes == null || !ChemFinisherRecipe.chemFinisherRecipes.Any((ChemFinisherRecipe x) => x != null && x.identifier == t.s))
			{
				return "unknown recipe";
			}
			return null;
		default:
			if (text.StartsWith("HOPPER_", StringComparison.Ordinal))
			{
				return "unknown hopper setting";
			}
			return "unknown setting";
		}
	}

	internal static int ServerAct(MachineDone d, GameItem m, int actor)
	{
		d.seq = ++seq;
		d.actor = actor;
		d.m = Ids.Of(m);
		PushContainersOf(m);
		int num = 0;
		Exception ex = null;
		Seed seed;
		using (Replay.Server(out seed))
		{
			d.seed = seed;
			Tape tape;
			using (Items.Record(out tape))
			{
				using (QuietFor(m, quiet: true))
				{
					d.tape = tape;
					try
					{
						num = RunOp(d, m);
					}
					catch (Exception ex2)
					{
						ex = ex2;
					}
				}
			}
		}
		if (ex != null)
		{
			Log.Error("machines: " + d.op + " on " + Describe(m) + " threw on the server (sent as far as it got): " + ex);
		}
		if (actor == 0 && d.op == "hopper" && num <= 0 && ex == null)
		{
			Tape tape2 = d.tape;
			if (tape2 == null || tape2.uids.Count == 0)
			{
				return num;
			}
		}
		d.dg = Digest(m);
		d.rd = SafeDigest();
		Net.ToAll("machine.done", d);
		AppliedCount++;
		if (actor > 0)
		{
			Locks.Touch(LockName(d.m), actor);
		}
		Log.Info("machines: #" + d.seq + " " + Net.PlayerName(actor) + " " + Describe(d) + " on " + Describe(m) + ((num != 1) ? (" -> " + num) : "") + (((d.tape?.uids.Count ?? 0) > 0) ? ("; new ids " + string.Join(",", d.tape.uids)) : ""));
		return num;
	}

	private static int RunOp(MachineDone d, GameItem m)
	{
		switch (d.op)
		{
		case "button":
		{
			Action action = CallbackOf(WidgetAt(m, d.w));
			if (action == null)
			{
				throw new InvalidOperationException("no button " + d.w + " (" + d.t + ")");
			}
			action();
			return 1;
		}
		case "slider":
		{
			object obj2 = WidgetAt(m, d.w);
			MachinerySlider val2 = (MachinerySlider)((obj2 is MachinerySlider) ? obj2 : null);
			if (val2 == null)
			{
				throw new InvalidOperationException("no slider " + d.w);
			}
			val2.SetValue(d.v, false);
			val2.onChanged?.Invoke(val2.value);
			return 1;
		}
		case "range":
		{
			object obj3 = WidgetAt(m, d.w);
			MachineryRangeSlider val3 = (MachineryRangeSlider)((obj3 is MachineryRangeSlider) ? obj3 : null);
			if (val3 == null)
			{
				throw new InvalidOperationException("no range slider " + d.w);
			}
			val3.SetValues(d.v, d.v2, false);
			val3.onChanged?.Invoke(val3.low, val3.high);
			return 1;
		}
		case "radio":
		{
			object obj = WidgetAt(m, d.w);
			MachineryRadioButton val = (MachineryRadioButton)((obj is MachineryRadioButton) ? obj : null);
			if (val == null)
			{
				throw new InvalidOperationException("no radio button " + d.w);
			}
			if (val.isOn != d.on)
			{
				val.SetOn(d.on, true);
			}
			else
			{
				val.onChanged?.Invoke(d.on);
				if (d.on && val.group != null)
				{
					val.group.onChanged?.Invoke(val.group.buttons.IndexOf(val));
				}
			}
			return 1;
		}
		case "tags":
			WriteTags(m, d.tags);
			m.ValidateShapeState();
			return 1;
		case "hopper":
			return HopperHelper.RunAndRecord(m);
		case "track":
			MachineCassettePlayer.OnNextButtonPressed(m, MachineCassettePlayer.GetCassetteInventory());
			return 1;
		default:
			throw new InvalidOperationException("unknown machine op " + d.op);
		}
	}

	private static void Reject(int player, MachineReq r, string kind, string why, string text, List<MachineTag> current = null)
	{
		text = text ?? RejectText(why);
		RefusedCount++;
		Log.Info("machines: refused " + Net.PlayerName(player) + "'s " + kind + " on #" + r.m + ((r.w >= 0) ? (" widget " + r.w + " (" + r.t + ")") : "") + ": " + why);
		if (player > 0)
		{
			Net.ToPlayer(player, "machine.reject", new MachineReject
			{
				q = r.q,
				op = kind,
				m = r.m,
				why = why,
				text = text,
				tags = current
			});
		}
	}

	private static List<MachineTag> Current(GameItem m, List<MachineTag> asked)
	{
		if (m == null || asked == null)
		{
			return null;
		}
		List<MachineTag> list = new List<MachineTag>();
		foreach (MachineTag item in asked.Take(64))
		{
			if (!string.IsNullOrEmpty(item?.k) && IsSettingKey(item.k))
			{
				list.Add(ToDto(item.k, m.state.GetTag(item.k)));
			}
		}
		return list;
	}

	private static string RejectText(string why)
	{
		return why switch
		{
			"gone" => "That machine is gone.", 
			"starting" => "The shop is not open yet.", 
			"widget" => "That control is not there any more.", 
			_ => "That didn't work.", 
		};
	}

	private static void OnTrackRequest(MachineReq r, int from)
	{
		if (r == null || !Net.IsLive || !Items.Ready)
		{
			return;
		}
		GameItem val = Find(r.m);
		GameItem val2 = (((Object)(object)EmporiumEntry.Instance != (Object)null) ? EmporiumEntry.Instance.cassettePlayer : null);
		if (val != null && val2 != null && val == val2)
		{
			if (val.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt() != 1 || val.GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt() != r.w || (val.GetTagReadonly("CURRENT_PLAYING_TRACK_ID_TAG").GetString() ?? "") != (r.x ?? ""))
			{
				TrackStale++;
				return;
			}
			ServerAct(new MachineDone
			{
				op = "track",
				q = r.q
			}, val, from);
		}
	}

	private static void OnLockRequest(MachineReq r, int from)
	{
		if (r == null)
		{
			return;
		}
		string name = LockName(r.m);
		if (!r.on)
		{
			Locks.Release(name, from);
			return;
		}
		GameItem val = Find(r.m);
		if (val == null || !IsModal(val))
		{
			Net.ToPlayer(from, "machine.reject", new MachineReject
			{
				q = r.q,
				op = "machine.lock",
				m = r.m,
				why = "bad",
				text = ""
			});
		}
		else if (!Locks.TryTake(name, from))
		{
			int id = Locks.Holder(name);
			Log.Info("machines: " + Net.PlayerName(from) + " opened " + Describe(val) + " while " + Net.PlayerName(id) + " has it open: refused");
			Net.ToPlayer(from, "machine.reject", new MachineReject
			{
				q = r.q,
				op = "machine.lock",
				m = r.m,
				why = "locked",
				text = Net.PlayerName(id) + " is using that " + KindName(val) + "."
			});
		}
	}

	public static bool IsModal(GameItem m)
	{
		if (m != null)
		{
			if (!HopperHelper.IsIntake(m) && !m.IsTag("STORAGE_COORDINATOR_TAG"))
			{
				return m.identifier == "chem_finisher";
			}
			return true;
		}
		return false;
	}

	private static string BusyFor(long id, int player)
	{
		if (id == 0L || player <= 0)
		{
			return null;
		}
		int num = Locks.Holder(LockName(id));
		if (num < 0 || num == player)
		{
			return null;
		}
		return Net.PlayerName(num) + " is using that machine.";
	}

	private static string BusyText(long id)
	{
		if (id == 0L)
		{
			return null;
		}
		int num = Locks.HeldBy(LockName(id));
		if (num < 0 || num == Net.LocalPlayer)
		{
			return null;
		}
		return Net.PlayerName(num) + " is using that machine.";
	}

	private static string KindName(GameItem m)
	{
		if (!HopperHelper.IsIntake(m))
		{
			if (!m.IsTag("STORAGE_COORDINATOR_TAG"))
			{
				return "machine";
			}
			return "coordinator";
		}
		return "hopper";
	}

	private static string ValidateUse(UseContext ctx)
	{
		if (ctx == null || ctx.Player <= 0)
		{
			return null;
		}
		GameItem[] array = (GameItem[])(object)new GameItem[2] { ctx.Source, ctx.Target };
		foreach (GameItem val in array)
		{
			if (val != null)
			{
				int num = Locks.Holder(LockName(val.uniqueId));
				if (num >= 0 && num != ctx.Player)
				{
					return Net.PlayerName(num) + " is using that " + KindName(val) + ".";
				}
			}
		}
		if (ctx.Op == UseOp.Activate && ctx.Source != null && ctx.Source.identifier == "deal_maker")
		{
			return CounterRefusal(ctx.Player);
		}
		return null;
	}

	private static string CounterRefusal(int player)
	{
		if (player <= 0)
		{
			return null;
		}
		if (!Customers.TryCounter(player, out var refusal))
		{
			return refusal;
		}
		return null;
	}

	private static UseReplay? PickUseMode(UseContext ctx)
	{
		if (ctx == null || !Net.IsServer || !Net.IsLive)
		{
			return null;
		}
		try
		{
			PushContainersOf(ctx.Source);
			if (ctx.Target != null && ctx.Target != ctx.Source)
			{
				PushContainersOf(ctx.Target);
			}
		}
		catch (Exception ex)
		{
			WarnOnce("usepush:" + ex.GetType().Name, "machines: container push before a use threw: " + ex.Message);
		}
		return null;
	}

	public static bool IsTurboBooster(GameItem it)
	{
		if (it != null)
		{
			if (!(it.identifier == "turbo_booster") && !it.IsTag("TURBO_BOOST_ADV"))
			{
				return it.IsTag("TURBO_BOOST_HYPERTECH");
			}
			return true;
		}
		return false;
	}

	private static bool IsMachine(GameItem it)
	{
		if (it != null && !Items.IsGone(it))
		{
			if (it.contentWindow == null && ((GameItemFunc)it).onCycleEndSlotItemFunc == null && ((GameItemFunc)it).onCycleEndEarlySlotItemFunc == null)
			{
				return HopperHelper.IsIntake(it);
			}
			return true;
		}
		return false;
	}

	private static void OnUsePerformed(UseContext ctx)
	{
		if (ctx == null || !ctx.OnServer || !Net.IsServer || (ctx.Op != UseOp.Target && ctx.Op != UseOp.Activate && ctx.Op != UseOp.Toggle))
		{
			return;
		}
		List<GameItem> list = new List<GameItem>();
		if (ctx.Op == UseOp.Target && IsMachine(ctx.Target))
		{
			list.Add(ctx.Target);
		}
		if (IsMachine(ctx.Source))
		{
			list.Add(ctx.Source);
		}
		foreach (GameItem item in list)
		{
			long num = item.uniqueId;
			if (num != 0L)
			{
				Net.ToAll("machine.check", new MachineCheck
				{
					m = num,
					dg = Digest(item),
					why = "use #" + ctx.Seq + " " + ctx.Op
				});
			}
		}
	}

	private static void OnRepairRequest(MachineRepairReq r, int from)
	{
		if (r == null || !Items.Ready)
		{
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (lastRepair.TryGetValue(from, out var value) && realtimeSinceStartup - value < 2f)
		{
			RepairsThrottled++;
			return;
		}
		GameItem val = Find(r.m);
		if (val != null)
		{
			lastRepair[from] = realtimeSinceStartup;
			string text = (string.IsNullOrEmpty(Args.Bot) ? "" : DiffNote(DigestText(val), r.mine));
			LastRepairWhy = Clip(r.why, 80);
			Log.Warn("machines: " + Net.PlayerName(from) + " differs on " + Describe(val) + " (" + LastRepairWhy + "); sending it whole" + text);
			Items.BroadcastSubtree(val, "machine-repair");
			RepairsSent++;
		}
	}

	private static void OnPlayerLeftServer(PlayerInfo p)
	{
		if (p != null && Net.IsServer)
		{
			lastRepair.Remove(p.Id);
		}
	}

	internal static string Clip(string s, int max)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder(Math.Min(s.Length, max) + 1);
		foreach (char c in s)
		{
			if (stringBuilder.Length >= max)
			{
				break;
			}
			stringBuilder.Append((char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format) ? ' ' : c);
		}
		if (stringBuilder.Length > 0 && char.IsHighSurrogate(stringBuilder[stringBuilder.Length - 1]))
		{
			stringBuilder.Length--;
		}
		if (stringBuilder.Length >= s.Length)
		{
			return stringBuilder.ToString();
		}
		return stringBuilder.ToString() + "...";
	}

	private static string DiffNote(string ours, string theirs)
	{
		if (string.IsNullOrEmpty(theirs))
		{
			return "";
		}
		if (theirs.Length > 12000)
		{
			theirs = theirs.Substring(0, 12000);
		}
		string[] array = ours.Split('\n');
		string[] array2 = theirs.Split('\n');
		List<string> list = new List<string>();
		HashSet<string> hashSet = new HashSet<string>(array2);
		HashSet<string> hashSet2 = new HashSet<string>(array);
		string[] array3 = array;
		foreach (string text in array3)
		{
			if (!hashSet.Contains(text))
			{
				list.Add("server: " + Clip(text, 160));
			}
		}
		array3 = array2;
		foreach (string text2 in array3)
		{
			if (!hashSet2.Contains(text2))
			{
				list.Add("client: " + Clip(text2, 160));
			}
		}
		if (list.Count != 0)
		{
			return "; differences: " + string.Join(" | ", list.Take(12)) + ((list.Count > 12) ? (" | +" + (list.Count - 12) + " more") : "");
		}
		return "; same text (hash order?)";
	}

	private static void OnDone(MachineDone d)
	{
		if (d == null || d.op == null)
		{
			return;
		}
		GameItem val = Find(d.m);
		if (val == null)
		{
			Mismatch("machine action #" + d.seq + " " + d.op + ": machine " + d.m + " is not here", resync: true);
			return;
		}
		bool quiet = DayLog.Replaying || (d.actor != 0 && d.actor != Net.LocalPlayer);
		int num = SoundsStopped();
		int soundsPlayed = ItemsUse.SoundsPlayed;
		int quietNoticesBlocked = ItemsUse.QuietNoticesBlocked;
		int noticesShown = ItemsUse.NoticesShown;
		bool flag = false;
		try
		{
			using ((d.seed != null) ? Replay.Client(d.seed) : Net.Apply())
			{
				using (Items.Play(d.tape))
				{
					using RunScope runScope = (RunScope)QuietFor(val, quiet);
					flag = runScope.Quiet;
					RunOp(d, val);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("machines: replay of #" + d.seq + " " + d.op + " on " + Describe(val) + " threw: " + ex);
		}
		lastRunOn[d.m] = (flag ? "quiet" : "loud") + ", " + (SoundsStopped() - num) + " stopped, " + (ItemsUse.SoundsPlayed - soundsPlayed) + " played, " + (ItemsUse.QuietNoticesBlocked - quietNoticesBlocked) + " notices kept off, " + (ItemsUse.NoticesShown - noticesShown) + " notices shown";
		AppliedCount++;
		LastApplied = d;
		lastOpOn[d.m] = d.op + " by " + d.actor + " #" + d.seq;
		if (d.dg != 0L && Digest(val) != d.dg)
		{
			AskRepair(val, "after " + Describe(d) + " #" + d.seq);
		}
		if (d.rd != 0L && SafeDigest() != d.rd)
		{
			Mismatch("shop digest differs after " + Describe(d) + " #" + d.seq + " on " + Describe(val), resync: true);
		}
	}

	private static void OnCheck(MachineCheck c)
	{
		if (c == null)
		{
			return;
		}
		GameItem val = Find(c.m);
		if (val != null)
		{
			if (Digest(val) == c.dg)
			{
				ChecksOk++;
			}
			else
			{
				AskRepair(val, c.why);
			}
		}
	}

	private static void OnReject(MachineReject r)
	{
		if (r == null)
		{
			return;
		}
		RejectCount++;
		LastReject = r.op + ": " + r.why + (string.IsNullOrEmpty(r.text) ? "" : (" (" + r.text + ")"));
		Log.Info("machines: the server refused " + LastReject);
		if (r.op == "machine.lock")
		{
			refusedModalId = r.m;
			refusedAt = Time.realtimeSinceStartup;
			if (Find(r.m) == heldModal)
			{
				heldModal = null;
				heldModalId = 0L;
			}
			CloseModalFor(r.m);
		}
		if (!string.IsNullOrEmpty(r.text))
		{
			Notify(r.text);
		}
		if (!(r.op == "machine.tags"))
		{
			return;
		}
		GameItem val = Find(r.m);
		if (val == null || r.tags == null)
		{
			return;
		}
		using (Net.Apply())
		{
			WriteTags(val, r.tags);
			val.ValidateShapeState();
		}
	}

	private static void OnTick(MachineTick t)
	{
		if (t?.items == null)
		{
			return;
		}
		using (Net.Apply())
		{
			foreach (MachineTickItem item in t.items)
			{
				GameItem val = Find(item.i);
				if (val == null || item.tags == null)
				{
					continue;
				}
				WriteTags(val, item.tags);
				if (item.w != null)
				{
					ApplyWater(val, item.w);
				}
				val.ValidateShapeState();
				if (val.IsTag("LIQUID_CONTAINER_TAG"))
				{
					try
					{
						SpriteHelper.UpdateWaterContainerSprite(val);
					}
					catch
					{
					}
				}
				TicksApplied++;
			}
		}
	}

	private static void AskRepair(GameItem m, string why)
	{
		MismatchCount++;
		long num = Ids.Of(m);
		Log.Warn("machines: " + Describe(m) + " differs from the server (" + why + "); asking for it whole");
		foreach (KeyValuePair<long, string> pendingRepair in pendingRepairs)
		{
			if (pendingRepair.Key == num)
			{
				return;
			}
		}
		pendingRepairs.Add(new KeyValuePair<long, string>(num, why));
		FlushRepairs();
	}

	private static void FlushRepairs()
	{
		if (pendingRepairs.Count == 0 || DayLog.Replaying || !Net.IsLive)
		{
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (realtimeSinceStartup - lastRepairSent < 2.25f)
		{
			return;
		}
		while (pendingRepairs.Count > 0)
		{
			KeyValuePair<long, string> keyValuePair = pendingRepairs[0];
			pendingRepairs.RemoveAt(0);
			GameItem val = Find(keyValuePair.Key);
			if (val != null && (!lastAsk.TryGetValue(keyValuePair.Key, out var value) || !(realtimeSinceStartup - value < 5f)))
			{
				SendRepair(keyValuePair.Key, keyValuePair.Value, val, realtimeSinceStartup);
				break;
			}
		}
	}

	private static void SendRepair(long id, string why, GameItem m, float now)
	{
		lastAsk[id] = now;
		lastRepairSent = now;
		string text = DigestText(m);
		if (text.Length > 12000)
		{
			text = text.Substring(0, 12000);
		}
		if (why != null && why.Length > 80)
		{
			why = why.Substring(0, 80);
		}
		Net.ToServer("machine.repair", new MachineRepairReq
		{
			m = id,
			why = why,
			mine = text
		});
		RepairsAsked++;
	}

	private static void Mismatch(string what, bool resync)
	{
		MismatchCount++;
		Log.Warn("machines: " + what + ((resync && !DayLog.Replaying) ? "; asking for a resync" : ""));
		if (resync && !DayLog.Replaying)
		{
			Snapshot.RequestResync("machines: " + what);
		}
	}

	private static void OnSnapshotLoaded()
	{
		LoadedCount++;
		pendingWidgets.Clear();
		pendingRepairs.Clear();
		dirty.Clear();
		heldModal = null;
		heldModalId = 0L;
	}

	[SystemTick]
	internal static void FrameTick()
	{
		if (!Net.IsOnline || !Game.ShopLoaded)
		{
			return;
		}
		try
		{
			if (Net.IsClient)
			{
				if (Net.IsLive)
				{
					FlushTags();
					FlushWidgets();
					PollModal();
					FlushRepairs();
				}
			}
			else if (Net.IsServer && Net.IsLive && Items.Ready)
			{
				float realtimeSinceStartup = Time.realtimeSinceStartup;
				if (realtimeSinceStartup - lastPush >= 3f)
				{
					lastPush = realtimeSinceStartup;
					PushTicks();
				}
			}
		}
		catch (Exception ex)
		{
			WarnOnce("tick:" + ex.GetType().Name, "machines: frame tick threw: " + ex);
		}
	}

	private static GameItem OpenModal()
	{
		try
		{
			object? obj = hopperOpen?.GetValue(null);
			GameItem val = (GameItem)((obj is GameItem) ? obj : null);
			if (val != null && !Items.IsGone(val))
			{
				return val;
			}
			object? obj2 = coordOpen?.GetValue(null);
			GameItem val2 = (GameItem)((obj2 is GameItem) ? obj2 : null);
			if (val2 != null && !Items.IsGone(val2))
			{
				return val2;
			}
			ChemFinisherUIManager instance = ChemFinisherUIManager.Instance;
			if ((Object)(object)instance != (Object)null && (Object)(object)instance.UIPanel != (Object)null && instance.UIPanel.activeSelf && instance.currentItem != null && !Items.IsGone(instance.currentItem))
			{
				return instance.currentItem;
			}
		}
		catch
		{
		}
		return null;
	}

	private static void PollModal()
	{
		GameItem val = OpenModal();
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		long num = ((val != null) ? Ids.Of(val) : 0);
		if (num != 0L && num == refusedModalId && realtimeSinceStartup - refusedAt < 5f)
		{
			CloseModalFor(num);
		}
		else if (val != heldModal)
		{
			if (heldModalId != 0L)
			{
				Send("machine.lock", new MachineReq
				{
					m = heldModalId,
					on = false
				});
			}
			heldModal = val;
			heldModalId = ((val != null) ? Ids.Of(val) : 0);
			if (heldModalId != 0L)
			{
				string text = BusyText(heldModalId);
				if (text != null)
				{
					Notify(text);
					CloseModalFor(heldModalId);
					heldModal = null;
					heldModalId = 0L;
				}
				else
				{
					Send("machine.lock", new MachineReq
					{
						m = heldModalId,
						on = true
					});
					lastKeepAlive = realtimeSinceStartup;
				}
			}
		}
		else if (heldModalId != 0L && realtimeSinceStartup - lastKeepAlive > 20f)
		{
			lastKeepAlive = realtimeSinceStartup;
			Send("machine.lock", new MachineReq
			{
				m = heldModalId,
				on = true
			});
		}
	}

	private static void CloseModalFor(long id)
	{
		try
		{
			GameItem val = Find(id);
			if (val == null)
			{
				return;
			}
			object? obj = hopperOpen?.GetValue(null);
			GameItem val2 = (GameItem)((obj is GameItem) ? obj : null);
			if (val2 != null && val2 == val)
			{
				CustomUIManager instance = CustomUIManager.Instance;
				if (instance != null)
				{
					instance.CloseWindow("intake_hopper");
				}
			}
			object? obj2 = coordOpen?.GetValue(null);
			GameItem val3 = (GameItem)((obj2 is GameItem) ? obj2 : null);
			if (val3 != null && val3 == val)
			{
				CustomUIManager instance2 = CustomUIManager.Instance;
				if (instance2 != null)
				{
					instance2.CloseWindow("storage_coordinator");
				}
			}
			ChemFinisherUIManager instance3 = ChemFinisherUIManager.Instance;
			if ((Object)(object)instance3 != (Object)null && instance3.currentItem == val && (Object)(object)instance3.UIPanel != (Object)null && instance3.UIPanel.activeSelf)
			{
				instance3.HideUI();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("machines: closing a window someone else holds: " + ex.Message);
		}
	}

	private static bool Ticked(GameItem it)
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
		if (dictionary != null)
		{
			if (!dictionary.ContainsKey("CONTAINER_TEMP_TAG"))
			{
				return dictionary.ContainsKey("HEATING_TAG");
			}
			return true;
		}
		return false;
	}

	private static void PushTicks(int onlyTo = -1)
	{
		List<MachineTickItem> list = new List<MachineTickItem>();
		foreach (GameItem item in Items.All())
		{
			if (!Ticked(item))
			{
				continue;
			}
			long num = item.uniqueId;
			if (num != 0L)
			{
				MachineWater w = WaterOf(item);
				long num2 = TagsHash(item) ^ WaterHash(w);
				if (onlyTo >= 0 || !pushed.TryGetValue(num, out var value) || value != num2)
				{
					pushed[num] = num2;
					list.Add(new MachineTickItem
					{
						i = num,
						tags = Dump(item),
						w = w
					});
				}
			}
		}
		if (list.Count == 0)
		{
			return;
		}
		MachineTick msg = new MachineTick
		{
			items = list
		};
		if (onlyTo >= 0)
		{
			Net.ToPlayer(onlyTo, "machine.tick", msg);
			TicksSent++;
			return;
		}
		foreach (int item2 in Net.Players.Keys.ToList())
		{
			Net.ToPlayer(item2, "machine.tick", msg);
		}
		TicksSent++;
	}

	private static void PushContainersOf(GameItem m)
	{
		List<MachineTickItem> list = null;
		foreach (GameItem item in Subtree(m))
		{
			if ((item != m || Ticked(item)) && Ticked(item) && item.uniqueId != 0)
			{
				MachineWater w = WaterOf(item);
				pushed[item.uniqueId] = TagsHash(item) ^ WaterHash(w);
				(list ?? (list = new List<MachineTickItem>())).Add(new MachineTickItem
				{
					i = item.uniqueId,
					tags = Dump(item),
					w = w
				});
			}
		}
		if (list != null)
		{
			Net.ToAll("machine.tick", new MachineTick
			{
				pre = true,
				items = list
			});
		}
	}

	private static void OnClientReady(int player)
	{
		if (!Net.IsServer)
		{
			return;
		}
		try
		{
			PushTicks(player);
		}
		catch (Exception ex)
		{
			Log.Warn("machines: tick push to a new player: " + ex.Message);
		}
	}

	private static int WaterIndex(ItemCondition c)
	{
		if (c == null || c.identifier == null)
		{
			return -1;
		}
		List<ItemCondition> conditionWaterPurity = ItemConditionList.conditionWaterPurity;
		for (int i = 0; i < conditionWaterPurity.Count; i++)
		{
			if (conditionWaterPurity[i] != null && conditionWaterPurity[i].identifier == c.identifier)
			{
				return i;
			}
		}
		return -1;
	}

	private static MachineWater WaterOf(GameItem it)
	{
		if (it == null || !it.IsTag("LIQUID_CONTAINER_TAG") || it.IsTag("WINE_CONTAINER_TAG"))
		{
			return null;
		}
		ItemFeature val = it.FindItemFeatureByCategory("CATEGORY_WATER_PURITY");
		if (val == null)
		{
			return null;
		}
		int mod = 0;
		try
		{
			if (featureValueMod != null)
			{
				mod = (int)featureValueMod.GetValue(val);
			}
		}
		catch
		{
		}
		return new MachineWater
		{
			v = val.customIntValue1,
			x = val.IsExposed(),
			m = val.isFeatureMatch,
			fc = WaterIndex(val.fakeCondition),
			rc = WaterIndex(val.realCondition),
			dis = val.isDisabled,
			mod = mod,
			uv = it.unitValue
		};
	}

	private static long WaterHash(MachineWater w)
	{
		if (w == null)
		{
			return 0L;
		}
		ulong h = 14695981039346656037uL;
		N(w.v);
		N(w.x ? 1 : 0);
		N(w.m ? 1 : 0);
		N(w.fc);
		N(w.rc);
		N(w.dis ? 1 : 0);
		N(w.mod);
		N(w.uv);
		return (long)(h * 31);
		void N(long v)
		{
			h ^= (ulong)v;
			h *= 1099511628211uL;
		}
	}

	private static void ApplyWater(GameItem it, MachineWater w)
	{
		try
		{
			if (!it.IsTag("LIQUID_CONTAINER_TAG") || it.IsTag("WINE_CONTAINER_TAG"))
			{
				return;
			}
			ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(it);
			if (waterFeature != null)
			{
				List<ItemCondition> conditionWaterPurity = ItemConditionList.conditionWaterPurity;
				if (w.fc >= 0 && w.fc < conditionWaterPurity.Count)
				{
					waterFeature.fakeCondition = conditionWaterPurity[w.fc];
				}
				if (w.rc >= 0 && w.rc < conditionWaterPurity.Count)
				{
					waterFeature.realCondition = conditionWaterPurity[w.rc];
				}
				waterFeature.customIntValue1 = w.v;
				waterFeature.isFeatureMatch = w.m;
				waterFeature.isDisabled = w.dis;
				waterFeature.SetValueModifier(w.mod);
				if (waterFeature.IsExposed() != w.x)
				{
					waterFeature.SetExposed(w.x);
				}
				it.unitValue = w.uv;
				WaterLabelsApplied++;
			}
		}
		catch (Exception ex)
		{
			WarnOnce("water:" + ex.GetType().Name, "machines: water label from the tick threw: " + ex.Message);
		}
	}

	private static MachineTag ToDto(string k, TagState s)
	{
		return new MachineTag
		{
			k = k,
			e = s.valueEnabled,
			s = (s.valueString ?? ""),
			i = s.valueInt,
			f = s.valueFloat,
			l = s.valueLong,
			d = s.valueDouble,
			b = s.valueBool
		};
	}

	private static List<MachineTag> Dump(GameItem it)
	{
		Dictionary<string, TagState> dict = it.state.dict;
		List<MachineTag> list = new List<MachineTag>(dict.Count);
		foreach (string item in dict.Keys.OrderBy<string, string>((string x) => x, StringComparer.Ordinal))
		{
			list.Add(ToDto(item, dict[item]));
		}
		return list;
	}

	private static void WriteTags(GameItem it, List<MachineTag> tags)
	{
		if (tags == null)
		{
			return;
		}
		captureOff++;
		try
		{
			foreach (MachineTag tag2 in tags)
			{
				if (!string.IsNullOrEmpty(tag2?.k))
				{
					TagState tag = it.state.GetTag(tag2.k);
					tag.valueEnabled = tag2.e;
					tag.valueString = tag2.s ?? "";
					tag.valueInt = tag2.i;
					tag.valueFloat = tag2.f;
					tag.valueLong = tag2.l;
					tag.valueDouble = tag2.d;
					tag.valueBool = tag2.b;
				}
			}
		}
		finally
		{
			captureOff--;
		}
	}

	private static bool IsDefault(TagState s)
	{
		if (!s.valueEnabled && s.valueInt == 0 && s.valueLong == 0L && !s.valueBool && s.valueFloat == 0f && s.valueDouble == 0.0)
		{
			return string.IsNullOrEmpty(s.valueString);
		}
		return false;
	}

	private static bool Volatile(string k)
	{
		if (!(k == "CONTAINER_TEMP_TAG"))
		{
			return k == "HEATING_TAG";
		}
		return true;
	}

	public static List<GameItem> Subtree(GameItem m)
	{
		List<GameItem> list = new List<GameItem>();
		if (m == null)
		{
			return list;
		}
		list.Add(m);
		List<GameItem> source;
		try
		{
			source = GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)m, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null);
		}
		catch
		{
			source = new List<GameItem>();
		}
		foreach (GameItem item in (from k in source.Where((GameItem k) => k != null && k != m).Distinct()
			orderby k.uniqueId
			select k).ThenBy<GameItem, string>((GameItem k) => k.identifier, StringComparer.Ordinal))
		{
			list.Add(item);
		}
		return list;
	}

	public static string DigestText(GameItem m)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (GameItem item in Subtree(m))
		{
			stringBuilder.Append('#').Append(item.uniqueId.ToString(CultureInfo.InvariantCulture)).Append(' ')
				.Append(item.identifier)
				.Append(" x")
				.Append(item.unitCount.ToString(CultureInfo.InvariantCulture))
				.Append('\n');
			TagSystem state = item.state;
			Dictionary<string, TagState> dictionary = ((state != null) ? state.dict : null);
			if (dictionary == null)
			{
				continue;
			}
			foreach (string item2 in dictionary.Keys.OrderBy<string, string>((string x) => x, StringComparer.Ordinal))
			{
				if (!Volatile(item2))
				{
					TagState val = dictionary[item2];
					if (val != null && !IsDefault(val))
					{
						stringBuilder.Append(item.uniqueId.ToString(CultureInfo.InvariantCulture)).Append('.').Append(item2)
							.Append('=')
							.Append(val.valueEnabled ? '1' : '0')
							.Append('|')
							.Append(val.valueInt.ToString(CultureInfo.InvariantCulture))
							.Append('|')
							.Append(val.valueLong.ToString(CultureInfo.InvariantCulture))
							.Append('|')
							.Append(val.valueBool ? '1' : '0')
							.Append('|')
							.Append(Math.Round(val.valueFloat, 3).ToString("R", CultureInfo.InvariantCulture))
							.Append('|')
							.Append(Math.Round(val.valueDouble, 3).ToString("R", CultureInfo.InvariantCulture))
							.Append('|')
							.Append(val.valueString ?? "")
							.Append('\n');
					}
				}
			}
		}
		return stringBuilder.ToString();
	}

	public static long Digest(GameItem m)
	{
		if (m == null)
		{
			return 0L;
		}
		ulong num = 14695981039346656037uL;
		string text = DigestText(m);
		foreach (char c in text)
		{
			num ^= c;
			num *= 1099511628211L;
		}
		if (num != 0L)
		{
			return (long)num;
		}
		return 1L;
	}

	private static long TagsHash(GameItem it)
	{
		ulong h = 14695981039346656037uL;
		Dictionary<string, TagState> dict = it.state.dict;
		foreach (string item in dict.Keys.OrderBy<string, string>((string x) => x, StringComparer.Ordinal))
		{
			TagState obj = dict[item];
			S(item);
			S(obj.valueEnabled ? "1" : "0");
			S(obj.valueInt.ToString(CultureInfo.InvariantCulture));
			S(obj.valueBool ? "1" : "0");
			S(obj.valueLong.ToString(CultureInfo.InvariantCulture));
			S(obj.valueFloat.ToString("R", CultureInfo.InvariantCulture));
			S(obj.valueDouble.ToString("R", CultureInfo.InvariantCulture));
			S(obj.valueString);
		}
		return (long)h;
		void S(string s)
		{
			if (s != null)
			{
				foreach (char c in s)
				{
					h ^= c;
					h *= 1099511628211uL;
				}
			}
			h ^= 255uL;
			h *= 1099511628211uL;
		}
	}

	public static string LastRunOn(long machineId)
	{
		if (!lastRunOn.TryGetValue(machineId, out var value))
		{
			return "";
		}
		return value;
	}

	private static int SoundsStopped()
	{
		return ItemsUse.QuietSoundsBlocked + ItemsUse.QuietSoundsStopped;
	}

	private static IDisposable QuietFor(GameItem m, bool quiet)
	{
		return new RunScope(quiet && (m == null || (long)m.uniqueId != -103));
	}

	private static string PartHash()
	{
		if (!Items.Ready)
		{
			return "";
		}
		ulong h = 14695981039346656037uL;
		int num = 0;
		List<GameItem> list = new List<GameItem>();
		foreach (GameItem item in Items.All())
		{
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
			if (dictionary != null && dictionary.Count != 0 && item.uniqueId != 0)
			{
				list.Add(item);
			}
		}
		list.Sort((GameItem a, GameItem b) => a.uniqueId.CompareTo(b.uniqueId));
		foreach (GameItem item2 in list)
		{
			Dictionary<string, TagState> dict = item2.state.dict;
			bool flag = false;
			foreach (string item3 in dict.Keys.Where(PartKey).OrderBy<string, string>((string x) => x, StringComparer.Ordinal))
			{
				TagState val = dict[item3];
				if (val != null && !IsDefault(val))
				{
					if (!flag)
					{
						S(item2.uniqueId.ToString(CultureInfo.InvariantCulture));
						flag = true;
						num++;
					}
					S(item3);
					S(val.valueEnabled ? "1" : "0");
					S(val.valueInt.ToString(CultureInfo.InvariantCulture));
					S(val.valueBool ? "1" : "0");
					S(val.valueString);
				}
			}
		}
		return num.ToString(CultureInfo.InvariantCulture) + ":" + h.ToString("x16");
		void S(string x)
		{
			if (x != null)
			{
				foreach (char c in x)
				{
					h ^= c;
					h *= 1099511628211uL;
				}
			}
			h ^= 255uL;
			h *= 1099511628211uL;
		}
	}

	private static bool PartKey(string k)
	{
		if (k != null)
		{
			if (!IsSettingKey(k))
			{
				switch (k)
				{
				default:
					return k == "CURRENT_PLAYING_TRACK_ID_TAG";
				case "HEATING_PLATE_STATE":
				case "CASETTE_PLAYER_STATE_INT":
				case "CURRENT_PLAYING_CASSETTE_INDEX_TAG":
					break;
				}
			}
			return true;
		}
		return false;
	}

	public static GameItem Find(long id)
	{
		if (id != 0L)
		{
			return Items.Find(id);
		}
		return null;
	}

	private static long SafeDigest()
	{
		try
		{
			return Replay.Digest();
		}
		catch (Exception ex)
		{
			Log.Warn("machines: Replay.Digest threw: " + ex.Message);
			return 0L;
		}
	}

	internal static string Describe(GameItem m)
	{
		if (m != null)
		{
			return (m.identifier ?? "?") + "#" + m.uniqueId.ToString(CultureInfo.InvariantCulture);
		}
		return "nothing";
	}

	private static string Describe(MachineDone d)
	{
		return d.op switch
		{
			"button" => "button " + d.w, 
			"slider" => "slider " + d.w + "=" + d.v.ToString(CultureInfo.InvariantCulture), 
			"range" => "range " + d.w + "=" + d.v.ToString(CultureInfo.InvariantCulture) + ".." + d.v2.ToString(CultureInfo.InvariantCulture), 
			"radio" => "radio " + d.w + (d.on ? " on" : " off"), 
			"tags" => "settings " + string.Join(",", (d.tags ?? new List<MachineTag>()).Select((MachineTag t) => t.k)), 
			_ => d.op, 
		};
	}

	private static void Notify(string text)
	{
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

	private static void WarnOnce(string key, string text)
	{
		if (warned.Add(key))
		{
			Log.Warn(text);
		}
	}
}
