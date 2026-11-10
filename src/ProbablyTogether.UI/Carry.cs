using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace ProbablyTogether.UI;

public static class Carry
{
	private sealed class Srv
	{
		public MsgCursor Last;

		public long InD;

		public int InC;

		public long MissD;

		public int MissC;

		public long HideD;

		public int HideC;
	}

	private sealed class Icon
	{
		public RectTransform Root;

		public Image Border;

		public Image Img;

		public TextMeshProUGUI Count;

		public long Item;

		public Sprite Sprite;

		public string CountText = "";

		public long CachedId;

		public GameItem Cached;

		public float ResolvedAt = -10f;
	}

	public const int MaxGroup = 20;

	private static bool localOn;

	private static bool localGroup;

	private static long localItem;

	private static int serial;

	private static readonly FieldInfo fRemove = AccessTools.Field(typeof(ItemMouseDragHandler), "currentRemove");

	private static readonly FieldInfo fSelected = AccessTools.Field(typeof(ItemMultiSelectHandler), "selectedItems");

	private static readonly Dictionary<int, Srv> srv = new Dictionary<int, Srv>();

	private static readonly Dictionary<int, Icon> icons = new Dictionary<int, Icon>();

	private static bool inited;

	public static long LocalItem
	{
		get
		{
			if (!localOn)
			{
				return 0L;
			}
			return localItem;
		}
	}

	public static bool LocalIsGroup
	{
		get
		{
			if (localOn)
			{
				return localGroup;
			}
			return false;
		}
	}

	public static int LocalSerial => serial;

	public static int StartsByHook { get; private set; }

	public static int StartsByPoll { get; private set; }

	public static int EndsByHook { get; private set; }

	public static int EndsByPoll { get; private set; }

	public static string LastEnd { get; private set; } = "";

	public static int ServerInvalid { get; private set; }

	public static int ServerHidden { get; private set; }

	public static int ServerCleared { get; private set; }

	internal static IEnumerable<int> ServerPlayers => srv.Keys;

	internal static void OnLocalDragStart(GameItem item, bool group)
	{
		if (Net.IsClient && item != null)
		{
			Begin(item, group);
			StartsByHook++;
		}
	}

	private static void Begin(GameItem item, bool group)
	{
		serial++;
		localOn = true;
		localGroup = group;
		localItem = Math.Max(0L, Ids.Of(item));
	}

	internal static void OnLocalDragEnd(string how)
	{
		if (Net.IsClient && localOn && !StillDragging())
		{
			localOn = false;
			localItem = 0L;
			EndsByHook++;
			LastEnd = how;
		}
	}

	private static bool StillDragging()
	{
		try
		{
			if (localGroup)
			{
				ItemMultiSelectHandler current = ItemMultiSelectHandler.current;
				return (Object)(object)current != (Object)null && current.IsGroupDragging;
			}
			ItemMouseDragHandler current2 = ItemMouseDragHandler.current;
			return (Object)(object)current2 != (Object)null && current2.IsDraggingItem && current2.GetCurrentItem() != null && Math.Max(0L, Ids.Of(current2.GetCurrentItem())) == localItem;
		}
		catch
		{
			return false;
		}
	}

	private static void Poll()
	{
		if (localOn)
		{
			if (!StillDragging())
			{
				localOn = false;
				localItem = 0L;
				EndsByPoll++;
				LastEnd = "poll";
			}
			return;
		}
		try
		{
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			if ((Object)(object)current != (Object)null && current.IsDraggingItem && current.GetCurrentItem() != null)
			{
				Begin(current.GetCurrentItem(), group: false);
				StartsByPoll++;
				return;
			}
			ItemMultiSelectHandler current2 = ItemMultiSelectHandler.current;
			int count;
			GameItem val = (((Object)(object)current2 != (Object)null && current2.IsGroupDragging) ? GroupLead(current2, out count) : null);
			if (val != null)
			{
				Begin(val, group: true);
				StartsByPoll++;
			}
		}
		catch
		{
		}
	}

	internal static GameItem GroupLead(ItemMultiSelectHandler m, out int count)
	{
		count = 0;
		GameItem val = null;
		try
		{
			if (fSelected?.GetValue(m) is List<GameItemElement> list)
			{
				foreach (GameItemElement item in list)
				{
					if (item != null && !item.IsDestroyed())
					{
						count++;
						if (val == null)
						{
							val = (GameItem)(object)item;
						}
					}
				}
			}
		}
		catch
		{
		}
		return val;
	}

	internal static void FillLocal(MsgCursor m)
	{
		Poll();
		m.d = 0L;
		m.n = 0;
		m.g = 0;
		m.c = 0;
		if (!localOn || localItem <= 0)
		{
			return;
		}
		m.d = localItem;
		m.c = serial;
		try
		{
			if (localGroup)
			{
				ItemMultiSelectHandler current = ItemMultiSelectHandler.current;
				int count = 0;
				GameItem val = (((Object)(object)current != (Object)null) ? GroupLead(current, out count) : null);
				m.g = Math.Max(0, Math.Min(20, count - 1));
				if (val != null)
				{
					m.n = Math.Max(0, val.MaxNumRemove());
				}
			}
			else
			{
				ItemMouseDragHandler current2 = ItemMouseDragHandler.current;
				if ((Object)(object)current2 != (Object)null && fRemove != null && fRemove.GetValue(current2) is int val2)
				{
					m.n = Math.Max(0, val2);
				}
			}
		}
		catch
		{
		}
	}

	private static Srv SrvFor(int p)
	{
		if (!Carry.srv.TryGetValue(p, out var value))
		{
			value = (Carry.srv[p] = new Srv());
		}
		return value;
	}

	internal static void ServerCursor(int from, MsgCursor m, MsgCursor o)
	{
		Srv srv = SrvFor(from);
		long num = Math.Max(0L, m.d);
		int c = m.c;
		srv.InD = num;
		srv.InC = c;
		if (srv.HideD != 0L && (num != srv.HideD || c != srv.HideC))
		{
			srv.HideD = 0L;
			srv.HideC = 0;
		}
		GameItem val = null;
		if (num != 0L && (num != srv.MissD || c != srv.MissC))
		{
			val = Ids.Find(num);
			if (val == null)
			{
				ServerInvalid++;
				srv.MissD = num;
				srv.MissC = c;
			}
			else
			{
				srv.MissD = 0L;
				srv.MissC = 0;
			}
		}
		if (val != null && srv.HideD != 0L)
		{
			val = null;
			ServerHidden++;
		}
		if (val == null)
		{
			o.d = 0L;
			o.n = 0;
			o.g = 0;
			o.c = 0;
		}
		else
		{
			o.d = num;
			o.c = c;
			o.n = Mathf.Clamp(m.n, 0, Math.Max(1, val.unitCount));
			o.g = Mathf.Clamp(m.g, 0, 20);
		}
		srv.Last = Copy(o);
	}

	internal static void ServerRefused(int player, string kind, ItemsReq m, string why)
	{
		if (!Net.IsServer || player <= 0 || m == null || !srv.TryGetValue(player, out var value) || value.Last == null || value.Last.d == 0L)
		{
			return;
		}
		long d = value.Last.d;
		bool flag = m.i == d;
		if (!flag && m.ids != null)
		{
			long[] ids = m.ids;
			for (int i = 0; i < ids.Length; i++)
			{
				if (ids[i] == d)
				{
					flag = true;
					break;
				}
			}
		}
		if (!flag)
		{
			return;
		}
		value.HideD = value.InD;
		value.HideC = value.InC;
		MsgCursor msgCursor = Copy(value.Last);
		msgCursor.d = 0L;
		msgCursor.n = 0;
		msgCursor.g = 0;
		msgCursor.c = 0;
		value.Last = msgCursor;
		int num = 0;
		foreach (int item in new List<int>(Net.Players.Keys))
		{
			if (item != player && Net.IsPlayerLive(item))
			{
				Net.ToPlayer(item, "presence.cursor", Copy(msgCursor));
				num++;
			}
		}
		ServerCleared++;
		if (Log.Allow("carry|refused", 2000L, out var skipped))
		{
			Log.Info("carry: " + Net.PlayerName(player) + "'s " + kind + " of item#" + d + " was refused (" + why + "): carried icon cleared on " + num + " other screen(s)" + Log.More(skipped));
		}
	}

	public static string ServerDescribe(int p)
	{
		if (!srv.TryGetValue(p, out var value))
		{
			return "none";
		}
		MsgCursor last = value.Last;
		return "in=" + value.InD + "/" + value.InC + " out=" + ((last != null) ? (last.d + "/" + last.n + "/" + last.g) : "-") + " hide=" + value.HideD;
	}

	internal static void ServerForget(int player)
	{
		srv.Remove(player);
	}

	internal static void ServerNewRun()
	{
		srv.Clear();
	}

	private static MsgCursor Copy(MsgCursor a)
	{
		return new MsgCursor
		{
			p = a.p,
			x = a.x,
			y = a.y,
			v = a.v,
			h = a.h,
			d = a.d,
			n = a.n,
			g = a.g,
			c = a.c
		};
	}

	public static long ShownItem(int player)
	{
		if (!icons.TryGetValue(player, out var value))
		{
			return 0L;
		}
		return value.Item;
	}

	public static Sprite ShownSprite(int player)
	{
		if (!icons.TryGetValue(player, out var value))
		{
			return null;
		}
		return value.Sprite;
	}

	public static string ShownCount(int player)
	{
		if (!icons.TryGetValue(player, out var value) || value.Item == 0L)
		{
			return "";
		}
		return value.CountText;
	}

	public static bool IconOnScreen(int player)
	{
		if (icons.TryGetValue(player, out var value) && (Object)(object)value.Root != (Object)null && ((Component)value.Root).gameObject.activeInHierarchy && (Object)(object)value.Img != (Object)null)
		{
			return (Object)(object)value.Img.sprite != (Object)null;
		}
		return false;
	}

	public static Sprite SpriteOf(GameItem item)
	{
		try
		{
			GameItem obj = ((item is GameItemElement) ? item : null);
			object result;
			if (obj == null)
			{
				result = null;
			}
			else
			{
				TreeNodeRender background = ((GameItemElement)obj).background;
				if (background == null)
				{
					result = null;
				}
				else
				{
					Image image = background.image;
					result = ((image != null) ? image.sprite : null);
				}
			}
			return (Sprite)result;
		}
		catch
		{
			return null;
		}
	}

	internal static void Draw(Presence.Remote r)
	{
		if (r == null)
		{
			return;
		}
		if (!icons.TryGetValue(r.Id, out var value))
		{
			value = (icons[r.Id] = new Icon());
		}
		Sprite val = null;
		if (r.Visible && r.Drag > 0 && (Object)(object)r.Widget != (Object)null)
		{
			val = SpriteOf(Resolve(value, r.Drag));
		}
		if ((Object)(object)val == (Object)null)
		{
			if ((Object)(object)value.Root != (Object)null && ((Component)value.Root).gameObject.activeSelf)
			{
				((Component)value.Root).gameObject.SetActive(false);
			}
			value.Item = 0L;
			value.Sprite = null;
			return;
		}
		if ((Object)(object)value.Root == (Object)null || (Object)(object)((Transform)value.Root).parent != (Object)(object)r.Widget)
		{
			Build(value, r);
		}
		if ((Object)(object)value.Img.sprite != (Object)(object)val)
		{
			value.Img.sprite = val;
		}
		string text = ((r.Group > 0) ? ("+" + Math.Min(r.Group, 20)) : ((r.Units > 1) ? ("x" + r.Units) : ""));
		if (text != value.CountText)
		{
			value.CountText = text;
			Kit.SetText(value.Count, text);
			((Component)value.Count).gameObject.SetActive(text.Length > 0);
		}
		Color val2 = Kit.PlayerColor(r.Id);
		val2.a = (r.Afk ? 0.5f : 0.95f);
		if (((Graphic)value.Border).color != val2)
		{
			((Graphic)value.Border).color = val2;
		}
		if (!((Component)value.Root).gameObject.activeSelf)
		{
			((Component)value.Root).gameObject.SetActive(true);
		}
		value.Item = r.Drag;
		value.Sprite = val;
	}

	private static GameItem Resolve(Icon ic, long id)
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (ic.CachedId == id)
		{
			GameItem cached = ic.Cached;
			GameItemElement val = (GameItemElement)(object)((cached is GameItemElement) ? cached : null);
			if (val != null && !val.IsDestroyed() && ((GameItem)val).uniqueId == id)
			{
				return ic.Cached;
			}
			if (ic.Cached == null && realtimeSinceStartup - ic.ResolvedAt < 0.5f)
			{
				return null;
			}
		}
		ic.CachedId = id;
		ic.ResolvedAt = realtimeSinceStartup;
		try
		{
			ic.Cached = Ids.Find(id);
		}
		catch
		{
			ic.Cached = null;
		}
		return ic.Cached;
	}

	private static void Build(Icon ic, Presence.Remote r)
	{
		if ((Object)(object)ic.Root != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)ic.Root).gameObject);
		}
		Image val = Kit.Box((Transform)(object)r.Widget, "carry", Kit.PlayerColor(r.Id));
		RectTransform rectTransform = ((Graphic)val).rectTransform;
		Vector2 anchorMin = (rectTransform.anchorMax = Vector2.zero);
		rectTransform.anchorMin = anchorMin;
		rectTransform.pivot = Vector2.zero;
		rectTransform.sizeDelta = new Vector2(48f, 48f);
		rectTransform.anchoredPosition = new Vector2(14f, 2f);
		((Transform)rectTransform).SetAsFirstSibling();
		Kit.Stretch(((Graphic)Kit.Box((Transform)(object)rectTransform, "plate", new Color(Kit.PlateColor.r, Kit.PlateColor.g, Kit.PlateColor.b, 0.88f))).rectTransform, 2f);
		Image val2 = Kit.Box((Transform)(object)rectTransform, "item", new Color(1f, 1f, 1f, DragAlpha()));
		val2.preserveAspect = true;
		Kit.Stretch(((Graphic)val2).rectTransform, 5f);
		TextMeshProUGUI val3 = Kit.Label((Transform)(object)rectTransform, "count", 13f, Kit.Text, 0f, (TextAlignmentOptions)1028);
		Kit.Stretch(((TMP_Text)val3).rectTransform, 4f);
		((Component)val3).gameObject.SetActive(false);
		ic.Root = rectTransform;
		ic.Border = val;
		ic.Img = val2;
		ic.Count = val3;
		ic.CountText = "";
	}

	private static float DragAlpha()
	{
		try
		{
			UISettings current = UISettings.current;
			if ((Object)(object)current != (Object)null)
			{
				return Mathf.Clamp(current.dragOpacity, 0.6f, 1f);
			}
		}
		catch
		{
		}
		return 0.85f;
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
			Net.PlayerLeft += delegate(PlayerInfo p)
			{
				if (p != null)
				{
					if (Net.IsServer)
					{
						ServerForget(p.Id);
					}
					else
					{
						icons.Remove(p.Id);
					}
				}
			};
			Net.Disconnected += delegate
			{
				ClientReset();
			};
			ItemsMove.Refused += ServerRefused;
			ServerMode.NewRunStarting += delegate
			{
				ServerNewRun();
			};
		}
		catch (Exception ex)
		{
			Log.Error("carry: init failed: " + ex);
		}
	}

	private static void ClientReset()
	{
		icons.Clear();
		localOn = false;
		localItem = 0L;
	}
}
