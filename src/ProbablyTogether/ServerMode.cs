using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

namespace ProbablyTogether;

internal static class ServerMode
{
	private enum Phase
	{
		WaitMenu,
		Loading,
		Running,
		Stopping,
		Done
	}

	private static Phase phase = Phase.WaitMenu;

	private static ServerConfig cfg;

	private static float menuSince = -1f;

	private static long loadStartMs;

	private static int loadStartFrame;

	private static int shopFrames;

	internal static bool NewRunPending;

	private static float nextStopCheck;

	private static long quitAtMs;

	private static long nightSnapshotAtMs = -1L;

	private static bool newRun;

	private static bool inPlace;

	private static string inPlaceWhy = "";

	private static bool saveQueued;

	private static string saveQueuedBy = "";

	internal static int NewRunsStarted;

	internal static string LastNewRunWhy = "";

	private static bool adminsLogged;

	private static readonly string[] ComingSoon = new string[5] { "SCAV", "MOONSHINER", "ORGAN_TRADER", "PHARMACIST", "STREET_FOOD_VENDOR" };

	internal static string EndedStamp;

	/// The new shop was made on the game's new-game screens; keep NewGameData as they left it.
	internal static bool KeepMenuChoices;

	/// Continue once from this earlier morning file of the shop.
	internal static string MorningFile;

	internal static int savesSeen;

	internal static ServerConfig Config => cfg;

	internal static bool RunEnded { get; private set; }

	internal static string Ending { get; private set; } = "";

	internal static bool Quitting => phase >= Phase.Stopping;

	internal static bool NewRunLoading
	{
		get
		{
			if (phase == Phase.Loading)
			{
				return inPlace;
			}
			return false;
		}
	}

	internal static string StatusState
	{
		get
		{
			if (phase >= Phase.Stopping || Server.Stopping)
			{
				return "stopping";
			}
			if (phase != Phase.Running || !Server.ShopReady)
			{
				return "loading";
			}
			if (!RunEnded)
			{
				return "running";
			}
			return "ended";
		}
	}

	internal static event Action<string> NewRunStarting;

	internal static event Action<string> NewRunUp;

	internal static void Start()
	{
		cfg = ServerConfig.Load();
		Log.Info("server mode: " + cfg.Describe());
		LogIgnoredAdmins(cfg);
		Locks.TakeoverSeconds = cfg.counterTakeoverSeconds;
		Systems.Outings.ServerReset();
		Systems.Members.ServerReset();
		Systems.Outings.Individual = cfg.individualOutings;
		Systems.Bays.Enabled = cfg.playerOwnership;
		Net.LocalPlayer = 0;
		if (!Server.Start(cfg))
		{
			BeginStop("could not listen on TCP " + cfg.port);
		}
		else
		{
			if (Net.IsHost)
			{
				Server.AddHost(HostName(), HostSteamId());
			}
			PanelLink.MarkDirty("start");
		}
	}

	private static string HostName()
	{
		try
		{
			if (SteamManager.Initialized)
			{
				return Steamworks.SteamFriends.GetPersonaName();
			}
		}
		catch
		{
		}
		return Args.Name ?? "Host";
	}

	private static ulong HostSteamId()
	{
		if (Args.Bot != null && ulong.TryParse(Args.Get("--br-steamid") ?? "", out var result))
		{
			return result;
		}
		try
		{
			if (SteamManager.Initialized)
			{
				return Steamworks.SteamUser.GetSteamID().m_SteamID;
			}
		}
		catch
		{
		}
		return 0uL;
	}

	private static void LogIgnoredAdmins(ServerConfig c)
	{
		if (!adminsLogged && c != null && c.HadAdminIds)
		{
			adminsLogged = true;
			Log.Info("server: server.json adminSteamIds is ignored: there is no in-game admin (kick and server messages come from the panel; a new run starts when every player votes for it, or from the panel)");
		}
	}

	internal static void Tick()
	{
		if (cfg == null)
		{
			return;
		}
		if (Net.IsDedicated && Application.targetFrameRate != cfg.frameRate)
		{
			Application.targetFrameRate = cfg.frameRate;
		}
		if (Net.IsDedicated && QualitySettings.vSyncCount != 0)
		{
			QualitySettings.vSyncCount = 0;
		}
		if (phase < Phase.Stopping && Time.realtimeSinceStartup >= nextStopCheck)
		{
			nextStopCheck = Time.realtimeSinceStartup + 1f;
			string path = Paths.Under("stop");
			if (File.Exists(path))
			{
				try
				{
					File.Delete(path);
				}
				catch
				{
				}
				BeginStop("stop file in the data folder");
			}
		}
		switch (phase)
		{
		case Phase.WaitMenu:
			if (Boot.Scene == "EmporiumMenu" && (Object)(object)NewGameData.Instance != (Object)null)
			{
				if (menuSince < 0f)
				{
					menuSince = Time.realtimeSinceStartup;
				}
				else if (Time.realtimeSinceStartup - menuSince > 2f)
				{
					StartShop();
				}
			}
			break;
		case Phase.Loading:
			if (Game.ShopLoaded && Time.frameCount > loadStartFrame + 1)
			{
				if (++shopFrames >= 5)
				{
					ShopUp();
				}
			}
			else if (Clock.Ms - loadStartMs > 240000)
			{
				if (!newRun)
				{
					Log.Error("server: could not open the save");
				}
				Log.Error("server: the shop did not load in 4 minutes; quitting");
				BeginStop("load failed");
			}
			break;
		case Phase.Running:
			if (nightSnapshotAtMs > 0 && Clock.Ms >= nightSnapshotAtMs)
			{
				nightSnapshotAtMs = -1L;
				if (Snapshot.NewMorning("night"))
				{
					Snapshot.SendToAll("night");
				}
			}
			else if (saveQueued && CanSaveNow() == null)
			{
				saveQueued = false;
				Log.Info("command: save (queued by " + saveQueuedBy + ") -> " + SaveSlotNow("queued save"));
			}
			break;
		case Phase.Stopping:
			if (Clock.Ms >= quitAtMs)
			{
				phase = Phase.Done;
				Log.Info("server: quitting");
				if (Net.IsHost)
				{
					Log.Info("host: the server stopped; the game stays open");
				}
				else
				{
					Application.Quit();
				}
			}
			break;
		}
		if (phase != Phase.Done)
		{
			PanelLink.Tick();
		}
	}

	private static void StartShop()
	{
		NewGameData instance = NewGameData.Instance;
		string text = SaveFiles.Slot(cfg.saveSlot);
		string text2 = Paths.Under(text);
		newRun = !File.Exists(text2);
		inPlace = false;
		string morning = MorningFile;
		MorningFile = null;
		bool keep = KeepMenuChoices;
		KeepMenuChoices = false;
		instance.saveFile = (!newRun && !string.IsNullOrEmpty(morning) && File.Exists(Paths.Under(morning))) ? morning : null;
		instance.isLoadFromMainMenu = false;
		if (!newRun)
		{
			string text3 = CheckSave(text);
			if (text3 != null)
			{
				Log.Error("server: could not open the save");
				Log.Error("server: slot " + cfg.saveSlot + " (" + text2 + ") is unreadable: " + text3 + "; the file is left as it is; quitting");
				BeginStop("could not open the save");
				return;
			}
			instance.saveSlot = cfg.saveSlot;
			Log.Info("server: loading save slot " + cfg.saveSlot + " (" + text2 + ", " + new FileInfo(text2).Length / 1024 + " KB)" + ((instance.saveFile != null) ? ("; from its earlier morning " + instance.saveFile) : ""));
			try
			{
				PanelLink.NoteSavedAt(File.GetLastWriteTimeUtc(text2));
			}
			catch
			{
			}
		}
		else
		{
			string text4 = ScenarioRefusal(cfg.newRun.scenario);
			if (text4 != null)
			{
				Log.Error("server: could not open the save");
				Log.Error("server: no save in slot " + cfg.saveSlot + " and the new run cannot start: " + text4 + "; quitting");
				BeginStop("could not open the save");
				return;
			}
			if (keep && (int)instance.startType != 0)
			{
				// Trade, perks, difficulty, skip intro and endless are already in NewGameData.
				instance.saveSlot = -1;
				instance.disableTutorialHint = true;
				NewRunPending = true;
			}
			else
			{
				ConfigureNewRun(instance);
			}
			Log.Info("server: no save in slot " + cfg.saveSlot + "; starting a new run (" + instance.startType.ToString() + ", " + instance.difficultyId + ", skip intro " + instance.skipIntro + ")");
		}
		BeginLoad();
	}

	internal unsafe static string ScenarioRefusal(string scenario)
	{
		if (!Enum.TryParse<NewGameData.StartType>(scenario ?? "", ignoreCase: true, out NewGameData.StartType result) || (int)result == 0)
		{
			return null;
		}
		bool flag = Array.IndexOf<string>(ComingSoon, result.ToString()) >= 0;
		if (!flag)
		{
			try
			{
				flag = StartingScenario.GetByStartType(result)?.comingSoon ?? false;
			}
			catch
			{
			}
		}
		if (!flag)
		{
			return null;
		}
		return "the scenario " + result.ToString() + " is marked coming soon in the game and cannot start; set newRun.scenario in server.json to a playable one (" + PlayableScenarios() + ")";
	}

	internal static List<KeyValuePair<string, string>> PlayableScenarioList()
	{
		List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
		foreach (StartingScenario item in StartingScenario.GetAll())
		{
			if (item == null || item.comingSoon || item.hidden || item.IsMod || (int)item.startType == 0)
			{
				continue;
			}
			string id = item.startType.ToString();
			if (Array.IndexOf(ComingSoon, id) >= 0 || list.Exists((KeyValuePair<string, string> x) => x.Key == id))
			{
				continue;
			}
			string title = id;
			try
			{
				string t = item.GetTitle();
				if (!string.IsNullOrEmpty(t))
				{
					title = t;
				}
			}
			catch
			{
			}
			list.Add(new KeyValuePair<string, string>(id, title));
		}
		return list;
	}

	/// Writes a small card per save slot (name, day, cash, scenario) for the Host window's list.
	internal static void WriteShopCard()
	{
		try
		{
			PlayerStore store = Game.Store;
			if (cfg == null || store == null || !Game.ShopLoaded)
			{
				return;
			}
			Newtonsoft.Json.Linq.JObject o = new Newtonsoft.Json.Linq.JObject();
			o["name"] = new Newtonsoft.Json.Linq.JValue(cfg.name);
			o["day"] = new Newtonsoft.Json.Linq.JValue((long)Game.Day);
			o["cash"] = new Newtonsoft.Json.Linq.JValue((long)store.playerCash);
			o["scenario"] = new Newtonsoft.Json.Linq.JValue(store.startType.ToString());
			o["saved"] = new Newtonsoft.Json.Linq.JValue(DateTime.UtcNow.ToString("o"));
			// The rules the host's list starts this shop with next time.
			o["maxPlayers"] = new Newtonsoft.Json.Linq.JValue((long)cfg.maxPlayers);
			o["individualOutings"] = new Newtonsoft.Json.Linq.JValue(cfg.individualOutings);
			o["playerOwnership"] = new Newtonsoft.Json.Linq.JValue(cfg.playerOwnership);
			o["customersPerPlayer"] = new Newtonsoft.Json.Linq.JValue(cfg.customersPerPlayer);
			o["rentPercentPerPlayer"] = new Newtonsoft.Json.Linq.JValue((long)cfg.rentPercentPerPlayer);
			if (Systems.DayNightRun.RunOver)
			{
				o["ended"] = new Newtonsoft.Json.Linq.JValue(Systems.DayNightRun.Ending ?? "");
				o["endedDay"] = new Newtonsoft.Json.Linq.JValue((long)Game.Day);
			}
			File.WriteAllText(Paths.Under("shop_" + cfg.saveSlot + ".json"), o.ToString());
			// The game's save list loses a run's custom name at every save, so put the shop's name back.
			try
			{
				if (!Systems.DayNightRun.RunOver)
				{
					SaveFiles.RenameSlot(cfg.saveSlot, cfg.name);
				}
			}
			catch (Exception ex2)
			{
				Log.Warn("server: the shop's name was not put on its save: " + ex2.Message);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("server: shop card not written: " + ex.Message);
		}
	}

	private static string PlayableScenarios()
	{
		List<string> list = new List<string>();
		try
		{
			foreach (StartingScenario item in StartingScenario.GetAll())
			{
				if (item != null && !item.comingSoon && !item.hidden && !item.IsMod && (int)item.startType != 0)
				{
					string text = item.startType.ToString();
					if (Array.IndexOf(ComingSoon, text) < 0 && !list.Contains(text))
					{
						list.Add(text);
					}
				}
			}
		}
		catch
		{
		}
		if (list.Count <= 0)
		{
			return "WATER_MERCHANT, APPRENTICE, FENCE, ROCK_BOTTOM, FARMER, GUNSMITH, DRUG_DEALER, RANCHER, AQUAFARMER";
		}
		return string.Join(", ", list);
	}

	private static string CheckSave(string rel)
	{
		try
		{
			if (!ES3.KeyExists("playerStore", rel))
			{
				return "no run in it (no playerStore)";
			}
			if (!ES3.KeyExists("storeStation", rel))
			{
				return "no run in it (no storeStation)";
			}
			return null;
		}
		catch (Exception ex)
		{
			return ex.GetType().Name + ": " + ex.Message;
		}
	}

	private static void ConfigureNewRun(NewGameData ngd)
	{
		// A run set up by the server starts with no perks (each player picks their own), not the old run's list.
		ngd.startingPerks = new System.Collections.Generic.List<StartingPerk>();
		ngd.saveFile = null;
		ngd.isLoadFromMainMenu = false;
		ngd.saveSlot = -1;
		if (!Enum.TryParse<NewGameData.StartType>(cfg.newRun.scenario ?? "", ignoreCase: true, out NewGameData.StartType result) || (int)result == 0)
		{
			Log.Warn("server: unknown scenario '" + cfg.newRun.scenario + "', using WATER_MERCHANT");
			result = (NewGameData.StartType)1;
		}
		ngd.startType = result;
		ngd.startScenarioId = "";
		ngd.skipIntro = cfg.newRun.skipIntro;
		ngd.disableTutorialHint = true;
		ngd.difficultyId = (string.IsNullOrEmpty(cfg.newRun.difficulty) ? "normal" : cfg.newRun.difficulty);
		ngd.endlessMode = cfg.endless;
		NewRunPending = true;
	}

	private static void BeginLoad()
	{
		phase = Phase.Loading;
		loadStartMs = Clock.Ms;
		loadStartFrame = Time.frameCount;
		shopFrames = 0;
		EmporiumEntry.sceneLoadStartTime = Time.realtimeSinceStartup;
		SceneManager.LoadScene("InventoryScene", (LoadSceneMode)0);
	}

	private static void ShopUp()
	{
		phase = Phase.Running;
		ProbablyTogether.UI.LoadingScreen.HostStarting = false;
		bool flag = inPlace;
		inPlace = false;
		if (flag)
		{
			Snapshot.SuppressRotate = false;
		}
		Ids.AfterLoad();
		bool flag2 = Snapshot.NewMorning(newRun ? "new run" : "server start");
		if (!flag2)
		{
			Log.Error("server: the first morning snapshot failed; joins will retry it");
		}
		PlayerStore store = Game.Store;
		if (newRun)
		{
			RunEnded = false;
			Ending = "";
		}
		Server.ShopReady = true;
		Server.UpdateStatus();
		Log.Info("server: shop up in " + (Clock.Ms - loadStartMs) + " ms (" + ((!newRun) ? "loaded" : (flag ? ("new run in place: " + inPlaceWhy) : "new run")) + ", slot " + store.saveSlotId + "): " + Digest.Compute().Describe() + "; " + Ids.Count + " item ids; accepting players");
		if (newRun && store.saveSlotId != cfg.saveSlot)
		{
			Log.Warn("server: the new run saved to slot " + store.saveSlotId + ", not " + cfg.saveSlot);
		}
		if (newRun)
		{
			string text = store.startType.ToString();
			Log.Info("server: new run started (" + text + ")");
			try
			{
				ServerMode.NewRunUp?.Invoke(text);
			}
			catch (Exception ex)
			{
				Log.Error("ServerMode.NewRunUp handler: " + ex);
			}
		}
		if (flag && flag2)
		{
			Snapshot.SendToAll("new run");
		}
		WriteShopCard();
		PanelLink.MarkDirty(newRun ? "new run" : "shop up");
	}

	internal static void SetRunEnded(string endingId)
	{
		if (Net.IsServer)
		{
			endingId = (string.IsNullOrEmpty(endingId) ? "unknown" : endingId);
			if (!RunEnded || !(Ending == endingId))
			{
				RunEnded = true;
				Ending = endingId;
				Log.Info("server: run ended (" + endingId + ")");
				PanelLink.MarkDirty("run ended");
			}
		}
	}

	private static string NewRunRefusal()
	{
		if (!Net.IsServer || cfg == null)
		{
			return "not a server";
		}
		if (phase >= Phase.Stopping)
		{
			return "the server is stopping";
		}
		if (phase != Phase.Running)
		{
			if (!inPlace)
			{
				return "the shop is still loading";
			}
			return "a new run is already loading";
		}
		if ((Object)(object)NewGameData.Instance == (Object)null)
		{
			return "the game's new-run data is missing";
		}
		return null;
	}

	internal static string NewRunCommand(bool force)
	{
		string text = NewRunRefusal();
		if (text != null)
		{
			return "refused: " + text;
		}
		if (!RunEnded && !force)
		{
			return "refused: the run is still going (newrun works after the run ends; 'newrun force' starts one now)";
		}
		string text2 = StartNewRun((force && !RunEnded) ? "panel: newrun force" : "panel: newrun");
		if (text2 != null)
		{
			return "refused: " + text2;
		}
		if (phase != Phase.Loading)
		{
			return "failed (see the log)";
		}
		return "starting a new run (" + cfg.newRun.scenario + ", " + cfg.newRun.difficulty + (cfg.endless ? ", endless" : "") + ")";
	}

	internal static string StartNewRun(string why)
	{
		string text = NewRunRefusal();
		if (text != null)
		{
			Log.Warn("server: new run (" + why + ") refused: " + text);
			return text;
		}
		NewGameData instance = NewGameData.Instance;
		why = (string.IsNullOrEmpty(why) ? "asked" : why);
		ReloadNewRunSettings();
		string text2 = ScenarioRefusal(cfg.newRun.scenario);
		if (text2 != null)
		{
			Log.Error("server: could not open the save");
			Log.Error("server: new run (" + why + ") refused: " + text2 + "; the current run stays as it is");
			return text2;
		}
		try
		{
			ServerMode.NewRunStarting?.Invoke(why);
		}
		catch (Exception ex)
		{
			Log.Error("ServerMode.NewRunStarting handler: " + ex);
		}
		string text3 = MoveRunAside(cfg.saveSlot);
		foreach (KeyValuePair<string, int> item in Locks.Table())
		{
			Locks.Release(item.Key, -1);
		}
		saveQueued = false;
		nightSnapshotAtMs = -1L;
		RunEnded = false;
		Ending = "";
		Server.ShopReady = false;
		ConfigureNewRun(instance);
		Snapshot.SuppressRotate = true;
		newRun = true;
		inPlace = true;
		inPlaceWhy = why;
		NewRunsStarted++;
		LastNewRunWhy = why;
		Log.Info("server: starting a new run in place (" + why + "): " + instance.startType.ToString() + ", " + instance.difficultyId + ", skip intro " + instance.skipIntro + ", endless " + instance.endlessMode + "; " + ((text3.Length > 0) ? ("the old run moved aside as " + text3) : ("no old run file in slot " + cfg.saveSlot)) + "; " + Server.PlayerCount + " player(s) stay connected");
		try
		{
			PlayerStore store = Game.Store;
			if (store != null)
			{
				store.Reset();
			}
		}
		catch (Exception ex2)
		{
			Log.Warn("server: PlayerStore.Reset: " + ex2.Message);
		}
		Game.ResetShopStatics();
		BeginLoad();
		PanelLink.MarkDirty("new run loading");
		return null;
	}

	private static void ReloadNewRunSettings()
	{
		try
		{
			ServerConfig serverConfig = ServerConfig.Load();
			LogIgnoredAdmins(serverConfig);
			string text = cfg.newRun.scenario + "/" + cfg.newRun.difficulty + "/" + cfg.newRun.skipIntro + "/" + cfg.endless;
			cfg.newRun = serverConfig.newRun ?? cfg.newRun;
			cfg.endless = serverConfig.endless;
			string text2 = cfg.newRun.scenario + "/" + cfg.newRun.difficulty + "/" + cfg.newRun.skipIntro + "/" + cfg.endless;
			if (text != text2)
			{
				Log.Info("server: new-run settings from server.json changed: " + text + " -> " + text2 + " (scenario/difficulty/skip intro/endless)");
			}
		}
		catch (Exception ex)
		{
			Log.Warn("server: server.json not re-read for the new run (" + ex.Message + "); using the start-up settings");
		}
	}

	private static string MoveRunAside(int slot)
	{
		string text = EndedStamp ?? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
		EndedStamp = null;
		string fileName = Path.GetFileName(SaveFiles.Slot(slot));
		string result = "";
		int num = 0;
		try
		{
			string root = Paths.Root;
			string[] files = System.IO.Directory.GetFiles(root, "save_" + slot.ToString(CultureInfo.InvariantCulture) + "*.es3");
			int num2 = default(int);
			foreach (string text2 in files)
			{
				string fileName2 = Path.GetFileName(text2);
				bool flag = string.Equals(fileName2, fileName, StringComparison.OrdinalIgnoreCase);
				if (flag || (SaveFiles.IsHistoryStem(Path.GetFileNameWithoutExtension(fileName2), out num2) && num2 == slot))
				{
					string text3 = "ended_" + text + "_" + fileName2;
					int num3 = 2;
					while (File.Exists(Path.Combine(root, text3)))
					{
						text3 = "ended_" + text + "-" + num3 + "_" + fileName2;
						num3++;
					}
					File.Move(text2, Path.Combine(root, text3));
					if (flag)
					{
						result = text3;
					}
					else
					{
						num++;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("server: could not move the old run aside (slot " + slot + "): " + ex.Message);
		}
		if (num > 0)
		{
			Log.Info("server: " + num + " day history file(s) of slot " + slot + " moved aside with the old run (ended_" + text + "_...)");
		}
		return result;
	}

	internal static void OnSlotSaved(bool quiet)
	{
		savesSeen++;
		WriteShopCard();
		PanelLink.NoteSaved(quiet ? "quiet" : "save");
		if (!quiet && phase == Phase.Running)
		{
			PlayerStore store = Game.Store;
			Log.Info("server: day " + Game.Day + " saved to slot " + store?.saveSlotId + " (autosave after the night)");
			if (saveQueued)
			{
				saveQueued = false;
				Log.Info("command: save (queued by " + saveQueuedBy + ") -> done by the night's save");
			}
			nightSnapshotAtMs = Clock.Ms;
		}
	}

	private static string CanSaveNow()
	{
		PlayerStore store = Game.Store;
		if (store == null || !Game.ShopLoaded || !Server.ShopReady)
		{
			return "loading";
		}
		if (nightSnapshotAtMs > 0)
		{
			return "night";
		}
		if ((int)store.storeState != 2)
		{
			return "day";
		}
		return null;
	}

	internal static string SaveCommand()
	{
		if (phase >= Phase.Stopping)
		{
			return "refused: the server is stopping";
		}
		if (phase != Phase.Running || !Server.ShopReady)
		{
			return "refused: the shop is still loading";
		}
		if (RunEnded)
		{
			return "refused: the run has ended (nothing to save)";
		}
		if (GamePatches.SaveIsBlocked)
		{
			return "refused: saves are blocked right now";
		}
		if (CanSaveNow() == null)
		{
			return SaveSlotNow("save command");
		}
		saveQueued = true;
		saveQueuedBy = "panel";
		return "queued: a day is in progress (" + Game.Store.storeState.ToString() + "); the game saves between days, so it saves at the next morning";
	}

	private static string SaveSlotNow(string why)
	{
		PlayerStore store = Game.Store;
		int num = savesSeen;
		Snapshot.SuppressRotate = true;
		try
		{
			store.SaveGame();
		}
		catch (Exception ex)
		{
			Log.Error("server: save (" + why + ") failed: " + ex);
			return "failed: " + ex.Message;
		}
		finally
		{
			Snapshot.SuppressRotate = false;
		}
		if (savesSeen <= num)
		{
			return "not saved (the save was skipped: see the log)";
		}
		return "saved slot " + store.saveSlotId + " (morning of day " + Game.Day + ")";
	}

	private static void BeginStop(string why)
	{
		if (phase < Phase.Stopping)
		{
			long ms = Clock.Ms;
			Log.Info("server: stopping (" + why + ")");
			bool num = phase == Phase.Running;
			phase = Phase.Stopping;
			Server.Stopping = true;
			Server.Goodbye("the server is stopping");
			if (num)
			{
				SaveForStop();
			}
			Server.StopListening();
			PanelLink.WriteNow("stopping");
			quitAtMs = Clock.Ms + 1500;
			Log.Info("server: stop prepared in " + (Clock.Ms - ms) + " ms; quitting in 1.5 s");
		}
	}

	internal static void Stop(string why)
	{
		BeginStop(why);
	}

	/// Host mode from the menu: start the shop on the next tick instead of after the usual 2 s.
	internal static void SkipMenuWait()
	{
		menuSince = Time.realtimeSinceStartup - 10f;
	}

	private static int awayFrameRate = -1;

	private static int awayVsync;

	/// Host mode: the host stops playing and the game stays a plain server for the others until HostRejoin.
	internal static bool HostStepOut()
	{
		if (!Net.IsHost || phase != Phase.Running)
		{
			return false;
		}
		if (Systems.HostTrip.Out)
		{
			Log.Info("host: cannot step out while out on a night trip: come back to the shop first");
			return false;
		}
		awayFrameRate = Application.targetFrameRate;
		awayVsync = QualitySettings.vSyncCount;
		Net.HostAway = true;
		Net.Role = Role.Server;
		Net.LocalPlayer = 0;
		Server.RemoveHost("stepped out");
		Log.Info("host: stepped out; serving " + Server.PlayerCount + " player(s) without a local player");
		return true;
	}

	internal static bool HostRejoin()
	{
		if (!Net.HostAway)
		{
			return false;
		}
		Net.HostAway = false;
		Net.Role = Role.Host;
		Application.targetFrameRate = awayFrameRate;
		QualitySettings.vSyncCount = awayVsync;
		Server.AddHost(HostName(), HostSteamId());
		ProbablyTogether.UI.Presence.ResyncLocks(Locks.Table());
		Log.Info("host: rejoined as player " + Net.LocalPlayer + "; " + Server.PlayerCount + " player(s)");
		return true;
	}

	internal static void HostLeave()
	{
		if (Net.HostAway)
		{
			Net.HostAway = false;
			Net.Role = Role.Host;
			Application.targetFrameRate = awayFrameRate;
			QualitySettings.vSyncCount = awayVsync;
		}
		if (!Net.IsHost)
		{
			return;
		}
		ProbablyTogether.UI.HostAwayUi.Reset();
		Log.Info("host: leaving: the server stops (" + Server.PlayerCount + " player(s) incl. the host)");
		try
		{
			Server.Stopping = true;
			Server.Goodbye("the host left");
			if (phase == Phase.Running)
			{
				SaveForStop();
			}
			Server.StopListening();
			PanelLink.WriteNow("stopping");
		}
		catch (Exception ex)
		{
			Log.Warn("host: stopping threw: " + ex.Message);
		}
		ProbablyTogether.UI.LoadingScreen.HostStarting = false;
		Server.ResetAfterHost();
		phase = Phase.WaitMenu;
		menuSince = -1f;
		cfg = null;
		quitAtMs = 0L;
		nightSnapshotAtMs = -1L;
		newRun = false;
		inPlace = false;
		saveQueued = false;
		RunEnded = false;
		Ending = "";
		Net.Role = Role.Off;
		Net.LocalPlayer = 0;
		Net.Players.Clear();
		Locks.ServerClear();
		Paths.ClearOverride();
		Paths.KeepSettings = false;
		Game.ToMainMenu("the host left");
	}

	internal static void LoadFailed(Exception e)
	{
		if (phase < Phase.Stopping)
		{
			Log.Error("server: could not open the save");
			Log.Error("server: the game could not load slot " + cfg?.saveSlot + ": " + e.GetType().Name + ": " + e.Message + "; the file is left as it is; quitting");
			BeginStop("could not open the save");
		}
	}

	private static void SaveForStop()
	{
		PlayerStore store = Game.Store;
		if (store == null || !Game.ShopLoaded)
		{
			return;
		}
		if (RunEnded)
		{
			Log.Info("server: the run has ended: nothing saved before stopping");
			return;
		}
		if (GamePatches.SaveIsBlocked)
		{
			Log.Info("server: saves are blocked: nothing saved before stopping");
			return;
		}
		if ((int)store.storeState == 2)
		{
			Snapshot.SuppressRotate = true;
			try
			{
				store.SaveGame();
				Log.Info("server: saved slot " + store.saveSlotId + " (morning of day " + Game.Day + ") before stopping");
				return;
			}
			catch (Exception ex)
			{
				Log.Error("server: save before stop failed: " + ex);
				return;
			}
			finally
			{
				Snapshot.SuppressRotate = false;
			}
		}
		Log.Info("server: a day is in progress (" + store.storeState.ToString() + "): kept this morning's save of day " + Game.Day + " (the game only saves between days; today's progress since the morning is not kept)");
	}

	internal static int NewRunSlot()
	{
		NewRunPending = false;
		return cfg.saveSlot;
	}
}
