using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Steamworks;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProbablyTogether.UI;

public static class JoinMenu
{
	public sealed class SavedServer
	{
		public string address = "";

		public string name = "";

		public string password = "";

		public string lastJoined = "";
	}

	public const string ButtonName = "bellun_probably_together_join";

	public const string WindowId = "bellun_probably_together.join";

	public const string NoticeId = "bellun_probably_together.notice";

	public const string AboutId = "bellun_probably_together.about";

	private const string ServersSave = "bellun_probably_together.servers";

	private const string NameSave = "bellun_probably_together.name";

	private const int MaxRemembered = 8;

	private static TextMeshProUGUI buttonLabel;

	private static bool dumped;

	private static SavedServer pending;

	private static bool joining;

	private static CustomUIWindow window;

	/// True while the open Join window is the game's own panel, not the plain window.
	private static bool panel;

	private static bool Up => panel ? HostPanels.JoinOpen : (window != null && window.IsAlive);

	private static HostPanels.ModsView modsView;

	private static Color? statusRed;

	private static bool hiddenForChild;

	private static string notice;

	private static string noticeServer;

	private static SavedServer rejoin;

	public const string ModsId = "bellun_probably_together.mods";

	private const string WorkshopPage = "https://steamcommunity.com/sharedfiles/filedetails/?id=";

	private static List<ModList.Info> modsNeeded;

	private static CustomUIWindow modsWindow;

	private static bool modsExtra;

	private static float modsNextPoll;

	private const float ModRowHeight = 48f;

	private static bool modsRebuilding;

	private static int modsStep;

	public static bool DialogOpen
	{
		get
		{
			if ((Object)(object)CustomUIManager.Instance != (Object)null)
			{
				return CustomUIManager.Instance.IsOpen("bellun_probably_together.join");
			}
			return false;
		}
	}

	public static bool IsVisible
	{
		get
		{
			if (panel)
			{
				return HostPanels.JoinVisible;
			}
			if (window != null && window.IsAlive)
			{
				return window.IsVisible;
			}
			return false;
		}
	}

	public static string Status { get; private set; } = "";

	public static string LastMessage { get; private set; } = "";

	public static GameObject Button { get; private set; }

	public static bool UsingFallbackButton { get; private set; }

	public static bool AboutOpen
	{
		get
		{
			if (HostPanels.MessageOpen("bellun_probably_together.about"))
			{
				return true;
			}
			if ((Object)(object)CustomUIManager.Instance != (Object)null)
			{
				return CustomUIManager.Instance.IsOpen("bellun_probably_together.about");
			}
			return false;
		}
	}

	public static bool NameReadOnly { get; private set; }

	public static bool StatusIsHint { get; private set; }

	public static string ModsStatus { get; private set; } = "";

	public static bool ModsOpen
	{
		get
		{
			if ((Object)(object)CustomUIManager.Instance != (Object)null)
			{
				return CustomUIManager.Instance.IsOpen("bellun_probably_together.mods");
			}
			return false;
		}
	}

	internal static void Tick()
	{
		if (!Ui.AtMenu)
		{
			Button = null;
			return;
		}
		MainMenuUIController instance = MainMenuUIController.Instance;
		if (!((Object)(object)instance == (Object)null))
		{
			if ((Object)(object)Button == (Object)null)
			{
				TryBuildButton(instance);
			}
			else if ((Object)(object)buttonLabel != (Object)null && ((TMP_Text)buttonLabel).text != Lang.Tr("Join a shop"))
			{
				((TMP_Text)buttonLabel).text = Lang.Tr("Join a shop");
				Kit.FitFont(buttonLabel);
			}
			if (DialogOpen)
			{
				UpdateStatus();
			}
			ModsTick();
			ShowPendingNotice(instance);
		}
	}

	private static void TryBuildButton(MainMenuUIController menu)
	{
		GameObject moddingButton = menu.moddingButton;
		if ((Object)(object)moddingButton == (Object)null || !moddingButton.activeInHierarchy)
		{
			return;
		}
		if (!dumped)
		{
			dumped = true;
			DumpMenu(moddingButton);
		}
		try
		{
			Transform parent = moddingButton.transform.parent;
			GameObject val = Object.Instantiate<GameObject>(moddingButton, parent);
			((Object)val).name = "bellun_probably_together_join";
			buttonLabel = val.GetComponentInChildren<TextMeshProUGUI>(true);
			Button componentInChildren = val.GetComponentInChildren<Button>(true);
			if ((Object)(object)buttonLabel == (Object)null || (Object)(object)componentInChildren == (Object)null)
			{
				Object.Destroy((Object)(object)val);
				BuildFallbackButton();
				return;
			}
			componentInChildren.onClick = new Button.ButtonClickedEvent();
			((UnityEvent)componentInChildren.onClick).AddListener((UnityAction)delegate
			{
				try
				{
					menu.OnAnyButtonClick();
				}
				catch
				{
				}
				OpenDialog();
			});
			MonoBehaviour[] componentsInChildren = val.GetComponentsInChildren<MonoBehaviour>(true);
			foreach (MonoBehaviour val2 in componentsInChildren)
			{
				if ((Object)(object)val2 != (Object)null && ((object)val2).GetType().Name == "LocalizeStringEvent")
				{
					((Behaviour)val2).enabled = false;
				}
			}
			((TMP_Text)buttonLabel).text = Lang.Tr("Join a shop");
			bool flag = (Object)(object)parent != (Object)null && (Object)(object)((Component)parent).GetComponent<LayoutGroup>() != (Object)null;
			val.transform.SetSiblingIndex(moddingButton.transform.GetSiblingIndex() + 1);
			if (!flag)
			{
				Transform transform = val.transform;
				RectTransform val3 = (RectTransform)(object)((transform is RectTransform) ? transform : null);
				if (val3 != null)
				{
					Transform transform2 = moddingButton.transform;
					RectTransform val4 = (RectTransform)(object)((transform2 is RectTransform) ? transform2 : null);
					if (val4 != null)
					{
						Vector2 anchoredPosition = val4.anchoredPosition;
						Rect rect = val4.rect;
						val3.anchoredPosition = anchoredPosition + new Vector2(0f, 0f - (rect.height + 10f));
					}
				}
			}
			val.SetActive(true);
			Button = val;
			UsingFallbackButton = false;
			HostMenu.BuildButton(menu, moddingButton, val, flag);
			Log.Info("ui: \"Join server\" button added next to Mods (" + (flag ? ("parent layout " + ((object)((Component)parent).GetComponent<LayoutGroup>()).GetType().Name) : "placed under it") + ")");
		}
		catch (Exception ex)
		{
			Log.Warn("ui: copying the Mods button failed (" + ex.Message + "); using a small panel instead");
			BuildFallbackButton();
		}
	}

	private static void BuildFallbackButton()
	{
		CustomUIManager instance = CustomUIManager.Instance;
		if (!((Object)(object)instance == (Object)null))
		{
			Button = instance.CreateWindow("bellun_probably_together.menu", (string)null, "menu", (GameObject)null, (CustomUIManager.Style)0).AddButton(Lang.Tr("Join server"), (Action)OpenDialog, "join").SetSize(260f, 90f)
				.SetPosition(new Vector2(0f, -420f))
				.Show()
				.Get("join")?.root;
			if ((Object)(object)Button != (Object)null)
			{
				((Object)Button).name = "bellun_probably_together_join";
			}
			buttonLabel = null;
			UsingFallbackButton = true;
			Log.Info("ui: \"Join server\" button added as a panel on the menu layer");
		}
	}

	private static void DumpMenu(GameObject src)
	{
		try
		{
			Transform parent = src.transform.parent;
			StringBuilder stringBuilder = new StringBuilder("ui: menu: Mods button '" + Path(src.transform) + "'");
			Transform transform = src.transform;
			RectTransform val = (RectTransform)(object)((transform is RectTransform) ? transform : null);
			if (val != null)
			{
				stringBuilder.Append(" rect " + Fmt(val));
			}
			stringBuilder.Append(" components [" + Components(src) + "]");
			if ((Object)(object)parent != (Object)null)
			{
				stringBuilder.Append("; parent '" + ((Object)parent).name + "' [" + Components(((Component)parent).gameObject) + "] siblings:");
				for (int i = 0; i < parent.childCount; i++)
				{
					Transform child = parent.GetChild(i);
					string[] obj = new string[6]
					{
						" ",
						i.ToString(),
						"=",
						((Object)child).name,
						((Component)child).gameObject.activeSelf ? "" : "(off)",
						null
					};
					RectTransform val2 = (RectTransform)(object)((child is RectTransform) ? child : null);
					obj[5] = ((val2 != null) ? (" " + Fmt(val2)) : "");
					stringBuilder.Append(string.Concat(obj));
				}
			}
			Log.Info(stringBuilder.ToString());
			if (!string.IsNullOrEmpty(Args.Bot))
			{
				Transform root = src.transform.root;
				StringBuilder stringBuilder2 = new StringBuilder("ui: menu tree under '" + ((Object)root).name + "':");
				DumpTree(root, 0, stringBuilder2, 0);
				Log.Info(stringBuilder2.ToString());
			}
		}
		catch (Exception ex)
		{
			Log.Warn("ui: menu dump failed: " + ex.Message);
		}
	}

	private static int DumpTree(Transform t, int depth, StringBuilder sb, int count)
	{
		if (count > 400 || depth > 7)
		{
			return count;
		}
		TextMeshProUGUI component = ((Component)t).GetComponent<TextMeshProUGUI>();
		string[] obj = new string[8]
		{
			"\n  ",
			new string(' ', depth * 2),
			((Object)t).name,
			((Component)t).gameObject.activeSelf ? "" : " (off)",
			null,
			null,
			null,
			null
		};
		RectTransform val = (RectTransform)(object)((t is RectTransform) ? t : null);
		obj[4] = ((val != null) ? (" " + Fmt(val)) : "");
		obj[5] = (((Object)(object)component != (Object)null) ? (" text='" + Short(((TMP_Text)component).text) + "'") : "");
		obj[6] = (((Object)(object)((Component)t).GetComponent<Button>() != (Object)null) ? " [Button]" : "");
		obj[7] = (((Object)(object)((Component)t).GetComponent<LayoutGroup>() != (Object)null) ? (" [" + ((object)((Component)t).GetComponent<LayoutGroup>()).GetType().Name + "]") : "");
		sb.Append(string.Concat(obj));
		count++;
		for (int i = 0; i < t.childCount; i++)
		{
			count = DumpTree(t.GetChild(i), depth + 1, sb, count);
		}
		return count;
	}

	private static string Short(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return ((s.Length > 30) ? (s.Substring(0, 30) + "...") : s).Replace("\n", " ");
		}
		return "";
	}

	private static string Path(Transform t)
	{
		if (!((Object)(object)t.parent == (Object)null))
		{
			return Path(t.parent) + "/" + ((Object)t).name;
		}
		return ((Object)t).name;
	}

	private static string Fmt(RectTransform r)
	{
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		object[] obj = new object[8]
		{
			r.anchoredPosition.x,
			r.anchoredPosition.y,
			null,
			null,
			null,
			null,
			null,
			null
		};
		Rect rect = r.rect;
		obj[2] = rect.width;
		rect = r.rect;
		obj[3] = rect.height;
		obj[4] = r.anchorMin.x;
		obj[5] = r.anchorMin.y;
		obj[6] = r.anchorMax.x;
		obj[7] = r.anchorMax.y;
		return string.Format(invariantCulture, "pos({0:F0},{1:F0}) size({2:F0},{3:F0}) anchor({4:F2},{5:F2})-({6:F2},{7:F2})", obj);
	}

	private static string Components(GameObject go)
	{
		List<string> list = new List<string>();
		Component[] components = go.GetComponents<Component>();
		foreach (Component val in components)
		{
			if ((Object)(object)val != (Object)null)
			{
				list.Add(((object)val).GetType().Name);
			}
		}
		return string.Join(",", list);
	}

	/// Set when the upstream mod is also loaded.
	internal static bool UpstreamWarning;

	internal static string UpstreamText => Lang.Tr("Another co-op mod is also turned on. Turn it off in Mods and restart: the two cannot run together.");

	private const int DefaultPort = 27700;

	/// Opens the Join window on the game's own panel. False if it could not be made.
	private static bool OpenPanel(List<SavedServer> list, SavedServer last)
	{
		if (!HostPanels.Available)
		{
			return false;
		}
		try
		{
			string name;
			bool flag = SteamName(out name);
			string text = last?.address ?? "";
			string port = DefaultPort.ToString(CultureInfo.InvariantCulture);
			int num = text.LastIndexOf(':');
			if (num > 0 && text.IndexOf(':') == num)
			{
				port = text.Substring(num + 1);
				text = text.Substring(0, num);
			}
			string footer = Mod.Name + " " + Mod.Version + ". " + Lang.Tr("A free, unofficial mod. Not made by or affiliated with Questing Goose Studio or tinyBuild.");
			HostPanels.OpenJoin("bellun_probably_together.join", text, port, flag ? name : DefaultName(), flag, last?.password ?? "", !string.IsNullOrEmpty(last?.password), footer, Join, delegate
			{
				panel = false;
				OnDialogClosed();
			});
			panel = true;
			window = null;
			NameReadOnly = flag;
			SetStatus(UpstreamWarning ? UpstreamText : ((LastMessage.Length > 0 && Net.ClientState == ClientState.Idle) ? LastMessage : ""));
			Log.Info("ui: Join window opened on the game's panel (" + list.Count + " remembered server(s))");
			return true;
		}
		catch (Exception ex)
		{
			panel = false;
			HostPanels.CloseJoin();
			Log.Warn("ui: the game's panel could not be used for the Join window, the plain one comes: " + ex.Message);
			return false;
		}
	}

	public static void OpenDialog()
	{
		CustomUIManager instance = CustomUIManager.Instance;
		if ((Object)(object)instance == (Object)null || !Ui.AtMenu)
		{
			return;
		}
		if (Net.ClientState != ClientState.Idle && !joining)
		{
			Log.Info("ui: Join window not opened: already " + Net.ClientState);
			return;
		}
		List<SavedServer> list = LoadServers();
		SavedServer savedServer = ((list.Count > 0) ? list[0] : null);
		if (OpenPanel(list, savedServer))
		{
			return;
		}
		CustomUIBuilder obj = instance.CreateWindow("bellun_probably_together.join", Lang.Tr("Join server"), "menu", (GameObject)null, (CustomUIManager.Style)0).SetCloseOnEscape(true).SetSize(640f, 720f);
		string name;
		bool flag = SteamName(out name);
		obj.AddLabel(Lang.Tr("Server address (from your server panel, like 203.0.113.5:27700)"), (string)null).AddInput(Lang.Tr("address:port"), savedServer?.address ?? "", (Action<string>)null, "address", (Action<string>)null).AddLabel(Lang.Tr(flag ? "Your name in the shop (your Steam name)" : "Your name in the shop"), (string)null)
			.AddInput("name", flag ? name : DefaultName(), (Action<string>)null, "name", (Action<string>)null)
			.AddLabel(Lang.Tr("Password (leave empty if the server has none)"), (string)null)
			.AddInput("", savedServer?.password ?? "", (Action<string>)null, "password", (Action<string>)null)
			.AddToggle(Lang.Tr("Remember password"), !string.IsNullOrEmpty(savedServer?.password), (Action<bool>)null, "remember");
		obj.AddWarning("", "status");
		CustomUIBuilder obj3 = Row(obj, 8f);
		RectTransform currentContainer2 = obj3.CurrentContainer;
		obj3.AddButton(Lang.Tr("About"), (Action)OpenAbout, "about").AddFlexibleSpace().AddButton(Lang.Tr("Cancel"), (Action)CloseDialog, "cancel")
			.AddButton(Lang.Tr("Join"), (Action)Join, "join");
		obj.OnClose((Action)OnDialogClosed).Center();
		window = obj.Show();
		FitButtonRow(window, currentContainer2, new string[3] { "about", "cancel", "join" }, new string[0]);
		FitHeight(window, 300f, 720f, 44f);
		CustomUIElement val = window.Get("password");
		if ((Object)(object)val?.input != (Object)null)
		{
			val.input.contentType = TMP_InputField.ContentType.Password;
			val.input.ForceLabelUpdate();
		}
		string[] array = new string[3] { "address", "name", "password" };
		foreach (string text in array)
		{
			TMP_InputField val2 = window.Get(text)?.input;
			if ((Object)(object)val2 != (Object)null)
			{
				val2.lineType = TMP_InputField.LineType.SingleLine;
				val2.characterLimit = ((text == "name") ? 24 : 128);
				((UnityEvent<string>)(object)val2.onSubmit).AddListener((UnityAction<string>)delegate
				{
					Join();
				});
			}
		}
		TMP_InputField val3 = window.Get("name")?.input;
		if ((Object)(object)val3 != (Object)null && flag)
		{
			val3.readOnly = true;
			val3.richText = false;
		}
		NameReadOnly = (Object)(object)val3 != (Object)null && val3.readOnly;
		SetStatus(UpstreamWarning ? UpstreamText : ((LastMessage.Length > 0 && Net.ClientState == ClientState.Idle) ? LastMessage : ""));
		RefitFonts();
		Log.Info("ui: Join window opened (" + list.Count + " remembered server(s))");
	}

	public static void CloseDialog()
	{
		CloseAbout();
		CustomUIManager instance = CustomUIManager.Instance;
		if (instance != null)
		{
			instance.CloseWindow("bellun_probably_together.join");
		}
	}

	internal static void JoinListed(string address, string name, bool? locked)
	{
		if (!Up)
		{
			OpenDialog();
		}
		if (panel && Up)
		{
			SavedServer savedServer2 = LoadServers().Find((SavedServer x) => string.Equals(x.address, address, StringComparison.OrdinalIgnoreCase));
			string text2 = ((locked == false) ? "" : (savedServer2?.password ?? ""));
			int num = address.LastIndexOf(':');
			bool flag = num > 0 && address.IndexOf(':') == num;
			HostPanels.JoinSet("address", flag ? address.Substring(0, num) : address);
			HostPanels.JoinSet("port", flag ? address.Substring(num + 1) : DefaultPort.ToString(CultureInfo.InvariantCulture));
			HostPanels.JoinSet("password", text2);
			HostPanels.JoinRemember = text2.Length > 0;
			if (locked == true && text2.Length == 0)
			{
				SetStatus(Lang.Trf("Enter the password for {0}, then press Join.", name), hint: true);
				Focus("password");
			}
			else
			{
				Join();
			}
			return;
		}
		if (window != null && window.IsAlive)
		{
			SavedServer savedServer = LoadServers().Find((SavedServer x) => string.Equals(x.address, address, StringComparison.OrdinalIgnoreCase));
			string text = ((locked == false) ? "" : (savedServer?.password ?? ""));
			CustomUIElement obj = window.Get("address");
			if (obj != null)
			{
				obj.SetString(address, false);
			}
			CustomUIElement obj2 = window.Get("password");
			if (obj2 != null)
			{
				obj2.SetString(text, false);
			}
			CustomUIElement obj3 = window.Get("remember");
			if (obj3 != null)
			{
				obj3.SetBool(text.Length > 0, false);
			}
			if (locked == true && text.Length == 0)
			{
				SetStatus(Lang.Trf("Enter the password for {0}, then press Join.", name), hint: true);
				Focus("password");
				Log.Info("ui: server list row put " + address + " in the Join window (asks for the password)");
			}
			else
			{
				Log.Info("ui: server list row joins " + address + ((text.Length > 0) ? " (saved password)" : ""));
				Join();
			}
		}
	}

	private static void Focus(string id)
	{
		if (panel)
		{
			try
			{
				HostPanels.JoinFocus(id);
			}
			catch (Exception ex2)
			{
				Log.Warn("ui: could not focus " + id + ": " + ex2.Message);
			}
			return;
		}
		CustomUIWindow obj = window;
		TMP_InputField val = ((obj == null) ? null : obj.Get(id)?.input);
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		try
		{
			((Selectable)val).Select();
			val.ActivateInputField();
		}
		catch (Exception ex)
		{
			Log.Warn("ui: could not focus " + id + ": " + ex.Message);
		}
	}

	internal static TextMeshProUGUI OneLine(CustomUIElement el)
	{
		object obj = el?.text;
		if (obj == null)
		{
			if (el == null)
			{
				obj = null;
			}
			else
			{
				GameObject root = el.root;
				obj = ((root != null) ? root.GetComponentInChildren<TextMeshProUGUI>(true) : null);
			}
		}
		TextMeshProUGUI val = (TextMeshProUGUI)obj;
		if ((Object)(object)val == (Object)null)
		{
			return null;
		}
		((TMP_Text)val).enableWordWrapping = false;
		((TMP_Text)val).overflowMode = (TextOverflowModes)1;
		return val;
	}

	internal static void FitButtonRow(CustomUIWindow w, RectTransform row, string[] keep, string[] shrink)
	{
		if (w == null || (Object)(object)row == (Object)null)
		{
			return;
		}
		try
		{
			List<string> list = new List<string>(keep);
			list.AddRange(shrink);
			foreach (string item in list)
			{
				OneLine(w.Get(item));
			}
			Canvas.ForceUpdateCanvases();
			LayoutRebuilder.ForceRebuildLayoutImmediate(w.ContentRoot);
			Dictionary<string, float> dictionary = new Dictionary<string, float>();
			StringBuilder stringBuilder = new StringBuilder();
			Rect rect;
			foreach (string item2 in list)
			{
				CustomUIElement val = w.Get(item2);
				object obj;
				if (val == null)
				{
					obj = null;
				}
				else
				{
					GameObject root = val.root;
					obj = ((root != null) ? root.transform : null);
				}
				RectTransform val2 = (RectTransform)((obj is RectTransform) ? obj : null);
				TextMeshProUGUI val3 = val?.text;
				if (!((Object)(object)val2 == (Object)null) && !((Object)(object)val3 == (Object)null))
				{
					rect = val2.rect;
					float width = rect.width;
					rect = ((TMP_Text)val3).rectTransform.rect;
					float num = Mathf.Max(20f, width - rect.width);
					float num2 = (dictionary[item2] = Mathf.Ceil(((TMP_Text)val3).GetPreferredValues(((TMP_Text)val3).text).x + num));
					string[] obj2 = new string[6] { " ", item2, " ", null, null, null };
					rect = val2.rect;
					obj2[3] = Px(rect.width);
					obj2[4] = "/";
					obj2[5] = Px(num2);
					stringBuilder.Append(string.Concat(obj2));
					rect = val2.rect;
					if (rect.width + 0.5f < num2)
					{
						Layout(val).preferredWidth = num2;
					}
				}
			}
			LayoutRebuilder.ForceRebuildLayoutImmediate(w.ContentRoot);
			rect = row.rect;
			float width2 = rect.width;
			float preferredWidth = LayoutUtility.GetPreferredWidth(row);
			if (preferredWidth <= width2 + 0.5f)
			{
				Log.Info("ui: window " + w.id + " buttons fit (" + Px(preferredWidth) + " of " + Px(width2) + " px; width/label:" + stringBuilder?.ToString() + ")");
				return;
			}
			foreach (KeyValuePair<string, float> item3 in dictionary)
			{
				LayoutElement obj3 = Layout(w.Get(item3.Key));
				obj3.preferredWidth = item3.Value;
				obj3.minWidth = ((Array.IndexOf(shrink, item3.Key) >= 0) ? Mathf.Min(item3.Value, 64f) : item3.Value);
				obj3.flexibleWidth = 0f;
			}
			LayoutRebuilder.ForceRebuildLayoutImmediate(w.ContentRoot);
			Log.Info("ui: window " + w.id + " buttons needed " + Px(preferredWidth) + " of " + Px(width2) + " px (width/label:" + stringBuilder?.ToString() + "); each now as wide as its label: " + Px(LayoutUtility.GetPreferredWidth(row)) + " px");
		}
		catch (Exception ex)
		{
			Log.Warn("ui: fitting the buttons of " + w.id + ": " + ex.Message);
		}
	}

	private static LayoutElement Layout(CustomUIElement el)
	{
		return el.root.GetComponent<LayoutElement>() ?? el.root.AddComponent<LayoutElement>();
	}

	private static string Px(float v)
	{
		return v.ToString("F0", CultureInfo.InvariantCulture);
	}

	public static void OpenAbout()
	{
		CustomUIManager instance = CustomUIManager.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		if (AboutOpen)
		{
			HostPanels.CloseMessage("bellun_probably_together.about");
			instance.CloseWindow("bellun_probably_together.about");
		}
		string[] array = AboutParagraphs(Chat.ChatKeyName);
		if (HostPanels.Available)
		{
			try
			{
				HostPanels.OpenMessage("bellun_probably_together.about", Mod.Name, array, -1, new HostPanels.Choice[1] { new HostPanels.Choice(Lang.Tr("Back")) });
				Log.Info("ui: About window opened on the game's panel");
				return;
			}
			catch (Exception ex)
			{
				Log.Warn("ui: the game's panel could not be used for About: " + ex.Message);
			}
		}
		CustomUIBuilder val = instance.CreateWindow("bellun_probably_together.about", Mod.Name.ToUpperInvariant(), "menu", (GameObject)null, (CustomUIManager.Style)0).SetCloseOnEscape(true).SetSize(620f, 560f);
		for (int i = 0; i < array.Length; i++)
		{
			if (i > 0)
			{
				val.AddSpace(6f);
			}
			val.AddLabel(array[i], "p" + i.ToString(CultureInfo.InvariantCulture));
		}
		val.AddSpace(10f);
		CustomUIBuilder val2 = Row(val, 8f);
		val2.AddFlexibleSpace().AddButton(Lang.Tr("Back"), (Action)CloseAbout, "back");
		CustomUIWindow val3 = val.Center().Show();
		for (int j = 0; j < array.Length; j++)
		{
			TextMeshProUGUI val4 = ((val3 == null) ? null : val3.Get("p" + j.ToString(CultureInfo.InvariantCulture))?.text);
			if (!((Object)(object)val4 == (Object)null))
			{
				((TMP_Text)val4).enableWordWrapping = true;
				((TMP_Text)val4).overflowMode = (TextOverflowModes)0;
				LayoutElement val5 = ((Component)val4).GetComponent<LayoutElement>() ?? ((Component)val4).gameObject.AddComponent<LayoutElement>();
				if (val5.preferredWidth <= 0f || val5.preferredWidth > 560f)
				{
					val5.preferredWidth = 560f;
				}
			}
		}
		FitHeight(val3, 240f, 560f, 0f);
		Log.Info("ui: About window opened");
	}

	private static string[] AboutParagraphs(string chatKey)
	{
		return new string[3]
		{
			Lang.Tr("Online co-op for Probably Stolen: 2 to 4 players run one shop together. One player hosts from the main menu (Host a shop) or runs a dedicated server; the others join by address and password."),
			Lang.Tr("One player serves the counter at a time while the others work the back room. The night starts when everyone is ready. Press {0} to chat.").Replace("{0}", chatKey ?? "Enter"),
			Mod.Name + " " + Mod.Version + ". " + Lang.Tr("A free, unofficial mod. Not made by or affiliated with Questing Goose Studio or tinyBuild. Its licences come with it, in the files next to the mod.")
		};
	}

	internal static void FitHeight(CustomUIWindow w, float min, float max, float slack)
	{
		try
		{
			RectTransform val = ((w != null) ? w.Rect : null);
			RectTransform val2 = ((w != null) ? w.ContentRoot : null);
			if (!((Object)(object)val == (Object)null) && !((Object)(object)val2 == (Object)null) && !((Object)(object)val2 == (Object)(object)val))
			{
				Canvas.ForceUpdateCanvases();
				LayoutRebuilder.ForceRebuildLayoutImmediate(val2);
				Rect rect = val.rect;
				float height = rect.height;
				rect = val2.rect;
				float num = height - rect.height;
				float num2 = LayoutUtility.GetPreferredHeight(val2) + num + slack;
				if (!float.IsNaN(num2) && !(num2 <= 0f))
				{
					rect = val.rect;
					w.SetSize(rect.width, Mathf.Clamp(num2, min, max));
					Log.Info("ui: window " + w.id + " fitted to " + Mathf.Clamp(num2, min, max).ToString("F0", CultureInfo.InvariantCulture) + " high (content " + LayoutUtility.GetPreferredHeight(val2).ToString("F0", CultureInfo.InvariantCulture) + ", chrome " + num.ToString("F0", CultureInfo.InvariantCulture) + ")");
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("ui: fitting window " + w?.id + ": " + ex.Message);
		}
	}

	public static void CloseAbout()
	{
		if (AboutOpen)
		{
			HostPanels.CloseMessage("bellun_probably_together.about");
			CustomUIManager.Instance.CloseWindow("bellun_probably_together.about");
		}
	}

	private static void OnDialogClosed()
	{
		window = null;
	}

	/// Shows the dropped-player notice and a shop's required mods without a shop.
	internal static void LabNotice(string text, string server)
	{
		notice = text;
		noticeServer = server;
	}

	internal static void LabMods()
	{
		ShowModsRefusal("mods", "", new List<ModList.Info>
		{
			new ModList.Info
			{
				Id = "better_shelves",
				Name = "Better Shelves",
				Ws = 3000000001uL
			},
			new ModList.Info
			{
				Id = "night_owl",
				Name = "Night Owl",
				Ws = 3000000002uL,
				Why = "different"
			},
			new ModList.Info
			{
				Id = "private_tweaks",
				Name = "Private Tweaks"
			}
		});
	}

	/// Types into whichever Join window is open. False if none is.
	internal static bool LabFill(string address, string name, string password)
	{
		if (panel && HostPanels.JoinOpen)
		{
			int num = (address ?? "").LastIndexOf(':');
			bool flag = num > 0 && address.IndexOf(':') == num;
			HostPanels.JoinSet("address", flag ? address.Substring(0, num) : address);
			HostPanels.JoinSet("port", flag ? address.Substring(num + 1) : DefaultPort.ToString(CultureInfo.InvariantCulture));
			if (!NameReadOnly && name != null)
			{
				HostPanels.JoinSet("name", name);
			}
			HostPanels.JoinSet("password", password ?? "");
			return true;
		}
		if (window == null || !window.IsAlive)
		{
			return false;
		}
		window.Get("address").SetString(address, true);
		if (name != null)
		{
			window.Get("name").SetString(name, true);
		}
		window.Get("password").SetString(password ?? "", true);
		return true;
	}

	internal static bool OnGamePanel => panel && HostPanels.JoinOpen;

	internal static void LabCloseAll()
	{
		HostPanels.CloseMessage("bellun_probably_together.notice");
		HostPanels.CloseMessage("bellun_probably_together.mods");
		HostPanels.CloseMessage("bellun_probably_together.about");
		CloseDialog();
	}

	internal static CustomUIBuilder Row(CustomUIBuilder b, float spacing)
	{
		HorizontalLayoutGroup val = Kit.Row((Transform)(object)b.CurrentContainer, "row", spacing);
		return new CustomUIBuilder(CustomUIManager.Instance, b.Handle, (RectTransform)((Component)val).transform);
	}

	internal static void Forget(SavedServer s)
	{
		if (s != null)
		{
			List<SavedServer> list = LoadServers();
			list.RemoveAll((SavedServer x) => string.Equals(x.address, s.address, StringComparison.OrdinalIgnoreCase));
			SaveServers(list);
		}
	}

	private static bool SteamName(out string name)
	{
		name = "";
		try
		{
			if (!SteamManager.Initialized)
			{
				return false;
			}
			name = Server_CleanName(SteamFriends.GetPersonaName());
			return name.Length > 0;
		}
		catch
		{
			name = "";
			return false;
		}
	}

	public static void Join()
	{
		if (!Up || (joining && Net.ClientState != ClientState.Idle))
		{
			return;
		}
		string text;
		string text2;
		string text4;
		bool flag;
		string name;
		if (panel)
		{
			// Two fields, as on the host's screen. An address pasted with its port ("1.2.3.4:27700") is taken whole.
			text = HostPanels.JoinText("address").Trim();
			string text5 = HostPanels.JoinText("port").Trim();
			if (text.Length > 0 && text.IndexOf(':') < 0 && text5.Length > 0)
			{
				text = text + ":" + text5;
			}
			text2 = (SteamName(out name) ? name : Server_CleanName(HostPanels.JoinText("name")));
			text4 = HostPanels.JoinText("password");
			flag = HostPanels.JoinRemember;
		}
		else
		{
			CustomUIElement obj = window.Get("address");
			text = (((obj != null) ? obj.GetString() : null) ?? "").Trim();
			if (!SteamName(out name))
			{
				CustomUIElement obj2 = window.Get("name");
				text2 = Server_CleanName((obj2 != null) ? obj2.GetString() : null);
			}
			else
			{
				text2 = name;
			}
			CustomUIElement obj3 = window.Get("password");
			text4 = ((obj3 != null) ? obj3.GetString() : null) ?? "";
			CustomUIElement obj4 = window.Get("remember");
			flag = obj4 != null && obj4.GetBool();
		}
		string text3 = text2;
		if (!ValidAddress(text))
		{
			SetStatus(Lang.Tr(panel ? "Enter the host's address, like 203.0.113.5, and the port." : "Enter the server address, like 203.0.113.5:27700."));
			return;
		}
		if (text3.Length == 0)
		{
			text3 = DefaultName();
		}
		if (!NameReadOnly)
		{
			SaveName(text3);
		}
		pending = new SavedServer
		{
			address = text,
			password = (flag ? text4 : "")
		};
		joining = true;
		LastMessage = "";
		SetStatus(Lang.Trf("Connecting to {0}...", Kit.Name(text)), hint: true);
		Log.Info("ui: joining " + text + " as '" + text3 + "' from the Join window");
		Net.Connect(text, text4, text3);
	}

	private static string Server_CleanName(string n)
	{
		return Server.CleanName(n ?? "");
	}

	internal static bool ValidAddress(string a)
	{
		if (string.IsNullOrWhiteSpace(a) || a.Length > 128 || a.IndexOf(' ') >= 0)
		{
			return false;
		}
		int num = a.LastIndexOf(':');
		if (num < 0)
		{
			return true;
		}
		if (num == 0 || a.IndexOf(':') != num)
		{
			return false;
		}
		if (int.TryParse(a.Substring(num + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var result) && result > 0)
		{
			return result < 65536;
		}
		return false;
	}

	private static void UpdateStatus()
	{
		if (joining)
		{
			switch (Net.ClientState)
			{
			case ClientState.Connecting:
				SetStatus(Lang.Trf("Connecting to {0}...", Kit.Name(Net.ServerAddress)), hint: true);
				break;
			case ClientState.Handshake:
				SetStatus(Lang.Tr("Joining..."), hint: true);
				break;
			case ClientState.AwaitSnapshot:
			case ClientState.Loading:
				SetStatus(Lang.Trf("Joined {0}. Loading the shop...", Kit.Name(Net.ServerName)), hint: true);
				break;
			}
		}
	}

	private static void SetStatus(string s, bool hint = false)
	{
		Status = Kit.Safe(s);
		StatusIsHint = hint;
		if (panel)
		{
			HostPanels.JoinNote(Status, hint);
			return;
		}
		if (window == null || !window.IsAlive)
		{
			return;
		}
		CustomUIElement val = window.Get("status");
		if (val != null)
		{
			if (!statusRed.HasValue && (Object)(object)val.text != (Object)null)
			{
				statusRed = ((Graphic)val.text).color;
			}
			val.SetText(Status);
			if ((Object)(object)val.text != (Object)null)
			{
				Kit.FitFont(val.text);
			}
			if (statusRed.HasValue)
			{
				val.SetTextColor(hint ? Kit.Text : statusRed.Value);
			}
		}
	}

	internal static void HideForChild()
	{
		if (panel && HostPanels.JoinVisible)
		{
			HostPanels.JoinShow(show: false);
			hiddenForChild = true;
			return;
		}
		if (window != null && window.IsAlive && window.IsVisible)
		{
			window.Hide();
			hiddenForChild = true;
		}
	}

	internal static void ShowAfterChild()
	{
		if (hiddenForChild)
		{
			hiddenForChild = false;
			if (panel)
			{
				HostPanels.JoinShow(show: true);
				return;
			}
			if (window != null && window.IsAlive)
			{
				window.Show();
				RefitFonts();
			}
		}
	}

	private static void RefitFonts()
	{
		if (window == null || !window.IsAlive)
		{
			return;
		}
		SetStatus(Status, StatusIsHint);
		string[] array = new string[2] { "address", "name" };
		foreach (string text in array)
		{
			CustomUIElement obj = window.Get(text);
			object obj2;
			if (obj == null)
			{
				obj2 = null;
			}
			else
			{
				TMP_InputField input = obj.input;
				obj2 = ((input != null) ? input.textComponent : null);
			}
			TextMeshProUGUI val = (TextMeshProUGUI)((obj2 is TextMeshProUGUI) ? obj2 : null);
			if (val != null)
			{
				Kit.FitFont(val);
			}
		}
	}

	internal static void FitScroll(CustomUIWindow w, float maxHeight, float minHeight = 0f)
	{
		try
		{
			if ((Object)(object)((w != null) ? w.Rect : null) == (Object)null)
			{
				return;
			}
			ScrollRect[] componentsInChildren = ((Component)w.Rect).GetComponentsInChildren<ScrollRect>(true);
			foreach (ScrollRect val in componentsInChildren)
			{
				if ((Object)(object)val.content == (Object)null)
				{
					continue;
				}
				LayoutRebuilder.ForceRebuildLayoutImmediate(val.content);
				float preferredHeight = LayoutUtility.GetPreferredHeight(val.content);
				if (float.IsNaN(preferredHeight) || preferredHeight <= 0f)
				{
					continue;
				}
				LayoutElement val2 = null;
				Transform val3 = ((Component)val).transform;
				while ((Object)(object)val3 != (Object)null && (Object)(object)val3 != (Object)(object)w.Rect)
				{
					LayoutElement component = ((Component)val3).GetComponent<LayoutElement>();
					if ((Object)(object)component != (Object)null && component.preferredHeight > 0f)
					{
						val2 = component;
						break;
					}
					val3 = val3.parent;
				}
				bool flag = preferredHeight <= maxHeight + 0.5f;
				if ((Object)(object)val2 != (Object)null)
				{
					val2.preferredHeight = (flag ? Mathf.Max(preferredHeight + 2f, minHeight) : maxHeight);
				}
				val.vertical = !flag;
				if (flag)
				{
					val.verticalScrollbarVisibility = (ScrollRect.ScrollbarVisibility)0;
				}
				if ((Object)(object)val.verticalScrollbar != (Object)null)
				{
					((Component)val.verticalScrollbar).gameObject.SetActive(!flag);
				}
				Scrollbar[] componentsInChildren2 = ((Component)val).GetComponentsInChildren<Scrollbar>(true);
				foreach (Scrollbar val4 in componentsInChildren2)
				{
					if ((int)val4.direction == 2 || (int)val4.direction == 3)
					{
						((Component)val4).gameObject.SetActive(!flag);
					}
				}
				if (flag)
				{
					val.verticalNormalizedPosition = 1f;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("ui: fitting a list in " + w?.id + ": " + ex.Message);
		}
	}

	private static string DefaultName()
	{
		try
		{
			if (ES3.KeyExists("bellun_probably_together.name"))
			{
				string text = ES3.Load<string>("bellun_probably_together.name");
				if (!string.IsNullOrWhiteSpace(text))
				{
					return text;
				}
			}
		}
		catch
		{
		}
		if (!string.IsNullOrEmpty(Args.Name))
		{
			return Args.Name;
		}
		try
		{
			if (SteamManager.Initialized)
			{
				string personaName = SteamFriends.GetPersonaName();
				if (!string.IsNullOrWhiteSpace(personaName))
				{
					return Server_CleanName(personaName);
				}
			}
		}
		catch
		{
		}
		return "Player";
	}

	private static void SaveName(string n)
	{
		try
		{
			ES3.Save<string>("bellun_probably_together.name", n);
		}
		catch (Exception ex)
		{
			Log.Warn("ui: name not saved: " + ex.Message);
		}
	}

	public static List<SavedServer> LoadServers()
	{
		try
		{
			if (!ES3.KeyExists("bellun_probably_together.servers"))
			{
				return new List<SavedServer>();
			}
			List<SavedServer> list = JsonConvert.DeserializeObject<List<SavedServer>>(ES3.Load<string>("bellun_probably_together.servers"), new JsonSerializerSettings
			{
				TypeNameHandling = (TypeNameHandling)0
			});
			if (list == null)
			{
				return new List<SavedServer>();
			}
			list.RemoveAll((SavedServer s) => s == null || !ValidAddress(s.address));
			return list;
		}
		catch (Exception ex)
		{
			Log.Warn("ui: remembered servers unreadable (" + ex.Message + "); starting a new list");
			return new List<SavedServer>();
		}
	}

	private static void SaveServers(List<SavedServer> list)
	{
		try
		{
			while (list.Count > 8)
			{
				list.RemoveAt(list.Count - 1);
			}
			ES3.Save<string>("bellun_probably_together.servers", JsonConvert.SerializeObject((object)list));
		}
		catch (Exception ex)
		{
			Log.Warn("ui: remembered servers not saved: " + ex.Message);
		}
	}

	private static void Remember(SavedServer s)
	{
		List<SavedServer> list = LoadServers();
		list.RemoveAll((SavedServer x) => string.Equals(x.address, s.address, StringComparison.OrdinalIgnoreCase));
		s.lastJoined = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
		list.Insert(0, s);
		SaveServers(list);
	}

	internal static void OnJoined()
	{
		if (pending != null)
		{
			pending.name = Net.ServerName ?? "";
			Remember(pending);
			Log.Info("ui: remembered server " + pending.address + ((pending.password.Length > 0) ? " (with password)" : ""));
			pending = null;
		}
		joining = false;
		notice = null;
	}

	internal static void OnDisconnected(string why)
	{
		string lastRefuseCode = Net.LastRefuseCode;
		string text = ((lastRefuseCode != null) ? Refusal(lastRefuseCode, Net.LastRefuseReason) : Plain(why));
		bool flag = IsOwnLeave(why);
		if (!flag)
		{
			LastMessage = text;
		}
		if (joining)
		{
			joining = false;
			rejoin = pending;
			if (pending != null && (lastRefuseCode == "mods" || lastRefuseCode == "extramods"))
			{
				SavedServer savedServer = LoadServers().Find((SavedServer x) => string.Equals(x.address, pending.address, StringComparison.OrdinalIgnoreCase));
				pending.name = ((!string.IsNullOrEmpty(Net.ServerName)) ? Net.ServerName : (savedServer?.name ?? ""));
				Remember(pending);
				Log.Info("ui: remembered server " + pending.address + " after a " + lastRefuseCode + " refusal (the player restarts and comes back)");
			}
			pending = null;
			if (DialogOpen)
			{
				SetStatus(text);
				Log.Info("ui: join failed: " + text);
				ShowModsRefusal(lastRefuseCode, Net.LastRefuseReason, Net.LastRefuseMods);
				return;
			}
		}
		else
		{
			rejoin = null;
		}
		if (!flag)
		{
			notice = text;
			noticeServer = (string.IsNullOrEmpty(Net.ServerName) ? Net.ServerAddress : Net.ServerName);
			Log.Info("ui: disconnected: " + text);
		}
	}

	private static void ShowPendingNotice(MainMenuUIController menu)
	{
		if (notice == null || DialogOpen || Net.ClientState != ClientState.Idle)
		{
			return;
		}
		CustomUIManager instance = CustomUIManager.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		string text = notice;
		notice = null;
		if (HostPanels.Available)
		{
			try
			{
				List<string> list = new List<string>();
				if (!string.IsNullOrEmpty(noticeServer))
				{
					list.Add(Lang.Trf("From {0}:", Kit.Name(noticeServer)));
				}
				list.Add(Kit.Safe(text));
				HostPanels.OpenMessage("bellun_probably_together.notice", Lang.Tr("Disconnected"), list, list.Count - 1, new HostPanels.Choice[2]
				{
					new HostPanels.Choice(Lang.Tr("OK")),
					new HostPanels.Choice(Lang.Tr("Join a shop"), OpenDialog)
				});
				Log.Info("ui: disconnected notice shown on the game's panel: " + text);
				return;
			}
			catch (Exception ex)
			{
				Log.Warn("ui: the game's panel could not be used for the notice: " + ex.Message);
			}
		}
		CustomUIBuilder val = instance.CreateWindow("bellun_probably_together.notice", Lang.Tr("Disconnected"), "menu", (GameObject)null, (CustomUIManager.Style)0).SetCloseOnEscape(true).SetSize(560f, 300f);
		if (!string.IsNullOrEmpty(noticeServer))
		{
			val.AddLabel(Lang.Trf("From {0}:", Kit.Name(noticeServer)), (string)null);
		}
		val.AddWarning(Kit.Safe(text), "reason");
		Row(val, 8f).AddFlexibleSpace().AddButton(Lang.Tr("Join window"), (Action)delegate
		{
			CustomUIManager instance2 = CustomUIManager.Instance;
			if (instance2 != null)
			{
				instance2.CloseWindow("bellun_probably_together.notice");
			}
			OpenDialog();
		}, "rejoin").AddButton(Lang.Tr("OK"), (Action)delegate
		{
			CustomUIManager instance2 = CustomUIManager.Instance;
			if (instance2 != null)
			{
				instance2.CloseWindow("bellun_probably_together.notice");
			}
		}, "ok");
		val.Center().Show();
		Log.Info("ui: disconnected notice shown: " + text);
	}

	private static bool IsOwnLeave(string why)
	{
		switch (why)
		{
		default:
			return why == "game closing";
		case "left":
		case "reconnecting":
		case "mod disabled":
			return true;
		}
	}

	public static string Refusal(string code, string reason)
	{
		reason = (reason ?? "").Trim();
		if (reason.Length > 0)
		{
			return Lang.Tr(Sentence(Kit.Safe((reason.Length > 300) ? reason.Substring(0, 300) : reason)));
		}
		return (code ?? "") switch
		{
			"password" => Lang.Tr("Wrong password."), 
			"full" => Lang.Tr("The server is full."), 
			"starting" => Lang.Tr("The server is still starting. Try again in a minute."), 
			"stopping" => Lang.Tr("The server is stopping. Try again in a minute."), 
			"protocol" => Lang.Tr("Your mod version does not match the server's. Update the mod."), 
			"game" => Lang.Tr("Your game version does not match the server's. Update the game."), 
			"kicked" => Lang.Tr("Removed by the server owner."), 
			_ => Lang.Tr("The server refused the join."), 
		};
	}

	public static string Plain(string why)
	{
		if (string.IsNullOrWhiteSpace(why))
		{
			return Lang.Tr("The connection ended.");
		}
		why = Kit.Safe(why.Trim());
		if (why.StartsWith("refused (", StringComparison.Ordinal))
		{
			if (Net.LastRefuseCode == null)
			{
				return Lang.Tr("The server refused the join.");
			}
			return Refusal(Net.LastRefuseCode, Net.LastRefuseReason);
		}
		if (why.StartsWith("server stopping", StringComparison.Ordinal))
		{
			return Lang.Tr("The server stopped. Try again in a minute.");
		}
		if (why.StartsWith("could not reach the server", StringComparison.Ordinal))
		{
			int num = why.IndexOf(':');
			string text = ((num > 0) ? why.Substring(num + 1).Trim() : "");
			if (text.Length <= 0)
			{
				return Lang.Tr("Could not reach the server. Check the address and port.");
			}
			return Lang.Trf("Could not reach the server. Check the address and port ({0}).", text.TrimEnd('.'));
		}
		if (why.StartsWith("server silent", StringComparison.Ordinal) || why.StartsWith("timed out", StringComparison.Ordinal))
		{
			return Lang.Tr("Lost the connection: the server stopped answering.");
		}
		if (why.Contains("snapshot") || why.Contains("loading the shop") || why.Contains("shop file") || why.Contains("while loading") || why.Contains("not ready to load"))
		{
			return Lang.Trf("Could not load the shop ({0}).", why.TrimEnd('.'));
		}
		return Lang.Tr(Sentence("Disconnected: " + why));
	}

	private static string Sentence(string s)
	{
		s = s.Trim();
		if (s.Length == 0)
		{
			return s;
		}
		s = char.ToUpperInvariant(s[0]) + s.Substring(1);
		if (!s.EndsWith(".") && !s.EndsWith("!") && !s.EndsWith("?"))
		{
			return s + ".";
		}
		return s;
	}

	private static bool ShowModsRefusal(string code, string reason, List<ModList.Info> list)
	{
		if ((code != "mods" && code != "extramods") || list == null || list.Count == 0)
		{
			return false;
		}
		CustomUIManager instance = CustomUIManager.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return false;
		}
		modsRebuilding = true;
		HostPanels.CloseMessage("bellun_probably_together.mods");
		instance.CloseWindow("bellun_probably_together.mods");
		modsRebuilding = false;
		modsView = null;
		modsNeeded = list;
		modsExtra = code == "extramods";
		bool flag = !modsExtra && HasWorkshop(list);
		if (HostPanels.Available)
		{
			try
			{
				List<string> list2 = new List<string>();
				List<string> list3 = new List<string>();
				List<Action> list4 = new List<Action>();
				foreach (ModList.Info item in list)
				{
					list2.Add(Kit.Safe((item.Name.Length > 0) ? item.Name : item.Id));
					list3.Add(Kit.Safe(Lang.Tr(FirstState(item))));
					ulong id2 = item.Ws;
					list4.Add((!modsExtra && id2 != 0L) ? ((Action)delegate
					{
						OpenWorkshopPage(id2);
					}) : null);
				}
				modsView = HostPanels.OpenMods("bellun_probably_together.mods", Lang.Tr("Mods for this shop"), Kit.Safe(Intro(list.Count, flag)), list2, list3, list4, Lang.Tr("Workshop page"), Steps(flag, list.Count), new HostPanels.Choice[2]
				{
					new HostPanels.Choice(Lang.Tr("Close")),
					new HostPanels.Choice(Lang.Tr("Open Mods"), OpenGameMods)
				}, delegate
				{
					modsView = null;
					OnModsClosed();
				});
				HideForChild();
				for (int j = 0; j < list.Count; j++)
				{
					PaintState(j, FirstState(list[j]));
				}
				modsStep = 0;
				PaintSteps(flag ? 1 : 0);
				modsNextPoll = 0f;
				SetModsStatus(flag ? Lang.Trf("{0} of {1} ready", 0, WorkshopCount()) : FirstStep());
				Log.Info("ui: mods window shown on the game's panel (" + code + "): " + ModList.Names(list));
				return true;
			}
			catch (Exception ex)
			{
				modsView = null;
				HostPanels.CloseMessage("bellun_probably_together.mods");
				Log.Warn("ui: the game's panel could not be used for the mods window: " + ex.Message);
			}
		}
		CustomUIBuilder val = instance.CreateWindow("bellun_probably_together.mods", Lang.Tr("Mods for this server"), "menu", (GameObject)null, (CustomUIManager.Style)0).SetCloseOnEscape(true).SetSize(640f, 640f);
		val.AddLabel(Kit.Safe(Intro(list.Count, flag)), "reason");
		val.AddSpace(4f).BeginScroll((float)list.Count * 48f);
		for (int i = 0; i < list.Count; i++)
		{
			ModList.Info info = list[i];
			CustomUIBuilder val2 = Row(val, 8f);
			val2.AddLabel(Kit.Safe(Kit.Name((info.Name.Length > 0) ? info.Name : info.Id)), "mod_" + i);
			val2.AddLabel(Kit.Safe(FirstState(info)), "state_" + i);
			if (!modsExtra && info.Ws != 0L)
			{
				ulong id = info.Ws;
				val2.AddButton(Lang.Tr("Workshop page"), (Action)delegate
				{
					OpenWorkshopPage(id);
				}, "ws_" + i);
			}
		}
		val.End();
		val.AddSeparator((string)null, 4);
		string[] array = Steps(flag, list.Count);
		for (int num = 0; num < array.Length; num++)
		{
			val.AddLabel(Kit.Safe(array[num]), "step_" + (num + 1));
		}
		val.AddSpace(4f).AddLabel("", "modstatus");
		Row(val, 8f).AddButton(Lang.Tr("Open Mods"), (Action)OpenGameMods, "openmods").AddFlexibleSpace().AddButton(Lang.Tr("Close"), (Action)delegate
		{
			CustomUIManager instance2 = CustomUIManager.Instance;
			if (instance2 != null)
			{
				instance2.CloseWindow("bellun_probably_together.mods");
			}
		}, "closemods");
		val.OnClose((Action)OnModsClosed).Center();
		modsWindow = val.Show();
		HideForChild();
		for (int num2 = 0; num2 < list.Count; num2++)
		{
			CustomUIElement val3 = modsWindow.Get("mod_" + num2);
			if ((Object)(object)val3?.root != (Object)null)
			{
				LayoutElement obj = val3.root.GetComponent<LayoutElement>() ?? val3.root.AddComponent<LayoutElement>();
				obj.minWidth = 80f;
				obj.preferredWidth = 80f;
				obj.flexibleWidth = 1f;
				TextMeshProUGUI val4 = OneLine(val3);
				if ((Object)(object)val4 != (Object)null)
				{
					Kit.FitFont(val4);
				}
			}
			CustomUIElement val5 = modsWindow.Get("state_" + num2);
			if ((Object)(object)val5?.root != (Object)null)
			{
				val5.SetFixedWidth(150f);
				if ((Object)(object)val5.text != (Object)null)
				{
					((TMP_Text)val5.text).enableWordWrapping = false;
					((TMP_Text)val5.text).overflowMode = (TextOverflowModes)1;
				}
			}
			PaintState(num2, FirstState(list[num2]));
		}
		FitScroll(modsWindow, 240f);
		modsStep = 0;
		PaintSteps(flag ? 1 : 0);
		FitHeight(modsWindow, 240f, 640f, 0f);
		modsNextPoll = 0f;
		SetModsStatus(flag ? Lang.Trf("{0} of {1} ready", 0, WorkshopCount()) : FirstStep());
		Log.Info("ui: mods window shown (" + code + "): " + ModList.Names(list));
		return true;
	}

	private static void OnModsClosed()
	{
		modsWindow = null;
		if (!modsRebuilding)
		{
			ShowAfterChild();
		}
	}

	private static string Intro(int n, bool ws)
	{
		if (modsExtra)
		{
			return Lang.Tr((n == 1) ? "This server only allows its own mods. Turn this one off to join:" : "This server only allows its own mods. Turn these off to join:");
		}
		if (!ws)
		{
			return Lang.Tr("This server runs mods that are not on the Steam Workshop. Ask its owner how to get them:");
		}
		if (n != 1)
		{
			return Lang.Trf("To play on this server you need the {0} mods it runs:", n);
		}
		return Lang.Tr("To play on this server you need the mod it runs:");
	}

	private static string[] Steps(bool ws, int n)
	{
		bool flag = n == 1;
		if (modsExtra)
		{
			return new string[2]
			{
				Lang.Tr(flag ? "1. Open Mods and press Deactivate on it" : "1. Open Mods and press Deactivate on each one"),
				Lang.Tr("2. Restart the game, then join")
			};
		}
		if (!ws)
		{
			return new string[0];
		}
		return new string[4]
		{
			Lang.Tr(flag ? "1. Press Workshop page, then Subscribe" : "1. Press Workshop page, then Subscribe (for each mod)"),
			Lang.Tr("2. Restart the game"),
			Lang.Tr(flag ? "3. Open Mods and press Activate on it (Yes if the game asks)" : "3. Open Mods and press Activate on each one (Yes if the game asks)"),
			Lang.Tr("4. Restart the game again, then join")
		};
	}

	private static void PaintSteps(int now)
	{
		if (modsView != null && now != modsStep)
		{
			modsStep = now;
			for (int j = 1; j <= modsView.Steps.Length; j++)
			{
				TextMeshProUGUI val = modsView.Steps[j - 1];
				if ((Object)(object)val != (Object)null)
				{
					((Graphic)val).color = (j < now) ? NewGameStyle.TAG_POSITIVE : ((j == now) ? NewGameStyle.TITLE : NewGameStyle.DESCRIPTION);
				}
			}
			return;
		}
		if (modsWindow == null || !modsWindow.IsAlive || now == modsStep)
		{
			return;
		}
		modsStep = now;
		for (int i = 1; i <= 4; i++)
		{
			CustomUIElement obj = modsWindow.Get("step_" + i);
			if (obj != null)
			{
				obj.SetTextColor((i < now) ? Kit.Good : ((i == now) ? Kit.Text : Kit.Dim));
			}
		}
	}

	private static string FirstState(ModList.Info m)
	{
		if (modsExtra)
		{
			return "Turn off";
		}
		if (m.Ws == 0L)
		{
			return "Not on the Workshop";
		}
		if (!((m.Why.Length > 0) ? (m.Why == "different") : ModList.Ids.Contains(m.Id)))
		{
			return "Not subscribed";
		}
		return "Different version";
	}

	private static void PaintState(int i, string text)
	{
		if (modsView != null)
		{
			TextMeshProUGUI val2 = (i >= 0 && i < modsView.States.Length) ? modsView.States[i] : null;
			if ((Object)(object)val2 != (Object)null)
			{
				((TMP_Text)val2).text = Kit.Safe(Lang.Tr(text));
				((Graphic)val2).color = (text == "Ready") ? NewGameStyle.TAG_POSITIVE : ((text == "Downloading...") ? NewGameStyle.DESCRIPTION : NewGameStyle.WARNING);
			}
			return;
		}
		CustomUIWindow obj = modsWindow;
		CustomUIElement val = ((obj != null) ? obj.Get("state_" + i) : null);
		if (val != null)
		{
			val.SetText(Kit.Safe(Lang.Tr(text)));
			if ((Object)(object)val.text != (Object)null)
			{
				Kit.FitFont(val.text);
			}
			val.SetTextColor((text == "Ready") ? Kit.Good : ((text == "Downloading...") ? Kit.Dim : Kit.Warn));
		}
	}

	private static int WorkshopCount()
	{
		int num = 0;
		if (modsNeeded != null)
		{
			foreach (ModList.Info item in modsNeeded)
			{
				if (item.Ws != 0L)
				{
					num++;
				}
			}
		}
		return num;
	}

	private static string FirstStep()
	{
		if (modsExtra)
		{
			return Lang.Tr("Turn them off in Mods (Deactivate), restart the game, then join.");
		}
		if (!HasWorkshop(modsNeeded))
		{
			return Lang.Tr("These mods are not on the Steam Workshop: ask the server owner how to get them.");
		}
		return Lang.Tr("Open each Workshop page and press Subscribe. Then restart the game, turn them on in Mods (Activate, and Yes if the game asks), and restart once more.");
	}

	private static bool HasWorkshop(List<ModList.Info> list)
	{
		if (list != null)
		{
			foreach (ModList.Info item in list)
			{
				if (item.Ws != 0L)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static void SetModsStatus(string s)
	{
		ModsStatus = s ?? "";
		if (modsView != null && (Object)(object)modsView.Status != (Object)null)
		{
			((TMP_Text)modsView.Status).text = Kit.Safe(ModsStatus);
			return;
		}
		if (modsWindow != null && modsWindow.IsAlive)
		{
			CustomUIElement obj = modsWindow.Get("modstatus");
			if (obj != null)
			{
				obj.SetText(Kit.Safe(ModsStatus));
			}
		}
	}

	private static void OpenWorkshopPage(ulong ws)
	{
		Log.Info("ui: mods: Workshop page " + ws + " pressed");
		try
		{
			if (SteamManager.Initialized && SteamUtils.IsOverlayEnabled())
			{
				SteamFriends.ActivateGameOverlayToWebPage("https://steamcommunity.com/sharedfiles/filedetails/?id=" + ws, (EActivateGameOverlayToWebPageMode)0);
				return;
			}
		}
		catch (Exception ex)
		{
			Log.Warn("ui: mods: Steam overlay not available (" + ex.Message + ")");
		}
		SetModsStatus(Lang.Tr("Turn on the Steam overlay to open it here, or search the Steam Workshop for the mod's name."));
	}

	internal static void ModsTick()
	{
		if (modsExtra || modsNeeded == null || !ModsOpen || !SteamManager.Initialized || Time.unscaledTime < modsNextPoll)
		{
			return;
		}
		modsNextPoll = Time.unscaledTime + 0.5f;
		int num = 0;
		int num2 = 0;
		try
		{
			for (int i = 0; i < modsNeeded.Count; i++)
			{
				ModList.Info info = modsNeeded[i];
				if (info.Ws != 0L)
				{
					num++;
					EItemState val = (EItemState)SteamUGC.GetItemState(new PublishedFileId_t(info.Ws));
					bool flag = ((int)val & 1) > 0;
					bool flag2 = ((int)val & 0x38) > 0;
					bool flag3 = flag && ((int)val & 4) != 0 && !flag2;
					if (flag3)
					{
						num2++;
					}
					PaintState(i, flag3 ? "Ready" : (flag ? "Downloading..." : FirstState(info)));
				}
			}
		}
		catch
		{
			return;
		}
		if (num == 0)
		{
			return;
		}
		PaintSteps((num2 < num) ? 1 : 2);
		if (num2 < num)
		{
			SetModsStatus(Lang.Trf("{0} of {1} ready", num2, num));
		}
		else
		{
			SetModsStatus((num == 1) ? Lang.Tr("Ready. Restart the game now.") : Lang.Trf("All {0} ready. Restart the game now.", num));
		}
		if (modsView != null && (Object)(object)modsView.Status != (Object)null)
		{
			((Graphic)modsView.Status).color = (num2 < num) ? NewGameStyle.DESCRIPTION : NewGameStyle.TAG_POSITIVE;
		}
		CustomUIWindow obj2 = modsWindow;
		if (obj2 != null)
		{
			CustomUIElement obj3 = obj2.Get("modstatus");
			if (obj3 != null)
			{
				obj3.SetTextColor((num2 < num) ? Kit.Dim : Kit.Good);
			}
		}
	}

	private static void OpenGameMods()
	{
		CustomUIManager instance = CustomUIManager.Instance;
		HostPanels.CloseMessage("bellun_probably_together.mods");
		if (instance != null)
		{
			instance.CloseWindow("bellun_probably_together.mods");
		}
		HostPanels.CloseJoin();
		if (instance != null)
		{
			instance.CloseWindow("bellun_probably_together.join");
		}
		try
		{
			MainMenuUIController instance2 = MainMenuUIController.Instance;
			if (instance2 == null)
			{
				return;
			}
			GameObject moddingButton = instance2.moddingButton;
			if (moddingButton != null)
			{
				Button component = moddingButton.GetComponent<Button>();
				if (component != null)
				{
					((UnityEvent)component.onClick).Invoke();
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("ui: mods: could not open the game's Mods menu: " + ex.Message);
		}
	}
}
