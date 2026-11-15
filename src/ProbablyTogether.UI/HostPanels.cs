using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProbablyTogether.UI;

internal static class HostPanels
{
	internal const string SetupWindow = "bellun_probably_together.host_setup";

	internal const string RulesWindow = "bellun_probably_together.host_rules";

	private const float Left = 58f;

	private const float Width = 974f;

	private sealed class Check
	{
		public bool On;

		public GameObject Mark;
	}

	private sealed class Panel
	{
		public CustomUIWindow Window;

		public NewGameDifficultyFrame Frame;

		public RectTransform Body;

		public readonly Dictionary<string, TMP_InputField> Inputs = new Dictionary<string, TMP_InputField>(StringComparer.Ordinal);

		public readonly Dictionary<string, Check> Checks = new Dictionary<string, Check>(StringComparer.Ordinal);

		public TextMeshProUGUI Note;
	}

	private static Panel setup;

	private static Panel rules;

	internal static string LastProblem = "";

	internal static bool SetupOpen => setup != null && setup.Window != null && setup.Window.IsAlive;

	internal static bool RulesOpen => rules != null && rules.Window != null && rules.Window.IsAlive;

	private static Panel Open(string id, string title, Action onContinue)
	{
		CustomUIManager instance = CustomUIManager.Instance;
		if ((Object)(object)instance == (Object)null || (Object)(object)instance.newGameDifficultyPrefab == (Object)null)
		{
			throw new InvalidOperationException("the game's new game panel is not available");
		}
		CustomUIWindow window = instance.CreateWindow(id, null, "menu", instance.newGameDifficultyPrefab).SetCloseOnEscape().GetWindow();
		NewGameDifficultyFrame frame = (window.root != null) ? window.root.GetComponent<NewGameDifficultyFrame>() : null;
		if ((Object)(object)frame == (Object)null)
		{
			window.Close();
			throw new InvalidOperationException("the game's new game panel has no frame");
		}
		Panel panel = new Panel
		{
			Window = window,
			Frame = frame
		};
		frame.headerTitle.text = title;
		// Hide the difficulty screen's own contents.
		foreach (Component item in new Component[4] { frame.platesHost, frame.prevButton, frame.nextButton, frame.optionsHost })
		{
			if ((Object)(object)item != (Object)null)
			{
				item.gameObject.SetActive(false);
			}
		}
		// Keep the prefab's rule over the Continue button; drop the one that closed the plates.
		try
		{
			List<RectTransform> list = new List<RectTransform>();
			foreach (Transform item2 in frame.panel)
			{
				RectTransform rectTransform = item2 as RectTransform;
				if ((Object)(object)rectTransform != (Object)null && item2.gameObject.activeSelf && (Object)(object)item2.GetComponent<Image>() != (Object)null && rectTransform.rect.height <= 3f && rectTransform.rect.width > 500f)
				{
					list.Add(rectTransform);
				}
			}
			foreach (RectTransform item3 in list.OrderBy((RectTransform r) => r.position.y).Skip(1))
			{
				item3.gameObject.SetActive(false);
			}
		}
		catch
		{
		}
		panel.Body = NewGameUIElements.NewRect(frame.panel, "bellun_probably_together_body", 0f, 0f, 1110f, 958f);
		frame.backButton.onClick.AddListener(NewGameUIElements.PlayClick);
		frame.continueButton.onClick.RemoveAllListeners();
		frame.continueButton.onClick.AddListener(delegate
		{
			NewGameUIElements.PlayClick();
			onContinue();
		});
		frame.continueLabel.text = LocHelper.GetLocalizedMainMenu("new_continue");
		frame.continueButton.interactable = true;
		NewGameUIElements.RefreshContinueLook(frame, hovered: false);
		panel.Note = NewGameUIElements.NewText(frame, panel.Body, "note", Left, 800f, Width, 60f, FontLoader.FontRole.Noto_Sans, 20f, NewGameStyle.WARNING_DANGER, TextAlignmentOptions.Center, wrap: true);
		panel.Note.text = "";
		return panel;
	}

	private static TextMeshProUGUI Label(Panel p, string name, float y, string text, float size = 22f, bool dim = false, float height = 30f, bool wrap = false, float x = Left, float width = Width)
	{
		TextMeshProUGUI textMeshProUGUI = NewGameUIElements.NewText(p.Frame, p.Body, name, x, y, width, height, FontLoader.FontRole.Noto_Sans, size, dim ? NewGameStyle.DESCRIPTION : NewGameStyle.START_WITH, wrap ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft, wrap);
		textMeshProUGUI.text = text;
		return textMeshProUGUI;
	}

	private static void Rule(Panel p, string name, float y)
	{
		NewGameUIElements.NewImage(p.Body, name, Left, y, Width, 2f, null, NewGameStyle.SEPARATOR);
	}

	private static TMP_InputField Input(Panel p, string id, float x, float y, float width, string value, int limit, bool password = false)
	{
		Image image = NewGameUIElements.NewImage(p.Body, id + "_frame", x, y, width, 46f, p.Frame.frame2Square, NewGameStyle.DIFF_CHECKBOX_BORDER);
		image.type = Image.Type.Sliced;
		TMP_InputField tMP_InputField = NewGameUIElements.NewInputField(p.Frame, p.Body, id, x + 14f, y, width - 28f, 46f, FontLoader.FontRole.Noto_Sans, 22f, NewGameStyle.TITLE, limit);
		if (password)
		{
			tMP_InputField.contentType = TMP_InputField.ContentType.Password;
		}
		tMP_InputField.text = value ?? "";
		tMP_InputField.ForceLabelUpdate();
		p.Inputs[id] = tMP_InputField;
		return tMP_InputField;
	}

	/// Copies the difficulty screen's "Endless Mode" checkbox.
	private static void Checkbox(Panel p, string id, float y, string text, bool on)
	{
		NewGameCheckbox endless = p.Frame.endless;
		Check check = new Check
		{
			On = on
		};
		p.Checks[id] = check;
		if (endless == null || (Object)(object)endless.root == (Object)null)
		{
			// No checkbox to copy in this build: show the state as text.
			Label(p, id, y, (on ? "[x] " : "[ ] ") + text);
			return;
		}
		GameObject gameObject = Object.Instantiate(endless.root.gameObject, p.Body);
		gameObject.name = "bellun_probably_together_" + id;
		gameObject.SetActive(true);
		Image[] componentsInChildren = endless.root.GetComponentsInChildren<Image>(includeInactive: true);
		Image[] componentsInChildren2 = gameObject.GetComponentsInChildren<Image>(includeInactive: true);
		int num = Array.IndexOf(componentsInChildren, endless.mark);
		int num2 = Array.IndexOf(componentsInChildren, endless.border);
		Image image = (num >= 0 && num < componentsInChildren2.Length) ? componentsInChildren2[num] : null;
		Image image2 = (num2 >= 0 && num2 < componentsInChildren2.Length) ? componentsInChildren2[num2] : null;
		TextMeshProUGUI componentInChildren = gameObject.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
		Button button = gameObject.GetComponentInChildren<Button>(includeInactive: true);
		CanvasGroup component = gameObject.GetComponent<CanvasGroup>();
		foreach (LocalizeStringEvent componentsInChild in gameObject.GetComponentsInChildren<LocalizeStringEvent>(includeInactive: true))
		{
			componentsInChild.enabled = false;
		}
		WildNetworkUI.SetRect((RectTransform)gameObject.transform, Left, y, Width, 32f);
		if ((Object)(object)componentInChildren != (Object)null)
		{
			componentInChildren.text = text;
			componentInChildren.enableWordWrapping = false;
			WildNetworkUI.SetRect(componentInChildren.rectTransform, 44f, 0f, Width - 44f, 32f);
		}
		if ((Object)(object)image2 != (Object)null)
		{
			image2.color = NewGameStyle.DIFF_CHECKBOX_BORDER;
		}
		if ((Object)(object)component != (Object)null)
		{
			component.alpha = 1f;
		}
		check.Mark = ((Object)(object)image != (Object)null) ? image.gameObject : null;
		if (check.Mark != null)
		{
			check.Mark.SetActive(on);
		}
		if ((Object)(object)button != (Object)null)
		{
			button.interactable = true;
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(delegate
			{
				NewGameUIElements.PlayClick();
				check.On = !check.On;
				if (check.Mark != null)
				{
					check.Mark.SetActive(check.On);
				}
			});
		}
	}

	private static string Text(Panel p, string id)
	{
		TMP_InputField value;
		return (p != null && p.Inputs.TryGetValue(id, out value) && (Object)(object)value != (Object)null) ? (value.text ?? "") : "";
	}

	private static void Note(Panel p, string text)
	{
		if (p != null && (Object)(object)p.Note != (Object)null)
		{
			p.Note.text = text ?? "";
		}
		if (!string.IsNullOrEmpty(text))
		{
			Log.Info("host: " + text);
		}
	}

	internal static bool PortFree(int port)
	{
		try
		{
			TcpListener tcpListener = new TcpListener(IPAddress.Any, port);
			tcpListener.Start();
			tcpListener.Stop();
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string LanAddress()
	{
		try
		{
			foreach (IPAddress address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
			{
				if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
				{
					continue;
				}
				byte[] addressBytes = address.GetAddressBytes();
				if (addressBytes[0] == 10 || (addressBytes[0] == 172 && addressBytes[1] >= 16 && addressBytes[1] <= 31) || (addressBytes[0] == 192 && addressBytes[1] == 168))
				{
					return address.ToString();
				}
			}
		}
		catch
		{
		}
		return null;
	}

	internal static void OpenSetup(string password, int port, Action<string, int> onContinue)
	{
		Close(ref setup);
		Panel p = Open(SetupWindow, Lang.Tr("Host a shop"), delegate
		{
			SetupContinue(onContinue);
		});
		setup = p;
		// Read-only: the server listens on every address; this is the one LAN friends type in.
		string text = LanAddress();
		Label(p, "iplabel", 132f, Lang.Tr("IP Address"));
		TMP_InputField tMP_InputField = Input(p, "ip", Left, 168f, Width, text ?? Lang.Tr("(no local network address found)"), 64);
		tMP_InputField.readOnly = true;
		tMP_InputField.textComponent.color = NewGameStyle.DESCRIPTION;
		Label(p, "pwlabel", 240f, Lang.Tr("Password"));
		Label(p, "pwhint", 240f, Lang.Tr("Leave it empty and anyone with the address can join."), 18f, dim: true, 30f, wrap: false, Left + 150f, Width - 150f);
		Input(p, "password", Left, 276f, Width, password, 128, password: true);
		Label(p, "portlabel", 350f, Lang.Tr("Port"));
		Input(p, "port", Left, 386f, 200f, port.ToString(CultureInfo.InvariantCulture), 5);
		Label(p, "porthint", 386f, Lang.Tr("Port forward this port (TCP) on your router"), 18f, dim: true, 46f, wrap: false, Left + 224f, Width - 224f);
		p.Window.Show();
	}

	internal static void SetupContinue(Action<string, int> onContinue)
	{
		if (!SetupOpen)
		{
			return;
		}
		string arg = Text(setup, "password");
		if (!int.TryParse(Text(setup, "port").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || result < 1024 || result > 65535)
		{
			Note(setup, Lang.Tr("The port must be a number from 1024 to 65535."));
			return;
		}
		if (!PortFree(result))
		{
			Note(setup, Lang.Trf("Port {0} is already in use on this PC.", result.ToString(CultureInfo.InvariantCulture)));
			return;
		}
		Note(setup, "");
		onContinue(arg, result);
	}

	internal sealed class Rules
	{
		public string Name = "";

		public int Players = 4;

		public bool OwnTrades = true;

		public bool SoloTrips;

		public double CustomersPerPlayer = 2.0;

		public int RentPercentPerPlayer = 50;
	}

	internal static void OpenRules(Rules r, Action<Rules> onContinue)
	{
		Close(ref rules);
		Panel p = Open(RulesWindow, Lang.Tr("New shop"), delegate
		{
			RulesContinue(onContinue);
		});
		rules = p;
		Label(p, "namelabel", 132f, Lang.Tr("Shop name"));
		Input(p, "name", Left, 168f, Width, r.Name, 40);
		Label(p, "maxlabel", 240f, Lang.Tr("Players"));
		Input(p, "max", Left, 276f, 120f, r.Players.ToString(CultureInfo.InvariantCulture), 1);
		Label(p, "maxhint", 276f, Lang.Tr("1 to 4, you included."), 18f, dim: true, 46f, wrap: false, Left + 144f, Width - 144f);
		Rule(p, "rule1", 350f);
		Checkbox(p, "own", 372f, Lang.Tr("Uniquely Qualified"), r.OwnTrades);
		Label(p, "ownhint", 406f, Lang.Tr("Every player has a storage bay, a trade and perks of their own."), 18f, dim: true, 28f, wrap: false, Left + 44f, Width - 44f);
		Checkbox(p, "solo", 450f, Lang.Tr("Solo Trips"), r.SoloTrips);
		Label(p, "solohint", 484f, Lang.Tr("Everyone goes out at night on their own: one trip per player each night."), 18f, dim: true, 28f, wrap: false, Left + 44f, Width - 44f);
		Rule(p, "rule2", 534f);
		Label(p, "custlabel", 556f, Lang.Tr("Extra customers a day for each added player"));
		Input(p, "custper", Left, 592f, 120f, r.CustomersPerPlayer.ToString("0.##", CultureInfo.InvariantCulture), 4);
		Label(p, "custhint", 592f, Lang.Tr("0 to 6."), 18f, dim: true, 46f, wrap: false, Left + 144f, Width - 144f);
		Label(p, "rentlabel", 664f, Lang.Tr("Rent increase for each added player (%)"));
		Input(p, "rentper", Left, 700f, 120f, r.RentPercentPerPlayer.ToString(CultureInfo.InvariantCulture), 3);
		Label(p, "renthint", 700f, Lang.Tr("0 to 500."), 18f, dim: true, 46f, wrap: false, Left + 144f, Width - 144f);
		p.Window.Show();
	}

	internal static void RulesContinue(Action<Rules> onContinue)
	{
		if (!RulesOpen)
		{
			return;
		}
		Rules rules = new Rules
		{
			Name = Text(HostPanels.rules, "name").Trim(),
			OwnTrades = HostPanels.rules.Checks["own"].On,
			SoloTrips = HostPanels.rules.Checks["solo"].On
		};
		if (rules.Name.Length == 0)
		{
			Note(HostPanels.rules, Lang.Tr("Give the shop a name."));
			return;
		}
		if (!int.TryParse(Text(HostPanels.rules, "max").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out rules.Players) || rules.Players < 1 || rules.Players > 4)
		{
			Note(HostPanels.rules, Lang.Tr("Players must be 1 to 4."));
			return;
		}
		if (!double.TryParse(Text(HostPanels.rules, "custper").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out rules.CustomersPerPlayer) || rules.CustomersPerPlayer < 0.0 || rules.CustomersPerPlayer > 6.0)
		{
			Note(HostPanels.rules, Lang.Tr("Extra customers per player must be a number from 0 to 6."));
			return;
		}
		if (!int.TryParse(Text(HostPanels.rules, "rentper").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out rules.RentPercentPerPlayer) || rules.RentPercentPerPlayer < 0 || rules.RentPercentPerPlayer > 500)
		{
			Note(HostPanels.rules, Lang.Tr("The rent increase per player must be a whole number from 0 to 500."));
			return;
		}
		Note(HostPanels.rules, "");
		onContinue(rules);
	}

	internal static void RulesNote(string text)
	{
		Note(rules, text);
	}

	private static void Close(ref Panel p)
	{
		Panel panel = p;
		p = null;
		try
		{
			if (panel != null && panel.Window != null && panel.Window.IsAlive)
			{
				panel.Window.Close();
			}
		}
		catch
		{
		}
	}

	internal static void CloseSetup()
	{
		Close(ref setup);
	}

	internal static void CloseRules()
	{
		Close(ref rules);
	}

	internal static void SetField(string id, string value)
	{
		foreach (Panel item in new Panel[2] { setup, rules })
		{
			if (item != null && item.Inputs.TryGetValue(id, out var value2) && (Object)(object)value2 != (Object)null)
			{
				value2.text = value;
			}
		}
	}
}
