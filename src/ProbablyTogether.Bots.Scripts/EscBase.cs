using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.UI;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace ProbablyTogether.Bots.Scripts;

public abstract class EscBase : BotScript
{
	protected static EscapeUIManager Esc
	{
		get
		{
			if (!((Object)(object)StoreUIManager.Instance != (Object)null))
			{
				return null;
			}
			return StoreUIManager.Instance.escapeUIManager;
		}
	}

	protected static bool MenuOpen
	{
		get
		{
			if ((Object)(object)Esc != (Object)null && (Object)(object)Esc.menuPanel != (Object)null)
			{
				return Esc.menuPanel.activeSelf;
			}
			return false;
		}
	}

	protected void Open(string name)
	{
		Do(name, delegate
		{
			Esc.OpenEscapeMenu();
			return "escape menu opened";
		});
		WaitFor(name + "-up", 5f, () => MenuOpen, () => "panel active " + MenuOpen);
		Delay(name + "-layout", 0.5f);
	}

	protected void Close(string name)
	{
		Do(name, delegate
		{
			Esc.CloseEscapeMenu();
			return "escape menu closed";
		});
	}

	protected static string Dump()
	{
		EscapeUIManager esc = Esc;
		object obj;
		if (esc == null)
		{
			obj = null;
		}
		else
		{
			GameObject menuPanel = esc.menuPanel;
			obj = ((menuPanel != null) ? menuPanel.transform : null);
		}
		Transform val = (Transform)obj;
		if ((Object)(object)val == (Object)null)
		{
			return "no menu panel";
		}
		List<string> list = new List<string>();
		RectTransform[] componentsInChildren = ((Component)val).GetComponentsInChildren<RectTransform>(false);
		foreach (RectTransform val2 in componentsInChildren)
		{
			if ((Object)(object)val2 == (Object)(object)val)
			{
				continue;
			}
			TextMeshProUGUI component = ((Component)val2).GetComponent<TextMeshProUGUI>();
			Button component2 = ((Component)val2).GetComponent<Button>();
			bool flag = (Object)(object)((Component)val2).GetComponent<Image>() != (Object)null && (Object)(object)component2 == (Object)null && (Object)(object)((Component)val2).GetComponentInParent<Button>() == (Object)null;
			if ((!((Object)(object)component != (Object)null) || !((Object)(object)((Component)component).GetComponentInParent<Button>() != (Object)null)) && (!((Object)(object)component == (Object)null) || !((Object)(object)component2 == (Object)null) || flag))
			{
				Rect val3 = Screen(val2);
				object obj2;
				if (!((Object)(object)component2 != (Object)null))
				{
					obj2 = (((Object)(object)component != (Object)null) ? ("text '" + Short(((TMP_Text)component).text) + "'") : ("box " + Comps((Transform)(object)val2)));
				}
				else
				{
					TextMeshProUGUI componentInChildren = ((Component)val2).GetComponentInChildren<TextMeshProUGUI>(true);
					obj2 = "'" + ((componentInChildren != null) ? ((TMP_Text)componentInChildren).text : null) + "'";
				}
				string text = (string)obj2;
				list.Add(((Object)val2).name + " " + text + " y" + val3.yMin.ToString("F0") + "-" + val3.yMax.ToString("F0") + " x" + val3.xMin.ToString("F0") + "-" + val3.xMax.ToString("F0"));
			}
		}
		return string.Join(" | ", list);
	}

	private static string Short(string s)
	{
		if (s != null)
		{
			if (s.Length <= 24)
			{
				return s;
			}
			return s.Substring(0, 24) + "...";
		}
		return "";
	}

	private static string Comps(Transform t)
	{
		return string.Join("+", from c in ((Component)t).GetComponents<Component>()
			select ((object)c).GetType().Name into n
			where n != "RectTransform" && n != "CanvasRenderer"
			select n);
	}

	protected static Rect Screen(RectTransform t)
	{
		Vector3[] array = (Vector3[])(object)new Vector3[4];
		t.GetWorldCorners(array);
		return Rect.MinMaxRect(array[0].x, array[0].y, array[2].x, array[2].y);
	}

	protected static List<(string name, Rect r, string label)> Rows()
	{
		List<(string, Rect, string)> list = new List<(string, Rect, string)>();
		EscapeUIManager esc = Esc;
		object obj;
		if (esc == null)
		{
			obj = null;
		}
		else
		{
			GameObject menuPanel = esc.menuPanel;
			obj = ((menuPanel != null) ? menuPanel.transform : null);
		}
		Transform val = (Transform)obj;
		if ((Object)(object)val == (Object)null)
		{
			return list;
		}
		Button[] componentsInChildren = ((Component)val).GetComponentsInChildren<Button>(false);
		foreach (Button val2 in componentsInChildren)
		{
			RectTransform t = (RectTransform)((Component)val2).transform;
			string name = ((Object)((Component)val2).gameObject).name;
			Rect item = Screen(t);
			TextMeshProUGUI componentInChildren = ((Component)val2).GetComponentInChildren<TextMeshProUGUI>(true);
			list.Add((name, item, ((componentInChildren != null) ? ((TMP_Text)componentInChildren).text : null) ?? ""));
		}
		return list.OrderByDescending(((string, Rect, string) x) => x.Item2.yMax).ToList();
	}

	protected static bool FooterOk()
	{
		return FooterWhy().StartsWith("ok", StringComparison.Ordinal);
	}

	protected static string FooterWhy()
	{
		RectTransform footerRect = EscapeMenu.FooterRect;
		RectTransform box = EscapeMenu.Box;
		if ((Object)(object)footerRect == (Object)null || (Object)(object)box == (Object)null)
		{
			return "footer " + ((Object)(object)footerRect != (Object)null) + ", box " + ((Object)(object)box != (Object)null);
		}
		if (!((Component)footerRect).gameObject.activeInHierarchy)
		{
			return "the footer we changed is not on screen";
		}
		Rect val = Screen(footerRect);
		Rect br = Screen(box);
		List<(string, Rect, string)> list = (from x in Rows()
			where br.Contains(x.r.center)
			select x).ToList();
		float num = ((list.Count > 0) ? list.Min<(string, Rect, string)>(((string name, Rect r, string label) x) => x.r.yMin) : float.MaxValue);
		if (val.yMax > num + 1f)
		{
			return "footer " + val.yMin.ToString("F0") + "-" + val.yMax.ToString("F0") + " is not under the last row (" + num.ToString("F0") + ")";
		}
		List<string> list2 = (from t in ((Component)box).GetComponentsInChildren<TextMeshProUGUI>(false)
			where ((TMP_Text)t).text.IndexOf("AUTOSAVE", StringComparison.OrdinalIgnoreCase) >= 0
			select ((Object)t).name).ToList();
		if (Net.IsClient && list2.Count > 0)
		{
			return "still the game's line: " + string.Join(",", list2);
		}
		return "ok: footer " + val.yMin.ToString("F0") + "-" + val.yMax.ToString("F0") + " under the last row " + num.ToString("F0") + ", box " + br.yMin.ToString("F0") + "-" + br.yMax.ToString("F0");
	}

	protected static string Outside()
	{
		RectTransform box = EscapeMenu.Box;
		if ((Object)(object)box == (Object)null)
		{
			return "no box";
		}
		Rect br = Screen(box);
		List<string> list = new List<string>();
		foreach (var item2 in from x in Rows()
			where x.r.center.x > br.xMin && x.r.center.x < br.xMax
			select x)
		{
			Rect item = item2.r;
			if (!(item.yMin < br.yMin - 1f))
			{
				item = item2.r;
				if (!(item.yMax > br.yMax + 1f))
				{
					continue;
				}
			}
			list.Add(item2.name);
		}
		RectTransform footerRect = EscapeMenu.FooterRect;
		if ((Object)(object)footerRect != (Object)null && ((Component)footerRect).gameObject.activeInHierarchy)
		{
			Rect val = Screen(footerRect);
			if (val.yMin < br.yMin - 0.25f * val.height)
			{
				list.Add("footer");
			}
		}
		return string.Join(",", list);
	}

	protected static string Overlaps()
	{
		List<(string, Rect, string)> list = Rows();
		List<string> list2 = new List<string>();
		for (int i = 0; i < list.Count; i++)
		{
			for (int j = i + 1; j < list.Count; j++)
			{
				Rect item = list[i].Item2;
				Rect item2 = list[j].Item2;
				float num = Mathf.Min(item.yMax, item2.yMax) - Mathf.Max(item.yMin, item2.yMin);
				float num2 = Mathf.Min(item.xMax, item2.xMax) - Mathf.Max(item.xMin, item2.xMin);
				if (num > 1f && num2 > 1f)
				{
					list2.Add(list[i].Item1 + "/" + list[j].Item1 + " " + num.ToString("F0") + "px");
				}
			}
		}
		return string.Join(", ", list2);
	}
}
