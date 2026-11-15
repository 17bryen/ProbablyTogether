using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

	internal static string MorningFile;

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

	internal static string CardName(int slot)
	{
		JObject jObject = Card(slot);
		string text = (jObject != null) ? (string)jObject["name"] : null;
		return string.IsNullOrWhiteSpace(text) ? null : text;
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
			foreach (int item in SaveFiles.IndexLoad("activeSaveSlotId", new List<int>()).ToList())
			{
				JObject jObject = Card(item);
				string text = (jObject != null) ? (string)jObject["name"] : null;
				if (string.IsNullOrWhiteSpace(text))
				{
					continue;
				}
				string text2 = text + EndedNote(jObject);
				SavePreviewData savePreviewData = SaveFiles.IndexLoad<SavePreviewData>("preview_" + item, null);
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
		if (!Ui.AtMenu || Net.IsOnline || (Object)(object)CustomUIManager.Instance == (Object)null || (Object)(object)CustomUIManager.Instance.loadGamePrefab == (Object)null)
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
			Slot = 0;
			MorningFile = null;
			SyncNames();
			SaveUIManager.InvalidateCache();
			ShowList();
			if (Window(ListWindow) == null)
			{
				throw new InvalidOperationException("the game's load screen did not open");
			}
			Opened++;
			Log.Info("host: the list of hosted shops is up (the game's load screen over " + HostMenu.HostFolder + ", " + SaveUIManager.GetOrderedSlotIds().Count + " shop(s))");
			return true;
		}
		catch (Exception ex)
		{
			LastProblem = "list: " + ex.Message;
			Log.Warn("host: the game's load screen could not be used for the hosted shops, the old window comes instead: " + ex.Message);
			End("the list could not open");
			return false;
		}
	}

	private static void ShowList()
	{
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
				loadGameFrame.headerTitle.text = Lang.Tr("Host a shop");
			}
			if (customUIWindow.root.transform.Find("bellun_probably_together_new_shop") == null && !NewShopOnContinue(loadGameFrame))
			{
				RectTransform rectTransform = loadGameFrame.panel;
				float num = ((Object)(object)rectTransform != (Object)null) ? rectTransform.rect.width : 1110f;
				NewGameCommonButton newGameCommonButton = NewGameCommonButton.Build(loadGameFrame, ((Object)(object)rectTransform != (Object)null) ? rectTransform : (RectTransform)customUIWindow.root.transform, "bellun_probably_together_new_shop", num - 420f, 28f, 260f, 48f, Lang.Tr("New shop"), NewShop);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("host: dressing the list threw (it works without): " + ex.Message);
		}
	}

	private static bool NewShopOnContinue(LoadGameFrame frame)
	{
		if ((Object)(object)frame.continueButton == (Object)null || ((Component)frame.continueButton).gameObject.activeInHierarchy)
		{
			return false;
		}
		((Component)frame.continueButton).gameObject.SetActive(true);
		if (!((Component)frame.continueButton).gameObject.activeInHierarchy)
		{
			((Component)frame.continueButton).gameObject.SetActive(false);
			return false;
		}
		frame.continueButton.onClick.RemoveAllListeners();
		frame.continueButton.onClick.AddListener(NewShop);
		frame.continueButton.interactable = true;
		if ((Object)(object)frame.continueLabel != (Object)null)
		{
			frame.continueLabel.text = Lang.Tr("New shop");
		}
		NewGameUIElements.RefreshContinueLook(frame, hovered: false);
		return true;
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
			NewGameSpecializationUI.Open();
			last = SpecWindow;
			idleSince = -1f;
			Log.Info("host: a new shop: the game's new game screens");
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
			Reached++;
			Log.Info("host: " + ((Slot > 0) ? ("shop " + Slot + ((MorningFile != null) ? (" from its earlier morning " + MorningFile) : " from its latest morning")) : ("a new shop: " + instance.startType.ToString() + ", " + instance.difficultyId + ", " + ((instance.startingPerks != null) ? instance.startingPerks.Count : 0) + " perk(s), skip intro " + instance.skipIntro + ", endless " + instance.endlessMode)) + "; asking for the server settings");
			last = "settings";
			idleSince = -1f;
			HostMenu.OpenDialog();
		}
		catch (Exception ex)
		{
			Log.Error("host: the last step did not open: " + ex);
		}
		return false;
	}

	internal static void Hosting()
	{
		Active = false;
		Slot = 0;
		last = "";
		idleSince = -1f;
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
		last = "";
		idleSince = -1f;
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
		if (last != DifficultyWindow)
		{
			try
			{
				CustomUIWindow customUIWindow = Window(DifficultyWindow);
				NewGameDifficultyFrame newGameDifficultyFrame = (customUIWindow != null) ? customUIWindow.root.GetComponent<NewGameDifficultyFrame>() : null;
				if ((Object)(object)newGameDifficultyFrame != (Object)null && (Object)(object)newGameDifficultyFrame.continueLabel != (Object)null)
				{
					newGameDifficultyFrame.continueLabel.text = LocHelper.GetLocalizedMainMenu("new_continue");
				}
			}
			catch
			{
			}
		}
		string text = (Window(ListWindow) != null) ? ListWindow : ((Window(SpecWindow) != null) ? SpecWindow : ((Window(PerkWindow) != null) ? PerkWindow : ((Window(DifficultyWindow) != null) ? DifficultyWindow : (HostMenu.DialogOpen ? "settings" : null))));
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
			return;
		}
		if (Time.realtimeSinceStartup - idleSince < 0.3f)
		{
			return;
		}
		if (last == SpecWindow || last == "settings")
		{
			try
			{
				ShowList();
			}
			catch (Exception ex)
			{
				End("the list could not reopen: " + ex.Message);
			}
			return;
		}
		End("closed");
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
