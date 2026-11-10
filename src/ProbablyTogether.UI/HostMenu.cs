using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProbablyTogether.UI;

/// "Host a shop" on the main menu: this game becomes the server and the host plays in it.
internal static class HostMenu
{
	public static GameObject HostButton { get; private set; }

	private static CustomUIWindow window;

	internal static string HostFolder => Paths.Original + "/probably-together-host";

	internal static bool DialogOpen => window != null;

	internal static void BuildButton(MainMenuUIController menu, GameObject moddingButton, GameObject joinButton, bool parentIsLayout)
	{
		if ((Object)(object)HostButton != (Object)null || (Object)(object)moddingButton == (Object)null || (Object)(object)joinButton == (Object)null)
		{
			return;
		}
		try
		{
			Transform parent = moddingButton.transform.parent;
			GameObject val = Object.Instantiate<GameObject>(moddingButton, parent);
			((Object)val).name = "humangenome_accomplice_host";
			TextMeshProUGUI label = val.GetComponentInChildren<TextMeshProUGUI>(true);
			Button button = val.GetComponentInChildren<Button>(true);
			if ((Object)(object)label == (Object)null || (Object)(object)button == (Object)null)
			{
				Object.Destroy((Object)(object)val);
				return;
			}
			button.onClick = new Button.ButtonClickedEvent();
			((UnityEvent)button.onClick).AddListener((UnityAction)delegate
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
			((TMP_Text)label).text = Lang.Tr("Host a shop");
			val.transform.SetSiblingIndex(joinButton.transform.GetSiblingIndex() + 1);
			if (!parentIsLayout)
			{
				RectTransform mine = val.transform as RectTransform;
				RectTransform join = joinButton.transform as RectTransform;
				if (mine != null && join != null)
				{
					mine.anchoredPosition = join.anchoredPosition + new Vector2(0f, 0f - (join.rect.height + 10f));
				}
			}
			val.SetActive(true);
			HostButton = val;
			Log.Info("ui: \"Host a shop\" button added next to Join server");
		}
		catch (Exception ex)
		{
			Log.Warn("ui: host button: " + ex.Message);
		}
	}

	public static void OpenDialog()
	{
		CustomUIManager instance = CustomUIManager.Instance;
		if ((Object)(object)instance == (Object)null || !Ui.AtMenu)
		{
			return;
		}
		if (Net.IsOnline)
		{
			Log.Info("ui: Host window not opened: already online as " + Net.Role);
			return;
		}
		JObject existing = ReadConfig();
		string defaultName = (SteamName(out var persona) ? (persona + "'s shop") : "My shop");
		CustomUIBuilder b = instance.CreateWindow("humangenome_accomplice.host", Lang.Tr("Host a shop"), "menu", (GameObject)null, (CustomUIManager.Style)0).SetCloseOnEscape(true).SetSize(640f, 640f);
		b.AddLabel(Lang.Tr("Your game runs the shop and you play in it. Friends join with your address and the password. Forward the port on your router."), (string)null)
			.AddLabel(Lang.Tr("Shop name"), (string)null)
			.AddInput("", Str(existing, "name", defaultName), (Action<string>)null, "name", (Action<string>)null)
			.AddLabel(Lang.Tr("Password (empty: anyone with the address can join)"), (string)null)
			.AddInput("", Str(existing, "password", ""), (Action<string>)null, "password", (Action<string>)null)
			.AddLabel(Lang.Tr("Port"), (string)null)
			.AddInput("", Str(existing, "port", "27700"), (Action<string>)null, "port", (Action<string>)null)
			.AddLabel(Lang.Tr("Players (1 to 4)"), (string)null)
			.AddInput("", Str(existing, "maxPlayers", "4"), (Action<string>)null, "max", (Action<string>)null);
		b.AddWarning("", "status");
		CustomUIBuilder row = JoinMenu.Row(b, 8f);
		RectTransform rowRect = row.CurrentContainer;
		row.AddFlexibleSpace().AddButton(Lang.Tr("Back"), (Action)CloseDialog, "back").AddButton(Lang.Tr("Host"), (Action)Start, "host");
		b.OnClose((Action)delegate
		{
			window = null;
		}).Center();
		window = b.Show();
		JoinMenu.FitButtonRow(window, rowRect, new string[2] { "back", "host" }, new string[0]);
		JoinMenu.FitHeight(window, 300f, 640f, 44f);
		CustomUIElement pw = window.Get("password");
		if ((Object)(object)pw?.input != (Object)null)
		{
			pw.input.contentType = TMP_InputField.ContentType.Password;
			pw.input.ForceLabelUpdate();
		}
		string[] ids = new string[4] { "name", "password", "port", "max" };
		foreach (string id in ids)
		{
			TMP_InputField input = window.Get(id)?.input;
			if ((Object)(object)input != (Object)null)
			{
				input.lineType = TMP_InputField.LineType.SingleLine;
				input.characterLimit = ((id == "name") ? 64 : ((id == "password") ? 128 : 5));
			}
		}
		string folderNote = (File.Exists(HostFolder + "/save_1.es3") ? Lang.Tr("A hosted shop exists: it continues.") : Lang.Tr("No hosted shop yet: a new run starts."));
		SetStatus(folderNote);
		Log.Info("ui: Host window opened (" + HostFolder + ")");
	}

	public static void CloseDialog()
	{
		CustomUIManager instance = CustomUIManager.Instance;
		if (instance != null)
		{
			instance.CloseWindow("humangenome_accomplice.host");
		}
		window = null;
	}

	private static void Start()
	{
		if (window == null)
		{
			return;
		}
		string name = (Field("name") ?? "").Trim();
		string password = Field("password") ?? "";
		if (!int.TryParse((Field("port") ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) || port < 1024 || port > 65535)
		{
			SetStatus(Lang.Tr("The port must be a number from 1024 to 65535."));
			return;
		}
		if (!int.TryParse((Field("max") ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var max) || max < 1 || max > 4)
		{
			SetStatus(Lang.Tr("Players must be 1 to 4."));
			return;
		}
		if (name.Length == 0)
		{
			name = "My shop";
		}
		if (!PortFree(port))
		{
			SetStatus(Lang.Tr("Port {0} is already in use on this PC.").Replace("{0}", port.ToString(CultureInfo.InvariantCulture)));
			return;
		}
		if (!Paths.SetOverride(HostFolder))
		{
			SetStatus(Lang.Tr("The hosted shop folder could not be created."));
			return;
		}
		try
		{
			JObject cfg = ReadConfig() ?? new JObject();
			cfg["port"] = (JToken)new JValue(port);
			cfg["name"] = (JToken)new JValue(name);
			cfg["password"] = (JToken)new JValue(password);
			cfg["maxPlayers"] = (JToken)new JValue(max);
			File.WriteAllText(HostFolder + "/server.json", ((JToken)cfg).ToString((Newtonsoft.Json.Formatting)1, Array.Empty<Newtonsoft.Json.JsonConverter>()));
		}
		catch (Exception ex)
		{
			SetStatus(Lang.Tr("Could not write the shop settings: ") + ex.Message);
			return;
		}
		Paths.KeepSettings = true;
		Net.Role = Role.Host;
		Log.Info("host: starting from the menu: '" + name + "' on port " + port + ", " + max + " player(s), folder " + HostFolder);
		LoadingScreen.HostStarting = true;
		ServerMode.Start();
		ServerMode.SkipMenuWait();
		CloseDialog();
	}

	private static bool PortFree(int port)
	{
		try
		{
			TcpListener l = new TcpListener(IPAddress.Any, port);
			l.Start();
			l.Stop();
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static JObject ReadConfig()
	{
		try
		{
			string path = HostFolder + "/server.json";
			if (File.Exists(path))
			{
				return JObject.Parse(File.ReadAllText(path));
			}
		}
		catch (Exception ex)
		{
			Log.Warn("host: server.json unreadable, starting from defaults: " + ex.Message);
		}
		return null;
	}

	private static string Str(JObject o, string key, string fallback)
	{
		try
		{
			JToken t = ((o != null) ? o[key] : null);
			if (t != null && (int)t.Type != 10)
			{
				return ((object)t).ToString();
			}
		}
		catch
		{
		}
		return fallback;
	}

	private static string Field(string id)
	{
		TMP_InputField input = window?.Get(id)?.input;
		return ((Object)(object)input != (Object)null) ? input.text : null;
	}

	private static void SetStatus(string s)
	{
		try
		{
			CustomUIElement el = window?.Get("status");
			if (el != null && (Object)(object)el.text != (Object)null)
			{
				Kit.SetText(el.text, s ?? "");
			}
		}
		catch
		{
		}
	}

	private static bool SteamName(out string name)
	{
		name = null;
		try
		{
			if (SteamManager.Initialized)
			{
				name = Steamworks.SteamFriends.GetPersonaName();
				return !string.IsNullOrEmpty(name);
			}
		}
		catch
		{
		}
		return false;
	}
}
