using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProbablyTogether.UI;

internal static class EscapeMenu
{
	internal const string InviteName = "humangenome_accomplice_invite";

	internal const string InviteText = "Copy invite";

	internal const string LeaveText = "Leave server";

	internal const string FooterText = "The server saves the shop after each night.";

	internal const string CopiedLine = "Server address copied. Send it to a friend. The password is not included: tell them yourself if there is one.";

	private static GameObject invite;

	private static RectTransform stackBox;

	private static RectTransform stackRow;

	private static RectTransform grownBox;

	private static TextMeshProUGUI mainLabel;

	private static TextMeshProUGUI footer;

	private static string mainOriginal;

	private static string footerOriginal;

	private static readonly List<Behaviour> silenced = new List<Behaviour>();

	private static EscapeUIManager owner;

	internal static string InviteLabel
	{
		get
		{
			if (!((Object)(object)invite != (Object)null) || !invite.activeSelf)
			{
				return "";
			}
			TextMeshProUGUI obj = Label(invite);
			return ((obj != null) ? ((TMP_Text)obj).text : null) ?? "";
		}
	}

	internal static string MainMenuLabel
	{
		get
		{
			if (!((Object)(object)mainLabel != (Object)null))
			{
				return "";
			}
			return ((TMP_Text)mainLabel).text;
		}
	}

	internal static string FooterLabel
	{
		get
		{
			if (!((Object)(object)footer != (Object)null))
			{
				return "";
			}
			return ((TMP_Text)footer).text;
		}
	}

	internal static bool InviteShown
	{
		get
		{
			if ((Object)(object)invite != (Object)null)
			{
				return invite.activeInHierarchy;
			}
			return false;
		}
	}

	internal static string LastCopied { get; private set; } = "";

	internal static int Copies { get; private set; }

	internal static string Layout { get; private set; } = "";

	internal static RectTransform Box { get; private set; }

	internal static RectTransform FooterRect
	{
		get
		{
			if (!((Object)(object)footer != (Object)null))
			{
				return null;
			}
			return ((TMP_Text)footer).rectTransform;
		}
	}

	internal static float Grown { get; private set; }

	internal static void OnOpened(EscapeUIManager esc)
	{
		if (!((Object)(object)esc == (Object)null) && !((Object)(object)esc.menuPanel == (Object)null))
		{
			if ((Object)(object)owner != (Object)(object)esc)
			{
				Forget(esc);
			}
			if ((Object)(object)mainLabel == (Object)null || (Object)(object)footer == (Object)null)
			{
				Find(esc);
			}
			if (Net.IsClient && Net.IsOnline)
			{
				Apply(esc);
			}
			else
			{
				Restore();
			}
		}
	}

	private static void Forget(EscapeUIManager esc)
	{
		owner = esc;
		invite = null;
		mainLabel = null;
		footer = null;
		mainOriginal = null;
		footerOriginal = null;
		stackBox = null;
		stackRow = null;
		grownBox = null;
		Box = null;
		silenced.Clear();
	}

	private static void Find(EscapeUIManager esc)
	{
		Transform transform = esc.menuPanel.transform;
		Button[] componentsInChildren = ((Component)transform).GetComponentsInChildren<Button>(true);
		foreach (Button val in componentsInChildren)
		{
			if (!((Object)(object)val == (Object)null) && !(((Object)((Component)val).gameObject).name == "humangenome_accomplice_invite") && Calls(val, "OnMainMenu") && (Object)(object)mainLabel == (Object)null)
			{
				mainLabel = ((Component)val).GetComponentInChildren<TextMeshProUGUI>(true);
				TextMeshProUGUI obj = mainLabel;
				mainOriginal = ((obj != null) ? ((TMP_Text)obj).text : null);
			}
		}
		Transform val2 = null;
		if ((Object)(object)mainLabel != (Object)null)
		{
			Button componentInParent = ((Component)mainLabel).GetComponentInParent<Button>(true);
			Transform val3 = (((Object)(object)componentInParent != (Object)null) ? StackChild(transform, ((Component)componentInParent).transform) : null);
			val2 = (((Object)(object)val3 != (Object)null) ? val3.parent : null);
		}
		float num = float.MaxValue;
		TextMeshProUGUI[] componentsInChildren2 = ((Component)(val2 ?? transform)).GetComponentsInChildren<TextMeshProUGUI>(false);
		foreach (TextMeshProUGUI val4 in componentsInChildren2)
		{
			if (!((Object)(object)val4 == (Object)null) && !InButton(((TMP_Text)val4).transform))
			{
				Vector3[] array = (Vector3[])(object)new Vector3[4];
				((TMP_Text)val4).rectTransform.GetWorldCorners(array);
				if (array[0].y < num)
				{
					num = array[0].y;
					footer = val4;
				}
			}
		}
		Box = (RectTransform)(object)((val2 is RectTransform) ? val2 : null);
		TextMeshProUGUI obj2 = footer;
		footerOriginal = ((obj2 != null) ? ((TMP_Text)obj2).text : null);
		Log.Info("ui: escape menu: main menu label " + (((Object)(object)mainLabel != (Object)null) ? ("'" + mainOriginal + "'") : "not found") + ", footer " + (((Object)(object)footer != (Object)null) ? ("'" + footerOriginal + "'") : "not found"));
	}

	private static void Apply(EscapeUIManager esc)
	{
		try
		{
			if ((Object)(object)invite == (Object)null)
			{
				invite = BuildInvite(esc);
			}
			if ((Object)(object)invite != (Object)null)
			{
				invite.SetActive(true);
				if ((Object)(object)grownBox == (Object)null && (Object)(object)stackBox != (Object)null)
				{
					Grow(stackBox, stackRow);
				}
				TextMeshProUGUI val = Label(invite);
				if ((Object)(object)val != (Object)null)
				{
					SetGameText(val, Lang.Tr("Copy invite"));
				}
			}
			if ((Object)(object)mainLabel != (Object)null)
			{
				Silence(mainLabel);
				SetGameText(mainLabel, Lang.Tr("Leave server"));
			}
			if ((Object)(object)footer != (Object)null)
			{
				Silence(footer);
				string s = Lang.Tr("The server saves the shop after each night.");
				if (((int)((TMP_Text)footer).fontStyle & 0x10) == 0 && footerOriginal != null && footerOriginal == footerOriginal.ToUpperInvariant())
				{
					s = Lang.Upper(s);
				}
				SetGameText(footer, s);
			}
		}
		catch (Exception e)
		{
			UiErrors.Report("escape menu", e);
		}
	}

	private static void Restore()
	{
		try
		{
			if ((Object)(object)invite != (Object)null)
			{
				invite.SetActive(false);
			}
			if ((Object)(object)grownBox != (Object)null)
			{
				RectTransform obj = grownBox;
				obj.sizeDelta -= new Vector2(0f, Grown);
				LayoutRebuilder.ForceRebuildLayoutImmediate(grownBox);
				grownBox = null;
			}
			foreach (Behaviour item in silenced)
			{
				if ((Object)(object)item != (Object)null)
				{
					item.enabled = true;
				}
			}
			bool num = silenced.Count > 0;
			silenced.Clear();
			if (!num)
			{
				if ((Object)(object)mainLabel != (Object)null && mainOriginal != null)
				{
					((TMP_Text)mainLabel).text = mainOriginal;
				}
				if ((Object)(object)footer != (Object)null && footerOriginal != null)
				{
					((TMP_Text)footer).text = footerOriginal;
				}
			}
		}
		catch (Exception e)
		{
			UiErrors.Report("escape menu restore", e);
		}
	}

	private static void Silence(TextMeshProUGUI t)
	{
		Transform val = ((TMP_Text)t).transform;
		while ((Object)(object)val != (Object)null)
		{
			Transform obj = val;
			EscapeUIManager obj2 = owner;
			if (!((Object)(object)obj != (Object)(object)((obj2 != null) ? obj2.menuPanel.transform.parent : null)))
			{
				break;
			}
			MonoBehaviour[] components = ((Component)val).GetComponents<MonoBehaviour>();
			foreach (MonoBehaviour val2 in components)
			{
				if ((Object)(object)val2 != (Object)null && ((Behaviour)val2).enabled && ((object)val2).GetType().Name == "LocalizeStringEvent")
				{
					((Behaviour)val2).enabled = false;
					silenced.Add((Behaviour)(object)val2);
				}
			}
			if (!((Object)(object)((Component)val).GetComponent<Button>() != (Object)null))
			{
				Transform obj3 = val;
				EscapeUIManager obj4 = owner;
				if (!((Object)(object)obj3 == (Object)(object)((obj4 != null) ? obj4.menuPanel.transform : null)))
				{
					val = val.parent;
					continue;
				}
				break;
			}
			break;
		}
	}

	private static void SetGameText(TextMeshProUGUI t, string s)
	{
		if (((TMP_Text)t).text != s)
		{
			((TMP_Text)t).text = s;
		}
		Kit.FitFont(t);
	}

	private static GameObject BuildInvite(EscapeUIManager esc)
	{
		Transform transform = esc.menuPanel.transform;
		Button val = null;
		Button val2 = null;
		Button[] componentsInChildren = ((Component)transform).GetComponentsInChildren<Button>(true);
		foreach (Button val3 in componentsInChildren)
		{
			if (!((Object)(object)val3 == (Object)null) && !(((Object)((Component)val3).gameObject).name == "humangenome_accomplice_invite"))
			{
				if (Calls(val3, "OnOption"))
				{
					val = val3;
				}
				if (Calls(val3, "OnMainMenu"))
				{
					val2 = val3;
				}
			}
		}
		if ((Object)(object)val == (Object)null || (Object)(object)val2 == (Object)null)
		{
			Log.Warn("ui: escape menu: no Options or Main Menu button to copy; no Copy invite");
			return null;
		}
		Transform val4 = StackChild(transform, ((Component)val).transform);
		Transform val5 = StackChild(transform, ((Component)val2).transform);
		if ((Object)(object)val4 == (Object)null || (Object)(object)val5 == (Object)null)
		{
			Log.Warn("ui: escape menu: buttons are not in one stack; no Copy invite");
			return null;
		}
		GameObject val6 = Object.Instantiate<GameObject>(((Component)val4).gameObject, val4.parent);
		((Object)val6).name = "humangenome_accomplice_invite";
		MonoBehaviour[] componentsInChildren2 = val6.GetComponentsInChildren<MonoBehaviour>(true);
		foreach (MonoBehaviour val7 in componentsInChildren2)
		{
			if ((Object)(object)val7 != (Object)null && ((object)val7).GetType().Name == "LocalizeStringEvent")
			{
				Object.Destroy((Object)(object)val7);
			}
		}
		Button componentInChildren = val6.GetComponentInChildren<Button>(true);
		componentInChildren.onClick = new Button.ButtonClickedEvent();
		((UnityEvent)componentInChildren.onClick).AddListener((UnityAction)delegate
		{
			try
			{
				esc.OnAnyClick();
			}
			catch
			{
			}
			CopyInvite();
		});
		val6.transform.SetSiblingIndex(val5.GetSiblingIndex());
		Transform parent = val4.parent;
		bool flag = (Object)(object)parent != (Object)null && (Object)(object)((Component)parent).GetComponent<LayoutGroup>() != (Object)null;
		if (!flag)
		{
			Transform src = ((val4 is RectTransform) ? val4 : null);
			Transform main = ((val5 is RectTransform) ? val5 : null);
			Transform transform2 = val6.transform;
			Make_Room(parent, (RectTransform)(object)src, (RectTransform)(object)main, (RectTransform)(object)((transform2 is RectTransform) ? transform2 : null));
		}
		else if ((Object)(object)((Component)parent).GetComponent<ContentSizeFitter>() == (Object)null)
		{
			stackBox = (RectTransform)(object)((parent is RectTransform) ? parent : null);
			stackRow = (RectTransform)(object)((val4 is RectTransform) ? val4 : null);
		}
		Layout = (flag ? ("in the menu's layout group (" + ((object)((Component)parent).GetComponent<LayoutGroup>()).GetType().Name + (((Object)(object)stackBox != (Object)null) ? ", fixed-height box: grows one row while it shows" : ", sized by its fitter") + ")") : ("placed by hand: " + Layout)) + "; copied from '" + ((Object)val4).name + "', above '" + ((Object)val5).name + "'";
		Log.Info("ui: escape menu: Copy invite added " + Layout);
		return val6;
	}

	private static void Make_Room(Transform parent, RectTransform src, RectTransform main, RectTransform copy)
	{
		if ((Object)(object)parent == (Object)null || (Object)(object)src == (Object)null || (Object)(object)main == (Object)null || (Object)(object)copy == (Object)null)
		{
			Layout = "nothing to measure";
			return;
		}
		float num = Mathf.Abs(src.anchoredPosition.y - main.anchoredPosition.y);
		int num2 = Mathf.Abs(((Transform)main).GetSiblingIndex() - ((Transform)src).GetSiblingIndex());
		if (num2 > 1)
		{
			num /= (float)num2;
		}
		if (num < 1f)
		{
			Rect rect = main.rect;
			num = rect.height + 8f;
		}
		copy.anchoredPosition = main.anchoredPosition;
		float num3 = main.anchoredPosition.y + 0.5f;
		foreach (Transform item in parent)
		{
			Transform val = item;
			if (!((Object)(object)val == (Object)(object)copy))
			{
				RectTransform val2 = (RectTransform)(object)((val is RectTransform) ? val : null);
				if (val2 != null && val2.anchoredPosition.y <= num3)
				{
					val2.anchoredPosition -= new Vector2(0f, num);
				}
			}
		}
		RectTransform val3 = (RectTransform)(object)((parent is RectTransform) ? parent : null);
		if (val3 != null)
		{
			val3.sizeDelta += new Vector2(0f, num);
		}
		Layout = "row " + num.ToString("F0", CultureInfo.InvariantCulture) + " px";
	}

	private static void Grow(RectTransform box, RectTransform row)
	{
		if (!((Object)(object)box == (Object)null) && !((Object)(object)row == (Object)null) && !((Object)(object)((Component)box).GetComponent<ContentSizeFitter>() != (Object)null))
		{
			VerticalLayoutGroup component = ((Component)box).GetComponent<VerticalLayoutGroup>();
			Rect rect = row.rect;
			float num = rect.height + (((Object)(object)component != (Object)null) ? ((HorizontalOrVerticalLayoutGroup)component).spacing : 0f);
			if (!(num <= 1f))
			{
				box.sizeDelta += new Vector2(0f, num);
				Grown = num;
				grownBox = box;
				LayoutRebuilder.ForceRebuildLayoutImmediate(box);
			}
		}
	}

	private static Transform StackChild(Transform panel, Transform t)
	{
		Transform val = t;
		while ((Object)(object)val != (Object)null)
		{
			if ((Object)(object)val.parent == (Object)(object)panel || ((Object)(object)val.parent != (Object)null && (Object)(object)((Component)val.parent).GetComponent<LayoutGroup>() != (Object)null))
			{
				return val;
			}
			val = val.parent;
		}
		return null;
	}

	private static TextMeshProUGUI Label(GameObject go)
	{
		if (!((Object)(object)go != (Object)null))
		{
			return null;
		}
		return go.GetComponentInChildren<TextMeshProUGUI>(true);
	}

	private static bool InButton(Transform t)
	{
		Transform val = t;
		while ((Object)(object)val != (Object)null)
		{
			if ((Object)(object)((Component)val).GetComponent<Button>() != (Object)null)
			{
				return true;
			}
			val = val.parent;
		}
		return false;
	}

	private static bool Calls(Button b, string method)
	{
		try
		{
			Button.ButtonClickedEvent onClick = b.onClick;
			for (int i = 0; i < ((UnityEventBase)onClick).GetPersistentEventCount(); i++)
			{
				if (((UnityEventBase)onClick).GetPersistentMethodName(i) == method)
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	internal static void CopyInvite()
	{
		string text = Net.ServerAddress ?? "";
		if (text.Length == 0)
		{
			Chat.Local(Lang.Tr("No server address to copy."));
			return;
		}
		try
		{
			GUIUtility.systemCopyBuffer = text;
		}
		catch (Exception ex)
		{
			Log.Warn("ui: copying the invite failed: " + ex.Message);
			Chat.Local(Lang.Tr("Could not copy the server address."));
			return;
		}
		LastCopied = text;
		Copies++;
		Log.Info("ui: invite copied to the clipboard (" + text.Length + " characters; not shown)");
		Chat.Local(Lang.Tr("Server address copied. Send it to a friend. The password is not included: tell them yourself if there is one."));
	}
}
