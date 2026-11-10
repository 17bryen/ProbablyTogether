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
		public long Item;

		public Sprite Sprite;

		public string CountText = "";

		public RectTransform Ghost;

		public Image GhostImg;

		public CanvasGroup Faded;

		public GameItemElement FadedEl;

		public GameInventory HiInv;

		public int HiX = int.MinValue;

		public int HiY;

		public bool HiOk;

		public string HiRef = "";

		public Vector2 GhostPos;

		public bool GhostHas;

		public string GhostKey = "";

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

	private static readonly FieldInfo fLastSlot = AccessTools.Field(typeof(ItemMouseDragHandler), "lastSlot");

	private static readonly FieldInfo fLastInventory = AccessTools.Field(typeof(ItemMouseDragHandler), "lastInventory");

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
			ItemMouseDragHandler held = ItemMouseDragHandler.current;
			GameItemElement heldEl = (((Object)(object)held != (Object)null) ? (held.GetCurrentItem() as GameItemElement) : null);
			if (heldEl != null && heldEl.tempShape != null)
			{
				m.o = heldEl.tempShape.orientation & 3;
				m.f = heldEl.tempShape.flipped;
			}
			m.t = null;
			Vector2 centreWorld = Vector2.zero;
			bool haveCentre = false;
			if (heldEl != null)
			{
				TreeNodeRender rot = heldEl.fakeRotator ?? heldEl.rotator;
				if (rot != null && (Object)(object)rot.rectTransform != (Object)null)
				{
					centreWorld = heldEl.GetFakeRotatorCenteredPosition();
					Vector2 area = Kit.ToGameArea(RectTransformUtility.WorldToScreenPoint(CanvasCamera(rot.rectTransform), (Vector3)centreWorld));
					m.cx = area.x;
					m.cy = area.y;
					haveCentre = true;
				}
			}
			if (heldEl != null && haveCentre && fLastSlot != null && fLastInventory != null)
			{
				GameInventory hoverInv = fLastInventory.GetValue(held) as GameInventory;
				SlotMarker hoverSlot = fLastSlot.GetValue(held) as SlotMarker;
				GameGridInventory hoverGrid = hoverInv as GameGridInventory;
				if (hoverGrid == null)
				{
					try
					{
						hoverGrid = RenderHandler.RaycastFindParentElement<GameInventory>((Vector2)Input.mousePosition, heldEl.fakeBackground) as GameGridInventory;
					}
					catch
					{
						hoverGrid = null;
					}
					hoverSlot = null;
				}
				if (hoverGrid != null && (Object)(object)hoverGrid.rectTransform != (Object)null)
				{
					Vector3 loc = ((Transform)hoverGrid.rectTransform).InverseTransformPoint((Vector3)centreWorld);
					m.lx = loc.x;
					m.ly = loc.y;
					m.t = Items.RefOf((GraphNodeStorage)(object)hoverGrid);
					if (hoverSlot != null && hoverSlot.inventory == hoverGrid && hoverSlot.itemGridShape != null)
					{
						m.tx = hoverSlot.itemGridShape.minX;
						m.ty = hoverSlot.itemGridShape.minY;
						m.ok = hoverSlot.numTransfer > 0;
					}
					else
					{
						m.tx = int.MinValue;
						m.ty = 0;
						m.ok = false;
					}
				}
			}
		}
		catch
		{
		}
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
			o.o = m.o & 3;
			o.f = m.f;
			o.t = m.t;
			o.tx = m.tx;
			o.ty = m.ty;
			o.ok = m.ok;
			o.cx = m.cx;
			o.cy = m.cy;
			o.lx = m.lx;
			o.ly = m.ly;
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
			c = a.c,
			o = a.o,
			f = a.f,
			t = a.t,
			tx = a.tx,
			ty = a.ty,
			ok = a.ok,
			cx = a.cx,
			cy = a.cy,
			lx = a.lx,
			ly = a.ly
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
		if (icons.TryGetValue(player, out var value) && (Object)(object)value.Ghost != (Object)null && ((Component)value.Ghost).gameObject.activeInHierarchy)
		{
			return (Object)(object)value.GhostImg.sprite != (Object)null;
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
		GameItem resolved = null;
		Sprite val = null;
		if (r.Visible && r.Drag > 0 && (Object)(object)r.Widget != (Object)null)
		{
			resolved = Resolve(value, r.Drag);
			val = SpriteOf(resolved);
		}
		GameItemElement el = resolved as GameItemElement;
		if ((Object)(object)val == (Object)null || el == null || el.IsDestroyed())
		{
			value.Item = 0L;
			value.Sprite = null;
			value.CountText = "";
			EndGhost(value);
			return;
		}
		value.CountText = ((r.Group > 0) ? ("+" + Math.Min(r.Group, 20)) : ((r.Units > 1) ? ("x" + r.Units) : ""));
		if (r.Target != null && !TargetVisible(r.Target))
		{
			value.Item = 0L;
			value.Sprite = null;
			HideGhost(value);
			ClearHi(value);
			Fade(value, el, on: true);
			return;
		}
		value.Item = r.Drag;
		value.Sprite = val;
		DrawGhost(value, r, el, val);
	}

	// Spectator ghost: anchored to the hovered inventory on this screen (hidden if it is not open here), else it follows the remote cursor.

	internal static int GhostDrawn;

	internal static string LastGhostNote = "";

	private static void DrawGhost(Icon ic, Presence.Remote r, GameItemElement el, Sprite sprite)
	{
		try
		{
			if ((Object)(object)ic.Ghost == (Object)null)
			{
				BuildGhost(ic);
			}
			GridShape ms = ((GameItem)el).modifiedShape;
			int w = ((ms != null) ? Math.Max(1, ms.width) : 1);
			int h = ((ms != null) ? Math.Max(1, ms.height) : 1);
			Fade(ic, el, on: true);
			Vector2 area;
			float cell;
			string key;
			if (r.Target != null)
			{
				GameGridInventory grid = Items.Resolve(r.Target) as GameGridInventory;
				if (grid == null || !GridVisible(grid))
				{
					LastGhostNote = "target not open here";
					HideGhost(ic);
					ClearHi(ic);
					return;
				}
				if (r.TX != int.MinValue)
				{
					GridShape shape = new GridShapeBuilder(ms).SetOrientation(r.Orient & 3).SetFlipped(r.Flip).SetPosition(r.TX, r.TY).Build();
					Highlight(ic, r, el, grid, shape);
				}
				else
				{
					ClearHi(ic);
				}
				float gs = GridSize();
				Vector3 local = new Vector3(r.LX, r.LY, 0f);
				Camera cam = CanvasCamera(grid.rectTransform);
				Vector2 center = RectTransformUtility.WorldToScreenPoint(cam, ((Transform)grid.rectTransform).TransformPoint(local));
				Vector2 right = RectTransformUtility.WorldToScreenPoint(cam, ((Transform)grid.rectTransform).TransformPoint(local + new Vector3(gs, 0f, 0f)));
				cell = Mathf.Max(1f, Mathf.Abs(right.x - center.x));
				area = Kit.ToGameArea(center);
				key = r.Target.ToString();
			}
			else
			{
				ClearHi(ic);
				cell = CellPixels(el, ms);
				area = new Vector2(r.CX, r.CY);
				key = "cursor";
			}
			if (!ic.GhostHas)
			{
				ic.GhostPos = area;
				ic.GhostHas = true;
				ic.GhostKey = key;
			}
			else
			{
				ic.GhostPos = Vector2.Lerp(ic.GhostPos, area, Mathf.Clamp01(Time.unscaledDeltaTime * 18f));
			}
			float scale = OverlayScale();
			ic.Ghost.anchorMin = ic.GhostPos;
			ic.Ghost.anchorMax = ic.GhostPos;
			ic.Ghost.anchoredPosition = Vector2.zero;
			ic.Ghost.sizeDelta = new Vector2((float)w * cell / scale, (float)h * cell / scale);
			((Transform)ic.Ghost).localEulerAngles = new Vector3(0f, 0f, 90f * (float)(r.Orient & 3));
			((Transform)ic.Ghost).localScale = new Vector3(r.Flip ? (-1f) : 1f, 1f, 1f);
			if ((Object)(object)ic.GhostImg.sprite != (Object)(object)sprite)
			{
				ic.GhostImg.sprite = sprite;
			}
			Color c = Color.white;
			c.a = DragAlpha();
			((Graphic)ic.GhostImg).color = c;
			if (!((Component)ic.Ghost).gameObject.activeSelf)
			{
				((Component)ic.Ghost).gameObject.SetActive(true);
			}
			GhostDrawn++;
		}
		catch (Exception ex)
		{
			if (Log.Allow("carry|ghost", 10000L, out var skipped))
			{
				Log.Warn("carry: ghost for " + Net.PlayerName(r.Id) + " threw: " + ex + Log.More(skipped));
			}
			EndGhost(ic);
		}
	}

	private static void HideGhost(Icon ic)
	{
		ic.GhostHas = false;
		if ((Object)(object)ic.Ghost != (Object)null && ((Component)ic.Ghost).gameObject.activeSelf)
		{
			((Component)ic.Ghost).gameObject.SetActive(false);
		}
	}

	private static void EndGhost(Icon ic)
	{
		HideGhost(ic);
		Fade(ic, null, on: false);
		ClearHi(ic);
	}

	private static void BuildGhost(Icon ic)
	{
		if ((Object)(object)ic.Ghost != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)ic.Ghost).gameObject);
		}
		Image img = Kit.Box((Transform)(object)Overlay.Root, "ghost", Color.white);
		RectTransform rt = ((Graphic)img).rectTransform;
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.anchoredPosition = Vector2.zero;
		rt.sizeDelta = new Vector2(32f, 32f);
		img.preserveAspect = false;
		((Graphic)img).raycastTarget = false;
		((Transform)rt).SetAsFirstSibling();
		ic.Ghost = rt;
		ic.GhostImg = img;
	}

	private static bool TargetVisible(ItemRef t)
	{
		try
		{
			GameGridInventory grid = Items.Resolve(t) as GameGridInventory;
			return grid != null && GridVisible(grid);
		}
		catch
		{
			return false;
		}
	}

	private static bool GridVisible(GameGridInventory grid)
	{
		try
		{
			TreeNodeRender handler = grid.handler;
			if (handler == null || (Object)(object)handler.rectTransform == (Object)null)
			{
				return false;
			}
			if (!((Component)handler).gameObject.activeInHierarchy)
			{
				return false;
			}
			Canvas cv = ((Component)handler).GetComponentInParent<Canvas>();
			if ((Object)(object)cv != (Object)null && !((Behaviour)cv).enabled)
			{
				return false;
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static float GridSize()
	{
		try
		{
			UISettings current = UISettings.current;
			if ((Object)(object)current != (Object)null && current.gridSize > 0)
			{
				return current.gridSize;
			}
		}
		catch
		{
		}
		return 16f;
	}

	private static float CellPixels(GameItemElement el, GridShape ms)
	{
		TreeNodeRender bg = el.background ?? el.handler;
		if (bg == null || (Object)(object)bg.rectTransform == (Object)null)
		{
			return 16f;
		}
		Vector3[] corners = new Vector3[4];
		bg.rectTransform.GetWorldCorners(corners);
		Camera cam = CanvasCamera(bg.rectTransform);
		Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
		Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
		float px = Mathf.Abs(b.x - a.x);
		int gw = ((ms != null) ? Math.Max(1, ms.maxX - ms.minX + 1) : 1);
		float cell = px / (float)gw;
		return (cell > 1f) ? cell : 16f;
	}

	private static float OverlayScale()
	{
		try
		{
			Canvas cv = ((Component)Overlay.Root).GetComponentInParent<Canvas>();
			if ((Object)(object)cv != (Object)null && cv.scaleFactor > 0.01f)
			{
				return cv.scaleFactor;
			}
		}
		catch
		{
		}
		return 1f;
	}

	private static Camera CanvasCamera(RectTransform rt)
	{
		Canvas cv = ((Component)rt).GetComponentInParent<Canvas>();
		if ((Object)(object)cv == (Object)null || (int)cv.renderMode == 0)
		{
			return null;
		}
		return cv.worldCamera;
	}

	private static void Fade(Icon ic, GameItemElement el, bool on)
	{
		if (!on || el == null)
		{
			if ((Object)(object)ic.Faded != (Object)null)
			{
				try
				{
					ic.Faded.alpha = 1f;
				}
				catch
				{
				}
			}
			ic.Faded = null;
			ic.FadedEl = null;
			return;
		}
		if (ic.FadedEl != el)
		{
			Fade(ic, null, on: false);
			TreeNodeRender root = el.handler;
			if (root == null)
			{
				return;
			}
			GameObject go = ((Component)root).gameObject;
			CanvasGroup cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
			ic.Faded = cg;
			ic.FadedEl = el;
		}
		ItemMouseDragHandler local = ItemMouseDragHandler.current;
		bool localHolds = (Object)(object)local != (Object)null && local.IsDraggingItem && local.GetCurrentItem() == el;
		if ((Object)(object)ic.Faded != (Object)null)
		{
			ic.Faded.alpha = (localHolds ? 1f : DragAlpha());
		}
	}

	private static void ClearHi(Icon ic)
	{
		if (ic.HiInv != null)
		{
			try
			{
				ic.HiInv.ClearHighlight();
			}
			catch
			{
			}
		}
		ic.HiInv = null;
		ic.HiX = int.MinValue;
		ic.HiRef = "";
	}

	private static void Highlight(Icon ic, Presence.Remote r, GameItemElement el, GameGridInventory grid, GridShape shape)
	{
		ItemMouseDragHandler local = ItemMouseDragHandler.current;
		if ((Object)(object)local != (Object)null && local.IsDraggingItem)
		{
			ClearHi(ic);
			return;
		}
		bool ok = r.TOk;
		if (ic.HiInv == grid && ic.HiX == shape.minX && ic.HiY == shape.minY && ic.HiOk == ok)
		{
			return;
		}
		ClearHi(ic);
		SlotMarker slot = new SlotMarker
		{
			item = el,
			inventory = grid,
			itemGridShape = shape,
			numTransfer = (ok ? 1 : 0)
		};
		List<TreeNodeRender> nodes = grid.HighlightSlot(slot, ok ? RenderHandler.BackgroundColor.Green : RenderHandler.BackgroundColor.Red, true);
		Color c = Kit.PlayerColor(r.Id);
		c.a = (ok ? 0.6f : 0.35f);
		if (nodes != null)
		{
			foreach (TreeNodeRender n in nodes)
			{
				if (n != null && (Object)(object)n.image != (Object)null)
				{
					((Graphic)n.image).color = c;
				}
			}
		}
		ic.HiInv = grid;
		ic.HiX = shape.minX;
		ic.HiY = shape.minY;
		ic.HiOk = ok;
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
