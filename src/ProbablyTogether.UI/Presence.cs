using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ProbablyTogether.Systems;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProbablyTogether.UI;

public static class Presence
{
	public sealed class Remote
	{
		public int Id;

		public float X = -1f;

		public float Y = -1f;

		public string View = "";

		public long Hover;

		public long Drag;

		public int Units;

		public int Group;

		public int Orient;

		public bool Flip;

		public ItemRef Target;

		public int TX;

		public int TY;

		public bool TOk;

		public float CX;

		public float CY;

		public float LX;

		public float LY;

		public bool Afk;

		public float LastCursorAt = -1f;

		public bool Visible;

		public Vector2 Anchor;

		public string LabelText = "";

		internal RectTransform Widget;

		internal Image Arrow;

		internal TextMeshProUGUI Name;

		internal TextMeshProUGUI Detail;

		internal Vector2 Drawn;

		internal bool HasDrawn;
	}

	private sealed class Describer
	{
		public string Prefix;

		public Func<string, string> Activity;

		public Func<string, Vector2?> Anchor;
	}

	private sealed class ServerPeer
	{
		public MsgPresenceState State;

		public long WindowStartMs;

		public int InWindow;
	}

	public static Vector2? FakePointer;

	public static string FakeView;

	public static long? FakeHover;

	public static long? FakeDrag;

	private static readonly List<Describer> describers = new List<Describer>();

	private static readonly Dictionary<int, ServerPeer> serverPeers = new Dictionary<int, ServerPeer>();

	private static readonly Dictionary<int, Remote> remotes = new Dictionary<int, Remote>();

	private static readonly Dictionary<string, int> heldLocks = new Dictionary<string, int>();

	private static MsgCursor lastSent;

	private static float lastSentAt = -10f;

	private static float lastStateAt = -10f;

	private static float lastInputAt;

	private static string lastStateView;

	private static bool lastStateAfk;

	private static Vector3 lastMouse;

	private const float SendInterval = 0.08f;

	private const float StateInterval = 5f;

	private const float AfkAfter = 180f;

	internal const string AimActivity = "is aiming the shotgun";

	internal const string AimLabelKey = "aim";

	private static RectTransform rosterPlate;

	private static TextMeshProUGUI rosterHeader;

	private static readonly Dictionary<int, TextMeshProUGUI> rosterRows = new Dictionary<int, TextMeshProUGUI>();

	private static readonly Dictionary<int, string> rosterTexts = new Dictionary<int, string>();

	private static readonly Dictionary<string, RectTransform> lockLabels = new Dictionary<string, RectTransform>();

	private static readonly Dictionary<string, string> lockLabelTexts = new Dictionary<string, string>();

	private static bool rosterDirty = true;

	private static float rosterAt;

	private static readonly Vector2 ShopCounter = new Vector2(0.5f, 0.47f);

	private static readonly Vector2 JacksonCounter = new Vector2(0.483f, 0.505f);

	private static StorePhoneObject phone;

	public static string LocalView => FakeView ?? ComputeView();

	public static IReadOnlyDictionary<int, Remote> Remotes => remotes;

	public static IReadOnlyDictionary<string, string> LockLabelTexts => lockLabelTexts;

	public static IReadOnlyDictionary<int, string> RosterTexts => rosterTexts;

	public static string RosterHeader
	{
		get
		{
			if (!((Object)(object)rosterHeader != (Object)null))
			{
				return "";
			}
			return ((TMP_Text)rosterHeader).text;
		}
	}

	public static int Aiming => SafeAiming();

	public static void DescribeLock(string prefix, Func<string, string> activity, Func<string, Vector2?> anchor = null)
	{
		if (!string.IsNullOrEmpty(prefix) && activity != null)
		{
			describers.RemoveAll((Describer d) => d.Prefix == prefix);
			describers.Insert(0, new Describer
			{
				Prefix = prefix,
				Activity = activity,
				Anchor = anchor
			});
		}
	}

	public static string ActivityFor(string lockName)
	{
		Describer describer = (string.IsNullOrEmpty(lockName) ? null : Find(lockName));
		if (describer != null)
		{
			return SafeActivity(describer, lockName);
		}
		return null;
	}

	public static string WhereIs(int player)
	{
		return Where(player);
	}

	internal static void Register()
	{
		Net.On<MsgCursor>("presence.cursor", ServerOnCursor, ClientOnCursor);
		Net.On<MsgPresenceState>("presence.state", ServerOnState, ClientOnState);
		DescribeLock("counter", (string _) => "is at the counter", (string _) => CounterAnchor());
		DescribeLock("phone", (string _) => "is on the phone", (string _) => PhoneAnchor());
		DescribeLock("outing", (string _) => "is choosing where to go");
		DescribeLock("outing.", (string _) => "is busy out there");
		DescribeLock("outing.jackson", (string _) => "is haggling with Dr. Jackson", (string _) => (!(DayNightOuting.LocationId == "dr_jackson")) ? ((Vector2?)null) : new Vector2?(JacksonCounter));
		DescribeLock("machine:", MachineActivity);
	}

	private static ServerPeer Peer(int p)
	{
		if (!serverPeers.TryGetValue(p, out var value))
		{
			value = (serverPeers[p] = new ServerPeer());
		}
		return value;
	}

	private static void RelayToOthers<T>(int from, string kind, T msg) where T : class
	{
		foreach (int item in new List<int>(Net.Players.Keys))
		{
			if (item != from && Net.IsPlayerLive(item))
			{
				Net.ToPlayer(item, kind, msg);
			}
		}
	}

	private static void ServerOnCursor(MsgCursor m, int from)
	{
		ServerPeer serverPeer = Peer(from);
		long ms = Clock.Ms;
		if (ms - serverPeer.WindowStartMs >= 1000)
		{
			serverPeer.WindowStartMs = ms;
			serverPeer.InWindow = 0;
		}
		if (++serverPeer.InWindow <= 20 && !float.IsNaN(m.x) && !float.IsNaN(m.y) && !float.IsInfinity(m.x) && !float.IsInfinity(m.y))
		{
			MsgCursor msgCursor = new MsgCursor
			{
				p = from,
				x = Mathf.Clamp(m.x, -0.5f, 1.5f),
				y = Mathf.Clamp(m.y, -0.5f, 1.5f),
				v = CleanView(m.v),
				h = Math.Max(0L, m.h)
			};
			Carry.ServerCursor(from, m, msgCursor);
			RelayToOthers(from, "presence.cursor", msgCursor);
		}
	}

	private static void ServerOnState(MsgPresenceState m, int from)
	{
		MsgPresenceState msgPresenceState = new MsgPresenceState
		{
			p = from,
			v = CleanView(m.v),
			afk = m.afk
		};
		Peer(from).State = msgPresenceState;
		RelayToOthers(from, "presence.state", msgPresenceState);
	}

	internal static void ServerOnClientReady(int player)
	{
		if (!Net.IsServer)
		{
			return;
		}
		foreach (KeyValuePair<int, ServerPeer> serverPeer in serverPeers)
		{
			if (serverPeer.Key != player && serverPeer.Value.State != null && Net.Players.ContainsKey(serverPeer.Key))
			{
				Net.ToPlayer(player, "presence.state", serverPeer.Value.State);
			}
		}
	}

	internal static void ServerForget(int player)
	{
		serverPeers.Remove(player);
	}

	private static string CleanView(string v)
	{
		if (string.IsNullOrEmpty(v))
		{
			return "";
		}
		if (v.Length > 48)
		{
			v = v.Substring(0, 48);
		}
		string text = v;
		foreach (char c in text)
		{
			if (char.IsControl(c) || c == '<' || c == '>')
			{
				return "";
			}
		}
		return v;
	}

	internal static string ComputeView()
	{
		if (Boot.Scene == "EmporiumMenu")
		{
			return "menu";
		}
		if (!Game.ShopLoaded || Snapshot.IsLoading)
		{
			return "loading";
		}
		try
		{
			MapUIManager instance = MapUIManager.Instance;
			if ((Object)(object)instance != (Object)null && instance.Current != null)
			{
				return "outside:" + instance.Current.Id;
			}
			StoreUIManager instance2 = StoreUIManager.Instance;
			if ((Object)(object)instance2 != (Object)null && instance2.isShootingMode)
			{
				return "shooting";
			}
			if ((Object)(object)instance2 != (Object)null && instance2.isMagnifyingMode)
			{
				return "magnify";
			}
		}
		catch
		{
		}
		return "store";
	}

	private static void SendTick()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		Vector3 mousePosition = Input.mousePosition;
		if (FakePointer.HasValue || mousePosition != lastMouse || Input.anyKey)
		{
			lastInputAt = realtimeSinceStartup;
		}
		lastMouse = mousePosition;
		bool flag = realtimeSinceStartup - lastInputAt > 180f;
		string localView = LocalView;
		if (localView != lastStateView || flag != lastStateAfk || realtimeSinceStartup - lastStateAt > 5f)
		{
			lastStateView = localView;
			lastStateAfk = flag;
			lastStateAt = realtimeSinceStartup;
			Net.ToServer("presence.state", new MsgPresenceState
			{
				v = localView,
				afk = flag
			});
		}
		if (!(realtimeSinceStartup - lastSentAt < 0.08f))
		{
			Vector2 val = (Vector2)((FakePointer) ?? Kit.ToGameArea(new Vector2(mousePosition.x, mousePosition.y)));
			MsgCursor msgCursor = new MsgCursor
			{
				x = (float)Math.Round(val.x, 4),
				y = (float)Math.Round(val.y, 4),
				v = localView,
				h = (FakeHover ?? ((localView == "store") ? HoveredId(mousePosition) : 0))
			};
			if (FakeDrag.HasValue)
			{
				msgCursor.d = FakeDrag.Value;
			}
			else
			{
				Carry.FillLocal(msgCursor);
			}
			if (lastSent == null || !(Mathf.Abs(msgCursor.x - lastSent.x) < 0.0015f) || !(Mathf.Abs(msgCursor.y - lastSent.y) < 0.0015f) || !(msgCursor.v == lastSent.v) || msgCursor.h != lastSent.h || msgCursor.d != lastSent.d || msgCursor.o != lastSent.o || msgCursor.f != lastSent.f || msgCursor.cx != lastSent.cx || msgCursor.cy != lastSent.cy || msgCursor.lx != lastSent.lx || msgCursor.ly != lastSent.ly || msgCursor.tx != lastSent.tx || msgCursor.ty != lastSent.ty || msgCursor.ok != lastSent.ok || (msgCursor.t == null) != (lastSent.t == null) || (msgCursor.t != null && lastSent.t != null && msgCursor.t.ToString() != lastSent.t.ToString()) || msgCursor.n != lastSent.n || msgCursor.g != lastSent.g || msgCursor.c != lastSent.c || !(realtimeSinceStartup - lastSentAt < 2f))
			{
				lastSent = msgCursor;
				lastSentAt = realtimeSinceStartup;
				Net.ToServer("presence.cursor", msgCursor);
			}
		}
	}

	private static long HoveredId(Vector3 mouse)
	{
		try
		{
			if ((Object)(object)EventSystem.current == (Object)null)
			{
				return 0L;
			}
			GameItem val = RenderHandler.RaycastElement<GameItem>(new Vector2(mouse.x, mouse.y), (TreeNodeRender)null);
			return (val != null) ? Math.Max(0L, Ids.Of(val)) : 0;
		}
		catch
		{
			return 0L;
		}
	}

	private static Remote Get(int p)
	{
		if (!remotes.TryGetValue(p, out var value))
		{
			Dictionary<int, Remote> dictionary = remotes;
			Remote obj = new Remote
			{
				Id = p
			};
			value = obj;
			dictionary[p] = obj;
		}
		return value;
	}

	private static void ClientOnCursor(MsgCursor m)
	{
		if (m.p != Net.LocalPlayer && Net.Players.ContainsKey(m.p))
		{
			Remote remote = Get(m.p);
			remote.X = m.x;
			remote.Y = m.y;
			remote.View = m.v ?? "";
			remote.Hover = m.h;
			remote.Drag = m.d;
			remote.Units = m.n;
			remote.Group = m.g;
			remote.Orient = m.o;
			remote.Flip = m.f;
			remote.Target = m.t;
			remote.TX = m.tx;
			remote.TY = m.ty;
			remote.TOk = m.ok;
			remote.CX = m.cx;
			remote.CY = m.cy;
			remote.LX = m.lx;
			remote.LY = m.ly;
			remote.LastCursorAt = Time.realtimeSinceStartup;
			rosterDirty = true;
		}
	}

	private static void ClientOnState(MsgPresenceState m)
	{
		if (m.p != Net.LocalPlayer && Net.Players.ContainsKey(m.p))
		{
			Remote remote = Get(m.p);
			remote.View = m.v ?? "";
			remote.Afk = m.afk;
			rosterDirty = true;
		}
	}

	internal static void ClientForget(int p)
	{
		if (remotes.TryGetValue(p, out var value) && (Object)(object)value.Widget != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)value.Widget).gameObject);
		}
		remotes.Remove(p);
		rosterDirty = true;
	}

	internal static void ClientReset()
	{
		foreach (Remote value in remotes.Values)
		{
			if ((Object)(object)value.Widget != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)value.Widget).gameObject);
			}
		}
		remotes.Clear();
		heldLocks.Clear();
		lastSent = null;
		lastStateView = null;
		rosterDirty = true;
	}

	internal static void OnShopLoaded()
	{
		lastStateView = null;
		lastSent = null;
		rosterDirty = true;
	}

	internal static void OnLockChanged(string name, int holder)
	{
		if (holder < 0)
		{
			heldLocks.Remove(name);
		}
		else
		{
			heldLocks[name] = holder;
		}
		rosterDirty = true;
	}

	private static int SafeAiming()
	{
		try
		{
			return Security.AimingPlayer;
		}
		catch
		{
			return -1;
		}
	}

	internal static void OnAimChanged(int player, bool on)
	{
		rosterDirty = true;
		Log.Info("ui: " + Net.PlayerName(player) + (on ? " is aiming the shotgun" : " lowered the shotgun"));
	}

	internal static void OnOverlayBuilt()
	{
		rosterRows.Clear();
		lockLabels.Clear();
		foreach (Remote value in remotes.Values)
		{
			value.Widget = null;
			value.HasDrawn = false;
		}
		rosterPlate = Kit.Plate((Transform)(object)Overlay.Corner, "roster", out var content, 0.88f, 9, 1f);
		Kit.Pin(rosterPlate, new Vector2(1f, 1f), Vector2.zero);
		rosterHeader = Kit.Label((Transform)(object)content, "header", 15f, Kit.Dim, 0f, (TextAlignmentOptions)4097);
		rosterDirty = true;
	}

	internal static void Tick()
	{
		if (!Ui.ShopOnline)
		{
			return;
		}
		try
		{
			SendTick();
		}
		catch (Exception e)
		{
			UiErrors.Report("presence send", e);
		}
		if ((Object)(object)Overlay.Root == (Object)null)
		{
			return;
		}
		string localView = LocalView;
		foreach (Remote value in remotes.Values)
		{
			DrawCursor(value, localView);
			Carry.Draw(value);
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (rosterDirty || realtimeSinceStartup - rosterAt > 1f)
		{
			rosterDirty = false;
			rosterAt = realtimeSinceStartup;
			DrawRoster();
		}
		DrawLockLabels();
	}

	private static void DrawCursor(Remote r, string myView)
	{
		PlayerInfo value;
		bool flag = Net.Players.TryGetValue(r.Id, out value) && r.LastCursorAt > 0f && r.View == myView && r.X >= 0f && r.X <= 1f && r.Y >= 0f && r.Y <= 1f;
		if (flag && (Object)(object)r.Widget == (Object)null)
		{
			BuildCursor(r, value);
		}
		if ((Object)(object)r.Widget == (Object)null)
		{
			r.Visible = false;
			return;
		}
		if (((Component)r.Widget).gameObject.activeSelf != flag)
		{
			((Component)r.Widget).gameObject.SetActive(flag);
		}
		r.Visible = flag;
		if (!flag)
		{
			r.HasDrawn = false;
			return;
		}
		Vector2 val = default(Vector2);
		val = new Vector2(r.X, r.Y);
		Vector2 val2;
		Vector2 drawn;
		if (r.HasDrawn)
		{
			val2 = val - r.Drawn;
			if (!(val2.sqrMagnitude > 0.04f))
			{
				drawn = Vector2.Lerp(r.Drawn, val, Mathf.Clamp01(Time.unscaledDeltaTime * 18f));
				goto IL_0125;
			}
		}
		drawn = val;
		goto IL_0125;
		IL_0125:
		r.Drawn = drawn;
		val2 = val - r.Drawn;
		if (val2.sqrMagnitude < 1E-07f)
		{
			r.Drawn = val;
		}
		r.HasDrawn = true;
		r.Anchor = val;
		val2 = (r.Widget.anchorMin = (r.Widget.anchorMax = r.Drawn));
		r.Widget.anchoredPosition = Vector2.zero;
		string text = Kit.Name(value.Name);
		Kit.SetText(r.Name, text);
		string text2 = "";
		if (r.Afk)
		{
			text2 = ((text2.Length == 0) ? "away" : (text2 + " (away)"));
		}
		string text3 = Lang.Tr(text2);
		if (((TMP_Text)r.Detail).text != text3)
		{
			Kit.SetText(r.Detail, text3);
			((Component)r.Detail).gameObject.SetActive(text2.Length > 0);
		}
		r.LabelText = text + ((text2.Length > 0) ? (": " + text2) : "");
		Color color = Kit.PlayerColor(r.Id);
		color.a = (r.Afk ? 0.5f : 1f);
		((Graphic)r.Arrow).color = color;
	}

	private static void BuildCursor(Remote r, PlayerInfo info)
	{
		RectTransform val = Kit.Rect("cursor_" + r.Id, (Transform)(object)Overlay.Cursors);
		val.sizeDelta = Vector2.zero;
		val.pivot = new Vector2(0f, 1f);
		Image val2 = Kit.Box((Transform)(object)val, "arrow", Kit.PlayerColor(r.Id));
		val2.sprite = Kit.Arrow();
		RectTransform rectTransform = ((Graphic)val2).rectTransform;
		Vector2 val3 = default(Vector2);
		val3 = new Vector2(0f, 0f);
		rectTransform.anchorMax = val3;
		rectTransform.anchorMin = val3;
		rectTransform.pivot = new Vector2(0f, 1f);
		rectTransform.sizeDelta = new Vector2(20f, 30f);
		rectTransform.anchoredPosition = Vector2.zero;
		RectTransform content;
		RectTransform obj = Kit.Plate((Transform)(object)val, "tag", out content, 0.85f, 5, 0f);
		val3 = new Vector2(0f, 0f);
		obj.anchorMax = val3;
		obj.anchorMin = val3;
		obj.pivot = new Vector2(0f, 1f);
		obj.anchoredPosition = new Vector2(16f, -22f);
		TextMeshProUGUI val4 = Kit.Label((Transform)(object)content, "name", 15f, Kit.PlayerColor(r.Id), 0f, (TextAlignmentOptions)4097);
		Kit.SetText(val4, Kit.Name(info?.Name));
		TextMeshProUGUI val5 = Kit.Label((Transform)(object)content, "detail", 13f, Kit.Text, 260f, (TextAlignmentOptions)4097);
		((Component)val5).gameObject.SetActive(false);
		r.Widget = val;
		r.Arrow = val2;
		r.Name = val4;
		r.Detail = val5;
	}

	private static string ItemName(long id)
	{
		try
		{
			GameItem val = Ids.Find(id);
			if (val == null)
			{
				return "an item";
			}
			string displayName = val.GetDisplayName(false);
			return string.IsNullOrEmpty(displayName) ? (val.identifier ?? "an item") : Kit.Name(StripTags(displayName));
		}
		catch
		{
			return "an item";
		}
	}

	private static string StripTags(string s)
	{
		if (s.IndexOf('<') < 0)
		{
			return s;
		}
		StringBuilder stringBuilder = new StringBuilder(s.Length);
		bool flag = false;
		foreach (char c in s)
		{
			switch (c)
			{
			case '<':
				flag = true;
				continue;
			case '>':
				flag = false;
				continue;
			}
			if (!flag)
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private static void DrawRoster()
	{
		if ((Object)(object)rosterPlate == (Object)null)
		{
			return;
		}
		List<int> list = new List<int>(Net.Players.Keys);
		list.Sort();
		int maxPlayers = Net.MaxPlayers;
		Kit.SetText(rosterHeader, Kit.Name(Net.ServerName) + "  " + list.Count.ToString(CultureInfo.InvariantCulture) + ((maxPlayers > 0) ? ("/" + maxPlayers.ToString(CultureInfo.InvariantCulture)) : ""));
		foreach (int item in new List<int>(rosterRows.Keys))
		{
			if (!Net.Players.ContainsKey(item))
			{
				if ((Object)(object)rosterRows[item] != (Object)null)
				{
					Object.Destroy((Object)(object)((Component)rosterRows[item]).gameObject);
				}
				rosterRows.Remove(item);
				rosterTexts.Remove(item);
			}
		}
		Transform parent = ((TMP_Text)rosterHeader).transform.parent;
		int num = ((TMP_Text)rosterHeader).transform.GetSiblingIndex() + 1;
		foreach (int item2 in list)
		{
			if (!rosterRows.TryGetValue(item2, out var value) || (Object)(object)value == (Object)null)
			{
				value = (rosterRows[item2] = Kit.Label(parent, "player_" + item2, 16f, Kit.Text, 340f, (TextAlignmentOptions)4097));
			}
			((TMP_Text)value).transform.SetSiblingIndex(num++);
			PlayerInfo playerInfo = Net.Players[item2];
			string text = Where(item2);
			string text2 = (ReadyBar.IsReady(item2) ? ("  <color=#" + Kit.Hex(Kit.Good) + ">ready</color>") : "");
			string word;
			string text3 = VoiceMark(item2, out word);
			string text4 = "<color=#" + Kit.Hex(Kit.PlayerColor(item2)) + ">■</color> " + Kit.Name(playerInfo.Name) + ((item2 == Net.LocalPlayer) ? (" " + Lang.Tr("(you)")) : "") + text3 + "  <color=#" + Kit.Hex(Kit.Dim) + ">" + Lang.Tr(text) + "</color>" + ((text2.Length > 0) ? ("  <color=#" + Kit.Hex(Kit.Good) + ">" + Lang.Tr("ready") + "</color>") : "");
			Kit.SetText(value, text4);
			rosterTexts[item2] = Kit.Name(playerInfo.Name) + ((item2 == Net.LocalPlayer) ? " (you)" : "") + " | " + text + ((text2.Length > 0) ? " | ready" : "") + ((word.Length > 0) ? (" | " + word) : "");
		}
	}

	private static string VoiceMark(int id, out string word)
	{
		word = "";
		try
		{
			return Voice.RosterMark(id, out word) ?? "";
		}
		catch
		{
			word = "";
			return "";
		}
	}

	internal static void RosterChanged()
	{
		rosterDirty = true;
	}

	private static string Where(int id)
	{
		if (id > 0 && id == Aiming)
		{
			return Trim("is aiming the shotgun");
		}
		foreach (KeyValuePair<string, int> heldLock in heldLocks)
		{
			if (heldLock.Value != id)
			{
				continue;
			}
			Describer describer = Find(heldLock.Key);
			if (describer != null)
			{
				string text = SafeActivity(describer, heldLock.Key);
				if (!string.IsNullOrEmpty(text))
				{
					return Trim(text);
				}
			}
		}
		remotes.TryGetValue(id, out var value);
		string text2 = ((id == Net.LocalPlayer) ? LocalView : ((value != null) ? value.View : ""));
		bool flag = id != Net.LocalPlayer && value != null && value.Afk;
		string text3;
		switch (text2)
		{
		default:
			if (text2.Length != 0)
			{
				goto case null;
			}
			text3 = "in the shop";
			break;
		case "store":
			text3 = "in the shop";
			break;
		case "magnify":
			text3 = "taking a close look";
			break;
		case "shooting":
			text3 = "aiming the shotgun";
			break;
		case "loading":
			text3 = "loading";
			break;
		case "menu":
			text3 = "in the menu";
			break;
		case null:
			text3 = (text2.StartsWith("outside:", StringComparison.Ordinal) ? ("out at " + PlaceTitle(text2.Substring(8))) : text2);
			break;
		}
		if (!flag)
		{
			return text3;
		}
		return text3 + ", away";
	}

	private static string Trim(string activity)
	{
		if (!activity.StartsWith("is ", StringComparison.Ordinal))
		{
			return activity;
		}
		return activity.Substring(3);
	}

	private static string PlaceTitle(string id)
	{
		try
		{
			AfterhourLocation obj = AfterhourLocation.Get(id);
			string text = ((obj != null) ? obj.GetTitle() : null);
			if (!string.IsNullOrEmpty(text))
			{
				return Kit.Name(StripTags(text));
			}
		}
		catch
		{
		}
		return Kit.Name(id);
	}

	private static Describer Find(string lockName)
	{
		Describer describer = null;
		foreach (Describer describer2 in describers)
		{
			if (lockName == describer2.Prefix)
			{
				return describer2;
			}
			if ((describer2.Prefix.EndsWith(":", StringComparison.Ordinal) || describer2.Prefix.EndsWith(".", StringComparison.Ordinal)) && lockName.StartsWith(describer2.Prefix, StringComparison.Ordinal) && (describer == null || describer2.Prefix.Length > describer.Prefix.Length))
			{
				describer = describer2;
			}
		}
		return describer;
	}

	private static string SafeActivity(Describer d, string lockName)
	{
		try
		{
			return d.Activity(lockName);
		}
		catch
		{
			return null;
		}
	}

	private static void DrawLockLabels()
	{
		HashSet<string> hashSet = new HashSet<string>();
		int aiming = Aiming;
		bool flag = false;
		foreach (KeyValuePair<string, int> heldLock in heldLocks)
		{
			if (heldLock.Value == Net.LocalPlayer || !Net.Players.TryGetValue(heldLock.Value, out var value))
			{
				continue;
			}
			Describer describer = Find(heldLock.Key);
			if (describer == null || describer.Anchor == null)
			{
				continue;
			}
			Vector2? val = null;
			try
			{
				val = describer.Anchor(heldLock.Key);
			}
			catch
			{
			}
			if (val.HasValue)
			{
				string activity = SafeActivity(describer, heldLock.Key) ?? "is busy";
				if (heldLock.Key == "counter" && heldLock.Value == aiming)
				{
					activity = "is aiming the shotgun";
					flag = true;
				}
				DrawLabel(heldLock.Key, heldLock.Value, value, activity, val.Value, hashSet);
			}
		}
		if (!flag && aiming > 0 && aiming != Net.LocalPlayer && Net.Players.TryGetValue(aiming, out var value2))
		{
			Vector2? val2 = null;
			try
			{
				val2 = CounterAnchor();
			}
			catch
			{
			}
			if (val2.HasValue)
			{
				DrawLabel("aim", aiming, value2, "is aiming the shotgun", val2.Value, hashSet);
			}
		}
		foreach (string item in new List<string>(lockLabels.Keys))
		{
			if (!hashSet.Contains(item))
			{
				if ((Object)(object)lockLabels[item] != (Object)null)
				{
					Object.Destroy((Object)(object)((Component)lockLabels[item]).gameObject);
				}
				lockLabels.Remove(item);
				lockLabelTexts.Remove(item);
			}
		}
	}

	private static void DrawLabel(string key, int player, PlayerInfo who, string activity, Vector2 a, HashSet<string> live)
	{
		if (!(a.x < 0f) && !(a.x > 1f) && !(a.y < 0f) && !(a.y > 1f))
		{
			string text = "<color=#" + Kit.Hex(Kit.PlayerColor(player)) + ">" + Kit.Name(who.Name) + "</color> " + Lang.Tr(activity);
			if (!lockLabels.TryGetValue(key, out var value) || (Object)(object)value == (Object)null)
			{
				value = Kit.Plate((Transform)(object)Overlay.Labels, "lock_" + key, out var content, 0.9f, 6, 0f);
				value.pivot = new Vector2(0.5f, 0f);
				Kit.Label((Transform)(object)content, "text", 16f, Kit.Text, 0f, (TextAlignmentOptions)4097);
				lockLabels[key] = value;
			}
			TextMeshProUGUI componentInChildren = ((Component)value).GetComponentInChildren<TextMeshProUGUI>(true);
			if ((Object)(object)componentInChildren != (Object)null)
			{
				Kit.SetText(componentInChildren, text);
			}
			RectTransform obj = value;
			Vector2 anchorMin = (value.anchorMax = a);
			obj.anchorMin = anchorMin;
			value.anchoredPosition = new Vector2(0f, 8f);
			if (!((Component)value).gameObject.activeSelf)
			{
				((Component)value).gameObject.SetActive(true);
			}
			lockLabelTexts[key] = Kit.Name(who.Name) + " " + activity;
			live.Add(key);
		}
	}

	private static Vector2? CounterAnchor()
	{
		StoreUIManager instance = StoreUIManager.Instance;
		if (!((Object)(object)instance != (Object)null) || !((Object)(object)instance.clientCounter != (Object)null) || !instance.clientCounter.activeInHierarchy)
		{
			return null;
		}
		return ShopCounter;
	}

	private static Vector2? PhoneAnchor()
	{
		if ((Object)(object)phone == (Object)null)
		{
			try
			{
				phone = Object.FindObjectOfType<StorePhoneObject>();
			}
			catch
			{
			}
		}
		if (!((Object)(object)phone != (Object)null))
		{
			return null;
		}
		return Kit.AreaOf(((Component)phone).gameObject);
	}

	private static string MachineActivity(string lockName)
	{
		if (long.TryParse(lockName.Substring("machine:".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result != 0L)
		{
			return "is using the " + ItemName(result);
		}
		return "is using a machine";
	}
}
