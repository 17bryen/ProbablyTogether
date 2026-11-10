using System;
using System.Collections.Generic;
using System.Globalization;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.EventSystems;

namespace ProbablyTogether.Systems;

public static class Pointing
{
	private sealed class Bucket
	{
		public double Tokens = 4.0;

		public long LastMs = Clock.Ms;

		public long LastNoMs = -100000L;
	}

	public const string KAt = "ping.at";

	public const string KShow = "ping.show";

	public const string KNo = "ping.no";

	public const string KeyBindOption = "ping";

	private const string KeySave = "humangenome_accomplice.pointKey";

	public const int Burst = 3;

	public const float ClientSecondsPerPing = 1.6f;

	public const int ServerBurst = 4;

	public const float ServerSecondsPerPing = 1.5f;

	public const string TooFast = "You are pointing too fast. Wait a moment.";

	public const string Gone = "That item is not in the shop any more.";

	public const string LocalTooFast = "Wait a moment before pointing again.";

	public static int ServerAccepted;

	public static int ServerRefused;

	public static int ServerRefusedRate;

	public static int ServerRefusedGone;

	public static int ServerSeq;

	public static int[] ServerLastTo = new int[0];

	public static int Sent;

	public static int LocalRefused;

	public static int Received;

	public static int IgnoredLoading;

	public static int RefusedByServer;

	public static string LastNo = "";

	public static MsgPingShow LastShow;

	private static readonly Dictionary<int, int> receivedFrom = new Dictionary<int, int>();

	private static bool inited;

	private static readonly Dictionary<int, Bucket> buckets = new Dictionary<int, Bucket>();

	private static double localTokens = 3.0;

	private static float localAt = -100f;

	private static float localNoteAt = -100f;

	private static bool keyLoaded;

	private static bool keyBindAdded;

	private static KeyCode pointKey = (KeyCode)103;

	private static string lastTakenKeyLog = "";

	public static int BucketCount => buckets.Count;

	public static double LocalTokens
	{
		get
		{
			Refill();
			return localTokens;
		}
	}

	public unsafe static KeyCode PointKey
	{
		get
		{
			LoadKey();
			return pointKey;
		}
		set
		{
			if (!KeyboardKey(value))
			{
				Log.Info("ping: point key " + value.ToString() + " refused (a keyboard key only: mouse buttons run the game's own clicks)");
				return;
			}
			pointKey = value;
			keyLoaded = true;
			try
			{
				ES3.Save<int>("humangenome_accomplice.pointKey", (int)value);
			}
			catch (Exception ex)
			{
				Log.Warn("ping: point key not saved: " + ex.Message);
			}
		}
	}

	private static bool CtrlHeld
	{
		get
		{
			if (!Input.GetKey((KeyCode)306))
			{
				return Input.GetKey((KeyCode)305);
			}
			return true;
		}
	}

	public static int ReceivedFrom(int player)
	{
		if (!receivedFrom.TryGetValue(player, out var value))
		{
			return 0;
		}
		return value;
	}

	[SystemInit]
	private static void Init()
	{
		if (inited)
		{
			return;
		}
		inited = true;
		Net.On<MsgPingAt>("ping.at", ServerOnAt, null);
		Net.On<MsgPingShow>("ping.show", null, ClientOnShow);
		Net.On<MsgPingNo>("ping.no", null, ClientOnNo);
		Net.PlayerLeft += delegate(PlayerInfo p)
		{
			if (Net.IsServer && p != null)
			{
				buckets.Remove(p.Id);
			}
		};
		Net.Disconnected += delegate
		{
			localTokens = 3.0;
			PointMarks.Clear();
		};
		Snapshot.Loaded += delegate
		{
			PointMarks.Clear();
		};
	}

	private static bool TakeServerToken(int player)
	{
		long ms = Clock.Ms;
		if (!buckets.TryGetValue(player, out var value))
		{
			Dictionary<int, Bucket> dictionary = buckets;
			Bucket obj = new Bucket
			{
				LastMs = ms
			};
			value = obj;
			dictionary[player] = obj;
		}
		value.Tokens = Math.Min(4.0, value.Tokens + (double)(ms - value.LastMs) / 1500.0);
		value.LastMs = ms;
		if (value.Tokens < 1.0)
		{
			return false;
		}
		value.Tokens -= 1.0;
		return true;
	}

	private static void ServerOnAt(MsgPingAt m, int from)
	{
		if (!Net.IsServer || m == null || from <= 0 || !Net.IsPlayerLive(from))
		{
			return;
		}
		if (!TakeServerToken(from))
		{
			ServerRefusedRate++;
			Refuse(from, "You are pointing too fast. Wait a moment.", "more than one ping per " + 1.5f.ToString("0.0", CultureInfo.InvariantCulture) + " s after a burst of " + 4);
			return;
		}
		if (float.IsNaN(m.x) || float.IsNaN(m.y) || float.IsInfinity(m.x) || float.IsInfinity(m.y))
		{
			Refuse(from, null, "a spot that is not a number");
			return;
		}
		string s = null;
		if (m.id != 0L)
		{
			GameItem val = Ids.Find(m.id);
			if (val == null || Ids.IsStandIn(val))
			{
				ServerRefusedGone++;
				Refuse(from, "That item is not in the shop any more.", "item " + m.id.ToString(CultureInfo.InvariantCulture) + " is not in the shop");
				return;
			}
			s = val.identifier ?? "";
		}
		MsgPingShow msgPingShow = new MsgPingShow
		{
			n = ++ServerSeq,
			p = from,
			name = Net.PlayerName(from),
			id = m.id,
			x = (float)Math.Round(Mathf.Clamp01(m.x), 4),
			y = (float)Math.Round(Mathf.Clamp01(m.y), 4),
			v = CleanView(m.v)
		};
		List<int> list = new List<int>();
		foreach (int item in new List<int>(Net.Players.Keys))
		{
			if (Net.IsPlayerLive(item))
			{
				Net.ToPlayer(item, "ping.show", msgPingShow);
				list.Add(item);
			}
		}
		list.Sort();
		ServerLastTo = list.ToArray();
		ServerAccepted++;
		Log.Info("server: ping #" + msgPingShow.n + " from " + msgPingShow.name + " (player " + from + ") at " + ((m.id != 0L) ? ("item " + m.id.ToString(CultureInfo.InvariantCulture) + " (" + Text.Clip(s, 64) + ")") : "a spot") + " x=" + msgPingShow.x.ToString("0.###", CultureInfo.InvariantCulture) + " y=" + msgPingShow.y.ToString("0.###", CultureInfo.InvariantCulture) + " view " + ((msgPingShow.v.Length == 0) ? "-" : msgPingShow.v) + " -> " + list.Count + " live player(s)");
	}

	private static void Refuse(int player, string why, string detail)
	{
		ServerRefused++;
		if (Log.Allow("ping|p" + player, 10000L, out var skipped))
		{
			Log.Info("server: ping from " + Net.PlayerName(player) + " refused: " + detail + Log.More(skipped));
		}
		if (why != null && buckets.TryGetValue(player, out var value))
		{
			long ms = Clock.Ms;
			if (ms - value.LastNoMs >= 2000)
			{
				value.LastNoMs = ms;
				Net.ToPlayer(player, "ping.no", new MsgPingNo
				{
					why = why
				});
			}
		}
	}

	internal static string CleanView(string v)
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
			if (char.IsControl(c) || c == '<' || c == '>' || char.IsSurrogate(c))
			{
				return "";
			}
		}
		return v;
	}

	private static void Refill()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		localTokens = Math.Min(3.0, localTokens + (double)((realtimeSinceStartup - localAt) / 1.6f));
		localAt = realtimeSinceStartup;
	}

	public static string PointAt(long id, Vector2 area, string view = null)
	{
		if (!Net.IsClient || !Net.IsLive)
		{
			return "offline";
		}
		if (Snapshot.IsLoading)
		{
			return "loading";
		}
		if (RunOver())
		{
			return "the run is over";
		}
		if (float.IsNaN(area.x) || float.IsNaN(area.y) || area.x < 0f || area.x > 1f || area.y < 0f || area.y > 1f)
		{
			return "outside the game area";
		}
		Refill();
		if (localTokens < 1.0)
		{
			LocalRefused++;
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (realtimeSinceStartup - localNoteAt > 3f)
			{
				localNoteAt = realtimeSinceStartup;
				Chat.Local("Wait a moment before pointing again.");
			}
			return "too fast";
		}
		localTokens -= 1.0;
		Sent++;
		Net.ToServer("ping.at", new MsgPingAt
		{
			id = id,
			x = (float)Math.Round(area.x, 4),
			y = (float)Math.Round(area.y, 4),
			v = (view ?? Presence.LocalView)
		});
		return null;
	}

	private static bool RunOver()
	{
		try
		{
			return DayNightRun.RunOver;
		}
		catch
		{
			return false;
		}
	}

	public static long ItemUnder(Vector2 screen)
	{
		try
		{
			if ((Object)(object)EventSystem.current == (Object)null)
			{
				return 0L;
			}
			GameItem val = RenderHandler.RaycastElement<GameItem>(screen, (TreeNodeRender)null);
			if (val == null || Ids.IsStandIn(val))
			{
				return 0L;
			}
			return Ids.Of(val);
		}
		catch
		{
			return 0L;
		}
	}

	public static bool KeyboardKey(KeyCode k)
	{
		if ((int)k < 323)
		{
			return (int)k != 27;
		}
		return false;
	}

	private static void LoadKey()
	{
		if (keyLoaded)
		{
			return;
		}
		keyLoaded = true;
		try
		{
			if (ES3.KeyExists("humangenome_accomplice.pointKey"))
			{
				pointKey = (KeyCode)ES3.Load<int>("humangenome_accomplice.pointKey");
			}
		}
		catch
		{
			pointKey = (KeyCode)103;
		}
		if (!KeyboardKey(pointKey))
		{
			pointKey = (KeyCode)103;
		}
	}

	private static void EnsureKeyBind()
	{
		if (keyBindAdded || !Ui.AtMenu)
		{
			return;
		}
		keyBindAdded = true;
		try
		{
			GameOptionRegistry.EnsureVanilla();
			ModHelper.AddGameOptionKeyBind("humangenome_accomplice", "keybind", "ping", (Func<string>)(() => Lang.Tr("Co-op point")), (Func<KeyCode>)(() => PointKey), (Action<KeyCode>)delegate(KeyCode k)
			{
				PointKey = k;
			}, (Func<string>)(() => Lang.Tr("On a co-op server: points at the item or spot under your mouse for everyone.")));
		}
		catch (Exception ex)
		{
			Log.Warn("ping: key bind row not added: " + ex.Message);
		}
	}

	internal static string GameBindUsing(KeyCode key)
	{
		if ((int)key == 0)
		{
			return null;
		}
		try
		{
			foreach (KeyBindHelper.Binding binding in KeyBindHelper.Bindings)
			{
				if (binding != null && KeyBindHelper.Get(binding.id) == key)
				{
					return binding.id;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static bool GameBusy()
	{
		try
		{
			if (Chat.OwnsKeyboard || StoreUIManager.IsAnyTyping() || KeyBindHelper.SuppressHotkeys)
			{
				return true;
			}
			if (MachineStorageCoordinator.IsBusy || HopperHelper.IsBusy)
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	[SystemTick]
	private static void Tick()
	{
		if (Net.IsDedicated || Args.Server)
		{
			return;
		}
		EnsureKeyBind();
		if (Net.IsClient)
		{
			PointMarks.Tick();
			if (Ui.ShopOnline)
			{
				PollInput();
			}
		}
	}

	private unsafe static void PollInput()
	{
		KeyCode val = PointKey;
		if ((int)val == 0 || !Input.GetKeyDown(val) || GameBusy() || CtrlHeld)
		{
			return;
		}
		string text = GameBindUsing(val);
		if (text != null)
		{
			if (lastTakenKeyLog != text)
			{
				lastTakenKeyLog = text;
				Log.Info("ping: the point key " + val.ToString() + " is also the game's '" + text + "' key: the game's action wins (pick another key in Options > Key binds)");
			}
			return;
		}
		Vector3 mousePosition = Input.mousePosition;
		Vector2 screen = default(Vector2);
		screen = new Vector2(mousePosition.x, mousePosition.y);
		Vector2 val2 = Kit.ToGameArea(screen);
		if (!(val2.x < 0f) && !(val2.x > 1f) && !(val2.y < 0f) && !(val2.y > 1f))
		{
			string text2 = PointAt(ItemUnder(screen), val2);
			if (text2 != null && text2 != "too fast")
			{
				Log.Info("ping: not sent (" + text2 + ")");
			}
		}
	}

	private static void ClientOnShow(MsgPingShow m)
	{
		if (m == null || !Net.IsClient)
		{
			return;
		}
		if (!Net.IsLive || Snapshot.IsLoading || DayLog.Replaying)
		{
			IgnoredLoading++;
			return;
		}
		Received++;
		receivedFrom[m.p] = ReceivedFrom(m.p) + 1;
		LastShow = m;
		bool flag = m.p == Net.LocalPlayer;
		string text = Kit.Safe(Kit.Name(m.name));
		string text3;
		if (m.id != 0L)
		{
			string text2 = ItemName(m.id);
			Chat.Local((flag ? "You point at " : (text + " points at ")) + text2 + ".");
			text3 = "item " + m.id.ToString(CultureInfo.InvariantCulture) + " (" + text2 + ")";
		}
		else
		{
			text3 = "a spot";
		}
		PointMarks.Mark mark = PointMarks.Add(m, text);
		Log.Info("client: ping #" + m.n + " from " + text + " at " + text3 + ": " + ((mark == null) ? "no marker" : mark.Describe()));
	}

	private static void ClientOnNo(MsgPingNo m)
	{
		RefusedByServer++;
		LastNo = m?.why ?? "";
		Log.Info("client: ping refused by the server: " + LastNo);
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (realtimeSinceStartup - localNoteAt > 3f)
		{
			localNoteAt = realtimeSinceStartup;
			Chat.Local(Kit.Safe(LastNo));
		}
	}

	public static string ItemName(long id)
	{
		try
		{
			GameItem val = Ids.Find(id);
			if (val == null)
			{
				return "something";
			}
			string text = val.GetDisplayName(true);
			if (string.IsNullOrEmpty(text))
			{
				text = val.identifier;
			}
			return string.IsNullOrEmpty(text) ? "something" : Kit.Safe(Kit.Name(Kit.Plain(text)));
		}
		catch
		{
			return "something";
		}
	}
}
