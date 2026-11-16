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

	private const float ButtonRow = 888f;

	private const float SideButton = 312f;

	private const float RightButtonX = 706f;

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

	private static NewGameCommonButton Extra(Panel p, string name, float x, float y, float width, string label, Action onClick)
	{
		return NewGameCommonButton.Build(p.Frame, p.Body, name, x, y, width, 44f, label, (UnityAction)delegate
		{
			NewGameUIElements.PlayClick();
			onClick?.Invoke();
		});
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

	internal static TextMeshProUGUI[] OpenMessage(string id, string title, IList<string> paragraphs, int warn, string ok, Action onOk, string left = null, Action onLeft = null, string right = null, Action onRight = null, Action onClosed = null, string layer = "menu")
	{
		CloseMessage(id);
		Panel p = null;
		p = Open(id, title, delegate
		{
			CloseMessage(id);
			onOk?.Invoke();
		}, layer);
		messages[id] = p;
		ContinueSays(p, ok);
		float y = 150f;
		TextMeshProUGUI[] array = new TextMeshProUGUI[paragraphs.Count];
		for (int i = 0; i < paragraphs.Count; i++)
		{
			array[i] = Para(p, "p" + i, ref y, paragraphs[i], 24f, (i == warn) ? NewGameStyle.WARNING : NewGameStyle.START_WITH, 22f);
		}
		if (!string.IsNullOrEmpty(left))
		{
			Extra(p, "left", Left, ButtonRow, SideButton, left, delegate
			{
				CloseMessage(id);
				onLeft?.Invoke();
			});
		}
		if (!string.IsNullOrEmpty(right))
		{
			Extra(p, "right", RightButtonX, ButtonRow, SideButton, right, delegate
			{
				CloseMessage(id);
				onRight?.Invoke();
			});
		}
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

	internal static ModsView OpenMods(string id, string title, string intro, IList<string> names, IList<string> states, IList<Action> pages, string pageLabel, IList<string> steps, string leftLabel, Action onLeft, string closeLabel, Action onClosed)
	{
		CloseMessage(id);
		Panel p = null;
		p = Open(id, title, delegate
		{
			CloseMessage(id);
		});
		messages[id] = p;
		ContinueSays(p, closeLabel);
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
		if (!string.IsNullOrEmpty(leftLabel))
		{
			Extra(p, "left", Left, ButtonRow, SideButton, leftLabel, delegate
			{
				CloseMessage(id);
				onLeft?.Invoke();
			});
		}
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
