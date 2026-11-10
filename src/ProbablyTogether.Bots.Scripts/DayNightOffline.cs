using System;
using System.IO;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DayNightOffline : DayNightBotBase
{
	private long startTicks;

	public override string Name => "daynight-offline";

	public override float TimeoutSeconds => 300f;

	private static int SentTotal()
	{
		return DayNight.Sent.Values.Sum();
	}

	protected override void Build()
	{
		WaitFor("menu", 90f, () => BotProbes.Scene == "EmporiumMenu" && (Object)(object)NewGameData.Instance != (Object)null, () => "scene " + BotProbes.Scene);
		Check("offline", () => Net.Role == ProbablyTogether.Role.Off, () => "role " + Net.Role);
		Do("new-run", delegate
		{
			startTicks = DateTime.UtcNow.Ticks;
			NewGameData instance = NewGameData.Instance;
			instance.saveFile = null;
			instance.isLoadFromMainMenu = false;
			instance.saveSlot = -1;
			instance.startType = (NewGameData.StartType)1;
			instance.startScenarioId = "";
			instance.skipIntro = true;
			instance.disableTutorialHint = true;
			instance.difficultyId = "normal";
			instance.endlessMode = false;
			EmporiumEntry.sceneLoadStartTime = Time.realtimeSinceStartup;
			SceneManager.LoadScene("InventoryScene", (LoadSceneMode)0);
			return "new run (WATER_MERCHANT, normal, skip intro) from the menu, offline";
		});
		WaitFor("shop", 120f, () => BotProbes.InShop && Game.ShopLoaded && (Object)(object)StoreShutterButton.Instance != (Object)null, () => "scene " + BotProbes.Scene);
		Delay("settle", 2f);
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return "day " + day0 + ", slot " + DayNightBotBase.Ps.saveSlotId;
		});
		Do("open", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "clicked";
		});
		Check("opened-at-once", () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State + " (vanilla opens in the click)");
		WaitFor("shutter-settled", 15f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0);
		Do("queue", delegate
		{
			if (DayNightBotBase.Ps.CanEndDay())
			{
				return "closable";
			}
			DayNightBotBase.Ps.storeClientManager.clientStack.Clear();
			return "story customer waiting: queue cleared (local, offline)";
		});
		Do("close", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "closed";
		});
		Check("closed-at-once", () => DayNightBotBase.State == "CLOSED" && DayNightBotBase.Ps.visitLeftTonight == 1, () => "state " + DayNightBotBase.State + ", visits " + DayNightBotBase.Ps.visitLeftTonight);
		Do("door", delegate
		{
			EmporiumEntry.Instance.selectedBackpack = null;
			MapUIManager.Instance.OpenUI();
			return "map asked";
		});
		WaitFor("map-open", 20f, () => TransitUI.IsOpen, () => "transit open " + TransitUI.IsOpen + " (the game's own fade)");
		Do("travel", delegate
		{
			MapUIManager.Instance.Travel(AfterhourLocation.Get("dumping_ground"));
			return "travel asked";
		});
		WaitFor("arrived", 20f, () => DayNightOuting.LocationId == "dumping_ground", () => "at " + DayNightOuting.LocationId);
		Do("search", delegate
		{
			ScavHelper.ScavengeDumpingGrounds();
			return "attempts " + DayNightBotBase.Ps.scavengingAttempts + ", wound " + DayNightBotBase.Ps.healthData.woundState + ", bag " + ((GameInventory)EmporiumEntry.Instance.afterhourInventory).childItems.Count;
		});
		Check("searched-at-once", () => DayNightBotBase.Ps.scavengingAttempts == 1, () => "attempts " + DayNightBotBase.Ps.scavengingAttempts);
		Do("return", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "return asked";
		});
		WaitFor("home", 20f, () => DayNightOuting.LocationId == "" && DayNightBotBase.Ps.visitLeftTonight == 0, () => "at " + DayNightOuting.LocationId + ", visits " + DayNightBotBase.Ps.visitLeftTonight);
		Do("end-day", delegate
		{
			EndOfDayUIManager.Instance.OpenUI();
			EndOfDayUIManager.Instance.OnEndDayButtonClick();
			return "End Day pressed";
		});
		WaitFor("night", 30f, () => DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		WaitFor("saved", 20f, delegate
		{
			string path = Paths.Under(SaveFiles.Slot(DayNightBotBase.Ps.saveSlotId));
			return File.Exists(path) && File.GetLastWriteTimeUtc(path).Ticks > startTicks;
		}, () => "slot " + DayNightBotBase.Ps.saveSlotId + " file " + Paths.Under(SaveFiles.Slot(DayNightBotBase.Ps.saveSlotId)));
		Check("no-traffic", () => SentTotal() == 0 && DayNight.Refusals == 0 && !DayNightRun.RunOver, () => "requests sent " + SentTotal() + ", refusals " + DayNight.Refusals);
	}
}
