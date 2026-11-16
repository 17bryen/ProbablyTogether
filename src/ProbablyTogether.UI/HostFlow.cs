using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ProbablyTogether.Systems;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.UI;

/// Hosting through the game's own screens: setup, the hosted save list, a new shop's rules, then New Game.
/// While Active the screens use the hosted folder, and StartNewGameClick hosts the shop.
internal static class HostFlow
{
	private const string ListWindow = "load_game";

	private const string SpecWindow = "new_game_specialization";

	private const string PerkWindow = "new_game_perks";

	private const string DifficultyWindow = "new_game_difficulty";

	/// While on, the data folder is the hosted folder and starting a game hosts it.
	internal static bool Active;

	/// 0 none chosen, -1 a new shop, above 0 the shop in that slot.
	internal static int Slot;

	/// An earlier morning's save to continue from, or null for the latest.
	internal static string MorningFile;

	private static HostPanels.Rules pending;

	private static bool syncing;

	private static bool moving;

	private static string last = "";

	private static float idleSince = -1f;

	internal static int Opened;

	internal static int Reached;

	internal static string LastProblem = "";

	private static CustomUIWindow Window(string id)
	{
		try
		{
			CustomUIManager instance = CustomUIManager.Instance;
			CustomUIWindow customUIWindow = ((Object)(object)instance != (Object)null) ? instance.GetWindow(id) : null;
			return (customUIWindow != null && customUIWindow.IsAlive) ? customUIWindow : null;
		}
		catch
		{
			return null;
		}
	}

	private static string ConfigPath => HostMenu.HostFolder + "/server.json";

	private static JObject Config()
	{
		try
		{
			return File.Exists(ConfigPath) ? JObject.Parse(File.ReadAllText(ConfigPath)) : new JObject();
		}
		catch
		{
			return new JObject();
		}
	}

	private static void WriteConfig(JObject cfg)
	{
		File.WriteAllText(ConfigPath, cfg.ToString());
	}

	private static string CardPath(int slot)
	{
		return HostMenu.HostFolder + "/shop_" + slot + ".json";
	}

	private static JObject Card(int slot)
	{
		try
		{
			string path = CardPath(slot);
			return File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : null;
		}
		catch
		{
			return null;
		}
	}

	private static string EndedNote(JObject card)
	{
		string text = (card != null) ? (string)card["ended"] : null;
		if (string.IsNullOrEmpty(text))
		{
			return "";
		}
		return Lang.Trf(" (run ended on day {0}: {1})", ((string)card["endedDay"]) ?? "?", text);
	}

	private static string Strip(string shown)
	{
		int num = (shown ?? "").IndexOf(" (run ended", StringComparison.Ordinal);
		return ((num >= 0) ? shown.Substring(0, num) : (shown ?? "")).Trim();
	}

	/// The game forgets a run's custom name at every save; put the shop's name back.
	private static void SyncNames()
	{
		syncing = true;
		try
		{
			foreach (int item in Paths.IndexLoad("activeSaveSlotId", new List<int>()).ToList())
			{
				JObject jObject = Card(item);
				string text = (jObject != null) ? (string)jObject["name"] : null;
				if (string.IsNullOrWhiteSpace(text))
				{
					continue;
				}
				string text2 = text + EndedNote(jObject);
				SavePreviewData savePreviewData = Paths.IndexLoad<SavePreviewData>("preview_" + item, null);
				if (savePreviewData != null && savePreviewData.customName != text2)
				{
					SaveFiles.RenameSlot(item, text2);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("host: putting the shops' names on the list threw: " + ex.Message);
		}
		finally
		{
			syncing = false;
		}
	}

	internal static void Renamed(int slot, string name)
	{
		if (!Active || syncing || slot <= 0)
		{
			return;
		}
		try
		{
			string text = Strip(name);
			JObject jObject = Card(slot) ?? new JObject();
			if (text.Length == 0)
			{
				jObject.Remove("name");
			}
			else
			{
				jObject["name"] = new JValue(text);
			}
			File.WriteAllText(CardPath(slot), jObject.ToString());
			Log.Info("host: shop " + slot + " is now called '" + text + "'");
		}
		catch (Exception ex)
		{
			Log.Warn("host: the new name of shop " + slot + " was not kept: " + ex.Message);
		}
	}

	internal static void Deleted(int slot)
	{
		if (!Active || slot <= 0)
		{
			return;
		}
		try
		{
			if (File.Exists(CardPath(slot)))
			{
				File.Delete(CardPath(slot));
			}
			Log.Info("host: shop " + slot + " deleted from the list of hosted shops");
		}
		catch (Exception ex)
		{
			Log.Warn("host: the card of deleted shop " + slot + " stayed: " + ex.Message);
		}
	}

	/// False if the game's screens could not be used; the old window opens instead.
	internal static bool Open()
	{
		CustomUIManager instance = CustomUIManager.Instance;
		if (!Ui.AtMenu || Net.IsOnline || (Object)(object)instance == (Object)null || (Object)(object)instance.loadGamePrefab == (Object)null || (Object)(object)instance.newGameDifficultyPrefab == (Object)null)
		{
			return false;
		}
		try
		{
			if (!Paths.SetOverride(HostMenu.HostFolder))
			{
				LastProblem = "the hosted shop folder could not be created";
				return false;
			}
			Paths.KeepSettings = true;
			SaveUIManager.InvalidateCache();
			Active = true;
			scanned = false;
			Slot = 0;
			MorningFile = null;
			pending = null;
			ShowSetup();
			if (!HostPanels.SetupOpen)
			{
				throw new InvalidOperationException("the setup screen did not open");
			}
			Opened++;
			Log.Info("host: the host flow is on (hosted shops in " + HostMenu.HostFolder + ")");
			return true;
		}
		catch (Exception ex)
		{
			LastProblem = "setup: " + ex.Message;
			Log.Warn("host: the game's screens could not be used for hosting, the old window comes instead: " + ex.Message);
			End("the setup screen could not open");
			return false;
		}
	}

	private static void ShowSetup()
	{
		JObject jObject = Config();
		int result;
		int port = (int.TryParse((string)jObject["port"], NumberStyles.Integer, CultureInfo.InvariantCulture, out result) && result >= 1024 && result <= 65535) ? result : 27700;
		HostPanels.OpenSetup(((string)jObject["password"]) ?? "", port, SetupDone);
		last = HostPanels.SetupWindow;
		idleSince = -1f;
	}

	private static void SetupDone(string password, int port)
	{
		moving = true;
		try
		{
			JObject jObject = Config();
			jObject["password"] = new JValue(password ?? "");
			jObject["port"] = new JValue(port);
			WriteConfig(jObject);
			HostPanels.CloseSetup();
			SyncNames();
			SaveUIManager.InvalidateCache();
			ShowList();
			Log.Info("host: port " + port + ", " + (string.IsNullOrEmpty(password) ? "no password" : "with a password") + "; the list of hosted shops is up (" + SaveUIManager.GetOrderedSlotIds().Count + " shop(s))");
		}
		catch (Exception ex)
		{
			LastProblem = "list: " + ex.Message;
			Log.Error("host: the list of hosted shops did not open: " + ex);
		}
		finally
		{
			moving = false;
		}
	}

	private static void ShowList()
	{
		try
		{
			// Scan once per visit; Back also returns to the list.
			List<int> list = scanned ? null : SaveFiles.ScanAndRegisterOrphans();
			scanned = true;
			if (list != null && list.Count > 0)
			{
				Log.Info("host: " + list.Count + " save file(s) found in the folder and added to the list: slot " + string.Join(", ", list));
			}
		}
		catch (Exception ex)
		{
			Log.Warn("host: looking for unlisted save files threw: " + ex.Message);
		}
		LoadGameUI.Open();
		CustomUIWindow customUIWindow = Window(ListWindow);
		LoadGameFrame loadGameFrame = (customUIWindow != null) ? customUIWindow.root.GetComponent<LoadGameFrame>() : null;
		if ((Object)(object)loadGameFrame == (Object)null)
		{
			return;
		}
		last = ListWindow;
		idleSince = -1f;
		try
		{
			if ((Object)(object)loadGameFrame.headerTitle != (Object)null)
			{
				loadGameFrame.headerTitle.text = Lang.Tr("Your shops");
			}
			if (customUIWindow.root.transform.Find("bellun_probably_together_new_shop") == null && loadGameFrame.panel.Find("bellun_probably_together_new_shop") == null)
			{
				RectTransform panel = loadGameFrame.panel;
				float num = ((Object)(object)panel != (Object)null) ? panel.rect.width : 1110f;
				NewGameCommonButton.Build(loadGameFrame, ((Object)(object)panel != (Object)null) ? panel : (RectTransform)customUIWindow.root.transform, "bellun_probably_together_new_shop", num - 420f, 28f, 260f, 48f, Lang.Tr("New shop"), NewShop);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("host: dressing the list threw (it works without): " + ex.Message);
		}
	}

	internal static void NewShop()
	{
		if (!Active)
		{
			return;
		}
		moving = true;
		try
		{
			Slot = -1;
			MorningFile = null;
			NewGameData.Instance.saveSlot = -1;
			NewGameData.Instance.saveFile = null;
			LoadGameUI.Close();
			ShowRules(null);
			Log.Info("host: a new shop: its rules first");
		}
		catch (Exception ex)
		{
			Log.Warn("host: the rules screen did not open: " + ex.Message);
		}
		finally
		{
			moving = false;
		}
	}

	private static void ShowRules(string note)
	{
		if (pending == null)
		{
			JObject jObject = Config();
			pending = new HostPanels.Rules
			{
				Name = HostMenu.DefaultShopName(),
				Players = Mathf.Clamp(((int?)jObject["maxPlayers"]) ?? 4, 1, 4),
				OwnTrades = ((bool?)jObject["playerOwnership"]) ?? true,
				SoloTrips = ((bool?)jObject["individualOutings"]) ?? false,
				CustomersPerPlayer = ((double?)jObject["customersPerPlayer"]) ?? 2.0,
				RentPercentPerPlayer = ((int?)jObject["rentPercentPerPlayer"]) ?? 50
			};
		}
		HostPanels.OpenRules(pending, RulesDone);
		if (!string.IsNullOrEmpty(note))
		{
			HostPanels.RulesNote(note);
		}
		last = HostPanels.RulesWindow;
		idleSince = -1f;
	}

	private static void RulesDone(HostPanels.Rules r)
	{
		moving = true;
		try
		{
			pending = r;
			HostPanels.CloseRules();
			NewGameSpecializationUI.Open();
			last = SpecWindow;
			idleSince = -1f;
			Log.Info("host: a new shop '" + r.Name + "': " + r.Players + " player(s), own trades " + r.OwnTrades + ", solo trips " + r.SoloTrips + ", +" + r.CustomersPerPlayer.ToString(CultureInfo.InvariantCulture) + " customer(s) and +" + r.RentPercentPerPlayer + "% rent per added player; the game's new game screens");
		}
		catch (Exception ex)
		{
			Log.Warn("host: the game's new game screen did not open: " + ex.Message);
		}
		finally
		{
			moving = false;
		}
	}

	/// True when this is not a hosted start; the game goes on by itself.
	internal static bool OnStartGame(MainMenuUIController menu)
	{
		if (!Active)
		{
			return true;
		}
		try
		{
			NewGameData instance = NewGameData.Instance;
			// The first step of the game's own method, which this replaces.
			instance.skipIntro = instance.skipIntro || ((Object)(object)menu.skipIntroLock != (Object)null && menu.skipIntroLock.activeSelf) || ((Object)(object)menu.skipToggle != (Object)null && menu.skipToggle.isOn);
			Slot = (instance.saveSlot > 0) ? instance.saveSlot : (-1);
			MorningFile = (Slot > 0) ? instance.saveFile : null;
			string text = Launch();
			if (text != null)
			{
				Log.Warn("host: not hosted: " + text);
				if (Slot < 0)
				{
					ShowRules(text);
				}
				else
				{
					ShowList();
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("host: hosting from the flow threw: " + ex);
		}
		return false;
	}

	private static int NextSlot()
	{
		int num = 0;
		try
		{
			foreach (string file in System.IO.Directory.GetFiles(HostMenu.HostFolder, "save_*.es3"))
			{
				Match match = Regex.Match(Path.GetFileName(file), "^save_(\\d+)(_day\\d+_.*)?\\.es3$");
				if (match.Success && int.TryParse(match.Groups[1].Value, out var result))
				{
					num = Math.Max(num, result);
				}
			}
			foreach (int item in Paths.IndexLoad("activeSaveSlotId", new List<int>()))
			{
				num = Math.Max(num, item);
			}
		}
		catch
		{
		}
		return num + 1;
	}

	/// Null when under way; else the reason it failed.
	private static string Launch()
	{
		bool flag = Slot < 0;
		NewGameData instance = NewGameData.Instance;
		JObject jObject = Config();
		int num;
		string text;
		HostPanels.Rules rules;
		if (flag)
		{
			rules = pending ?? new HostPanels.Rules
			{
				Name = HostMenu.DefaultShopName()
			};
			string scenario = instance.startType.ToString();
			if (!ServerMode.PlayableScenarioList().Any((KeyValuePair<string, string> s) => string.Equals(s.Key, scenario, StringComparison.OrdinalIgnoreCase)))
			{
				return Lang.Tr("That trade cannot start a shared shop. Pick another.");
			}
			num = NextSlot();
			text = rules.Name;
			JObject jObject2 = new JObject();
			jObject2["scenario"] = new JValue(scenario);
			jObject2["difficulty"] = new JValue(string.IsNullOrEmpty(instance.difficultyId) ? "normal" : instance.difficultyId);
			jObject2["skipIntro"] = new JValue(instance.skipIntro);
			jObject["newRun"] = jObject2;
			jObject["endless"] = new JValue(instance.endlessMode);
		}
		else
		{
			num = Slot;
			// The rules from the shop's card; without a card, the last ones used.
			JObject jObject3 = Card(num) ?? new JObject();
			text = ((string)jObject3["name"]) ?? ((string)jObject["name"]) ?? HostMenu.DefaultShopName();
			rules = new HostPanels.Rules
			{
				Name = text,
				Players = Mathf.Clamp(((int?)jObject3["maxPlayers"]) ?? ((int?)jObject["maxPlayers"]) ?? 4, 1, 4),
				OwnTrades = ((bool?)jObject3["playerOwnership"]) ?? ((bool?)jObject["playerOwnership"]) ?? true,
				SoloTrips = ((bool?)jObject3["individualOutings"]) ?? ((bool?)jObject["individualOutings"]) ?? false,
				CustomersPerPlayer = ((double?)jObject3["customersPerPlayer"]) ?? ((double?)jObject["customersPerPlayer"]) ?? 2.0,
				RentPercentPerPlayer = ((int?)jObject3["rentPercentPerPlayer"]) ?? ((int?)jObject["rentPercentPerPlayer"]) ?? 50
			};
		}
		int port = ((int?)jObject["port"]) ?? 27700;
		if (!HostPanels.PortFree(port))
		{
			return Lang.Trf("Port {0} is already in use on this PC.", port.ToString(CultureInfo.InvariantCulture));
		}
		jObject["name"] = new JValue(text);
		jObject["maxPlayers"] = new JValue(rules.Players);
		jObject["saveSlot"] = new JValue(num);
		jObject["individualOutings"] = new JValue(rules.SoloTrips);
		jObject["playerOwnership"] = new JValue(rules.OwnTrades);
		jObject["customersPerPlayer"] = new JValue(rules.CustomersPerPlayer);
		jObject["rentPercentPerPlayer"] = new JValue(rules.RentPercentPerPlayer);
		try
		{
			WriteConfig(jObject);
		}
		catch (Exception ex)
		{
			return Lang.Tr("Could not write the shop settings: ") + ex.Message;
		}
		Reached++;
		Paths.KeepSettings = true;
		Net.Role = Role.Host;
		Log.Info("host: starting from the flow: '" + text + "' on port " + port + ", " + rules.Players + " player(s), slot " + num + (flag ? (" (new: " + instance.startType.ToString() + ", " + instance.difficultyId + ", " + ((instance.startingPerks != null) ? instance.startingPerks.Count : 0) + " perk(s))") : ((MorningFile != null) ? (" (continue from " + MorningFile + ")") : " (continue)")) + ", own trades " + rules.OwnTrades + ", solo trips " + rules.SoloTrips);
		LoadingScreen.HostStarting = true;
		// Keep the menu's choices (founder's perks, a mod's difficulty) instead of the server's defaults.
		ServerMode.KeepMenuChoices = flag;
		Perks.FounderPickedOnMenu = flag;
		ServerMode.MorningFile = flag ? null : MorningFile;
		Active = false;
		Slot = 0;
		pending = null;
		last = "";
		idleSince = -1f;
		ServerMode.Start();
		ServerMode.SkipMenuWait();
		return null;
	}

	private static void End(string why)
	{
		if (!Active)
		{
			return;
		}
		Active = false;
		Slot = 0;
		MorningFile = null;
		pending = null;
		last = "";
		idleSince = -1f;
		HostPanels.CloseSetup();
		HostPanels.CloseRules();
		if (!Net.IsOnline && Net.Role != Role.Host)
		{
			Paths.ClearOverride();
			Paths.KeepSettings = false;
		}
		try
		{
			SaveUIManager.InvalidateCache();
		}
		catch
		{
		}
		Log.Info("host: the host flow is over (" + why + ")");
	}

	/// Each menu tick, a flow whose screen closed steps back or ends.
	private static int idleFrame;

	private static bool scanned;

	internal static void Tick()
	{
		if (!Active || moving)
		{
			return;
		}
		if (!Ui.AtMenu)
		{
			End("left the menu");
			return;
		}
		string text = HostPanels.SetupOpen ? HostPanels.SetupWindow : ((Window(ListWindow) != null) ? ListWindow : (HostPanels.RulesOpen ? HostPanels.RulesWindow : ((Window(SpecWindow) != null) ? SpecWindow : ((Window(PerkWindow) != null) ? PerkWindow : ((Window(DifficultyWindow) != null) ? DifficultyWindow : null)))));
		if (text != null)
		{
			last = text;
			idleSince = -1f;
			return;
		}
		// Closed with Back or Escape: open the screen before at once.
		// Two frames tell a step back from the game moving to its next screen.
		if (idleSince < 0f)
		{
			idleSince = Time.realtimeSinceStartup;
			idleFrame = Time.frameCount;
			return;
		}
		if (Time.frameCount - idleFrame < 2)
		{
			return;
		}
		try
		{
			if (last == SpecWindow)
			{
				ShowRules(null);
			}
			else if (last == HostPanels.RulesWindow)
			{
				pending = null;
				ShowList();
			}
			else if (last == ListWindow)
			{
				ShowSetup();
			}
			else
			{
				End("closed");
			}
		}
		catch (Exception ex)
		{
			End("a step back could not open: " + ex.Message);
		}
	}
}

/// Starts a game from the menu: a new game after difficulty, or a loaded save.
[HarmonyPatch(typeof(MainMenuUIController), "StartNewGameClick")]
internal static class HostFlowStartPatch
{
	private static bool Prefix(MainMenuUIController __instance)
	{
		return HostFlow.OnStartGame(__instance);
	}
}

[HarmonyPatch(typeof(SaveFiles), "RenameSlot")]
internal static class HostFlowRenamePatch
{
	private static void Postfix(int slotId, string name)
	{
		HostFlow.Renamed(slotId, name);
	}
}

[HarmonyPatch(typeof(SaveFiles), "DeleteSlot")]
internal static class HostFlowDeletePatch
{
	private static void Postfix(int slotId)
	{
		HostFlow.Deleted(slotId);
	}
}

/// A host with no finished run in this folder still picks perks for a new shop.
[HarmonyPatch(typeof(NewGamePerksUI), "Open")]
internal static class HostFlowPerksOpenPatch
{
	private static void Postfix()
	{
		if (HostFlow.Active)
		{
			PerkScreens.Unlock();
		}
	}
}
