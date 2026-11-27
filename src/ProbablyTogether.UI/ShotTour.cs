using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ProbablyTogether.Systems;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.UI;

/// Test only (--br-shots=<folder>, --br-shots-mode=host|join|list|menus, host-random|join-random): a windowed copy
/// walks the screens and saves a picture of each. The -random modes tick Random (and Force) on the setup screens.
internal static class ShotTour
{
	private static bool inited;

	private static bool on;

	private static string dir = "";

	private static string mode = "";

	private static string prefix = "";

	private static bool random;

	private static bool plainShot;

	private static bool plainPerksShot;

	private static bool ticked;

	private static bool perksTicked;

	private static int step;

	private static float at;

	private static int count;

	private static float menuSince = -1f;

	private static bool loadingShot;

	private static float Since => Time.realtimeSinceStartup - at;

	private static void Next()
	{
		step++;
		at = Time.realtimeSinceStartup;
	}

	private static void Shot(string name)
	{
		count++;
		string text = dir + "/" + prefix + "-" + count.ToString("00") + "-" + name + ".png";
		ScreenCapture.CaptureScreenshot(text);
		Log.Info("shots: " + text + (random ? (" (random trade " + RandomBoxes.TradeOn + ", perks " + RandomBoxes.PerksOn + ")") : ""));
	}

	private static bool Alive(string id)
	{
		CustomUIManager instance = CustomUIManager.Instance;
		CustomUIWindow customUIWindow = ((Object)(object)instance != (Object)null) ? instance.GetWindow(id) : null;
		return customUIWindow != null && customUIWindow.IsAlive;
	}

	internal static void Tick()
	{
		if (!inited)
		{
			inited = true;
			dir = (Args.Get("--br-shots") ?? "").Replace('\\', '/').TrimEnd('/');
			prefix = Args.Get("--br-shots-mode") ?? "host";
			random = prefix.EndsWith("-random", StringComparison.Ordinal);
			mode = random ? prefix.Substring(0, prefix.Length - 7) : prefix;
			on = dir.Length > 0 && Args.Bot != null;
			if (on)
			{
				System.IO.Directory.CreateDirectory(dir);
				PerkScreens.LabOn = true;
				at = Time.realtimeSinceStartup;
				Log.Info("shots: a tour of the screens (" + mode + ") into " + dir);
			}
		}
		if (!on)
		{
			return;
		}
		Application.runInBackground = true;
		try
		{
			if (mode == "join" && random)
			{
				JoinRandom();
			}
			else if (mode == "join")
			{
				Join();
			}
			else if (mode == "menus")
			{
				Menus();
			}
			else
			{
				Menu();
				if (mode == "host")
				{
					HostHelp();
				}
			}
		}
		catch (Exception ex)
		{
			on = false;
			Log.Error("shots: the tour threw at step " + step + ": " + ex);
		}
	}

	private static void Menu()
	{
		MainMenuUIController instance = MainMenuUIController.Instance;
		NewGameData instance2 = NewGameData.Instance;
		if (step <= 13 && (!Ui.AtMenu || (Object)(object)instance == (Object)null || (Object)(object)instance2 == (Object)null))
		{
			menuSince = -1f;
			return;
		}
		if (step == 0 && menuSince < 0f)
		{
			menuSince = Time.realtimeSinceStartup;
		}
		switch (step)
		{
		case 0:
			if (Time.realtimeSinceStartup - menuSince > 5f)
			{
				Shot("main-menu");
				Next();
			}
			break;
		case 1:
			if (Since > 1f)
			{
				bool flag = HostFlow.Open();
				Log.Info("shots: setup screen " + (flag ? "up" : ("NOT up: " + HostFlow.LastProblem)));
				Next();
			}
			break;
		case 2:
			if (Since > 2f)
			{
				Shot("host-setup");
				Next();
			}
			break;
		case 3:
			if (Since > 1f)
			{
				HostPanels.SetupContinue(delegate(string pw, int port)
				{
					Flow("SetupDone", pw, port);
				});
				Next();
			}
			break;
		case 4:
			if (Since > 2f)
			{
				Shot((mode == "list") ? "host-list-with-a-shop" : "host-list-empty");
				Next();
				if (mode == "list")
				{
					step = 100;
				}
			}
			break;
		case 5:
			if (Since > 1f)
			{
				HostFlow.NewShop();
				Next();
			}
			break;
		case 6:
			if (Since > 2f)
			{
				Shot("new-shop-rules");
				Next();
			}
			break;
		case 7:
			if (Since > 1f)
			{
				HostPanels.SetField("name", "The Corner Shop");
				HostPanels.RulesContinue(delegate(HostPanels.Rules r)
				{
					Flow("RulesDone", r);
				});
				if (random)
				{
					SelectScenario("DRUG_DEALER");
				}
				Next();
			}
			break;
		case 8:
			if (Since > 2f && random && !plainShot)
			{
				// Unticked first: the row of boxes next to a long description.
				plainShot = true;
				Shot("new-shop-trade-unticked");
				at = Time.realtimeSinceStartup;
			}
			else if (Since > 1f && random && plainShot && !ticked)
			{
				// A picture is taken at the end of the frame: tick a step after it.
				ticked = true;
				RandomBoxes.LabTick(trade: true, force: false);
				RandomBoxes.LabTick(trade: true, force: true);
				RandomBoxes.LabOpenSelect(trade: true);
				at = Time.realtimeSinceStartup;
			}
			else if (Since > 2f)
			{
				Shot("new-shop-trade");
				Next();
			}
			break;
		case 9:
			if (Since > 1f)
			{
				string want = Args.Get("--br-ui-host-scenario") ?? "GUNSMITH";
				StartingScenario startingScenario = StartingScenario.GetVisible().First((StartingScenario x) => string.Equals(x.startType.ToString(), want, StringComparison.OrdinalIgnoreCase));
				instance2.startType = startingScenario.startType;
				instance2.startScenarioId = startingScenario.id;
				NewGameSpecializationUI.Close();
				instance.OpenPerkInterface();
				foreach (string item in (Args.Get("--br-ui-host-perks") ?? "saving,alert").Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries))
				{
					StartingPerk startingPerk = Perks.Def(item.Trim());
					if (startingPerk != null)
					{
						instance2.startingPerks.Add(startingPerk);
					}
				}
				PerkScreens.Unlock();
				Next();
			}
			break;
		case 10:
			if (Since > 2f && random && !plainPerksShot)
			{
				plainPerksShot = true;
				Shot("new-shop-perks-unticked");
				at = Time.realtimeSinceStartup;
			}
			else if (Since > 1f && random && plainPerksShot && !perksTicked)
			{
				perksTicked = true;
				RandomBoxes.LabTick(trade: false, force: false);
				RandomBoxes.LabTick(trade: false, force: true);
				at = Time.realtimeSinceStartup;
			}
			else if (Since > 2f)
			{
				Shot("new-shop-perks");
				Next();
			}
			break;
		case 11:
			if (Since > 1f)
			{
				NewGamePerksUI.CloseWithoutReturning();
				instance.OnPerksContinue();
				Next();
			}
			break;
		case 12:
			if (Since > 2f)
			{
				Shot("new-shop-difficulty");
				Next();
			}
			break;
		case 13:
			if (Since > 1f)
			{
				instance2.difficultyId = "normal";
				NewGameDifficultyUI.CloseWithoutReturning();
				instance.OnPerkFinishedClick();
				Next();
			}
			break;
		case 14:
			if (Net.IsHost && Net.IsLive && Game.ShopLoaded)
			{
				Next();
			}
			break;
		case 15:
			if (Since > 6f && (!random || RandomReveal.Opened > 0 || Since > 30f))
			{
				Shot(random ? "host-reveal" : "host-in-the-shop");
				Next();
			}
			break;
		// The host asks to leave.
		case 16:
			if (random && Since > 1.5f && HostPanels.MessageOpen(HostPanels.RevealWindow))
			{
				HostPanels.CloseMessage(HostPanels.RevealWindow);
				at = Time.realtimeSinceStartup;
				Log.Info("shots: host-in-the-shop (the reveal closed)");
			}
			else if (mode == "host" && Since > 1.5f)
			{
				HostAwayUi.AskLeave(StoreUIManager.Instance.confirmPanelUIManager);
				Next();
			}
			break;
		case 17:
			if (Since > 2.5f)
			{
				Shot("host-leaving");
				Next();
			}
			break;
		case 18:
			if (Since > 1f)
			{
				HostPanels.CloseMessage("bellun_probably_together.leave");
				Next();
			}
			break;
		}
	}

	/// The menu's other windows: the Join panel, a dropped player's notice, the mods a shop asks for.
	private static void Menus()
	{
		if (!Ui.AtMenu || (Object)(object)MainMenuUIController.Instance == (Object)null)
		{
			menuSince = -1f;
			return;
		}
		if (step == 0 && menuSince < 0f)
		{
			menuSince = Time.realtimeSinceStartup;
		}
		switch (step)
		{
		case 0:
			if (Time.realtimeSinceStartup - menuSince > 5f)
			{
				JoinMenu.OpenDialog();
				Next();
			}
			break;
		case 1:
			if (Since > 2.5f)
			{
				Shot("join");
				Next();
			}
			break;
		case 2:
			if (Since > 1f)
			{
				JoinMenu.LabCloseAll();
				JoinMenu.LabNotice("The host closed the shop.", "The Corner Shop");
				Next();
			}
			break;
		case 3:
			if (Since > 2.5f)
			{
				Shot("disconnected");
				Next();
			}
			break;
		case 4:
			if (Since > 1f)
			{
				JoinMenu.LabCloseAll();
				JoinMenu.LabMods();
				Next();
			}
			break;
		case 5:
			if (Since > 2.5f)
			{
				Shot("mods");
				Next();
			}
			break;
		case 6:
			if (Since > 1f)
			{
				JoinMenu.LabCloseAll();
				JoinMenu.OpenAbout();
				Next();
			}
			break;
		case 7:
			if (Since > 2.5f)
			{
				Shot("about");
				Next();
			}
			break;
		case 8:
			if (Since > 1f)
			{
				JoinMenu.LabCloseAll();
				Log.Info("shots: menus-done");
				Next();
			}
			break;
		}
	}

	private static void Flow(string method, params object[] args)
	{
		typeof(HostFlow).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
	}

	private static void SelectScenario(string startType)
	{
		List<StartingScenario> visible = StartingScenario.GetVisible();
		int num = visible.FindIndex((StartingScenario s) => string.Equals(s.startType.ToString(), startType, StringComparison.OrdinalIgnoreCase));
		if (num < 0)
		{
			return;
		}
		AccessTools.Field(typeof(NewGameSpecializationUI), "selected")?.SetValue(null, num);
		AccessTools.Method(typeof(NewGameSpecializationUI), "RefreshStrip", Type.EmptyTypes, (Type[])null)?.Invoke(null, null);
		AccessTools.Method(typeof(NewGameSpecializationUI), "ShowScenario", Type.EmptyTypes, (Type[])null)?.Invoke(null, null);
	}

	private static void Join()
	{
		if (!loadingShot && Net.IsClient && !Net.IsLive && Time.realtimeSinceStartup - at > 0.5f && LoadingScreen.Text.Length > 0)
		{
			loadingShot = true;
			Shot("join-loading");
		}
		switch (step)
		{
		case 0:
			if (PerkScreens.SpecOpen)
			{
				Next();
			}
			break;
		case 1:
			if (Since > 2.5f)
			{
				Shot("join-trade-screen");
				Next();
			}
			break;
		case 2:
			if (Since > 1f)
			{
				SelectScenario(Specs.Of(1) ?? "GUNSMITH");
				Next();
			}
			break;
		case 3:
			if (Since > 1.5f)
			{
				Shot("join-trade-taken");
				Next();
			}
			break;
		case 4:
			if (Since > 1f)
			{
				SelectScenario("DRUG_DEALER");
				Next();
			}
			break;
		case 5:
			if (Since > 1.5f)
			{
				Shot("join-trade-free");
				PerkScreens.SpecContinue();
				Next();
			}
			break;
		case 6:
			if (PerkScreens.PerksOpen && !PerkScreens.SpecOpen)
			{
				Next();
			}
			break;
		case 7:
			if (Since > 2.5f)
			{
				Shot("join-perk-screen");
				Next();
			}
			break;
		case 8:
			if (Since > 1f)
			{
				List<StartingPerk> startingPerks = NewGameData.Instance.startingPerks;
				foreach (string item in new string[4] { "saving", "side_street", "illiterate", "tinkerer" })
				{
					startingPerks.Add(Perks.Def(item));
				}
				PerkScreens.Unlock();
				Next();
			}
			break;
		case 9:
			if (Since > 1.5f)
			{
				Shot("join-perks-selected");
				Next();
			}
			break;
		case 10:
			if (Since > 1f)
			{
				PerkScreens.PerksContinue();
				Next();
			}
			break;
		case 11:
			if (Perks.Picked(Members.LocalSeat) && !PerkScreens.PerksOpen)
			{
				Next();
			}
			break;
		case 12:
			if (Since > 4f)
			{
				Shot("join-in-the-shop");
				Next();
			}
			break;
		// The founder lets this player choose again (the host copy's HostHelp types the commands).
		case 13:
			if (Repick.Mine == Repick.Trade && PerkScreens.SpecOpen && Since > 2f)
			{
				SelectScenario(Specs.Of(Members.LocalSeat) ?? "DRUG_DEALER");
				Next();
			}
			break;
		// A picture is taken at the end of the frame, so screen changes show a step later.
		case 14:
			if (Since > 2f)
			{
				Shot("join-retrade-own-trade");
				Next();
			}
			break;
		case 15:
			if (Since > 1f)
			{
				SelectScenario("FARMER");
				Next();
			}
			break;
		case 16:
			if (Since > 1.5f)
			{
				Shot("join-retrade-another-trade");
				Next();
			}
			break;
		case 17:
			if (Since > 1f)
			{
				PerkScreens.Back();
				Next();
			}
			break;
		case 18:
			if (Repick.Mine == Repick.PerksKind && PerkScreens.PerksOpen)
			{
				Next();
			}
			break;
		case 19:
			if (Since > 2.5f)
			{
				Shot("join-reperk-screen");
				Next();
			}
			break;
		case 20:
			if (Since > 1f)
			{
				PerkScreens.Back();
				Next();
			}
			break;
		case 21:
			if (Since > 2.5f)
			{
				Shot("join-back-in-the-shop");
				Next();
			}
			break;
		}
	}

	/// A player joining a shop that deals trades and perks at random: the locked boxes, the reveal, the repick screens.
	private static void JoinRandom()
	{
		if (!loadingShot && Net.IsClient && !Net.IsLive && Time.realtimeSinceStartup - at > 0.5f && LoadingScreen.Text.Length > 0)
		{
			loadingShot = true;
			Shot("join-loading");
		}
		switch (step)
		{
		case 0:
			if (PerkScreens.SpecOpen)
			{
				Next();
			}
			break;
		case 1:
			if (Since > 2.5f)
			{
				Shot("join-trade-forced");
				Next();
			}
			break;
		case 2:
			if (Since > 1f)
			{
				PerkScreens.SpecContinue();
				Next();
			}
			break;
		case 3:
			if (PerkScreens.PerksOpen && !PerkScreens.SpecOpen)
			{
				Next();
			}
			break;
		case 4:
			if (Since > 2.5f)
			{
				Shot("join-perks-forced");
				Next();
			}
			break;
		case 5:
			if (Since > 1f)
			{
				PerkScreens.PerksContinue();
				Next();
			}
			break;
		case 6:
			if (RandomReveal.Opened > 0 && HostPanels.MessageOpen(HostPanels.RevealWindow))
			{
				Next();
			}
			break;
		case 7:
			if (Since > 2.5f)
			{
				Shot("join-reveal");
				Next();
			}
			break;
		case 8:
			if (Since > 1f)
			{
				HostPanels.CloseMessage(HostPanels.RevealWindow);
				Next();
			}
			break;
		case 9:
			if (Repick.Mine == Repick.Trade && PerkScreens.SpecOpen && Since > 2f)
			{
				Next();
			}
			break;
		case 10:
			if (Since > 2.5f)
			{
				Shot("join-retrade-forced");
				Next();
			}
			break;
		case 11:
			if (Since > 1f)
			{
				PerkScreens.Back();
				Next();
			}
			break;
		case 12:
			if (Repick.Mine == Repick.PerksKind && PerkScreens.PerksOpen)
			{
				Next();
			}
			break;
		case 13:
			if (Since > 2.5f)
			{
				Shot("join-reperk-forced");
				Next();
			}
			break;
		case 14:
			if (Since > 1f)
			{
				PerkScreens.Back();
				Next();
			}
			break;
		case 15:
			if (Since > 2.5f)
			{
				Shot("join-back-in-the-shop");
				Next();
			}
			break;
		}
	}

	private static int helped;

	private static float helpAt;

	/// Host copy, after its tour: offers the joining player another trade, then their perks, for the joining copy's pictures.
	private static void HostHelp()
	{
		if (!Net.IsHost || !Net.IsLive || !Game.ShopLoaded)
		{
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		bool flag = Specs.Of(2) != null && Perks.Picked(2) && Members.Online(2);
		switch (helped)
		{
		case 0:
			if (flag)
			{
				helped = 1;
				helpAt = realtimeSinceStartup;
			}
			break;
		case 1:
			if (realtimeSinceStartup - helpAt > 9f)
			{
				Log.Info("shots: /retrade -> " + Leaving.ChatCommand(Net.LocalPlayer, whole: false, Members.NameOf(2)));
				helped = 2;
			}
			break;
		case 2:
			if (Repick.Cancelled >= 1)
			{
				helped = 3;
				helpAt = realtimeSinceStartup;
			}
			break;
		case 3:
			if (realtimeSinceStartup - helpAt > 2f)
			{
				Log.Info("shots: /reperk -> " + Repick.ChatCommand(Net.LocalPlayer, Repick.PerksKind, Members.NameOf(2)));
				helped = 4;
			}
			break;
		}
	}
}
