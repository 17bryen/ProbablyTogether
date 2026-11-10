using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ProbablyTogether.Systems;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace ProbablyTogether.UI;

public static class CrewPanel
{
	internal const string RootName = "bellun_probably_together_crew";

	private const float Gap = 24f;

	private const float Margin = 12f;

	private const float RosterClear = 260f;

	private const float LabelWidth = 168f;

	private const float CellWidth = 86f;

	private const float Padding = 12f;

	private const float MinScale = 0.6f;

	private const int MaxColumns = 6;

	private static readonly (string label, Func<MsgCrewRow, int> get, bool money)[] Lines = new(string, Func<MsgCrewRow, int>, bool)[9]
	{
		("Customers served", (MsgCrewRow r) => r.deals, false),
		("Items sold", (MsgCrewRow r) => r.sold, false),
		("Money made", (MsgCrewRow r) => r.earned, true),
		("Items bought", (MsgCrewRow r) => r.bought, false),
		("Money spent", (MsgCrewRow r) => r.spent, true),
		("Stolen goods caught", (MsgCrewRow r) => r.caught, false),
		("Repairs", (MsgCrewRow r) => r.repairs, false),
		("Machine uses", (MsgCrewRow r) => r.uses, false),
		("Items made", (MsgCrewRow r) => r.made, false)
	};

	private static RectTransform root;

	private static RectTransform plate;

	private static string builtKey = "";

	private static float readAt = -10f;

	private static bool wasOpen;

	internal static MsgCrewTally LabPreview;

	public static bool Visible { get; private set; }

	public static string Text { get; private set; } = "";

	public static string ShownLines { get; private set; } = "";

	public static string Anchor { get; private set; } = "";

	public static int Shown { get; private set; }

	public static int PerBlock { get; private set; }

	public static Rect? PlateArea { get; private set; }

	public static Rect? TabletRect { get; private set; }

	private static bool Headless
	{
		get
		{
			if (!Args.Server)
			{
				return Net.IsDedicated;
			}
			return true;
		}
	}

	[SystemTick]
	internal static void Tick()
	{
		if (Headless || !Net.HasLocalPlayer)
		{
			Hide();
			return;
		}
		try
		{
			ClientTick();
		}
		catch (Exception e)
		{
			UiErrors.Report("crew panel", e);
		}
	}

	private static void ClientTick()
	{
		EndOfDayUIManager val = (Ui.ShopOnline ? EndOfDayUIManager.Instance : null);
		MsgCrewTally msgCrewTally = (((Object)(object)val != (Object)null && val.IsOpen) ? (LabPreview ?? Crew.Today) : null);
		if (msgCrewTally == null)
		{
			Hide();
			wasOpen = false;
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (!EnsureRoot())
		{
			return;
		}
		if (realtimeSinceStartup - readAt >= 0.25f || !wasOpen || (Object)(object)plate == (Object)null)
		{
			readAt = realtimeSinceStartup;
			Rect? val2 = TabletArea(val);
			int per = FitColumns(val2);
			string text = ((msgCrewTally == LabPreview) ? "lab#" : "") + msgCrewTally.day + "#" + msgCrewTally.seq + "#" + msgCrewTally.rows.Count + "#" + per + "#" + Lang.Version;
			if (text != builtKey || (Object)(object)plate == (Object)null)
			{
				Build(msgCrewTally, text, per);
			}
			if (!((Component)root).gameObject.activeSelf)
			{
				((Component)root).gameObject.SetActive(true);
			}
			bool num = Place(val2, !wasOpen);
			if (num && !Visible)
			{
				Shown++;
			}
			Visible = num;
		}
		wasOpen = true;
	}

	private static void Hide()
	{
		if ((Object)(object)root != (Object)null && ((Component)root).gameObject.activeSelf)
		{
			((Component)root).gameObject.SetActive(false);
		}
		Visible = false;
		PlateArea = null;
		Text = "";
		builtKey = "";
	}

	private static bool EnsureRoot()
	{
		if ((Object)(object)root != (Object)null)
		{
			return true;
		}
		CustomUIManager instance = CustomUIManager.Instance;
		RectTransform val = (((Object)(object)instance != (Object)null) ? instance.GetLayerRoot("overlay") : null);
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}
		Transform val2 = ((Transform)val).Find("bellun_probably_together_crew");
		if ((Object)(object)val2 != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)val2).gameObject);
		}
		root = Kit.Rect("bellun_probably_together_crew", (Transform)(object)val);
		Kit.Stretch(root);
		((Transform)root).SetSiblingIndex(Math.Min(1, ((Transform)val).childCount - 1));
		builtKey = "";
		return true;
	}

	private static int FitColumns(Rect? area)
	{
		if (!area.HasValue || (Object)(object)root == (Object)null)
		{
			return 6;
		}
		Rect rect = root.rect;
		float rw = Mathf.Max(1f, rect.width);
		return Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(RightRoom(area.Value, rw), LeftRoom(area.Value, rw)) - 24f - 168f) / 86f), 1, 6);
	}

	private static float RightRoom(Rect tablet, float rw)
	{
		return (1f - tablet.xMax) * rw - 24f - 12f;
	}

	private static float LeftRoom(Rect tablet, float rw)
	{
		return tablet.xMin * rw - 24f - 12f;
	}

	private static void Build(MsgCrewTally t, string key, int per)
	{
		if ((Object)(object)plate != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)plate).gameObject);
		}
		builtKey = key;
		plate = Kit.Plate((Transform)(object)root, "crew", out var content, 0.94f, 12);
		Kit.Pin(plate, new Vector2(1f, 0.5f), new Vector2(-16f, 0f));
		StringBuilder stringBuilder = new StringBuilder();
		List<MsgCrewRow> cols = t.rows.Where((MsgCrewRow r) => Crew.Active(r) || r.on).Take(6).ToList();
		int num = t.rows.Count((MsgCrewRow r) => Crew.Active(r) || r.on) - cols.Count;
		string value = "Crew report - day " + t.day.ToString(CultureInfo.InvariantCulture) + ((num > 0) ? ("  (+" + num + " more)") : "");
		Kit.SetText(Kit.Label((Transform)(object)content, "title", 18f, Kit.Text, 0f, (TextAlignmentOptions)4097), (num > 0) ? Lang.Trf("Crew report - day {0}  (+{1} more)", t.day, num) : Lang.Trf("Crew report - day {0}", t.day));
		stringBuilder.Append(value);
		List<(string, Func<MsgCrewRow, int>, bool)> list = Lines.Where(((string label, Func<MsgCrewRow, int> get, bool money) l) => cols.Any((MsgCrewRow r) => l.get(r) != 0)).ToList();
		ShownLines = string.Join(",", list.Select<(string, Func<MsgCrewRow, int>, bool), string>(((string label, Func<MsgCrewRow, int> get, bool money) l) => l.label));
		per = Mathf.Clamp(per, 1, 6);
		PerBlock = Math.Min(per, Math.Max(1, cols.Count));
		float val = 168f + 86f * (float)PerBlock;
		if (list.Count == 0)
		{
			string text = "A quiet day: nothing sold, bought, made or repaired.";
			Kit.SetText(Kit.Label((Transform)(object)content, "quiet", 15f, Kit.Dim, Math.Max(260f, val), (TextAlignmentOptions)4097), Lang.Tr(text));
			stringBuilder.Append('\n').Append(text);
		}
		else
		{
			for (int num2 = 0; num2 < cols.Count; num2 += PerBlock)
			{
				List<MsgCrewRow> list2 = cols.Skip(num2).Take(PerBlock).ToList();
				if (num2 > 0)
				{
					LayoutElement obj = ((Component)Kit.Rect("block-gap", (Transform)(object)content)).gameObject.AddComponent<LayoutElement>();
					obj.minHeight = 8f;
					obj.preferredHeight = 8f;
				}
				Transform transform = ((Component)Kit.Row((Transform)(object)content, "names", 0f)).transform;
				Cell(transform, "", 168f, Kit.Dim, right: false);
				stringBuilder.Append("\n|");
				foreach (MsgCrewRow item in list2)
				{
					Color c = (item.on ? Kit.PlayerColor(item.p) : Kit.Dim);
					Cell(transform, item.on ? Kit.Name(item.n) : (Kit.Name(item.n) + "\n<size=85%>" + Lang.Tr("(left)") + "</size>"), 86f, c, right: true);
					stringBuilder.Append(Kit.Name(item.n)).Append(item.on ? "" : " (left)").Append('|');
				}
				foreach (var item2 in list)
				{
					Transform transform2 = ((Component)Kit.Row((Transform)(object)content, "line", 0f)).transform;
					Cell(transform2, Lang.Tr(item2.Item1), 168f, Kit.Dim, right: false);
					stringBuilder.Append('\n').Append(item2.Item1).Append('|');
					foreach (MsgCrewRow item3 in list2)
					{
						int num3 = item2.Item2(item3);
						string s = ((num3 == 0) ? "-" : (item2.Item3 ? TabletUI.Credits((long)num3) : num3.ToString(CultureInfo.InvariantCulture)));
						Cell(transform2, s, 86f, (num3 == 0) ? Kit.Dim : Kit.Text, right: true);
						stringBuilder.Append(num3.ToString(CultureInfo.InvariantCulture)).Append('|');
					}
				}
			}
		}
		if (t.best != null && t.best.gain > 0)
		{
			string text2 = (string.IsNullOrEmpty(t.best.who) ? "" : (" with " + Kit.Safe(t.best.who)));
			string text3 = ((t.best.items > 1) ? (", " + t.best.items.ToString(CultureInfo.InvariantCulture) + " items") : "");
			string english = "Best deal: " + Kit.Name(t.best.n) + " +" + TabletUI.Credits((long)t.best.gain) + " over the first price" + text2 + text3;
			Kit.SetText(Kit.Label((Transform)(object)content, "best", 15f, Kit.Good, Math.Max(260f, val), (TextAlignmentOptions)4097), BestShown(t.best, english));
			stringBuilder.Append('\n').Append("Best deal: ").Append(Kit.Name(t.best.n))
				.Append(" +")
				.Append(t.best.gain.ToString(CultureInfo.InvariantCulture))
				.Append(" over the first price")
				.Append(text2)
				.Append(text3);
		}
		Text = stringBuilder.ToString();
		LayoutRebuilder.ForceRebuildLayoutImmediate(plate);
	}

	private static string BestShown(MsgCrewBest b, string english)
	{
		if (Lang.Code == "en")
		{
			return english;
		}
		string text = Kit.Name(b.n);
		string text2 = TabletUI.Credits((long)b.gain);
		string text3 = (string.IsNullOrEmpty(b.who) ? null : Kit.Safe(b.who));
		string text4 = b.items.ToString(CultureInfo.InvariantCulture);
		if (text3 != null && b.items > 1)
		{
			return Lang.Trf("Best deal: {0} +{1} over the first price with {2}, {3} items", text, text2, text3, text4);
		}
		if (text3 != null)
		{
			return Lang.Trf("Best deal: {0} +{1} over the first price with {2}", text, text2, text3);
		}
		if (b.items > 1)
		{
			return Lang.Trf("Best deal: {0} +{1} over the first price, {2} items", text, text2, text4);
		}
		return Lang.Trf("Best deal: {0} +{1} over the first price", text, text2);
	}

	private static void Cell(Transform row, string s, float w, Color c, bool right)
	{
		TextMeshProUGUI obj = Kit.Label(row, "cell", 15f, c, 0f, (TextAlignmentOptions)(right ? 4100 : 4097));
		((TMP_Text)obj).enableWordWrapping = false;
		((TMP_Text)obj).overflowMode = (TextOverflowModes)1;
		LayoutElement obj2 = ((Component)obj).gameObject.AddComponent<LayoutElement>();
		obj2.preferredWidth = w;
		obj2.minWidth = w;
		Kit.SetText(obj, s);
	}

	private static bool Place(Rect? a, bool log)
	{
		if ((Object)(object)plate == (Object)null || (Object)(object)root == (Object)null)
		{
			return false;
		}
		Rect val = root.rect;
		float rw = Mathf.Max(1f, val.width);
		val = root.rect;
		float num = Mathf.Max(1f, val.height);
		val = plate.rect;
		float num2 = Mathf.Max(1f, val.width);
		val = plate.rect;
		float num3 = Mathf.Max(1f, val.height);
		float num4 = 1f;
		TabletRect = a;
		Vector2 val2 = default(Vector2);
		Vector2 pivot = default(Vector2);
		Vector2 anchoredPosition = default(Vector2);
		string text2;
		if (a.HasValue)
		{
			Rect value = a.Value;
			float num5 = RightRoom(value, rw);
			float num6 = LeftRoom(value, rw);
			bool flag = num2 <= num5 || (num2 > num6 && num5 >= num6);
			float num7 = (flag ? num5 : num6);
			num4 = Mathf.Min(new float[3]
			{
				1f,
				num7 / num2,
				(num - 24f) / num3
			});
			if (num4 < 0.6f)
			{
				if (((Component)plate).gameObject.activeSelf)
				{
					((Component)plate).gameObject.SetActive(false);
				}
				PlateArea = null;
				string text = "hidden: no room beside the tablet (" + num2.ToString("F0", CultureInfo.InvariantCulture) + "x" + num3.ToString("F0", CultureInfo.InvariantCulture) + " needs scale " + num4.ToString("F2", CultureInfo.InvariantCulture) + ")";
				if (log || Anchor != text)
				{
					Log.Info("ui: crew panel " + text);
				}
				Anchor = text;
				return false;
			}
			float num8 = num3 * num4 / num;
			if (flag)
			{
				float num9 = Mathf.Min(value.yMax, 1f - 260f / num);
				num9 = Mathf.Max(num9, num8 + 12f / num);
				val2 = new Vector2(value.xMax, num9);
				pivot = new Vector2(0f, 1f);
				anchoredPosition = new Vector2(24f, 0f);
				text2 = "right of the tablet";
			}
			else
			{
				float num10 = Mathf.Max(Mathf.Min(value.yMax, 1f - 12f / num), num8 + 12f / num);
				val2 = new Vector2(value.xMin, num10);
				pivot = new Vector2(1f, 1f);
				anchoredPosition = new Vector2(-24f, 0f);
				text2 = "left of the tablet";
			}
		}
		else
		{
			val2 = new Vector2(1f, 0.5f);
			pivot = val2;
			anchoredPosition = new Vector2(-16f, 0f);
			text2 = "right edge (tablet not measurable)";
		}
		if (!((Component)plate).gameObject.activeSelf)
		{
			((Component)plate).gameObject.SetActive(true);
		}
		((Transform)plate).localScale = new Vector3(num4, num4, 1f);
		plate.anchorMin = val2;
		plate.anchorMax = val2;
		plate.pivot = pivot;
		plate.anchoredPosition = anchoredPosition;
		PlateArea = AreaOf(plate);
		string[] obj = new string[14]
		{
			text2,
			" at ",
			val2.x.ToString("F3", CultureInfo.InvariantCulture),
			",",
			val2.y.ToString("F3", CultureInfo.InvariantCulture),
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		object obj2;
		if (!a.HasValue)
		{
			obj2 = "";
		}
		else
		{
			string[] obj3 = new string[8] { "; tablet ", null, null, null, null, null, null, null };
			val = a.Value;
			obj3[1] = val.xMin.ToString("F3", CultureInfo.InvariantCulture);
			obj3[2] = "..";
			val = a.Value;
			obj3[3] = val.xMax.ToString("F3", CultureInfo.InvariantCulture);
			obj3[4] = " x ";
			val = a.Value;
			obj3[5] = val.yMin.ToString("F3", CultureInfo.InvariantCulture);
			obj3[6] = "..";
			val = a.Value;
			obj3[7] = val.yMax.ToString("F3", CultureInfo.InvariantCulture);
			obj2 = string.Concat(obj3);
		}
		obj[5] = (string)obj2;
		obj[6] = "; panel ";
		obj[7] = num2.ToString("F0", CultureInfo.InvariantCulture);
		obj[8] = "x";
		obj[9] = num3.ToString("F0", CultureInfo.InvariantCulture);
		obj[10] = ((num4 < 1f) ? (" scaled " + num4.ToString("F2", CultureInfo.InvariantCulture)) : "");
		obj[11] = ", ";
		obj[12] = PerBlock.ToString();
		obj[13] = " player(s) a block";
		Anchor = string.Concat(obj);
		if (log)
		{
			Log.Info("ui: crew panel shown (" + Anchor + ")");
		}
		return true;
	}

	private static Rect? TabletArea(EndOfDayUIManager eod)
	{
		try
		{
			GameObject val = (((Object)(object)eod != (Object)null) ? eod.UI : null);
			if ((Object)(object)val == (Object)null)
			{
				return null;
			}
			EndOfDayTabletFrame component = val.GetComponent<EndOfDayTabletFrame>();
			Rect? result = AreaOf(((Object)(object)component != (Object)null && (Object)(object)((TabletFrame)component).panel != (Object)null) ? (((TabletFrame)component).panel as RectTransform) : null);
			if (!result.HasValue)
			{
				return null;
			}
			Rect? val2 = DealTabletArea();
			if (!val2.HasValue)
			{
				return result;
			}
			Rect value = result.Value;
			Rect value2 = val2.Value;
			return Rect.MinMaxRect(Mathf.Min(value.xMin, value2.xMin), Mathf.Min(value.yMin, value2.yMin), Mathf.Max(value.xMax, value2.xMax), Mathf.Max(value.yMax, value2.yMax));
		}
		catch
		{
			return null;
		}
	}

	private static Rect? DealTabletArea()
	{
		NegociationUIManager instance = NegociationUIManager.Instance;
		if ((Object)(object)instance == (Object)null || !instance.IsOpen || (Object)(object)instance.uiObject == (Object)null)
		{
			return null;
		}
		TabletFrame componentInChildren = instance.uiObject.GetComponentInChildren<TabletFrame>(true);
		return AreaOf(((Object)(object)componentInChildren != (Object)null && (Object)(object)componentInChildren.panel != (Object)null) ? (componentInChildren.panel as RectTransform) : null);
	}

	private static Rect? AreaOf(RectTransform rt)
	{
		try
		{
			if ((Object)(object)rt == (Object)null || !((Component)rt).gameObject.activeInHierarchy)
			{
				return null;
			}
			Canvas componentInParent = ((Component)rt).GetComponentInParent<Canvas>();
			Camera val = null;
			if ((Object)(object)componentInParent != (Object)null && (int)componentInParent.renderMode != 0)
			{
				val = componentInParent.worldCamera;
				if ((Object)(object)val == (Object)null)
				{
					val = Camera.main;
				}
			}
			Vector3[] array = (Vector3[])(object)new Vector3[4];
			rt.GetWorldCorners(array);
			float num = float.MaxValue;
			float num2 = float.MaxValue;
			float num3 = float.MinValue;
			float num4 = float.MinValue;
			Vector3[] array2 = array;
			foreach (Vector3 val2 in array2)
			{
				Vector2 val3 = Kit.ToGameArea(((Object)(object)val != (Object)null) ? (Vector2)(val.WorldToScreenPoint(val2)) : (Vector2)(val2));
				num = Mathf.Min(num, val3.x);
				num2 = Mathf.Min(num2, val3.y);
				num3 = Mathf.Max(num3, val3.x);
				num4 = Mathf.Max(num4, val3.y);
			}
			if (!(num3 > num) || !(num4 > num2) || float.IsNaN(num) || float.IsNaN(num2))
			{
				return null;
			}
			return Rect.MinMaxRect(num, num2, num3, num4);
		}
		catch
		{
			return null;
		}
	}
}
