using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProbablyTogether.Systems;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProbablyTogether.UI;

/// The Random and Force checkboxes on the game's trade and perk screens: on the host's New Game screens and on the
/// screens a joining player gets. A ticked Random greys the screen's own choices; the server deals at Continue.
internal static class RandomBoxes
{
	private sealed class Box
	{
		public GameObject Root;

		public GameObject Mark;

		public TextMeshProUGUI Text;

		public Button Button;

		public CanvasGroup Group;

		public bool On;

		public bool Locked;

		public void Set(bool on)
		{
			On = on;
			if (Mark != null)
			{
				Mark.SetActive(on);
			}
		}

		public void Lock(bool locked)
		{
			Locked = locked;
			if ((Object)(object)Button != (Object)null)
			{
				Button.interactable = !locked;
			}
			if ((Object)(object)Group != (Object)null)
			{
				Group.alpha = locked ? 0.6f : 1f;
			}
		}

		public void Show(bool show)
		{
			if (Root != null)
			{
				Root.SetActive(show);
			}
		}
	}

	private const string BodyName = "bellun_probably_together_random";

	private const float Left = 58f;

	/// The host's choices for a new shop.
	internal static bool HostTrade;

	internal static bool HostPerks;

	internal static bool HostForceTrade;

	internal static bool HostForcePerks;

	/// A joining player's (or a repick's) choices on the screens up now.
	internal static bool JoinTrade;

	internal static bool JoinPerks;

	private static Box tradeBox;

	private static Box perksBox;

	private static NewGameSpecializationFrame specFrame;

	private static NewGamePerksFrame perksFrame;

	private static bool applying;

	private static readonly FieldInfo selectedField = AccessTools.Field(typeof(NewGameSpecializationUI), "selected");

	private static readonly FieldInfo scenariosField = AccessTools.Field(typeof(NewGameSpecializationUI), "scenarios");

	private static readonly MethodInfo showScenario = AccessTools.Method(typeof(NewGameSpecializationUI), "ShowScenario", Type.EmptyTypes, (Type[])null);

	private static readonly MethodInfo refreshStrip = AccessTools.Method(typeof(NewGameSpecializationUI), "RefreshStrip", Type.EmptyTypes, (Type[])null);

	private static readonly MethodInfo rebuildPerks = AccessTools.Method(typeof(NewGamePerksUI), "Rebuild", Type.EmptyTypes, (Type[])null);

	private static bool Hosting => HostFlow.Active;

	private static bool Joining => !HostFlow.Active && (PerkScreens.Covering || Versus.Picking);

	internal static bool TradeOn => Hosting ? HostTrade : (Joining && (JoinTrade || RandomStarts.ForceTrade));

	internal static bool PerksOn => Hosting ? HostPerks : (Joining && (JoinPerks || RandomStarts.ForcePerks));

	/// A new shop starts with nothing random.
	internal static void ResetHost()
	{
		HostTrade = false;
		HostPerks = false;
		HostForceTrade = false;
		HostForcePerks = false;
	}

	internal static void ResetJoin()
	{
		JoinTrade = false;
		JoinPerks = false;
	}

	private static NewGameCheckbox Source()
	{
		CustomUIManager instance = CustomUIManager.Instance;
		NewGameDifficultyFrame newGameDifficultyFrame = ((Object)(object)instance != (Object)null && (Object)(object)instance.newGameDifficultyPrefab != (Object)null) ? instance.newGameDifficultyPrefab.GetComponent<NewGameDifficultyFrame>() : null;
		return newGameDifficultyFrame?.endless;
	}

	/// A copy of the difficulty screen's "Endless Mode" checkbox.
	private static Box NewBox(RectTransform parent, string id, float x, float y, float width, string text, Action<bool> changed)
	{
		NewGameCheckbox newGameCheckbox = Source();
		if (newGameCheckbox == null || (Object)(object)newGameCheckbox.root == (Object)null)
		{
			return null;
		}
		GameObject gameObject = Object.Instantiate(newGameCheckbox.root.gameObject, parent);
		gameObject.name = "bellun_probably_together_" + id;
		gameObject.SetActive(true);
		Image[] componentsInChildren = newGameCheckbox.root.GetComponentsInChildren<Image>(includeInactive: true);
		Image[] componentsInChildren2 = gameObject.GetComponentsInChildren<Image>(includeInactive: true);
		int num = Array.IndexOf(componentsInChildren, newGameCheckbox.mark);
		int num2 = Array.IndexOf(componentsInChildren, newGameCheckbox.border);
		Image image = (num >= 0 && num < componentsInChildren2.Length) ? componentsInChildren2[num] : null;
		Image image2 = (num2 >= 0 && num2 < componentsInChildren2.Length) ? componentsInChildren2[num2] : null;
		TextMeshProUGUI componentInChildren = gameObject.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
		foreach (LocalizeStringEvent componentsInChild in gameObject.GetComponentsInChildren<LocalizeStringEvent>(includeInactive: true))
		{
			componentsInChild.enabled = false;
		}
		WildNetworkUI.SetRect((RectTransform)gameObject.transform, x, y, width, 32f);
		if ((Object)(object)componentInChildren != (Object)null)
		{
			componentInChildren.text = text;
			componentInChildren.enableWordWrapping = false;
			WildNetworkUI.SetRect(componentInChildren.rectTransform, 44f, 0f, width - 44f, 32f);
			WildNetworkUI.AutoSize(componentInChildren, 14f);
		}
		if ((Object)(object)image2 != (Object)null)
		{
			image2.color = NewGameStyle.DIFF_CHECKBOX_BORDER;
		}
		CanvasGroup canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
		canvasGroup.alpha = 1f;
		Box box = new Box
		{
			Root = gameObject,
			Text = componentInChildren,
			Mark = ((Object)(object)image != (Object)null) ? image.gameObject : null,
			Button = gameObject.GetComponentInChildren<Button>(includeInactive: true),
			Group = canvasGroup
		};
		box.Set(on: false);
		if ((Object)(object)box.Button != (Object)null)
		{
			box.Button.interactable = true;
			box.Button.onClick.RemoveAllListeners();
			box.Button.onClick.AddListener(delegate
			{
				if (!box.Locked)
				{
					NewGameUIElements.PlayClick();
					box.Set(!box.On);
					changed(box.On);
				}
			});
		}
		return box;
	}

	/// "For Yourself" or "For All Players", right after the Random box's label so the two read as one phrase.
	private sealed class Select
	{
		public RectTransform Root;

		public TextMeshProUGUI Text;

		public RectTransform List;

		public readonly List<TextMeshProUGUI> Options = new List<TextMeshProUGUI>();

		public CanvasGroup Group;

		public bool All;

		public Action<bool> Changed;

		public void Set(bool all)
		{
			All = all;
			Text.text = all ? Lang.Tr("For All Players") : Lang.Tr("For Yourself");
			for (int i = 0; i < Options.Count; i++)
			{
				Options[i].color = ((i == 1) == all) ? NewGameStyle.BUTTON_HOVER : NewGameStyle.START_WITH;
			}
		}

		public void Enable(bool on)
		{
			Group.alpha = on ? 1f : 0.4f;
			Group.interactable = on;
			Group.blocksRaycasts = on;
			if (!on)
			{
				Open(open: false);
			}
		}

		public void Open(bool open)
		{
			if ((Object)(object)List != (Object)null)
			{
				List.gameObject.SetActive(open);
				if (open)
				{
					List.SetAsLastSibling();
				}
			}
		}

		public void Show(bool show)
		{
			Root.gameObject.SetActive(show);
			if (!show)
			{
				Open(open: false);
			}
		}
	}

	private const float BoxGap = 8f;

	private const float RowHeight = 32f;

	private static Select tradeSelect;

	private static Select perksSelect;

	/// Lab: the trade screen's list of choices is open.
	internal static void LabOpenSelect(bool trade)
	{
		Select select = trade ? tradeSelect : perksSelect;
		if (select != null && (Object)(object)select.Root != (Object)null)
		{
			select.Open(open: true);
		}
	}

	/// Where the screen's Continue button is, in the coordinates of a rect laid over the panel.
	private static Rect ContinueRect(NewGamePanelFrame frame, RectTransform body)
	{
		try
		{
			Component component = ((Object)(object)frame.continueOuter != (Object)null) ? (Component)frame.continueOuter : frame.continueButton;
			Vector3[] array = new Vector3[4];
			((RectTransform)component.transform).GetWorldCorners(array);
			Vector3 vector = body.InverseTransformPoint(array[1]);
			Vector3 vector2 = body.InverseTransformPoint(array[3]);
			Rect result = new Rect(vector.x, 0f - vector.y, vector2.x - vector.x, vector.y - vector2.y);
			if (result.width > 60f && result.height > 16f)
			{
				return result;
			}
		}
		catch
		{
		}
		return new Rect(427f, 890f, 256f, 40f);
	}

	/// A dark plate with the checkbox's border, as the game's own fields have.
	private static Image Plate(RectTransform parent, NewGamePanelFrame frame, string name, float x, float y, float w, float h)
	{
		Image image = NewGameUIElements.NewImage(parent, name + "_bg", x, y, w, h, null, NewGameStyle.DIFF_CHECKBOX_BG, raycast: true);
		Image image2 = NewGameUIElements.NewImage(parent, name + "_border", x, y, w, h, frame.frame2Square, NewGameStyle.DIFF_CHECKBOX_BORDER);
		image2.type = Image.Type.Sliced;
		return image;
	}

	private static Select NewSelect(NewGamePanelFrame frame, RectTransform parent, string id, float x, float y, float width, float size, Color color, Action<bool> changed)
	{
		Select select = new Select
		{
			Changed = changed
		};
		RectTransform rectTransform = NewGameUIElements.NewRect(parent, "bellun_probably_together_" + id, x, y, width, RowHeight);
		select.Root = rectTransform;
		select.Group = rectTransform.gameObject.AddComponent<CanvasGroup>();
		Image image = Plate(rectTransform, frame, "field", 0f, 0f, width, RowHeight);
		select.Text = NewGameUIElements.NewText(frame, rectTransform, "text", 10f, 0f, width - 38f, RowHeight, FontLoader.FontRole.Noto_Sans, size, color, TextAlignmentOptions.MidlineLeft, wrap: false);
		// The game's "next" arrow, turned to point down.
		Image image2 = NewGameUIElements.NewImage(rectTransform, "arrow", width - 26f, 8f, 16f, 16f, frame.arrowNextNormal, NewGameStyle.BULLET);
		image2.rectTransform.pivot = new Vector2(0.5f, 0.5f);
		image2.rectTransform.anchoredPosition += new Vector2(8f, -8f);
		image2.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);
		Button button = image.gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		button.onClick.AddListener(delegate
		{
			NewGameUIElements.PlayClick();
			select.Open(!select.List.gameObject.activeSelf);
		});
		// The list opens upwards: the row sits at the bottom of the panel.
		select.List = NewGameUIElements.NewRect(parent, "bellun_probably_together_" + id + "_list", x, y - 2f * RowHeight - 4f, width, 2f * RowHeight);
		string[] array = new string[2] { Lang.Tr("For Yourself"), Lang.Tr("For All Players") };
		for (int i = 0; i < 2; i++)
		{
			bool all = i == 1;
			Image image3 = Plate(select.List, frame, "option" + i, 0f, (float)i * RowHeight, width, RowHeight);
			TextMeshProUGUI textMeshProUGUI = NewGameUIElements.NewText(frame, select.List, "option" + i + "_text", 10f, (float)i * RowHeight, width - 20f, RowHeight, FontLoader.FontRole.Noto_Sans, size, color, TextAlignmentOptions.MidlineLeft, wrap: false);
			textMeshProUGUI.text = array[i];
			textMeshProUGUI.raycastTarget = false;
			select.Options.Add(textMeshProUGUI);
			Button button2 = image3.gameObject.AddComponent<Button>();
			button2.targetGraphic = image3;
			ColorBlock colors = button2.colors;
			colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
			colors.colorMultiplier = 2f;
			button2.colors = colors;
			button2.onClick.AddListener(delegate
			{
				NewGameUIElements.PlayClick();
				select.Set(all);
				select.Open(open: false);
				select.Changed(all);
			});
		}
		select.Text.raycastTarget = false;
		select.Open(open: false);
		select.Set(all: false);
		return select;
	}

	/// The Random box and, for the host, its select, left of Continue on the button line.
	private static void Corners(RectTransform r, RectTransform body, out float left, out float top, out float bottom)
	{
		Vector3[] array = new Vector3[4];
		r.GetWorldCorners(array);
		left = body.InverseTransformPoint(array[1]).x;
		top = 0f - body.InverseTransformPoint(array[1]).y;
		bottom = 0f - body.InverseTransformPoint(array[0]).y;
	}

	/// Just over the line over Continue, as far from it as the line is from the button. What the screen has there
	/// (the trade screen's warning, the perk screen's counters) moves up a row, not into what is above it.
	/// roomy: a tall host whose own content sits at its top (the trade's "Start with" list), not in the way.
	private static float PlaceRow(NewGamePanelFrame frame, RectTransform body, Rect cont, RectTransform roomy, out float movedTop, out float movedBottom, out float lineTop)
	{
		movedTop = -1f;
		movedBottom = -1f;
		lineTop = -1f;
		float num = -1f;
		foreach (Image componentsInChild in frame.panel.GetComponentsInChildren<Image>(includeInactive: false))
		{
			RectTransform rectTransform = componentsInChild.rectTransform;
			if (rectTransform.rect.height <= 3f && rectTransform.rect.width >= 500f && !rectTransform.IsChildOf(body))
			{
				Corners(rectTransform, body, out var _, out var top, out var _);
				if (top < cont.y && top > num)
				{
					num = top;
				}
			}
		}
		if (num < 0f)
		{
			return cont.y - RowHeight - 16f;
		}
		lineTop = num;
		float num2 = Mathf.Max(8f, cont.y - num - 2f);
		float num3 = num - num2 - RowHeight;
		List<RectTransform> list = new List<RectTransform>();
		float num4 = float.MinValue;
		float num5 = float.MaxValue;
		List<KeyValuePair<float, float>> list2 = new List<KeyValuePair<float, float>>();
		foreach (Transform item in frame.panel)
		{
			RectTransform rectTransform2 = item as RectTransform;
			if ((Object)(object)rectTransform2 == (Object)null || (Object)(object)rectTransform2 == (Object)(object)body || !item.gameObject.activeSelf || rectTransform2.rect.height <= 3f)
			{
				continue;
			}
			Corners(rectTransform2, body, out var _, out var top2, out var bottom);
			if (top2 >= num3 - 40f && top2 < num && bottom <= num + 1f)
			{
				list.Add(rectTransform2);
				num4 = Mathf.Max(num4, bottom);
				num5 = Mathf.Min(num5, top2);
			}
			else if ((Object)(object)rectTransform2 != (Object)(object)roomy)
			{
				list2.Add(new KeyValuePair<float, float>(top2, bottom));
			}
		}
		if (list.Count > 0)
		{
			float num6 = num4 - (num3 - 8f);
			float num7 = list2.Where((KeyValuePair<float, float> p) => p.Value <= num5).Select((KeyValuePair<float, float> p) => p.Value).DefaultIfEmpty(0f).Max();
			num6 = Mathf.Min(num6, Mathf.Max(0f, num5 - num7 - 6f));
			if (num6 > 0f)
			{
				foreach (RectTransform item2 in list)
				{
					item2.anchoredPosition += new Vector2(0f, num6 / Mathf.Max(0.01f, item2.lossyScale.y / body.lossyScale.y));
				}
			}
			movedTop = num5 - Mathf.Max(0f, num6);
			movedBottom = num4 - Mathf.Max(0f, num6);
			Log.Info("random: " + list.Count + " thing(s) over the line moved up " + num6.ToString("0") + " for the row");
		}
		return num3;
	}

	private static void CentreSelected(NewGamePerksFrame frame, RectTransform body, float countersTop)
	{
		try
		{
			Corners(frame.selectedLabel.rectTransform, body, out var _, out var _, out var bottom);
			Corners(frame.selectedSlots, body, out var _, out var top, out var _);
			// A selected perk's slot is 72 high, built at the top of its row.
			float num = bottom + (countersTop - bottom - 72f) / 2f - top;
			foreach (Component item in new Component[3] { frame.selectedSlots, frame.selectedPrevButton, frame.selectedNextButton })
			{
				RectTransform rectTransform = ((Object)(object)item != (Object)null) ? (item.transform as RectTransform) : null;
				if ((Object)(object)rectTransform != (Object)null)
				{
					rectTransform.anchoredPosition -= new Vector2(0f, num / Mathf.Max(0.01f, rectTransform.lossyScale.y / body.lossyScale.y));
				}
			}
			Log.Info("random: the selected perks moved " + num.ToString("0") + " (heading ends " + bottom.ToString("0") + ", counters start " + countersTop.ToString("0") + ")");
		}
		catch (Exception ex)
		{
			Log.Warn("random: placing the selected perks threw: " + ex.Message);
		}
	}

	/// The Random box and, for the host, its select: in the text column, just over the line over Continue.
	private static void Row(NewGamePanelFrame frame, RectTransform body, RectTransform column, RectTransform roomy, string id, string label, Action<bool> randomChanged, Action<bool> allChanged, out Box box, out Select select)
	{
		Rect rect = ContinueRect(frame, body);
		Corners(column, body, out var left, out var _, out var _);
		float num = Mathf.Max(20f, left);
		float y = PlaceRow(frame, body, rect, roomy, out var movedTop, out var movedBottom, out var lineTop);
		if (frame is NewGamePerksFrame perks && movedBottom > 0f && lineTop > movedBottom)
		{
			// Midway between the counters and the line; the selected perks midway between their heading and the counters.
			y = movedBottom + (lineTop - movedBottom - RowHeight) / 2f;
			CentreSelected(perks, body, movedTop);
		}
		box = NewBox(body, "random_" + id, num, y, 400f, label, randomChanged);
		select = null;
		if (box == null || (Object)(object)box.Text == (Object)null)
		{
			return;
		}
		TextMeshProUGUI text = box.Text;
		float fontSize = text.fontSize;
		float num2 = 44f + Mathf.Ceil(text.GetPreferredValues(label).x) + 2f;
		WildNetworkUI.SetRect((RectTransform)box.Root.transform, num, y, num2, RowHeight);
		WildNetworkUI.SetRect(text.rectTransform, 44f, 0f, num2 - 44f, RowHeight);
		if (Hosting)
		{
			float width = Mathf.Ceil(Mathf.Max(text.GetPreferredValues(Lang.Tr("For Yourself")).x, text.GetPreferredValues(Lang.Tr("For All Players")).x)) + 48f;
			select = NewSelect(frame, body, "all_" + id, num + num2 + BoxGap, y, width, fontSize, text.color, allChanged);
		}
		Log.Info("random: the " + id + " row at " + num.ToString("0") + "," + y.ToString("0") + ", Continue at " + rect.y.ToString("0"));
	}

	private static RectTransform Body(NewGamePanelFrame frame)
	{
		Transform transform = frame.panel.Find(BodyName);
		if ((Object)(object)transform != (Object)null)
		{
			Object.Destroy(transform.gameObject);
		}
		return NewGameUIElements.NewRect(frame.panel, BodyName, 0f, 0f, frame.panel.rect.width, frame.panel.rect.height);
	}

	/// The trade screen opened (or came back): its boxes, and the random look if ticked.
	internal static void AttachSpec()
	{
		CustomUIWindow customUIWindow = CustomUIManager.Instance?.GetWindow(NewGameSpecializationUI.WINDOW_ID);
		NewGameSpecializationFrame newGameSpecializationFrame = (customUIWindow != null && customUIWindow.IsAlive) ? customUIWindow.root.GetComponent<NewGameSpecializationFrame>() : null;
		if ((Object)(object)newGameSpecializationFrame == (Object)null || (!Hosting && !Joining))
		{
			return;
		}
		if ((Object)(object)specFrame != (Object)(object)newGameSpecializationFrame || (Object)(object)newGameSpecializationFrame.panel.Find(BodyName) == (Object)null)
		{
			specFrame = newGameSpecializationFrame;
			RectTransform rectTransform = Body(newGameSpecializationFrame);
			Row(newGameSpecializationFrame, rectTransform, newGameSpecializationFrame.description.rectTransform, newGameSpecializationFrame.bulletsHost, "trade", Lang.Tr("Random trade"), delegate(bool on)
			{
				if (Hosting)
				{
					HostTrade = on;
				}
				else
				{
					JoinTrade = on;
				}
				SpecLook();
			}, delegate(bool all)
			{
				HostForceTrade = all;
			}, out tradeBox, out tradeSelect);
			Log.Info("random: boxes on the trade screen (" + (Hosting ? "host" : "joining player") + ")");
		}
		if (tradeBox != null)
		{
			bool flag = !Hosting && RandomStarts.ForceTrade;
			tradeBox.Set(TradeOn);
			tradeBox.Lock(flag);
		}
		tradeSelect?.Set(HostForceTrade);
		SpecLook();
	}

	internal static void AttachPerks()
	{
		CustomUIWindow customUIWindow = CustomUIManager.Instance?.GetWindow("new_game_perks");
		NewGamePerksFrame newGamePerksFrame = (customUIWindow != null && customUIWindow.IsAlive) ? customUIWindow.root.GetComponent<NewGamePerksFrame>() : null;
		if ((Object)(object)newGamePerksFrame == (Object)null || (!Hosting && !Joining))
		{
			return;
		}
		if ((Object)(object)perksFrame != (Object)(object)newGamePerksFrame || (Object)(object)newGamePerksFrame.panel.Find(BodyName) == (Object)null)
		{
			perksFrame = newGamePerksFrame;
			RectTransform rectTransform = Body(newGamePerksFrame);
			Row(newGamePerksFrame, rectTransform, newGamePerksFrame.instruction.rectTransform, null, "perks", Lang.Tr("Random perks"), delegate(bool on)
			{
				if (Hosting)
				{
					HostPerks = on;
				}
				else
				{
					JoinPerks = on;
				}
				PerksChanged(on);
			}, delegate(bool all)
			{
				HostForcePerks = all;
			}, out perksBox, out perksSelect);
			Log.Info("random: boxes on the perk screen (" + (Hosting ? "host" : "joining player") + ")");
		}
		if (perksBox != null)
		{
			bool flag = !Hosting && RandomStarts.ForcePerks;
			perksBox.Set(PerksOn);
			perksBox.Lock(flag);
		}
		perksSelect?.Set(HostForcePerks);
		if (PerksOn)
		{
			PerksChanged(on: true);
		}
		else
		{
			PerksLook();
		}
	}

	/// The select only for a shop of own trades; it can be changed only while the host's own is random.
	private static void ForceShown()
	{
		bool flag = Hosting && HostFlow.OwnTrades;
		// A screen closed since: its select is gone with it.
		if (tradeSelect != null && (Object)(object)tradeSelect.Root == (Object)null)
		{
			tradeSelect = null;
		}
		if (perksSelect != null && (Object)(object)perksSelect.Root == (Object)null)
		{
			perksSelect = null;
		}
		if (tradeSelect != null)
		{
			tradeSelect.Show(flag);
			tradeSelect.Enable(HostTrade);
			if (!HostTrade || !flag)
			{
				HostForceTrade = false;
				tradeSelect.Set(all: false);
			}
		}
		if (perksSelect != null)
		{
			perksSelect.Show(flag);
			perksSelect.Enable(HostPerks);
			if (!HostPerks || !flag)
			{
				HostForcePerks = false;
				perksSelect.Set(all: false);
			}
		}
	}

	private static void Grey(Component c, bool grey)
	{
		if ((Object)(object)c == (Object)null)
		{
			return;
		}
		CanvasGroup canvasGroup = c.GetComponent<CanvasGroup>() ?? c.gameObject.AddComponent<CanvasGroup>();
		canvasGroup.alpha = grey ? 0.3f : 1f;
		canvasGroup.blocksRaycasts = !grey;
		canvasGroup.interactable = !grey;
	}

	/// The trade screen in its random look: the strip greyed, the texts say what happens.
	internal static void SpecLook()
	{
		NewGameSpecializationFrame newGameSpecializationFrame = specFrame;
		if ((Object)(object)newGameSpecializationFrame == (Object)null || applying)
		{
			return;
		}
		ForceShown();
		bool tradeOn = TradeOn;
		Grey(newGameSpecializationFrame.stripSlots, tradeOn);
		Grey(newGameSpecializationFrame.prevButton, tradeOn);
		Grey(newGameSpecializationFrame.nextButton, tradeOn);
		if ((Object)(object)newGameSpecializationFrame.startWithLabel != (Object)null)
		{
			newGameSpecializationFrame.startWithLabel.gameObject.SetActive(!tradeOn);
		}
		if ((Object)(object)newGameSpecializationFrame.bulletsHost != (Object)null)
		{
			newGameSpecializationFrame.bulletsHost.gameObject.SetActive(!tradeOn);
		}
		if (!tradeOn)
		{
			if (newGameSpecializationFrame.title.text == Lang.Tr("A trade at random"))
			{
				applying = true;
				try
				{
					showScenario?.Invoke(null, null);
				}
				finally
				{
					applying = false;
				}
			}
			return;
		}
		if (Hosting)
		{
			// The game's Continue wants a trade that can start; the server replaces it with the dealt one.
			SelectStartable();
		}
		newGameSpecializationFrame.title.text = Lang.Tr("A trade at random");
		newGameSpecializationFrame.tag.text = Hosting ? Lang.Tr("Dealt when the shop starts") : Lang.Tr("Dealt when you continue");
		newGameSpecializationFrame.tag.color = NewGameStyle.TagColor(StartingScenario.TagTone.Warning);
		newGameSpecializationFrame.description.text = Hosting ? Lang.Tr("One of the trades is dealt to you when the shop starts, Rock Bottom included. You find out which once you are in the shop.") : Lang.Tr("One of the trades nobody in this shop has yet is dealt to you, Rock Bottom included. You find out which once you are in the shop.");
		newGameSpecializationFrame.warning.text = (!Hosting && RandomStarts.ForceTrade) ? Lang.Tr("This shop deals every new player's trade at random.") : "";
		newGameSpecializationFrame.warning.color = NewGameStyle.WARNING;
		newGameSpecializationFrame.continueButton.interactable = true;
		NewGameUIElements.RefreshContinueLook(newGameSpecializationFrame, hovered: false);
	}

	private static void SelectStartable()
	{
		try
		{
			if (!(scenariosField?.GetValue(null) is List<StartingScenario> list))
			{
				return;
			}
			StartingScenario selected = NewGameSpecializationUI.Selected;
			if (selected != null && selected.CanStart() && !selected.IsMod)
			{
				return;
			}
			int num = list.FindIndex((StartingScenario s) => s != null && !s.IsMod && s.CanStart());
			if (num >= 0)
			{
				selectedField?.SetValue(null, num);
				refreshStrip?.Invoke(null, null);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("random: picking a trade the game can start threw: " + ex.Message);
		}
	}

	private static void PerksChanged(bool on)
	{
		if (on)
		{
			NewGameData.Instance?.startingPerks?.Clear();
		}
		applying = true;
		try
		{
			rebuildPerks?.Invoke(null, null);
		}
		catch (Exception ex)
		{
			Log.Warn("random: redrawing the perk screen threw: " + (ex.InnerException ?? ex).Message);
		}
		finally
		{
			applying = false;
		}
		PerksLook();
	}

	/// The perk screen in its random look, after each of the game's redraws.
	internal static void PerksLook()
	{
		NewGamePerksFrame newGamePerksFrame = perksFrame;
		if ((Object)(object)newGamePerksFrame == (Object)null || applying)
		{
			return;
		}
		ForceShown();
		bool perksOn = PerksOn;
		Grey(newGamePerksFrame.availableSlots, perksOn);
		Grey(newGamePerksFrame.selectedSlots, perksOn);
		Grey(newGamePerksFrame.prevButton, perksOn);
		Grey(newGamePerksFrame.nextButton, perksOn);
		Grey(newGamePerksFrame.pointsCounter, perksOn);
		Grey(newGamePerksFrame.perksCounter, perksOn);
		if (!perksOn)
		{
			return;
		}
		newGamePerksFrame.instruction.text = (!Hosting && RandomStarts.ForcePerks) ? Lang.Tr("This shop deals every new player's perks at random: 3 points, good and bad mixed.") : Lang.Tr("Your perks are dealt at random when you continue: 3 points, good and bad mixed.");
		newGamePerksFrame.instruction.color = NewGameStyle.DESCRIPTION;
		newGamePerksFrame.continueButton.interactable = true;
		NewGameUIElements.RefreshContinueLook(newGamePerksFrame, hovered: false);
	}

	/// Lab: ticks a box as a click would.
	internal static void LabTick(bool trade, bool force)
	{
		if (trade)
		{
			if (force)
			{
				HostForceTrade = true;
				tradeSelect?.Set(all: true);
			}
			else if (Hosting)
			{
				HostTrade = true;
			}
			else
			{
				JoinTrade = true;
			}
			AttachSpec();
			return;
		}
		if (force)
		{
			HostForcePerks = true;
			perksSelect?.Set(all: true);
		}
		else if (Hosting)
		{
			HostPerks = true;
		}
		else
		{
			JoinPerks = true;
		}
		AttachPerks();
	}
}

[HarmonyPatch(typeof(NewGameSpecializationUI), "Open")]
internal static class RandomBoxesSpecOpenPatch
{
	[HarmonyPriority(Priority.Low)]
	private static void Postfix()
	{
		try
		{
			RandomBoxes.AttachSpec();
		}
		catch (Exception ex)
		{
			Log.Warn("random: the trade screen's boxes threw: " + ex.Message);
		}
	}
}

[HarmonyPatch(typeof(NewGamePerksUI), "Open")]
internal static class RandomBoxesPerksOpenPatch
{
	[HarmonyPriority(Priority.Low)]
	private static void Postfix()
	{
		try
		{
			RandomBoxes.AttachPerks();
		}
		catch (Exception ex)
		{
			Log.Warn("random: the perk screen's boxes threw: " + ex.Message);
		}
	}
}

[HarmonyPatch]
internal static class RandomBoxesShowScenarioPatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(NewGameSpecializationUI), "ShowScenario", Type.EmptyTypes, (Type[])null);

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	[HarmonyPriority(Priority.Low)]
	private static void Postfix()
	{
		try
		{
			if (RandomBoxes.TradeOn)
			{
				RandomBoxes.SpecLook();
			}
		}
		catch
		{
		}
	}
}

[HarmonyPatch]
internal static class RandomBoxesRebuildPatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(NewGamePerksUI), "Rebuild", Type.EmptyTypes, (Type[])null);

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	[HarmonyPriority(Priority.Low)]
	private static void Postfix()
	{
		try
		{
			if (RandomBoxes.PerksOn)
			{
				RandomBoxes.PerksLook();
			}
		}
		catch
		{
		}
	}
}
