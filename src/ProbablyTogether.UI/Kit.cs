using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProbablyTogether.UI;

internal static class Kit
{
	internal static readonly Color PlateColor = new Color(0.16078432f, 2f / 15f, 0.20392157f, 0.96f);

	internal static readonly Color Border = new Color(46f / 85f, 0.7490196f, 11f / 15f, 1f);

	internal static readonly Color Text = new Color(0.8509804f, 0.8509804f, 0.8509804f, 1f);

	internal static readonly Color Dim = new Color(0.62f, 0.6f, 0.66f, 1f);

	internal static readonly Color Good = new Color(0.55f, 0.85f, 0.55f, 1f);

	internal static readonly Color Warn = new Color(0.95f, 0.7f, 0.45f, 1f);

	private static readonly RenderHandler.ColorPalette[] playerPalette;

	private static readonly List<TextMeshProUGUI> labels;

	private static TMP_FontAsset font;

	private static TextMeshProUGUI fontSource;

	private static float fontCheckedAt;

	private static readonly Dictionary<string, TMP_FontAsset> fontFor;

	internal static int CjkSwitches;

	internal static string LastCjkFont;

	private static Sprite arrow;

	internal static TMP_FontAsset Font
	{
		get
		{
			if ((Object)(object)font == (Object)null && Time.realtimeSinceStartup - fontCheckedAt > 0.5f)
			{
				RefreshFont();
			}
			return font;
		}
	}

	internal static Color PlayerColor(int id)
	{
		if (id <= 0)
		{
			return Text;
		}
		try
		{
			return (Color32)(RenderHandler.GetColorFromPalette(playerPalette[(id - 1) % playerPalette.Length]));
		}
		catch
		{
			return Text;
		}
	}

	internal static string Hex(Color c)
	{
		return ColorUtility.ToHtmlStringRGB(c);
	}

	internal static Vector2 ToGameArea(Vector2 screen)
	{
		float num = Mathf.Max(1, LetterboxManager.GameAreaWidth);
		float num2 = Mathf.Max(1, LetterboxManager.GameAreaHeight);
		return new Vector2((screen.x - (float)LetterboxManager.GameAreaX) / num, (screen.y - (float)LetterboxManager.GameAreaY) / num2);
	}

	internal static Vector2 ToScreen(Vector2 area)
	{
		return new Vector2((float)LetterboxManager.GameAreaX + area.x * (float)LetterboxManager.GameAreaWidth, (float)LetterboxManager.GameAreaY + area.y * (float)LetterboxManager.GameAreaHeight);
	}

	internal static Vector2? AreaOf(GameObject go)
	{
		if ((Object)(object)go == (Object)null || !go.activeInHierarchy)
		{
			return null;
		}
		try
		{
			Transform transform = go.transform;
			RectTransform val = (RectTransform)(object)((transform is RectTransform) ? transform : null);
			Vector3 val4;
			if (val != null)
			{
				GraphicRaycaster componentInParent = go.GetComponentInParent<GraphicRaycaster>();
				Camera val2 = (((Object)(object)componentInParent != (Object)null) ? ((BaseRaycaster)componentInParent).eventCamera : null);
				Vector3[] array = (Vector3[])(object)new Vector3[4];
				val.GetWorldCorners(array);
				Vector3 val3 = (array[1] + array[2]) * 0.5f;
				val4 = (((Object)(object)val2 != (Object)null) ? val2.WorldToScreenPoint(val3) : val3);
			}
			else
			{
				Camera main = Camera.main;
				if ((Object)(object)main == (Object)null)
				{
					return null;
				}
				Collider2D component = go.GetComponent<Collider2D>();
				if ((Object)(object)component != (Object)null)
				{
					Bounds bounds = component.bounds;
					val4 = main.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
				}
				else
				{
					val4 = main.WorldToScreenPoint(go.transform.position);
				}
				if (val4.z < 0f)
				{
					return null;
				}
			}
			return ToGameArea(new Vector2(val4.x, val4.y));
		}
		catch
		{
			return null;
		}
	}

	internal static RectTransform Rect(string name, Transform parent)
	{
		RectTransform val = (RectTransform)new GameObject(name, new Type[1] { typeof(RectTransform) })
		{
			layer = (((Object)(object)parent != (Object)null) ? ((Component)parent).gameObject.layer : 5)
		}.transform;
		((Transform)val).SetParent(parent, false);
		return val;
	}

	internal static void Stretch(RectTransform r, float inset = 0f)
	{
		r.anchorMin = Vector2.zero;
		r.anchorMax = Vector2.one;
		r.pivot = new Vector2(0.5f, 0.5f);
		r.offsetMin = new Vector2(inset, inset);
		r.offsetMax = new Vector2(0f - inset, 0f - inset);
	}

	internal static void Pin(RectTransform r, Vector2 at, Vector2 offset)
	{
		r.anchorMin = at;
		r.anchorMax = at;
		r.pivot = at;
		r.anchoredPosition = offset;
	}

	internal static Image Box(Transform parent, string name, Color color, bool raycast = false)
	{
		Image obj = ((Component)Rect(name, parent)).gameObject.AddComponent<Image>();
		((Graphic)obj).color = color;
		((Graphic)obj).raycastTarget = raycast;
		return obj;
	}

	internal static RectTransform Plate(Transform parent, string name, out RectTransform content, float alpha = 0.96f, int padding = 10, float spacing = 2f, bool raycast = false)
	{
		Image obj = Box(parent, name, Border, raycast);
		((Graphic)obj).color = new Color(Border.r, Border.g, Border.b, Mathf.Min(1f, alpha + 0.04f));
		RectTransform rectTransform = ((Graphic)obj).rectTransform;
		Image obj2 = Box((Transform)(object)rectTransform, "plate", new Color(PlateColor.r, PlateColor.g, PlateColor.b, alpha), raycast);
		Stretch(((Graphic)obj2).rectTransform, 2f);
		((Component)obj2).gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
		VerticalLayoutGroup obj3 = ((Component)rectTransform).gameObject.AddComponent<VerticalLayoutGroup>();
		((LayoutGroup)obj3).padding = new RectOffset(padding, padding, padding - 2, padding - 2);
		((HorizontalOrVerticalLayoutGroup)obj3).spacing = spacing;
		((HorizontalOrVerticalLayoutGroup)obj3).childControlWidth = true;
		((HorizontalOrVerticalLayoutGroup)obj3).childControlHeight = true;
		((HorizontalOrVerticalLayoutGroup)obj3).childForceExpandWidth = false;
		((HorizontalOrVerticalLayoutGroup)obj3).childForceExpandHeight = false;
		ContentSizeFitter obj4 = ((Component)rectTransform).gameObject.AddComponent<ContentSizeFitter>();
		obj4.horizontalFit = (ContentSizeFitter.FitMode)2;
		obj4.verticalFit = (ContentSizeFitter.FitMode)2;
		content = rectTransform;
		return rectTransform;
	}

	internal static HorizontalLayoutGroup Row(Transform parent, string name, float spacing = 6f)
	{
		HorizontalLayoutGroup obj = ((Component)Rect(name, parent)).gameObject.AddComponent<HorizontalLayoutGroup>();
		((HorizontalOrVerticalLayoutGroup)obj).spacing = spacing;
		((HorizontalOrVerticalLayoutGroup)obj).childControlWidth = true;
		((HorizontalOrVerticalLayoutGroup)obj).childControlHeight = true;
		((HorizontalOrVerticalLayoutGroup)obj).childForceExpandWidth = false;
		((HorizontalOrVerticalLayoutGroup)obj).childForceExpandHeight = false;
		return obj;
	}

	internal static TextMeshProUGUI Label(Transform parent, string name, float size, Color color, float maxWidth = 0f, TextAlignmentOptions align = (TextAlignmentOptions)4097)
	{
		RectTransform val = Rect(name, parent);
		TextMeshProUGUI val2 = ((Component)val).gameObject.AddComponent<TextMeshProUGUI>();
		TMP_FontAsset val3 = Font;
		if ((Object)(object)val3 != (Object)null)
		{
			((TMP_Text)val2).font = val3;
		}
		((TMP_Text)val2).fontSize = size;
		((Graphic)val2).color = color;
		((TMP_Text)val2).alignment = align;
		((Graphic)val2).raycastTarget = false;
		((TMP_Text)val2).richText = true;
		((TMP_Text)val2).enableWordWrapping = maxWidth > 0f;
		((TMP_Text)val2).overflowMode = (TextOverflowModes)0;
		if (maxWidth > 0f)
		{
			((Component)val).gameObject.AddComponent<LayoutElement>().preferredWidth = maxWidth;
		}
		labels.Add(val2);
		return val2;
	}

	internal static Button TextButton(Transform parent, string name, string text, float size, Action onClick, out TextMeshProUGUI label)
	{
		Image val = Box(parent, name, new Color(Border.r, Border.g, Border.b, 0.35f), raycast: true);
		HorizontalLayoutGroup obj = ((Component)val).gameObject.AddComponent<HorizontalLayoutGroup>();
		((LayoutGroup)obj).padding = new RectOffset(8, 8, 2, 2);
		((HorizontalOrVerticalLayoutGroup)obj).childControlWidth = true;
		((HorizontalOrVerticalLayoutGroup)obj).childControlHeight = true;
		((HorizontalOrVerticalLayoutGroup)obj).childForceExpandWidth = false;
		((HorizontalOrVerticalLayoutGroup)obj).childForceExpandHeight = false;
		Button val2 = ((Component)val).gameObject.AddComponent<Button>();
		((Selectable)val2).targetGraphic = (Graphic)(object)val;
		ColorBlock colors = ((Selectable)val2).colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
		colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
		((Selectable)val2).colors = colors;
		label = Label(((Component)val).transform, "text", size, Text, 0f, (TextAlignmentOptions)4097);
		((TMP_Text)label).text = text;
		if (onClick != null)
		{
			((UnityEvent)val2.onClick).AddListener((UnityAction)delegate
			{
				try
				{
					onClick();
				}
				catch (Exception ex)
				{
					Log.Error("ui: button " + name + ": " + ex);
				}
			});
		}
		return val2;
	}

	internal static void Tick()
	{
		if (Time.realtimeSinceStartup - fontCheckedAt > (((Object)(object)fontSource == (Object)null) ? 5f : 1f))
		{
			RefreshFont();
		}
	}

	internal static void RefreshFont()
	{
		fontCheckedAt = Time.realtimeSinceStartup;
		TMP_FontAsset val = null;
		try
		{
			if ((Object)(object)fontSource == (Object)null || !((Component)fontSource).gameObject.activeInHierarchy)
			{
				fontSource = null;
				TMPLocalizer[] array = Object.FindObjectsOfType<TMPLocalizer>();
				foreach (TMPLocalizer val2 in array)
				{
					if (!((Object)(object)val2 == (Object)null) && (int)val2.fontRole == 0)
					{
						TextMeshProUGUI component = ((Component)val2).GetComponent<TextMeshProUGUI>();
						if ((Object)(object)component != (Object)null && (Object)(object)((TMP_Text)component).font != (Object)null)
						{
							fontSource = component;
							break;
						}
					}
				}
			}
			if ((Object)(object)fontSource != (Object)null)
			{
				val = ((TMP_Text)fontSource).font;
			}
			if ((Object)(object)val == (Object)null && (Object)(object)FontLoader.Instance != (Object)null)
			{
				val = FontLoader.Instance.notoSans;
			}
		}
		catch
		{
		}
		if ((Object)(object)val == (Object)null || (Object)(object)val == (Object)(object)font)
		{
			return;
		}
		font = val;
		fontFor.Clear();
		for (int num = labels.Count - 1; num >= 0; num--)
		{
			TextMeshProUGUI val3 = labels[num];
			if ((Object)(object)val3 == (Object)null)
			{
				labels.RemoveAt(num);
			}
			else
			{
				((TMP_Text)val3).font = FontFor(((TMP_Text)val3).text) ?? font;
			}
		}
	}

	internal static void SetText(TextMeshProUGUI t, string text)
	{
		if ((Object)(object)t == (Object)null)
		{
			return;
		}
		text = text ?? "";
		if (!(((TMP_Text)t).text == text))
		{
			((TMP_Text)t).text = text;
			TMP_FontAsset val = FontFor(text);
			if ((Object)(object)val != (Object)null && (Object)(object)((TMP_Text)t).font != (Object)(object)val)
			{
				((TMP_Text)t).font = val;
			}
		}
	}

	internal static TMP_FontAsset FontFor(string text)
	{
		TMP_FontAsset val = Font;
		if ((Object)(object)val == (Object)null || string.IsNullOrEmpty(text))
		{
			return val;
		}
		string text2 = Plain(text);
		bool flag = true;
		string text3 = text2;
		for (int i = 0; i < text3.Length; i++)
		{
			if (text3[i] > '\u007f')
			{
				flag = false;
				break;
			}
		}
		if (flag)
		{
			return val;
		}
		if (fontFor.TryGetValue(text2, out var value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		TMP_FontAsset val2 = val;
		try
		{
			if (!Has(val, text2))
			{
				foreach (TMP_FontAsset item in CjkOrder(text2))
				{
					if ((Object)(object)item != (Object)null && Has(item, text2))
					{
						val2 = item;
						CjkSwitches++;
						LastCjkFont = ((Object)item).name;
						break;
					}
				}
			}
		}
		catch
		{
			val2 = val;
		}
		if (fontFor.Count > 400)
		{
			fontFor.Clear();
		}
		fontFor[text2] = val2;
		return val2;
	}

	internal static void FitFont(TextMeshProUGUI t)
	{
		if ((Object)(object)t == (Object)null || (Object)(object)((TMP_Text)t).font == (Object)null || string.IsNullOrEmpty(((TMP_Text)t).text))
		{
			return;
		}
		string text = Plain(((TMP_Text)t).text);
		bool flag = true;
		string text2 = text;
		for (int i = 0; i < text2.Length; i++)
		{
			if (text2[i] > '\u007f')
			{
				flag = false;
				break;
			}
		}
		if (flag)
		{
			return;
		}
		try
		{
			if (!Has(((TMP_Text)t).font, text))
			{
				TMP_FontAsset val = FontFor(((TMP_Text)t).text);
				if ((Object)(object)val != (Object)null && (Object)(object)val != (Object)(object)((TMP_Text)t).font)
				{
					((TMP_Text)t).font = val;
				}
			}
		}
		catch
		{
		}
	}

	private static bool Has(TMP_FontAsset f, string plain)
	{
		foreach (char c in plain)
		{
			if (c > ' ' && !char.IsSurrogate(c) && c != '\u00a0' && !f.HasCharacter(c, true, true))
			{
				return false;
			}
		}
		return true;
	}

	private static IEnumerable<TMP_FontAsset> CjkOrder(string plain)
	{
		FontLoader fl = FontLoader.Instance;
		if ((Object)(object)fl == (Object)null)
		{
			yield break;
		}
		bool flag = false;
		bool flag2 = false;
		foreach (char c in plain)
		{
			if ((c >= '가' && c <= '힣') || (c >= 'ᄀ' && c <= 'ᇿ') || (c >= '\u3130' && c <= '\u318f'))
			{
				flag = true;
			}
			else if (c >= '\u3040' && c <= 'ヿ')
			{
				flag2 = true;
			}
		}
		if (flag)
		{
			yield return fl.koreanFont;
			yield return fl.simplifiedChineseFont;
			yield return fl.japaneseFont;
		}
		else if (flag2)
		{
			yield return fl.japaneseFont;
			yield return fl.simplifiedChineseFont;
			yield return fl.koreanFont;
		}
		else
		{
			yield return fl.simplifiedChineseFont;
			yield return fl.japaneseFont;
			yield return fl.koreanFont;
		}
	}

	internal static string Plain(string s)
	{
		if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0)
		{
			return s ?? "";
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

	internal static Sprite Arrow()
	{
		if ((Object)(object)arrow != (Object)null)
		{
			return arrow;
		}
		Vector2[] poly = (Vector2[])(object)new Vector2[7]
		{
			new Vector2(1f, 1f),
			new Vector2(1f, 23f),
			new Vector2(6.5f, 18f),
			new Vector2(10.5f, 27.5f),
			new Vector2(14f, 26f),
			new Vector2(10f, 16.8f),
			new Vector2(17.5f, 16.8f)
		};
		Texture2D val = new Texture2D(20, 30, (TextureFormat)4, false)
		{
			filterMode = (FilterMode)1,
			wrapMode = (TextureWrapMode)1
		};
		Color32[] array = (Color32[])(object)new Color32[600];
		Color32 val2 = default(Color32);
		for (int i = 0; i < 30; i++)
		{
			for (int j = 0; j < 20; j++)
			{
				Vector2 p = new Vector2((float)j + 0.5f, (float)i + 0.5f);
				float num = EdgeDistance(p, poly);
				bool flag = Inside(p, poly);
				if (flag && num >= 1.6f)
				{
					val2 = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
				}
				else if (flag || num < 1.2f)
				{
					val2 = new Color32((byte)30, (byte)24, (byte)38, byte.MaxValue);
				}
				else
				{
					val2 = new Color32((byte)0, (byte)0, (byte)0, (byte)0);
				}
				array[(29 - i) * 20 + j] = val2;
			}
		}
		val.SetPixels32(array);
		val.Apply();
		arrow = Sprite.Create(val, new Rect(0f, 0f, 20f, 30f), new Vector2(0f, 1f), 100f);
		return arrow;
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

	internal static string Name(string n)
	{
		if (!string.IsNullOrEmpty(n))
		{
			return n.Replace("<", "‹").Replace(">", "›");
		}
		return "?";
	}

	internal static string Safe(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return s.Replace('<', '‹');
		}
		return "";
	}

	static Kit()
	{
		playerPalette = new RenderHandler.ColorPalette[6]
		{
			RenderHandler.ColorPalette.LightBlue, RenderHandler.ColorPalette.LightRed, RenderHandler.ColorPalette.LightGreen,
			RenderHandler.ColorPalette.LightYellow, RenderHandler.ColorPalette.LightPurple, RenderHandler.ColorPalette.LightOrange
		};
		labels = new List<TextMeshProUGUI>();
		fontCheckedAt = -10f;
		fontFor = new Dictionary<string, TMP_FontAsset>();
		LastCjkFont = "";
	}
}
