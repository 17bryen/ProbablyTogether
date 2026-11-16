using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProbablyTogether.UI;

/// The join, message and mods windows, built on the game's own new-game panel like the host screens.
/// The plain windows remain as the fallback where the game's panel is unavailable.
internal static partial class HostPanels
{
	private static Panel join;

	private static readonly Dictionary<string, Panel> messages = new Dictionary<string, Panel>(StringComparer.Ordinal);

	/// A button of a panel: what it says, and what it does once the panel has closed (nothing: it only closes).
	internal sealed class Choice
	{
		public readonly string Label;

		public readonly Action Do;

		public Choice(string label, Action act = null)
		{
			Label = label;
			Do = act;
		}
	}

	private const float ButtonGap = 40f;

	/// Where the panel's own Continue button is, in the body's coordinates (x from the left, y from the top).
	private static Rect ContinueRect(Panel p)
	{
		try
		{
			Component component = ((Object)(object)p.Frame.continueOuter != (Object)null) ? (Component)p.Frame.continueOuter : p.Frame.continueButton;
			Vector3[] array = new Vector3[4];
			((RectTransform)component.transform).GetWorldCorners(array);
			Vector3 vector = p.Body.InverseTransformPoint(array[1]);
			Vector3 vector2 = p.Body.InverseTransformPoint(array[3]);
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

	/// The middle of the panel's own line over its button row, in the body's coordinates.
	private static float RowCentre(Panel p, Rect own)
	{
		try
		{
			RectTransform rectTransform = null;
			float num = float.MaxValue;
			foreach (Transform item in p.Frame.panel)
			{
				RectTransform rectTransform2 = item as RectTransform;
				if ((Object)(object)rectTransform2 != (Object)null && item.gameObject.activeSelf && (Object)(object)item.GetComponent<Image>() != (Object)null && rectTransform2.rect.height <= 3f && rectTransform2.rect.width > 500f && rectTransform2.position.y < num)
				{
					num = rectTransform2.position.y;
					rectTransform = rectTransform2;
				}
			}
			if ((Object)(object)rectTransform != (Object)null)
			{
				Vector3[] array = new Vector3[4];
				rectTransform.GetWorldCorners(array);
				float x = p.Body.InverseTransformPoint(array[0]).x;
				float x2 = p.Body.InverseTransformPoint(array[3]).x;
				if (x2 - x > 300f)
				{
					return (x + x2) / 2f;
				}
			}
		}
		catch
		{
		}
		return own.x + own.width / 2f;
	}

	private static void Shift(Transform t, float dx)
	{
		RectTransform rectTransform = t as RectTransform;
		if ((Object)(object)rectTransform != (Object)null)
		{
			rectTransform.anchoredPosition += new Vector2(dx, 0f);
		}
	}

	/// One to three buttons in a row, each the size of the game's Continue, centred where it is, ordered by how much each does.
	/// The panel's own button is one of them, at index `own`.
	private static void Buttons(Panel p, string id, IList<Choice> choices, int own)
	{
		Rect rect = ContinueRect(p);
		int num = Mathf.Min(choices.Count, 3);
		float num2 = (float)num * rect.width + (float)(num - 1) * ButtonGap;
		// One button stays put; a row centres under the line over the buttons, as the cut corner narrows the right side.
		float num3 = ((num > 1) ? RowCentre(p, rect) : (rect.x + rect.width / 2f)) - num2 / 2f;
		for (int i = 0; i < num; i++)
		{
			float num4 = num3 + (float)i * (rect.width + ButtonGap);
			Choice choice = choices[i];
			if (i == own)
			{
				ContinueSays(p, choice.Label);
				float num5 = num4 - rect.x;
				if (Mathf.Abs(num5) > 0.5f)
				{
					Transform transform = p.Frame.continueButton.transform;
					Shift(transform, num5);
					foreach (Component item in new Component[3] { p.Frame.continueOuter, p.Frame.continueInner, p.Frame.continueLabel })
					{
						if ((Object)(object)item != (Object)null && !item.transform.IsChildOf(transform))
						{
							Shift(item.transform, num5);
						}
					}
				}
				continue;
			}
			NewGameCommonButton.Build(p.Frame, p.Body, "choice" + i, num4, rect.y, rect.width, rect.height, choice.Label, (UnityAction)delegate
			{
				NewGameUIElements.PlayClick();
				CloseMessage(id);
				choice.Do?.Invoke();
			});
		}
	}

	/// The place of the panel's own button among this many: the middle of three, else the first.
	private static int Own(int count)
	{
		return (count >= 3) ? 1 : 0;
	}

	internal static bool Available
	{
		get
		{
			try
			{
				CustomUIManager instance = CustomUIManager.Instance;
				return (Object)(object)instance != (Object)null && (Object)(object)instance.newGameDifficultyPrefab != (Object)null;
			}
			catch
			{
				return false;
			}
		}
	}

	private static TextMeshProUGUI Para(Panel p, string name, ref float y, string text, float size, Color color, float gap = 16f)
	{
		TextMeshProUGUI textMeshProUGUI = NewGameUIElements.NewText(p.Frame, p.Body, name, Left, y, Width, 30f, FontLoader.FontRole.Noto_Sans, size, color, TextAlignmentOptions.TopLeft, wrap: true);
		textMeshProUGUI.text = text ?? "";
		float num = 30f;
		try
		{
			num = Mathf.Max(30f, Mathf.Ceil(textMeshProUGUI.GetPreferredValues(textMeshProUGUI.text, Width, 0f).y));
		}
		catch
		{
		}
		WildNetworkUI.SetRect(textMeshProUGUI.rectTransform, Left, y, Width, num);
		y += num + gap;
		return textMeshProUGUI;
	}

	private static void ContinueSays(Panel p, string label)
	{
		p.Frame.continueLabel.text = label;
		NewGameUIElements.RefreshContinueLook(p.Frame, hovered: false);
	}

	internal static bool JoinOpen => join != null && join.Window != null && join.Window.IsAlive;

	internal static bool JoinVisible => JoinOpen && join.Window.IsVisible;

	internal static void OpenJoin(string id, string address, string port, string name, bool nameFixed, string password, bool remember, string footer, Action onJoin, Action onClosed)
	{
		Close(ref join);
		Panel p = Open(id, Lang.Tr("Join a shop"), onJoin);
		join = p;
		ContinueSays(p, Lang.Tr("Join"));
		Label(p, "iplabel", 132f, Lang.Tr("IP Address"));
		Label(p, "iphint", 132f, Lang.Tr("The host reads it off their \"Host a shop\" screen, or gives you their public address."), 18f, dim: true, 30f, wrap: false, Left + 150f, Width - 150f);
		Input(p, "address", Left, 168f, Width, address, 128);
		Label(p, "portlabel", 240f, Lang.Tr("Port"));
		Input(p, "port", Left, 276f, 200f, port, 5);
		Label(p, "porthint", 276f, Lang.Tr("27700 unless the host chose another."), 18f, dim: true, 46f, wrap: false, Left + 224f, Width - 224f);
		Label(p, "pwlabel", 350f, Lang.Tr("Password"));
		Label(p, "pwhint", 350f, Lang.Tr("Leave it empty if the shop has none."), 18f, dim: true, 30f, wrap: false, Left + 150f, Width - 150f);
		Input(p, "password", Left, 386f, Width, password, 128, password: true);
		Checkbox(p, "remember", 452f, Lang.Tr("Remember password"), remember);
		Rule(p, "rule1", 506f);
		Label(p, "namelabel", 528f, Lang.Tr(nameFixed ? "Your name in the shop (your Steam name)" : "Your name in the shop"));
		TMP_InputField tMP_InputField = Input(p, "name", Left, 564f, Width, name, 24);
		if (nameFixed)
		{
			tMP_InputField.readOnly = true;
			tMP_InputField.richText = false;
			tMP_InputField.textComponent.color = NewGameStyle.DESCRIPTION;
		}
		if (!string.IsNullOrEmpty(footer))
		{
			float y = 668f;
			Para(p, "footer", ref y, footer, 17f, NewGameStyle.DESCRIPTION);
		}
		foreach (string item in new string[4] { "address", "port", "password", "name" })
		{
			TMP_InputField tMP_InputField2 = p.Inputs[item];
			tMP_InputField2.lineType = TMP_InputField.LineType.SingleLine;
			tMP_InputField2.onSubmit.AddListener(delegate
			{
				onJoin?.Invoke();
			});
		}
		p.Window.onClose = delegate
		{
			if (join == p)
			{
				join = null;
			}
			onClosed?.Invoke();
		};
		p.Window.Show();
	}

	internal static string JoinText(string id)
	{
		return Text(join, id);
	}

	internal static void JoinSet(string id, string value)
	{
		if (join != null && join.Inputs.TryGetValue(id, out var value2) && (Object)(object)value2 != (Object)null)
		{
			value2.text = value ?? "";
		}
	}

	internal static bool JoinRemember
	{
		get
		{
			return join != null && join.Checks.TryGetValue("remember", out var value) && value.On;
		}
		set
		{
			if (join != null && join.Checks.TryGetValue("remember", out var value2))
			{
				value2.On = value;
				if (value2.Mark != null)
				{
					value2.Mark.SetActive(value);
				}
			}
		}
	}

	/// The line over the button: red for what went wrong, dim for what is going on.
	internal static void JoinNote(string text, bool hint)
	{
		if (join != null && (Object)(object)join.Note != (Object)null)
		{
			join.Note.text = text ?? "";
			join.Note.color = hint ? NewGameStyle.DESCRIPTION : NewGameStyle.WARNING_DANGER;
		}
	}

	internal static void JoinFocus(string id)
	{
		if (join != null && join.Inputs.TryGetValue(id, out var value) && (Object)(object)value != (Object)null)
		{
			value.Select();
			value.ActivateInputField();
		}
	}

	internal static void JoinShow(bool show)
	{
		if (JoinOpen)
		{
			if (show)
			{
				join.Window.Show();
			}
			else
			{
				join.Window.Hide();
			}
		}
	}

	internal static void CloseJoin()
	{
		Close(ref join);
	}

	internal static bool MessageOpen(string id)
	{
		return messages.TryGetValue(id, out var value) && value.Window != null && value.Window.IsAlive;
	}

	internal static void CloseMessage(string id)
	{
		if (messages.TryGetValue(id, out var value))
		{
			messages.Remove(id);
			Close(ref value);
		}
	}

	/// Paragraph `warn` (-1: none) is in the warning colour; one to three buttons, each closing the panel first.
	/// Returns the paragraphs' labels so a caller can change them later.
	internal static TextMeshProUGUI[] OpenMessage(string id, string title, IList<string> paragraphs, int warn, IList<Choice> choices, Action onClosed = null, string layer = "menu")
	{
		CloseMessage(id);
		int own = Own(choices.Count);
		Panel p = null;
		p = Open(id, title, delegate
		{
			CloseMessage(id);
			choices[own].Do?.Invoke();
		}, layer);
		messages[id] = p;
		float y = 150f;
		TextMeshProUGUI[] array = new TextMeshProUGUI[paragraphs.Count];
		for (int i = 0; i < paragraphs.Count; i++)
		{
			array[i] = Para(p, "p" + i, ref y, paragraphs[i], 24f, (i == warn) ? NewGameStyle.WARNING : NewGameStyle.START_WITH, 22f);
		}
		Buttons(p, id, choices, own);
		p.Window.onClose = delegate
		{
			if (messages.TryGetValue(id, out var value) && value == p)
			{
				messages.Remove(id);
			}
			onClosed?.Invoke();
		};
		p.Window.Show();
		return array;
	}

	internal sealed class ModsView
	{
		public TextMeshProUGUI[] States;

		public TextMeshProUGUI[] Steps;

		public TextMeshProUGUI Status;
	}

	private const int ModRows = 7;

	internal static ModsView OpenMods(string id, string title, string intro, IList<string> names, IList<string> states, IList<Action> pages, string pageLabel, IList<string> steps, IList<Choice> choices, Action onClosed)
	{
		CloseMessage(id);
		int own = Own(choices.Count);
		Panel p = null;
		p = Open(id, title, delegate
		{
			CloseMessage(id);
			choices[own].Do?.Invoke();
		});
		messages[id] = p;
		ModsView modsView = new ModsView
		{
			States = new TextMeshProUGUI[names.Count],
			Steps = new TextMeshProUGUI[steps.Count]
		};
		float y = 132f;
		Para(p, "intro", ref y, intro, 22f, NewGameStyle.START_WITH, 14f);
		int num = Mathf.Min(names.Count, ModRows);
		for (int i = 0; i < num; i++)
		{
			TextMeshProUGUI textMeshProUGUI = Label(p, "mod_" + i, y, names[i], 22f, dim: false, 44f, wrap: false, Left, 500f);
			textMeshProUGUI.overflowMode = TextOverflowModes.Ellipsis;
			textMeshProUGUI.color = NewGameStyle.TITLE;
			modsView.States[i] = Label(p, "state_" + i, y, states[i], 20f, dim: true, 44f, wrap: false, Left + 520f, 220f);
			if (pages[i] != null)
			{
				Action action = pages[i];
				NewGameCommonButton.Build(p.Frame, p.Body, "ws_" + i, Left + 754f, y + 3f, 220f, 38f, pageLabel, (UnityAction)delegate
				{
					NewGameUIElements.PlayClick();
					action();
				});
			}
			y += 46f;
		}
		if (names.Count > num)
		{
			Label(p, "more", y, Lang.Trf("and {0} more", names.Count - num), 18f, dim: true);
			y += 34f;
		}
		y += 6f;
		Rule(p, "rule1", y);
		y += 18f;
		for (int j = 0; j < steps.Count; j++)
		{
			modsView.Steps[j] = Label(p, "step_" + (j + 1), y, steps[j], 20f, dim: true, 30f);
			y += 32f;
		}
		y += 8f;
		modsView.Status = NewGameUIElements.NewText(p.Frame, p.Body, "modstatus", Left, Mathf.Max(y, 760f), Width, 60f, FontLoader.FontRole.Noto_Sans, 20f, NewGameStyle.DESCRIPTION, TextAlignmentOptions.Center, wrap: true);
		modsView.Status.text = "";
		Buttons(p, id, choices, own);
		p.Window.onClose = delegate
		{
			if (messages.TryGetValue(id, out var value) && value == p)
			{
				messages.Remove(id);
			}
			onClosed?.Invoke();
		};
		p.Window.Show();
		return modsView;
	}
}
