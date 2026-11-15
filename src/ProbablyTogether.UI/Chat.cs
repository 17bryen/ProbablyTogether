using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProbablyTogether.UI;

public static class Chat
{
	public sealed class Line
	{
		public int N;

		public int Player;

		public string Name = "";

		public string Text = "";

		public bool System;

		public bool Owner;

		public bool History;

		public float ShownAt;

		internal TextMeshProUGUI Label;
	}

	public const int MaxChars = 200;

	public const string KeyBindOption = "chat";

	private const string ChatKeySave = "bellun_probably_together.chatKey";

	private static bool keyBindAdded;

	private static bool keyLoaded;

	private static KeyCode chatKey = (KeyCode)116;

	private static int seq;

	private static readonly Dictionary<int, Queue<long>> recent = new Dictionary<int, Queue<long>>();

	private static readonly List<Line> lines = new List<Line>();

	private static int lastSeen;

	private static bool greeted;

	private static RectTransform logRoot;

	private static CustomUIElement input;

	private static int openedFrame = -10;

	private static bool typingHeld;

	private const float FadeAfter = 10f;

	private const float FadeTime = 1f;

	private const float LineAlpha = 0.72f;

	private const int ShowClosed = 8;

	private const int ShowOpen = 14;

	public const string OwnerPrefix = "Server owner:";

	public static IReadOnlyList<Line> Lines => lines;

	public static string LastNotice { get; private set; } = "";

	public static bool IsOpen { get; private set; }

	public static int ClosedFrame { get; private set; } = -10;

	public static bool OwnsKeyboard
	{
		get
		{
			if (!IsOpen)
			{
				return Time.frameCount - ClosedFrame <= 1;
			}
			return true;
		}
	}

	public static KeyCode ChatKey
	{
		get
		{
			LoadKey();
			return chatKey;
		}
		set
		{
			chatKey = value;
			keyLoaded = true;
			try
			{
				ES3.Save<int>("bellun_probably_together.chatKey", (int)value);
			}
			catch (Exception ex)
			{
				Log.Warn("ui: chat key not saved: " + ex.Message);
			}
		}
	}

	internal static int ServerSeq => seq;

	internal static int RecentCount => recent.Count;

	internal static int LastSeen => lastSeen;

	internal static string ChatKeyName => KeyName(ChatKey);

	public static event Action<Line> LineAdded;

	internal static void Register()
	{
		Net.On<MsgChatSay>("chat.say", ServerOnSay, null);
		Net.On<MsgChatLine>("chat.line", null, ClientOnLine);
		Net.On<MsgChatNotice>("chat.notice", null, ClientOnNotice);
	}

	private static void LoadKey()
	{
		if (keyLoaded)
		{
			return;
		}
		keyLoaded = true;
		try
		{
			if (ES3.KeyExists("bellun_probably_together.chatKey"))
			{
				chatKey = (KeyCode)ES3.Load<int>("bellun_probably_together.chatKey");
			}
		}
		catch
		{
			chatKey = (KeyCode)116;
		}
		if ((int)chatKey == 0 || (int)chatKey == 27)
		{
			chatKey = (KeyCode)116;
		}
	}

	private static void EnsureKeyBind()
	{
		if (keyBindAdded || !Ui.AtMenu)
		{
			return;
		}
		keyBindAdded = true;
		try
		{
			GameOptionRegistry.EnsureVanilla();
			ModHelper.AddGameOptionKeyBind("bellun_probably_together", "keybind", "chat", (Func<string>)(() => Lang.Tr("Co-op chat")), (Func<KeyCode>)(() => ChatKey), (Action<KeyCode>)delegate(KeyCode k)
			{
				ChatKey = k;
			}, (Func<string>)(() => Lang.Tr("Opens the chat box when you are on a co-op server.")));
		}
		catch (Exception ex)
		{
			Log.Warn("ui: chat key bind row not added: " + ex.Message);
		}
	}

	private static void ServerOnSay(MsgChatSay m, int from)
	{
		string text = Clean(m.text);
		if (text.StartsWith("/trade", StringComparison.OrdinalIgnoreCase))
		{
			Net.ToPlayer(from, "chat.notice", new MsgChatNotice
			{
				text = ProbablyTogether.Systems.Specs.ChatCommand(from, text.Substring(6).Trim())
			});
			return;
		}
		if (text.StartsWith("/ledger", StringComparison.OrdinalIgnoreCase))
		{
			Net.ToPlayer(from, "chat.notice", new MsgChatNotice
			{
				text = ProbablyTogether.Systems.Standing.ChatCommand()
			});
			return;
		}
		if (text.StartsWith("/perks", StringComparison.OrdinalIgnoreCase))
		{
			Net.ToPlayer(from, "chat.notice", new MsgChatNotice
			{
				text = ProbablyTogether.Systems.Perks.ChatCommand(from, text.Substring(6))
			});
			return;
		}
		if (text.StartsWith("/retrade", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/remove", StringComparison.OrdinalIgnoreCase))
		{
			bool flag = text.StartsWith("/remove", StringComparison.OrdinalIgnoreCase);
			Net.ToPlayer(from, "chat.notice", new MsgChatNotice
			{
				text = ProbablyTogether.Systems.Leaving.ChatCommand(from, flag, text.Substring(flag ? 7 : 8).Trim())
			});
			return;
		}
		if (text.Length == 0)
		{
			Net.ToPlayer(from, "chat.notice", new MsgChatNotice
			{
				text = "Empty message: nothing sent."
			});
			return;
		}
		if (!recent.TryGetValue(from, out var value))
		{
			value = (recent[from] = new Queue<long>());
		}
		long ms = Clock.Ms;
		while (value.Count > 0 && ms - value.Peek() > 5000)
		{
			value.Dequeue();
		}
		if (value.Count >= 5)
		{
			Log.Info("server: chat from " + Net.PlayerName(from) + " refused (more than 5 lines in 5 s)");
			Net.ToPlayer(from, "chat.notice", new MsgChatNotice
			{
				text = "You are sending messages too fast. Wait a moment."
			});
			return;
		}
		value.Enqueue(ms);
		MsgChatLine msgChatLine = new MsgChatLine
		{
			n = ++seq,
			p = from,
			name = Net.PlayerName(from),
			text = text
		};
		Log.Info("server: chat.line #" + msgChatLine.n + " from " + msgChatLine.name + ": " + text);
		Net.ToAll("chat.line", msgChatLine);
	}

	public static void Announce(string text)
	{
		Post(text, owner: false);
	}

	public static void AnnounceOwner(string text)
	{
		Post(text, owner: true);
	}

	private static void Post(string text, bool owner)
	{
		if (Net.IsServer)
		{
			text = Clean(text);
			if (text.Length != 0)
			{
				MsgChatLine msgChatLine = new MsgChatLine
				{
					n = ++seq,
					p = 0,
					name = "",
					text = text,
					sys = true,
					own = owner
				};
				Log.Info("server: chat.line #" + msgChatLine.n + (owner ? " (server owner): " : " (system): ") + text);
				Net.ToAll("chat.line", msgChatLine);
			}
		}
	}

	internal static void ServerForget(int player)
	{
		recent.Remove(player);
	}

	internal static string Clean(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder(Math.Min(s.Length, 208));
		bool flag = false;
		for (int i = 0; i < s.Length; i++)
		{
			char c = s[i];
			if (char.IsHighSurrogate(c))
			{
				if (i + 1 >= s.Length || !char.IsLowSurrogate(s[i + 1]))
				{
					continue;
				}
				if (CharUnicodeInfo.GetUnicodeCategory(s, i) == UnicodeCategory.Format)
				{
					i++;
					continue;
				}
				if (stringBuilder.Length + 2 > 200)
				{
					break;
				}
				stringBuilder.Append(c).Append(s[i + 1]);
				i++;
				flag = false;
			}
			else
			{
				if (char.IsLowSurrogate(c))
				{
					continue;
				}
				if (char.IsControl(c) || c == '\u2028' || c == '\u2029')
				{
					c = ' ';
				}
				else if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format)
				{
					continue;
				}
				if (c == ' ')
				{
					if (flag || stringBuilder.Length == 0)
					{
						continue;
					}
					flag = true;
				}
				else
				{
					flag = false;
				}
				if (c == '<')
				{
					c = '‹';
				}
				stringBuilder.Append(c);
				if (stringBuilder.Length >= 200)
				{
					break;
				}
			}
		}
		return stringBuilder.ToString().TrimEnd();
	}

	public static bool Send(string text)
	{
		if (!Net.HasLocalPlayer || !Net.IsLive)
		{
			return false;
		}
		if (text == null)
		{
			return false;
		}
		text = text.Trim();
		if (text.Length == 0)
		{
			return false;
		}
		if (text.Length > 400)
		{
			text = text.Substring(0, 400);
		}
		Net.ToServer("chat.say", new MsgChatSay
		{
			text = text
		});
		return true;
	}

	public static void Local(string text)
	{
		if (!Ui.Headless && Net.HasLocalPlayer)
		{
			Add(new Line
			{
				Text = Kit.Safe(Lang.Tr(text)),
				System = true
			});
		}
	}

	private static void ClientOnLine(MsgChatLine m)
	{
		if (m != null && m.n > lastSeen)
		{
			lastSeen = m.n;
			Line line = new Line
			{
				N = m.n,
				Player = m.p,
				Name = (m.name ?? ""),
				Text = Clean(m.text),
				System = (m.sys || m.p == 0),
				Owner = (m.own && m.p == 0),
				History = DayLog.Replaying
			};
			if (line.System && !line.Owner)
			{
				line.Text = Lang.Tr(line.Text);
			}
			Log.Info("client: chat.line #" + m.n + (line.History ? " (today's log)" : "") + " from " + (line.Owner ? "the server owner" : (line.System ? "system" : line.Name)) + ": " + line.Text);
			Add(line);
		}
	}

	private static void ClientOnNotice(MsgChatNotice m)
	{
		LastNotice = m?.text ?? "";
		Log.Info("client: chat notice: " + LastNotice);
		Local(LastNotice);
	}

	private static void Add(Line l)
	{
		l.ShownAt = Time.realtimeSinceStartup;
		lines.Add(l);
		while (lines.Count > 60)
		{
			Line line = lines[0];
			lines.RemoveAt(0);
			if ((Object)(object)line.Label != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)((TMP_Text)line.Label).transform.parent).gameObject);
			}
		}
		if ((Object)(object)logRoot != (Object)null)
		{
			Draw(l);
		}
		try
		{
			Chat.LineAdded?.Invoke(l);
		}
		catch (Exception ex)
		{
			Log.Error("ui: Chat.LineAdded handler: " + ex);
		}
	}

	internal static void ClientReset()
	{
		if (IsOpen)
		{
			Close(byEscape: false);
		}
		foreach (Line line in lines)
		{
			if ((Object)(object)line.Label != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)((TMP_Text)line.Label).transform.parent).gameObject);
			}
		}
		lines.Clear();
		lastSeen = 0;
		greeted = false;
		LastNotice = "";
	}

	internal static void OnShopLoaded()
	{
		if (!greeted)
		{
			greeted = true;
			Local(Lang.Trf("You joined {0}. Press {1} to chat.", Kit.Name(Net.ServerName), KeyName(ChatKey)));
		}
	}

	private unsafe static string KeyName(KeyCode k)
	{
		try
		{
			return KeyBindHelper.KeyName(k);
		}
		catch
		{
			return k.ToString();
		}
	}

	internal static void OnOverlayBuilt()
	{
		logRoot = Kit.Rect("chat_log", (Transform)(object)Overlay.ChatArea);
		logRoot.anchorMin = new Vector2(0f, 0f);
		logRoot.anchorMax = new Vector2(1f, 0f);
		logRoot.pivot = new Vector2(0f, 0f);
		logRoot.anchoredPosition = Vector2.zero;
		VerticalLayoutGroup obj = ((Component)logRoot).gameObject.AddComponent<VerticalLayoutGroup>();
		((HorizontalOrVerticalLayoutGroup)obj).spacing = 2f;
		((HorizontalOrVerticalLayoutGroup)obj).childControlWidth = true;
		((HorizontalOrVerticalLayoutGroup)obj).childControlHeight = true;
		((HorizontalOrVerticalLayoutGroup)obj).childForceExpandWidth = false;
		((HorizontalOrVerticalLayoutGroup)obj).childForceExpandHeight = false;
		((Component)logRoot).gameObject.AddComponent<ContentSizeFitter>().verticalFit = (ContentSizeFitter.FitMode)2;
		foreach (Line line in lines)
		{
			Draw(line);
		}
		input = null;
		try
		{
			CustomUIBuilder obj2 = CustomUIManager.Instance.BuildInto(Overlay.InputRoot, true, "bellun_probably_together.chat");
			obj2.AddInput(Lang.Tr("Say something to the shop..."), "", (Action<string>)null, "text", (Action<string>)null);
			input = obj2.Last;
			if ((Object)(object)input?.input != (Object)null)
			{
				TMP_InputField obj3 = input.input;
				obj3.characterLimit = 200;
				obj3.lineType = TMP_InputField.LineType.SingleLine;
				((UnityEvent<string>)(object)obj3.onSubmit).AddListener((UnityAction<string>)delegate
				{
					Submit();
				});
				input.SetPreferredSize(540f, 40f).SetFlexible(1f, -1f);
				((Object)((Component)obj3).gameObject).name = "bellun_probably_together_chat_input";
			}
			else
			{
				Log.Warn("ui: chat input prefab has no TMP_InputField; chat can be read but not typed");
			}
		}
		catch (Exception ex)
		{
			Log.Warn("ui: chat input not built: " + ex.Message);
		}
	}

	private static void Draw(Line l)
	{
		Image obj = Kit.Box((Transform)(object)logRoot, "line_" + ((l.N > 0) ? l.N.ToString(CultureInfo.InvariantCulture) : "local"), new Color(Kit.PlateColor.r, Kit.PlateColor.g, Kit.PlateColor.b, 0.72f));
		HorizontalLayoutGroup obj2 = ((Component)obj).gameObject.AddComponent<HorizontalLayoutGroup>();
		((LayoutGroup)obj2).padding = new RectOffset(8, 8, 3, 3);
		((HorizontalOrVerticalLayoutGroup)obj2).childControlWidth = true;
		((HorizontalOrVerticalLayoutGroup)obj2).childControlHeight = true;
		((HorizontalOrVerticalLayoutGroup)obj2).childForceExpandWidth = false;
		((HorizontalOrVerticalLayoutGroup)obj2).childForceExpandHeight = false;
		TextMeshProUGUI val = Kit.Label(((Component)obj).transform, "text", 17f, (l.System && !l.Owner) ? Kit.Dim : Kit.Text, 520f, (TextAlignmentOptions)4097);
		Kit.SetText(val, OwnerLine(l) ?? (l.System ? ("<i>" + l.Text + "</i>") : ("<color=#" + Kit.Hex(Kit.PlayerColor(l.Player)) + ">" + Kit.Name(l.Name) + "</color>  " + l.Text)));
		l.Label = val;
	}

	internal static string OwnerLine(Line l)
	{
		if (l != null && l.Owner)
		{
			return "<color=#" + Kit.Hex(Kit.Warn) + "><b>" + Lang.Tr("Server owner:") + "</b></color>  " + l.Text;
		}
		return null;
	}

	public static void Open()
	{
		if (IsOpen || !Ui.ShopOnline || (Object)(object)input?.input == (Object)null)
		{
			return;
		}
		IsOpen = true;
		openedFrame = Time.frameCount;
		((Component)Overlay.InputRoot).gameObject.SetActive(true);
		TMP_InputField val = input.input;
		val.text = "";
		try
		{
			if ((Object)(object)EventSystem.current != (Object)null)
			{
				EventSystem.current.SetSelectedGameObject(((Component)val).gameObject);
			}
			val.ActivateInputField();
		}
		catch (Exception e)
		{
			UiErrors.Report("chat focus", e);
		}
		HoldTyping(on: true);
	}

	public static void Close()
	{
		Close(byEscape: false);
	}

	public static void CloseByEscape()
	{
		Close(byEscape: true);
	}

	private static void Close(bool byEscape)
	{
		if (!IsOpen)
		{
			return;
		}
		IsOpen = false;
		ClosedFrame = Time.frameCount;
		try
		{
			TMP_InputField val = input?.input;
			if ((Object)(object)val != (Object)null)
			{
				val.DeactivateInputField(false);
				if ((Object)(object)EventSystem.current != (Object)null && (Object)(object)EventSystem.current.currentSelectedGameObject == (Object)(object)((Component)val).gameObject)
				{
					EventSystem.current.SetSelectedGameObject((GameObject)null);
				}
			}
		}
		catch (Exception e)
		{
			UiErrors.Report("chat unfocus", e);
		}
		if ((Object)(object)Overlay.InputRoot != (Object)null)
		{
			((Component)Overlay.InputRoot).gameObject.SetActive(false);
		}
	}

	private static void Submit()
	{
		if (IsOpen && !((Object)(object)input?.input == (Object)null))
		{
			string text = input.input.text;
			Close(byEscape: false);
			if (!string.IsNullOrWhiteSpace(text))
			{
				Send(text);
			}
		}
	}

	private static void HoldTyping(bool on)
	{
		StoreUIManager instance = StoreUIManager.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			typingHeld = false;
			return;
		}
		if (on)
		{
			instance.StartTyping();
		}
		else
		{
			instance.StopTyping();
		}
		typingHeld = on;
	}

	internal static void Tick()
	{
		EnsureKeyBind();
		if (!Ui.ShopOnline)
		{
			if (IsOpen)
			{
				Close(byEscape: false);
			}
			typingHeld = false;
			return;
		}
		if (IsOpen)
		{
			HoldTyping(on: true);
			if (Input.GetKeyDown((KeyCode)27))
			{
				CloseByEscape();
			}
			else if (Input.GetKeyDown((KeyCode)13) || Input.GetKeyDown((KeyCode)271))
			{
				Submit();
			}
			else if (Time.frameCount - openedFrame > 3 && (Object)(object)input?.input != (Object)null && !input.input.isFocused && ((Object)(object)EventSystem.current == (Object)null || (Object)(object)EventSystem.current.currentSelectedGameObject != (Object)(object)((Component)input.input).gameObject))
			{
				Close(byEscape: false);
			}
			else if (Time.frameCount - openedFrame <= 2 && (Object)(object)input?.input != (Object)null && input.input.text.Length == 1 && char.ToUpperInvariant(input.input.text[0]) == ((object)ChatKey).ToString()[0])
			{
				input.input.text = "";
			}
		}
		else
		{
			if (typingHeld && Time.frameCount - ClosedFrame > 1)
			{
				HoldTyping(on: false);
			}
			if (Time.frameCount != ClosedFrame && Input.GetKeyDown(ChatKey) && !KeyBindHelper.SuppressHotkeys && !StoreUIManager.IsAnyTyping())
			{
				Open();
			}
		}
		Fade();
		if ((Object)(object)Overlay.ChatArea != (Object)null)
		{
			Overlay.ChatArea.anchoredPosition = new Vector2(16f, IsOpen ? 68f : 16f);
		}
	}

	private static void Fade()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		int num = (IsOpen ? 14 : 8);
		for (int i = 0; i < lines.Count; i++)
		{
			Line line = lines[i];
			if ((Object)(object)line.Label == (Object)null)
			{
				continue;
			}
			GameObject gameObject = ((Component)((TMP_Text)line.Label).transform.parent).gameObject;
			bool num2 = i >= lines.Count - num;
			float num3 = 1f;
			if (!IsOpen)
			{
				float num4 = realtimeSinceStartup - line.ShownAt - (line.History ? (-8f) : 0f);
				num3 = ((num4 < 10f) ? 1f : Mathf.Clamp01(1f - (num4 - 10f) / 1f));
			}
			bool flag = num2 && num3 > 0.01f;
			if (gameObject.activeSelf != flag)
			{
				gameObject.SetActive(flag);
			}
			if (flag && Mathf.Abs(((TMP_Text)line.Label).alpha - num3) > 0.01f)
			{
				((TMP_Text)line.Label).alpha = num3;
				Image component = gameObject.GetComponent<Image>();
				if ((Object)(object)component != (Object)null)
				{
					Color color = ((Graphic)component).color;
					color.a = 0.72f * num3;
					((Graphic)component).color = color;
				}
			}
		}
	}
}
