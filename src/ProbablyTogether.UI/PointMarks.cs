using System;
using System.Collections.Generic;
using System.Globalization;
using ProbablyTogether.Systems;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProbablyTogether.UI;

public static class PointMarks
{
	public sealed class Mark
	{
		public int Player;

		public int N;

		public long ItemId;

		public Vector2 Spot;

		public string View = "";

		public float Born;

		public string Name = "";

		public string Text = "";

		public bool Visible;

		public bool OnItem;

		public Vector2 Anchor;

		public Rect ItemArea;

		internal GameItem Item;

		internal float FoundAt = -10f;

		internal float CoverAt = -10f;

		public bool Reached;

		public string Why = "";

		internal RectTransform Widget;

		internal RectTransform Frame;

		internal RectTransform Pin;

		internal RectTransform Tag;

		internal TextMeshProUGUI TagText;

		internal Graphic[] Graphics;

		internal float[] BaseAlpha;

		public float Age => Time.realtimeSinceStartup - Born;

		public string Describe()
		{
			return ((!Visible) ? ("no marker: " + Why) : (OnItem ? "marker on the item" : "marker at the spot")) + " anchor " + Anchor.x.ToString("0.###", CultureInfo.InvariantCulture) + "," + Anchor.y.ToString("0.###", CultureInfo.InvariantCulture) + ", tag '" + Text + "'";
		}
	}

	public const float Life = 4f;

	public const float FadeTime = 0.6f;

	public const float PulseTime = 0.35f;

	public const float CoverEvery = 0.25f;

	private const float Pad = 4f;

	private const float Bar = 3f;

	private const float Outline = 1.5f;

	private const float PinW = 22f;

	private const float PinH = 30f;

	private const float Gap = 3f;

	private const float FlipAbove = 0.86f;

	private static readonly Dictionary<int, Mark> marks = new Dictionary<int, Mark>();

	public static int Made;

	public static int Expired;

	public static int Replaced;

	public static int LastExpiredPlayer = -1;

	public static float LastExpiredAge;

	private static RectTransform root;

	private static readonly Vector3[] sc = (Vector3[])(object)new Vector3[4];

	private static readonly Vector2[] samples = (Vector2[])(object)new Vector2[5]
	{
		new Vector2(0.5f, 0.5f),
		new Vector2(0.25f, 0.25f),
		new Vector2(0.75f, 0.25f),
		new Vector2(0.25f, 0.75f),
		new Vector2(0.75f, 0.75f)
	};

	private static Sprite pinSprite;

	public static IReadOnlyDictionary<int, Mark> Marks => marks;

	internal static Mark Add(MsgPingShow m, string cleanName)
	{
		if (marks.TryGetValue(m.p, out var value))
		{
			Destroy(value);
			marks.Remove(m.p);
			Replaced++;
		}
		Mark mark = new Mark
		{
			Player = m.p,
			N = m.n,
			ItemId = m.id,
			Spot = new Vector2(m.x, m.y),
			View = (m.v ?? ""),
			Born = Time.realtimeSinceStartup,
			Name = (cleanName ?? "?")
		};
		mark.Text = ((m.id != 0L) ? mark.Name : (mark.Name + " points here"));
		marks[m.p] = mark;
		Made++;
		Place(mark);
		return mark;
	}

	internal static void Clear()
	{
		foreach (Mark value in marks.Values)
		{
			Destroy(value);
		}
		marks.Clear();
	}

	private static void Destroy(Mark k)
	{
		if ((Object)(object)k?.Widget != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)k.Widget).gameObject);
		}
		if (k != null)
		{
			k.Widget = null;
		}
	}

	internal static void Tick()
	{
		if (marks.Count == 0)
		{
			return;
		}
		if (!Ui.ShopOnline)
		{
			Clear();
			return;
		}
		List<int> list = null;
		foreach (Mark value in marks.Values)
		{
			float age = value.Age;
			if (age >= 4f)
			{
				List<int> obj = list ?? new List<int>();
				list = obj;
				obj.Add(value.Player);
				continue;
			}
			try
			{
				Place(value);
				Draw(value, age);
			}
			catch (Exception e)
			{
				UiErrors.Report("ping marker", e);
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (int item in list)
		{
			Mark mark = marks[item];
			LastExpiredPlayer = item;
			LastExpiredAge = mark.Age;
			Destroy(mark);
			marks.Remove(item);
			Expired++;
		}
	}

	private static void Place(Mark k)
	{
		k.OnItem = false;
		k.Visible = false;
		k.Why = "";
		if (k.ItemId != 0L)
		{
			GameItem val = k.Item;
			int num;
			if (val != null && val.uniqueId == k.ItemId)
			{
				GameItemElement val2 = (GameItemElement)(object)((val is GameItemElement) ? val : null);
				num = ((val2 != null && val2.IsDestroyed()) ? 1 : 0);
			}
			else
			{
				num = 1;
			}
			bool flag = (byte)num != 0;
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (flag && realtimeSinceStartup - k.FoundAt >= 0.5f)
			{
				k.FoundAt = realtimeSinceStartup;
				val = (k.Item = Ids.Find(k.ItemId));
				k.CoverAt = -10f;
			}
			else if (flag)
			{
				val = null;
			}
			if (val == null)
			{
				k.Reached = false;
				k.CoverAt = -10f;
				k.Why = "item not found here";
				return;
			}
			if (!Corners(val, sc) || !AreaOf(sc, out var area))
			{
				k.Reached = false;
				k.CoverAt = -10f;
				k.Why = "item not drawn on this screen (a closed window, off the game area)";
				return;
			}
			if (realtimeSinceStartup - k.CoverAt >= 0.25f)
			{
				k.CoverAt = realtimeSinceStartup;
				k.Reached = Reaches(val, sc, k.Spot);
			}
			if (!k.Reached)
			{
				k.Why = "item covered here (the game's raycast does not reach it)";
				return;
			}
			k.ItemArea = area;
			k.OnItem = (k.Visible = true);
			k.Anchor = new Vector2(area.center.x, area.yMax);
		}
		else
		{
			string text = "";
			try
			{
				text = Presence.LocalView;
			}
			catch
			{
			}
			if (k.View == text)
			{
				k.Visible = true;
				k.Anchor = k.Spot;
			}
			else
			{
				k.Why = "another view (" + ((k.View.Length == 0) ? "-" : k.View) + ")";
			}
		}
	}

	public static bool ItemArea(GameItem item, out Rect area)
	{
		area = default(Rect);
		Vector3[] screen = (Vector3[])(object)new Vector3[4];
		if (Corners(item, screen))
		{
			return AreaOf(screen, out area);
		}
		return false;
	}

	public static bool OnThisScreen(GameItem item, out Rect area, Vector2? pointer = null)
	{
		Vector3[] screen = (Vector3[])(object)new Vector3[4];
		area = default(Rect);
		if (Corners(item, screen) && AreaOf(screen, out area))
		{
			return Reaches(item, screen, (Vector2)((pointer) ?? new Vector2(-1f, -1f)));
		}
		return false;
	}

	private static bool Corners(GameItem item, Vector3[] screen)
	{
		PixelElement val = (PixelElement)(object)((item is PixelElement) ? item : null);
		if (val == null)
		{
			return false;
		}
		GameItemElement val2 = (GameItemElement)(object)((item is GameItemElement) ? item : null);
		if (val2 != null && val2.IsDestroyed())
		{
			return false;
		}
		RectTransform rectTransform;
		try
		{
			TreeNodeRender handler = val.handler;
			if ((Object)(object)handler == (Object)null || !((Component)handler).gameObject.activeInHierarchy)
			{
				return false;
			}
			rectTransform = val.rectTransform;
		}
		catch
		{
			return false;
		}
		if ((Object)(object)rectTransform == (Object)null || !((Component)rectTransform).gameObject.activeInHierarchy)
		{
			return false;
		}
		GraphicRaycaster componentInParent = ((Component)rectTransform).GetComponentInParent<GraphicRaycaster>();
		Camera val3 = (((Object)(object)componentInParent != (Object)null) ? ((BaseRaycaster)componentInParent).eventCamera : null);
		rectTransform.GetWorldCorners(screen);
		if ((Object)(object)val3 != (Object)null)
		{
			for (int i = 0; i < 4; i++)
			{
				screen[i] = val3.WorldToScreenPoint(screen[i]);
			}
		}
		return true;
	}

	private static bool AreaOf(Vector3[] screen, out Rect area)
	{
		area = default(Rect);
		float num = float.MaxValue;
		float num2 = float.MaxValue;
		float num3 = float.MinValue;
		float num4 = float.MinValue;
		for (int i = 0; i < 4; i++)
		{
			Vector2 val = Kit.ToGameArea(new Vector2(screen[i].x, screen[i].y));
			if (float.IsNaN(val.x) || float.IsNaN(val.y))
			{
				return false;
			}
			num = Mathf.Min(num, val.x);
			num3 = Mathf.Max(num3, val.x);
			num2 = Mathf.Min(num2, val.y);
			num4 = Mathf.Max(num4, val.y);
		}
		if (num3 - num <= 0f || num4 - num2 <= 0f)
		{
			return false;
		}
		if (num3 < 0f || num > 1f || num4 < 0f || num2 > 1f)
		{
			return false;
		}
		area = Rect.MinMaxRect(num, num2, num3, num4);
		return true;
	}

	private static bool Reaches(GameItem item, Vector3[] screen, Vector2 pointer)
	{
		try
		{
			if ((Object)(object)EventSystem.current == (Object)null)
			{
				return false;
			}
			Vector3 bl = screen[0];
			Vector3 tl = screen[1];
			Vector3 br = screen[3];
			if (RenderHandler.RaycastElement<GameItem>(Point(samples[0]), (TreeNodeRender)null) == item)
			{
				return true;
			}
			if (pointer.x >= 0f && pointer.x <= 1f && pointer.y >= 0f && pointer.y <= 1f && RenderHandler.RaycastElement<GameItem>(Kit.ToScreen(pointer), (TreeNodeRender)null) == item)
			{
				return true;
			}
			for (int i = 1; i < samples.Length; i++)
			{
				if (RenderHandler.RaycastElement<GameItem>(Point(samples[i]), (TreeNodeRender)null) == item)
				{
					return true;
				}
			}
			Vector2 Point(Vector2 uv)
			{
				Vector3 val = bl + (br - bl) * uv.x + (tl - bl) * uv.y;
				return new Vector2(val.x, val.y);
			}
		}
		catch
		{
		}
		return false;
	}

	private static bool EnsureRoot()
	{
		if ((Object)(object)root != (Object)null)
		{
			return true;
		}
		if ((Object)(object)Overlay.Root == (Object)null)
		{
			return false;
		}
		foreach (Mark value in marks.Values)
		{
			value.Widget = null;
		}
		root = Kit.Rect("pings", (Transform)(object)Overlay.Root);
		Kit.Stretch(root);
		((Transform)root).SetAsFirstSibling();
		return true;
	}

	private static void Draw(Mark k, float age)
	{
		if (!EnsureRoot())
		{
			return;
		}
		if ((Object)(object)k.Widget == (Object)null)
		{
			Build(k);
		}
		GameObject gameObject = ((Component)k.Widget).gameObject;
		if (gameObject.activeSelf != k.Visible)
		{
			gameObject.SetActive(k.Visible);
		}
		if (!k.Visible)
		{
			return;
		}
		RectTransform widget = k.Widget;
		bool flag;
		Vector2 val = default(Vector2);
		if (k.OnItem)
		{
			widget.anchorMin = k.ItemArea.min;
			widget.anchorMax = k.ItemArea.max;
			widget.offsetMin = new Vector2(-4f, -4f);
			widget.offsetMax = new Vector2(4f, 4f);
			flag = k.ItemArea.yMax > 0.86f;
		}
		else
		{
			val = (widget.anchorMin = (widget.anchorMax = k.Spot));
			val = (widget.offsetMin = (widget.offsetMax = Vector2.zero));
			flag = k.Spot.y > 0.86f;
		}
		if (((Component)k.Frame).gameObject.activeSelf != k.OnItem)
		{
			((Component)k.Frame).gameObject.SetActive(k.OnItem);
		}
		float num = (k.OnItem ? 3f : 0f);
		RectTransform pin = k.Pin;
		RectTransform pin2 = k.Pin;
		val = new Vector2(0.5f, flag ? 0f : 1f);
		pin2.anchorMax = val;
		pin.anchorMin = val;
		k.Pin.pivot = new Vector2(0.5f, 0f);
		float num2 = ((age < 0.35f) ? (1f + 0.3f * (1f - age / 0.35f)) : 1f);
		((Transform)k.Pin).localScale = new Vector3(num2, (flag ? (-1f) : 1f) * num2, 1f);
		k.Pin.anchoredPosition = new Vector2(0f, flag ? (0f - num) : num);
		RectTransform tag = k.Tag;
		RectTransform tag2 = k.Tag;
		val = new Vector2(0.5f, flag ? 0f : 1f);
		tag2.anchorMax = val;
		tag.anchorMin = val;
		k.Tag.pivot = new Vector2(0.5f, flag ? 1f : 0f);
		k.Tag.anchoredPosition = new Vector2(0f, flag ? (0f - (num + 30f + 2f)) : (num + 30f + 2f));
		float num3 = ((age > 3.4f) ? Mathf.Clamp01((4f - age) / 0.6f) : 1f);
		for (int i = 0; i < k.Graphics.Length; i++)
		{
			Graphic val4 = k.Graphics[i];
			if (!((Object)(object)val4 == (Object)null))
			{
				Color color = val4.color;
				float num4 = k.BaseAlpha[i] * num3;
				if (Mathf.Abs(color.a - num4) > 0.01f)
				{
					color.a = num4;
					val4.color = color;
				}
			}
		}
	}

	private static void Build(Mark k)
	{
		Color val = Kit.PlayerColor(k.Player);
		Color c = default(Color);
		c = new Color(0.11764706f, 8f / 85f, 0.14901961f, 0.9f);
		RectTransform val2 = Kit.Rect("ping_" + k.Player, (Transform)(object)root);
		val2.pivot = new Vector2(0.5f, 0.5f);
		RectTransform val3 = Kit.Rect("frame", (Transform)(object)val2);
		Kit.Stretch(val3);
		Bars(val3, c, 6f, 1.5f);
		Bars(val3, val, 3f, 0f);
		Image val4 = Kit.Box((Transform)(object)val2, "pin", val);
		val4.sprite = PinSprite();
		((Graphic)val4).rectTransform.sizeDelta = new Vector2(22f, 30f);
		RectTransform content;
		RectTransform tag = Kit.Plate((Transform)(object)val2, "tag", out content, 0.9f, 5, 0f);
		TextMeshProUGUI val5 = Kit.Label((Transform)(object)content, "text", 15f, Kit.Text, 0f, (TextAlignmentOptions)4097);
		Kit.SetText(val5, (k.ItemId != 0L) ? ("<color=#" + Kit.Hex(val) + ">" + k.Name + "</color>") : ("<color=#" + Kit.Hex(val) + ">" + k.Name + "</color> " + Lang.Tr("points here")));
		k.Widget = val2;
		k.Frame = val3;
		k.Pin = ((Graphic)val4).rectTransform;
		k.Tag = tag;
		k.TagText = val5;
		k.Graphics = ((Component)val2).GetComponentsInChildren<Graphic>(true);
		k.BaseAlpha = new float[k.Graphics.Length];
		for (int i = 0; i < k.Graphics.Length; i++)
		{
			k.Graphics[i].raycastTarget = false;
			k.BaseAlpha[i] = k.Graphics[i].color.a;
		}
	}

	private static void Bars(RectTransform parent, Color c, float t, float e)
	{
		for (int i = 0; i < 4; i++)
		{
			RectTransform rectTransform = ((Graphic)Kit.Box((Transform)(object)parent, "bar" + i, c)).rectTransform;
			rectTransform.pivot = new Vector2(0.5f, 0.5f);
			switch (i)
			{
			case 0:
				rectTransform.anchorMin = new Vector2(0f, 1f);
				rectTransform.anchorMax = new Vector2(1f, 1f);
				rectTransform.offsetMin = new Vector2(0f - e, 0f - (t - e));
				rectTransform.offsetMax = new Vector2(e, e);
				break;
			case 1:
				rectTransform.anchorMin = new Vector2(0f, 0f);
				rectTransform.anchorMax = new Vector2(1f, 0f);
				rectTransform.offsetMin = new Vector2(0f - e, 0f - e);
				rectTransform.offsetMax = new Vector2(e, t - e);
				break;
			case 2:
				rectTransform.anchorMin = new Vector2(0f, 0f);
				rectTransform.anchorMax = new Vector2(0f, 1f);
				rectTransform.offsetMin = new Vector2(0f - e, 0f - e);
				rectTransform.offsetMax = new Vector2(t - e, e);
				break;
			default:
				rectTransform.anchorMin = new Vector2(1f, 0f);
				rectTransform.anchorMax = new Vector2(1f, 1f);
				rectTransform.offsetMin = new Vector2(0f - (t - e), 0f - e);
				rectTransform.offsetMax = new Vector2(e, e);
				break;
			}
		}
	}

	internal static Sprite PinSprite()
	{
		if ((Object)(object)pinSprite != (Object)null)
		{
			return pinSprite;
		}
		Vector2 val = default(Vector2);
		val = new Vector2(11f, 10.5f);
		Vector2 val2 = default(Vector2);
		val2 = new Vector2(11f, 29f);
		Vector2 val3 = val2 - val;
		float magnitude = val3.magnitude;
		float num = Mathf.Asin(9.5f / magnitude) * 57.29578f;
		float num2 = num;
		float num3 = 180f - num;
		List<Vector2> list = new List<Vector2>();
		for (float num4 = num3; num4 < num2 + 360f; num4 += 10f)
		{
			float num5 = num4 * (MathF.PI / 180f);
			list.Add(val + new Vector2(Mathf.Cos(num5), Mathf.Sin(num5)) * 9.5f);
		}
		float num6 = (num2 + 360f) * (MathF.PI / 180f);
		list.Add(val + new Vector2(Mathf.Cos(num6), Mathf.Sin(num6)) * 9.5f);
		list.Add(val2);
		Vector2[] poly = list.ToArray();
		Texture2D val4 = new Texture2D(22, 30, (TextureFormat)4, false)
		{
			filterMode = (FilterMode)1,
			wrapMode = (TextureWrapMode)1
		};
		Color32[] array = (Color32[])(object)new Color32[660];
		Color32 val6 = default(Color32);
		for (int i = 0; i < 30; i++)
		{
			for (int j = 0; j < 22; j++)
			{
				Vector2 val5 = new Vector2((float)j + 0.5f, (float)i + 0.5f);
				bool flag = Inside(val5, poly);
				float num7 = EdgeDistance(val5, poly);
				val3 = val5 - val;
				bool flag2 = val3.magnitude < 3.6f;
				if (flag && num7 >= 1.6f && !flag2)
				{
					val6 = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
				}
				else if (flag || num7 < 1.2f)
				{
					val6 = new Color32((byte)30, (byte)24, (byte)38, byte.MaxValue);
				}
				else
				{
					val6 = new Color32((byte)0, (byte)0, (byte)0, (byte)0);
				}
				array[(29 - i) * 22 + j] = val6;
			}
		}
		val4.SetPixels32(array);
		val4.Apply();
		pinSprite = Sprite.Create(val4, new Rect(0f, 0f, 22f, 30f), new Vector2(0.5f, 0f), 100f);
		return pinSprite;
	}

	private static bool Inside(Vector2 p, Vector2[] poly)
	{
		bool flag = false;
		int num = 0;
		int num2 = poly.Length - 1;
		while (num < poly.Length)
		{
			if (poly[num].y > p.y != poly[num2].y > p.y && p.x < (poly[num2].x - poly[num].x) * (p.y - poly[num].y) / (poly[num2].y - poly[num].y) + poly[num].x)
			{
				flag = !flag;
			}
			num2 = num++;
		}
		return flag;
	}

	private static float EdgeDistance(Vector2 p, Vector2[] poly)
	{
		float num = float.MaxValue;
		int num2 = 0;
		int num3 = poly.Length - 1;
		while (num2 < poly.Length)
		{
			Vector2 val = poly[num3];
			Vector2 val2 = poly[num2] - val;
			float num4 = Mathf.Clamp01(Vector2.Dot(p - val, val2) / Mathf.Max(1E-05f, val2.sqrMagnitude));
			float num5 = num;
			Vector2 val3 = val + val2 * num4 - p;
			num = Mathf.Min(num5, val3.magnitude);
			num3 = num2++;
		}
		return num;
	}
}
