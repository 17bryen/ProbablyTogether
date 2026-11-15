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

/// Test only (--br-shots=<folder>, --br-shots-mode=host|join|list): a windowed copy walks the screens and saves
/// a picture of each.
internal static class ShotTour
{
	private static bool inited;

	private static bool on;

	private static string dir = "";

	private static string mode = "";

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
		string text = dir + "/" + mode + "-" + count.ToString("00") + "-" + name + ".png";
		ScreenCapture.CaptureScreenshot(text);
		Log.Info("shots: " + text);
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
			mode = Args.Get("--br-shots-mode") ?? "host";
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
			if (mode == "join")
			{
				Join();
			}
			else
			{
				Menu();
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
		if (step <= 11 && (!Ui.AtMenu || (Object)(object)instance == (Object)null || (Object)(object)instance2 == (Object)null))
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
				Log.Info("shots: host list " + (flag ? "up" : ("NOT up: " + HostFlow.LastProblem)));
				Next();
			}
			break;
		case 2:
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
		case 3:
			if (Since > 1f)
			{
				HostFlow.NewShop();
				Next();
			}
			break;
		case 4:
			if (Since > 2f)
			{
				Shot("new-shop-trade");
				Next();
			}
			break;
		case 5:
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
		case 6:
			if (Since > 2f)
			{
				Shot("new-shop-perks");
				Next();
			}
			break;
		case 7:
			if (Since > 1f)
			{
				NewGamePerksUI.CloseWithoutReturning();
				instance.OnPerksContinue();
				Next();
			}
			break;
		case 8:
			if (Since > 2f)
			{
				Shot("new-shop-difficulty");
				Next();
			}
			break;
		case 9:
			if (Since > 1f)
			{
				instance2.difficultyId = "normal";
				NewGameDifficultyUI.CloseWithoutReturning();
				instance.OnPerkFinishedClick();
				Next();
			}
			break;
		case 10:
			if (Since > 2f)
			{
				Shot("host-last-step");
				Next();
			}
			break;
		case 11:
			if (Since > 1f)
			{
				HostMenu.PressHost();
				Next();
			}
			break;
		case 12:
			if (Net.IsHost && Net.IsLive && Game.ShopLoaded)
			{
				Next();
			}
			break;
		case 13:
			if (Since > 6f)
			{
				Shot("host-in-the-shop");
				Next();
			}
			break;
		}
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
		}
	}
}
