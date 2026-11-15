using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
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
	private sealed class Slot
	{
		public int N;

		public string Name;

		public string Label;
	}

	private static readonly string[] DifficultyIds = new string[4] { "cozy", "normal", "hard", "challenge" };

	private static readonly string[] DifficultyNames = new string[4] { "Price Gouge (cozy)", "Mark Up (normal)", "Thin Margins (hard)", "Red Ink (challenge)" };

	public static GameObject HostButton { get; private set; }

	private static CustomUIWindow window;

	private static readonly List<Slot> slots = new List<Slot>();

	private static readonly List<KeyValuePair<string, string>> scenarios = new List<KeyValuePair<string, string>>();

	private static int slotIndex;

	private static int scenarioIndex;

	private static int difficultyIndex = 1;

	private static bool skipIntro = true;

	private static bool endless;

	private static bool individual;

	private static bool ownership = true;

	/// Where hosted shops live; --br-host-folder=<path> overrides it.
	internal static string HostFolder
	{
		get
		{
			string text = Args.Get("--br-host-folder");
			return string.IsNullOrEmpty(text) ? (Paths.Original + "/probably-together-host") : text.Replace('\\', '/').TrimEnd('/');
		}
	}

	internal static bool DialogOpen => window != null;

	private static bool NewSelected => slotIndex >= slots.Count;

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
			((Object)val).name = "bellun_probably_together_host";
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
				if (!HostFlow.Open())
				{
					OpenDialog();
				}
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

	private static void LoadSlots(JObject config)
	{
		slots.Clear();
		string folder = HostFolder;
		int configured = 1;
		try
		{
			JToken t = ((config != null) ? config["saveSlot"] : null);
			if (t != null)
			{
				configured = (int)t;
			}
		}
		catch
		{
		}
		try
		{
			if (System.IO.Directory.Exists(folder))
			{
				List<int> found = new List<int>();
				foreach (string file in System.IO.Directory.GetFiles(folder, "save_*.es3"))
				{
					Match m = Regex.Match(Path.GetFileName(file), "^save_(\\d+)\\.es3$");
					if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n > 0)
					{
						found.Add(n);
					}
				}
				found.Sort();
				foreach (int n2 in found)
				{
					slots.Add(Describe(folder, n2, n2 == configured, config));
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("host: listing shops: " + ex.Message);
		}
		slotIndex = slots.Count;
		for (int i = 0; i < slots.Count; i++)
		{
			if (slots[i].N == configured)
			{
				slotIndex = i;
			}
		}
		if (slotIndex >= slots.Count && slots.Count > 0)
		{
			slotIndex = slots.Count - 1;
		}
	}

	private static Slot Describe(string folder, int n, bool isConfigured, JObject config)
	{
		Slot slot = new Slot
		{
			N = n,
			Name = null
		};
		JObject card = ReadJson(folder + "/shop_" + n.ToString(CultureInfo.InvariantCulture) + ".json");
		if (card == null && isConfigured)
		{
			card = ReadJson(folder + "/status.json");
		}
		string detail = "";
		if (card != null)
		{
			slot.Name = Str(card, "name", null);
			string day = Str(card, "day", "");
			string cash = Str(card, "cash", "");
			string scenario = Str(card, "scenario", "");
			detail = ((day.Length > 0) ? (Lang.Tr("day") + " " + day) : "") + ((cash.Length > 0) ? (", " + cash + " " + Lang.Tr("credits")) : "") + ((scenario.Length > 0) ? (", " + ScenarioTitle(scenario)) : "");
		}
		if (slot.Name == null && isConfigured)
		{
			slot.Name = Str(config, "name", null);
		}
		slot.Label = (slot.Name ?? (Lang.Tr("Shop") + " " + n.ToString(CultureInfo.InvariantCulture))) + ((detail.Length > 0) ? ("  (" + detail.TrimStart(',', ' ') + ")") : "");
		return slot;
	}

	private static void LoadScenarios(JObject config)
	{
		scenarios.Clear();
		try
		{
			foreach (KeyValuePair<string, string> item in ServerMode.PlayableScenarioList())
			{
				scenarios.Add(item);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("host: listing scenarios: " + ex.Message);
		}
		if (scenarios.Count == 0)
		{
			scenarios.Add(new KeyValuePair<string, string>("WATER_MERCHANT", "Water Merchant"));
		}
		string wanted = "WATER_MERCHANT";
		string wantedDifficulty = "normal";
		try
		{
			JObject nr = ((config != null) ? (config["newRun"] as JObject) : null);
			if (nr != null)
			{
				wanted = Str(nr, "scenario", wanted);
				wantedDifficulty = Str(nr, "difficulty", wantedDifficulty);
				JToken si = nr["skipIntro"];
				if (si != null)
				{
					skipIntro = (bool)si;
				}
			}
			JToken io = ((config != null) ? config["individualOutings"] : null);
			if (io != null)
			{
				individual = (bool)io;
			}
			JToken po = ((config != null) ? config["playerOwnership"] : null);
			if (po != null)
			{
				ownership = (bool)po;
			}
			JToken en = ((config != null) ? config["endless"] : null);
			if (en != null)
			{
				endless = (bool)en;
			}
		}
		catch
		{
		}
		scenarioIndex = 0;
		for (int i = 0; i < scenarios.Count; i++)
		{
			if (string.Equals(scenarios[i].Key, wanted, StringComparison.OrdinalIgnoreCase))
			{
				scenarioIndex = i;
			}
		}
		difficultyIndex = Math.Max(0, Array.IndexOf(DifficultyIds, wantedDifficulty.ToLowerInvariant()));
	}

	private static string ScenarioTitle(string id)
	{
		foreach (KeyValuePair<string, string> scenario in scenarios)
		{
			if (string.Equals(scenario.Key, id, StringComparison.OrdinalIgnoreCase))
			{
				return scenario.Value;
			}
		}
		return id;
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
		JObject existing = ReadJson(HostFolder + "/server.json");
		LoadScenarios(existing);
		LoadSlots(existing);
		bool flow = HostFlow.Active && HostFlow.Slot != 0;
		if (flow)
		{
			// The host flow's last step: the shop (or a new one) was chosen on the game's own screens.
			int num0 = (HostFlow.Slot > 0) ? slots.FindIndex((Slot s) => s.N == HostFlow.Slot) : (-1);
			slotIndex = (num0 >= 0) ? num0 : slots.Count;
		}
		string defaultName = (SteamName(out var persona) ? (persona + "'s shop") : "My shop");
		List<string> slotLabels = new List<string>();
		foreach (Slot slot in slots)
		{
			slotLabels.Add(slot.Label);
		}
		slotLabels.Add(Lang.Tr("New shop"));
		List<string> scenarioLabels = new List<string>();
		foreach (KeyValuePair<string, string> scenario in scenarios)
		{
			scenarioLabels.Add(scenario.Value);
		}
		string initialName = (NewSelected ? ((slots.Count == 0) ? Str(existing, "name", defaultName) : defaultName) : (slots[slotIndex].Name ?? Str(existing, "name", defaultName)));
		CustomUIBuilder b = instance.CreateWindow("bellun_probably_together.host", Lang.Tr("Host a shop"), "menu", (GameObject)null, (CustomUIManager.Style)0).SetCloseOnEscape(true).SetSize(640f, 900f);
		b.AddLabel(Lang.Tr("Shop to open"), "slotlabel")
			.AddDropdown((IList<string>)slotLabels, slotIndex, (Action<int>)OnSlotChanged, "slot")
			.AddLabel(Lang.Tr("Shop name"), (string)null)
			.AddInput("", initialName, (Action<string>)null, "name", (Action<string>)null)
			.AddLabel(Lang.Tr("Password (empty: anyone with the address can join)"), (string)null)
			.AddInput("", Str(existing, "password", ""), (Action<string>)null, "password", (Action<string>)null)
			.AddLabel(Lang.Tr("Port (forward it on your router)"), (string)null)
			.AddInput("", Str(existing, "port", "27700"), (Action<string>)null, "port", (Action<string>)null)
			.AddLabel(Lang.Tr("Players (1 to 4)"), (string)null)
			.AddInput("", Str(existing, "maxPlayers", "4"), (Action<string>)null, "max", (Action<string>)null)
			.AddToggle(Lang.Tr("Everyone goes out at night on their own (one trip per player each night)"), individual, (Action<bool>)delegate(bool v)
			{
				individual = v;
			}, "individual")
			.AddToggle(Lang.Tr("Each player has a storage bay and a trade of their own"), ownership, (Action<bool>)delegate(bool v)
			{
				ownership = v;
			}, "ownership")
			.AddLabel(Lang.Tr("Extra customers a day for each added player"), (string)null)
			.AddInput("", Str(existing, "customersPerPlayer", "2"), (Action<string>)null, "custper", (Action<string>)null)
			.AddLabel(Lang.Tr("Rent increase for each added player (%)"), (string)null)
			.AddInput("", Str(existing, "rentPercentPerPlayer", "50"), (Action<string>)null, "rentper", (Action<string>)null)
			.AddSeparator("newsep", 4)
			.AddLabel(Lang.Tr("New shop: scenario and difficulty"), "newlabel")
			.AddDropdown((IList<string>)scenarioLabels, scenarioIndex, (Action<int>)delegate(int i)
			{
				scenarioIndex = i;
			}, "scenario")
			.AddDropdown((IList<string>)DifficultyNames, difficultyIndex, (Action<int>)delegate(int i)
			{
				difficultyIndex = i;
			}, "difficulty")
			.AddToggle(Lang.Tr("Skip the intro"), skipIntro, (Action<bool>)delegate(bool v)
			{
				skipIntro = v;
			}, "skipintro")
			.AddToggle(Lang.Tr("Endless mode"), endless, (Action<bool>)delegate(bool v)
			{
				endless = v;
			}, "endless");
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
		JoinMenu.FitHeight(window, 300f, 900f, 44f);
		CustomUIElement pw = window.Get("password");
		if ((Object)(object)pw?.input != (Object)null)
		{
			pw.input.contentType = TMP_InputField.ContentType.Password;
			pw.input.ForceLabelUpdate();
		}
		string[] ids = new string[6] { "name", "password", "port", "max", "custper", "rentper" };
		foreach (string id in ids)
		{
			TMP_InputField input = window.Get(id)?.input;
			if ((Object)(object)input != (Object)null)
			{
				input.lineType = TMP_InputField.LineType.SingleLine;
				input.characterLimit = ((id == "name") ? 64 : ((id == "password") ? 128 : 5));
			}
		}
		ApplySlot(setName: false);
		if (flow)
		{
			foreach (string id2 in new string[9] { "slotlabel", "slot", "newsep", "newlabel", "scenario", "difficulty", "skipintro", "endless", "newsep2" })
			{
				window.Get(id2)?.SetVisible(false);
			}
			if (NewSelected)
			{
				SetStatus(Lang.Tr("A new shop starts on day 1, with the trade, perks and difficulty you just chose."));
			}
			else if (HostFlow.MorningFile != null)
			{
				SetStatus(Lang.Tr("This shop continues from the earlier morning you chose. Its later days are replaced when it saves."));
			}
			JoinMenu.FitHeight(window, 300f, 900f, 44f);
		}
		Log.Info("ui: Host window opened (" + HostFolder + "; " + slots.Count + " shop(s), " + scenarios.Count + " scenario(s))");
	}

	private static void OnSlotChanged(int index)
	{
		slotIndex = index;
		ApplySlot(setName: true);
	}

	private static void ApplySlot(bool setName)
	{
		if (window == null)
		{
			return;
		}
		bool isNew = NewSelected;
		try
		{
			string[] ids = new string[4] { "scenario", "difficulty", "skipintro", "endless" };
			foreach (string id in ids)
			{
				CustomUIElement el = window.Get(id);
				if (el != null)
				{
					el.SetInteractable(isNew);
				}
			}
			if (setName && !isNew && slots[slotIndex].Name != null)
			{
				TMP_InputField input = window.Get("name")?.input;
				if ((Object)(object)input != (Object)null)
				{
					input.text = slots[slotIndex].Name;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("host: window update: " + ex.Message);
		}
		SetStatus(JoinMenu.UpstreamWarning ? JoinMenu.UpstreamText : (isNew ? Lang.Tr("A new shop starts on day 1.") : Lang.Tr("This shop continues from its last saved morning.")));
	}

	public static void CloseDialog()
	{
		CustomUIManager instance = CustomUIManager.Instance;
		if (instance != null)
		{
			instance.CloseWindow("bellun_probably_together.host");
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
		if (!double.TryParse((Field("custper") ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var custPer) || custPer < 0.0 || custPer > 6.0)
		{
			SetStatus(Lang.Tr("Extra customers per player must be a number from 0 to 6."));
			return;
		}
		if (!int.TryParse((Field("rentper") ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rentPer) || rentPer < 0 || rentPer > 500)
		{
			SetStatus(Lang.Tr("The rent increase per player must be a whole number from 0 to 500."));
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
		bool isNew = NewSelected;
		int slotNumber;
		if (isNew)
		{
			slotNumber = 1;
			foreach (Slot slot in slots)
			{
				slotNumber = Math.Max(slotNumber, slot.N + 1);
			}
		}
		else
		{
			slotNumber = slots[slotIndex].N;
		}
		string folder = HostFolder;
		if (!Paths.SetOverride(folder))
		{
			SetStatus(Lang.Tr("The hosted shop folder could not be created."));
			return;
		}
		string scenario = scenarios[Mathf.Clamp(scenarioIndex, 0, scenarios.Count - 1)].Key;
		string difficulty = DifficultyIds[Mathf.Clamp(difficultyIndex, 0, DifficultyIds.Length - 1)];
		bool flow = HostFlow.Active && HostFlow.Slot != 0;
		bool skip = skipIntro;
		bool endlessRun = endless;
		if (flow && isNew)
		{
			NewGameData instance = NewGameData.Instance;
			scenario = instance.startType.ToString();
			difficulty = string.IsNullOrEmpty(instance.difficultyId) ? "normal" : instance.difficultyId;
			skip = instance.skipIntro;
			endlessRun = instance.endlessMode;
			if (!scenarios.Any((KeyValuePair<string, string> s) => string.Equals(s.Key, scenario, StringComparison.OrdinalIgnoreCase)))
			{
				SetStatus(Lang.Tr("That trade cannot start a shared shop. Go back and pick another."));
				return;
			}
		}
		try
		{
			JObject cfg = ReadJson(folder + "/server.json") ?? new JObject();
			cfg["port"] = (JToken)new JValue(port);
			cfg["name"] = (JToken)new JValue(name);
			cfg["password"] = (JToken)new JValue(password);
			cfg["maxPlayers"] = (JToken)new JValue(max);
			cfg["saveSlot"] = (JToken)new JValue(slotNumber);
			cfg["individualOutings"] = (JToken)new JValue(individual);
			cfg["playerOwnership"] = (JToken)new JValue(ownership);
			cfg["customersPerPlayer"] = (JToken)new JValue(custPer);
			cfg["rentPercentPerPlayer"] = (JToken)new JValue(rentPer);
			if (isNew)
			{
				JObject nr = new JObject();
				nr["scenario"] = (JToken)new JValue(scenario);
				nr["difficulty"] = (JToken)new JValue(difficulty);
				nr["skipIntro"] = (JToken)new JValue(skip);
				cfg["newRun"] = (JToken)nr;
				cfg["endless"] = (JToken)new JValue(endlessRun);
			}
			File.WriteAllText(folder + "/server.json", ((JToken)cfg).ToString((Newtonsoft.Json.Formatting)1, Array.Empty<Newtonsoft.Json.JsonConverter>()));
		}
		catch (Exception ex)
		{
			SetStatus(Lang.Tr("Could not write the shop settings: ") + ex.Message);
			return;
		}
		Paths.KeepSettings = true;
		Net.Role = Role.Host;
		Log.Info("host: starting from the menu: '" + name + "' on port " + port + ", " + max + " player(s), slot " + slotNumber + (isNew ? (" (new: " + scenario + ", " + difficulty + ")") : " (continue)") + ", folder " + folder);
		LoadingScreen.HostStarting = true;
		// A shop made on the game's own screens keeps their choices; the server does not overwrite them.
		ServerMode.KeepMenuChoices = flow && isNew;
		ProbablyTogether.Systems.Perks.FounderPickedOnMenu = flow && isNew;
		ServerMode.MorningFile = (flow && !isNew) ? HostFlow.MorningFile : null;
		if (flow)
		{
			HostFlow.Hosting();
		}
		ServerMode.Start();
		ServerMode.SkipMenuWait();
		CloseDialog();
	}

	/// Headless test: --br-ui-host --br-ui-hostflow walks the host flow; --br-ui-host-slot=new (--br-ui-host-scenario=<ID>,
	/// --br-ui-host-perks=a,b) or =<n> (--br-ui-host-morning=<file>); --br-ui-host-go presses Host.
	private static void FlowTest()
	{
		try
		{
			bool flag = HostFlow.Open();
			List<string> list = new List<string>();
			foreach (int orderedSlotId in SaveUIManager.GetOrderedSlotIds())
			{
				SavePreviewData preview = SaveUIManager.GetPreview(orderedSlotId);
				list.Add(orderedSlotId + "=" + ((preview != null) ? (preview.GetDisplayName() + " day " + preview.dayCount) : "?"));
			}
			Log.Info("ui: host flow test: list " + (flag ? "up" : ("NOT up: " + HostFlow.LastProblem)) + "; shops [" + string.Join(" | ", list) + "]; load screen open " + LoadGameUI.IsOpen);
			if (!flag)
			{
				return;
			}
			MainMenuUIController instance = MainMenuUIController.Instance;
			NewGameData instance2 = NewGameData.Instance;
			string text = Args.Get("--br-ui-host-slot") ?? "new";
			if (text == "new")
			{
				HostFlow.NewShop();
				string want = Args.Get("--br-ui-host-scenario") ?? "WATER_MERCHANT";
				StartingScenario startingScenario = StartingScenario.GetVisible().FirstOrDefault((StartingScenario x) => string.Equals(x.startType.ToString(), want, StringComparison.OrdinalIgnoreCase));
				CustomUIManager instance3 = CustomUIManager.Instance;
				Log.Info("ui: host flow test: trade screen " + ((instance3.GetWindow("new_game_specialization") != null && instance3.GetWindow("new_game_specialization").IsAlive) ? "up" : "NOT up") + "; scenario " + ((startingScenario != null) ? startingScenario.id : "not found"));
				if (startingScenario == null)
				{
					return;
				}
				instance2.startType = startingScenario.startType;
				instance2.startScenarioId = startingScenario.id;
				NewGameSpecializationUI.Close();
				instance.OpenPerkInterface();
				bool flag2 = instance3.GetWindow("new_game_perks") != null && instance3.GetWindow("new_game_perks").IsAlive;
				foreach (string item in (Args.Get("--br-ui-host-perks") ?? "").Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries))
				{
					StartingPerk startingPerk = ProbablyTogether.Systems.Perks.Def(item.Trim());
					if (startingPerk != null)
					{
						instance2.startingPerks.Add(startingPerk);
					}
				}
				Log.Info("ui: host flow test: perk screen " + (flag2 ? "up" : "NOT up") + "; picked [" + string.Join(",", instance2.startingPerks.Select((StartingPerk q) => q.id)) + "]");
				NewGamePerksUI.CloseWithoutReturning();
				instance.OnPerksContinue();
				bool flag3 = instance3.GetWindow("new_game_difficulty") != null && instance3.GetWindow("new_game_difficulty").IsAlive;
				Log.Info("ui: host flow test: difficulty screen " + (flag3 ? "up" : "NOT up"));
				instance2.difficultyId = "normal";
				NewGameDifficultyUI.CloseWithoutReturning();
				instance.OnPerkFinishedClick();
			}
			else
			{
				instance2.saveSlot = int.Parse(text, CultureInfo.InvariantCulture);
				instance2.saveFile = Args.Get("--br-ui-host-morning");
				LoadGameUI.Close();
				instance.StartNewGameClick();
			}
			Log.Info("ui: host flow test: last step " + ((window != null) ? "up" : "NOT up") + "; flow slot " + HostFlow.Slot + "; name '" + (Field("name") ?? "") + "', port " + (Field("port") ?? "") + "; reached " + HostFlow.Reached);
			if (window != null && Args.Has("--br-ui-host-go"))
			{
				Start();
			}
		}
		catch (Exception ex)
		{
			Log.Error("ui: host flow test threw: " + ex);
		}
	}

	/// Headless test: --br-ui-host opens the window, --br-ui-host-slot=new|<n> and --br-ui-host-scenario=<ID> choose,
	/// --br-ui-host-go presses Host.
	internal static void TestHook()
	{
		if (!Args.Has("--br-ui-host"))
		{
			return;
		}
		if (Args.Has("--br-ui-hostflow"))
		{
			FlowTest();
			return;
		}
		try
		{
			OpenDialog();
			if (window == null)
			{
				Log.Error("ui: host test: the window did not open");
				return;
			}
			string wantSlot = Args.Get("--br-ui-host-slot");
			if (!string.IsNullOrEmpty(wantSlot))
			{
				int index = slots.Count;
				if (wantSlot != "new" && int.TryParse(wantSlot, out var n))
				{
					index = slots.FindIndex((Slot s) => s.N == n);
					if (index < 0)
					{
						Log.Error("ui: host test: no shop in slot " + n);
						return;
					}
				}
				SetDropdown("slot", index);
			}
			string wantScenario = Args.Get("--br-ui-host-scenario");
			if (!string.IsNullOrEmpty(wantScenario))
			{
				int index2 = scenarios.FindIndex((KeyValuePair<string, string> s) => string.Equals(s.Key, wantScenario, StringComparison.OrdinalIgnoreCase));
				if (index2 >= 0)
				{
					SetDropdown("scenario", index2);
				}
			}
			List<string> labels = new List<string>();
			foreach (Slot slot in slots)
			{
				labels.Add(slot.N + "=" + slot.Label);
			}
			Log.Info("ui: host test: window open; shops [" + string.Join(" | ", labels) + "]; selected " + (NewSelected ? "new" : slots[slotIndex].N.ToString()) + "; scenario " + scenarios[scenarioIndex].Key + " of " + scenarios.Count + "; difficulty " + DifficultyIds[difficultyIndex] + "; scenario control enabled " + (window.Get("scenario")?.dropdown != null && ((Selectable)window.Get("scenario").dropdown).interactable));
			if (Args.Has("--br-ui-host-go"))
			{
				Start();
			}
		}
		catch (Exception ex)
		{
			Log.Error("ui: host test threw: " + ex);
		}
	}

	private static void SetDropdown(string id, int index)
	{
		TMP_Dropdown dd = window?.Get(id)?.dropdown;
		if ((Object)(object)dd != (Object)null)
		{
			dd.value = index;
		}
		if (id == "slot" && slotIndex != index)
		{
			OnSlotChanged(index);
		}
		if (id == "scenario")
		{
			scenarioIndex = index;
		}
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

	private static JObject ReadJson(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				return JObject.Parse(File.ReadAllText(path));
			}
		}
		catch (Exception ex)
		{
			Log.Warn("host: " + Path.GetFileName(path) + " unreadable: " + ex.Message);
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
