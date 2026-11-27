using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lang = ProbablyTogether.UI.Lang;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

public sealed class VsRules
{
	public string run = "";

	public string name = "";

	/// The run ends at the bedtime of this day.
	public int days = 14;

	public int bedSeconds = 120;

	public string difficulty = "normal";

	/// Days the lobby has started: one more after every bedtime.
	public int lobbyDay = 1;

	public bool ended;

	/// Every player's trade, perks, are dealt at random on their own copy.
	public bool randomTrade;

	public bool randomPerks;
}

public sealed class VsRow
{
	public int player;

	public string name = "";

	public ulong steam;

	public string spec = "";

	public int day;

	public int cash;

	public int debt;

	public Dictionary<string, int> rep = new Dictionary<string, int>();

	/// starting, morning, open, closed, out, night, over, offline
	public string state = "starting";

	public bool ready;

	public int saveDay;

	public string saveHash = "";

	public int copyDay;

	public string copyHash = "";

	public int Worth => cash - debt;

	public bool InSync
	{
		get
		{
			if (saveHash.Length > 0)
			{
				return saveHash == copyHash;
			}
			return false;
		}
	}
}

public sealed class MsgVsStatus
{
	public string spec = "";

	public int day;

	public int cash;

	public int debt;

	public Dictionary<string, int> rep = new Dictionary<string, int>();

	public string state = "";

	public int saveDay;

	public string saveHash = "";
}

public sealed class MsgVsBoard
{
	public VsRules rules;

	public List<VsRow> rows = new List<VsRow>();

	/// Seconds until bedtime is forced, or -1 while nobody is ready.
	public int secondsLeft = -1;
}

public sealed class MsgVsBed
{
	public bool ready;
}

public sealed class MsgVsNight
{
	public int day;

	public bool timeout;
}

public sealed class MsgVsSave
{
	public int day;

	public string hash = "";

	public int part;

	public int parts;

	public string data = "";
}

public sealed class MsgVsSaved
{
	public int day;

	public string hash = "";

	public bool ok;

	public string why = "";
}

/// Versus mode: every player runs a shop of their own in their own save; the host keeps a checked copy of every
/// player's latest save, the board (cash minus debt) and the shared bedtime. The game layer sees a solo game
/// (Net.Role reads Off); this system talks over the session through Net.Link.
public static class Versus
{
	private sealed class Upload
	{
		public int Day;

		public string Hash = "";

		public string[] Parts = new string[0];

		public int Got;
	}

	internal const string KStatus = "vs.status";

	internal const string KBoard = "vs.board";

	internal const string KBed = "vs.bed";

	internal const string KNight = "vs.night";

	internal const string KSave = "vs.save";

	internal const string KSaved = "vs.saved";

	internal const string KEnd = "vs.end";

	private const int PartChars = 600000;

	internal static VsRules Rules;

	private static readonly Dictionary<int, VsRow> rows = new Dictionary<int, VsRow>();

	private static List<VsRow> board = new List<VsRow>();

	private static int boardSecondsLeft = -1;

	private static float boardAt;

	// Host
	private static readonly Dictionary<int, Upload> uploads = new Dictionary<int, Upload>();

	private static long bedDeadlineMs;

	private static bool boardDirty;

	private static float boardSentAt;

	// Folders
	private static string runFolder;

	private static string prevOverride;

	private static bool foldersSet;

	// This copy's own shop
	private static bool picking;

	private static float pickIdleSince = -1f;

	private static bool loadStarted;

	private static bool shopUp;

	private static bool localReady;

	private static bool nightPending;

	private static float closeWaitUntil = -1f;

	private static int nightFromDay;

	private static bool saveDirty;

	private static int savedSlot = -1;

	private static string lastSaveHash = "";

	private static int lastSaveDay;

	private static float statusAt;

	private static string lastStatus = "";

	internal static bool NightAllowed;

	/// What this copy's own shop was dealt at random (null: picked).
	internal static string DealtTrade;

	internal static List<string> DealtPerks;

	internal static int NightsRun;

	internal static int NightsForced;

	internal static int UploadsSent;

	internal static int CopiesStored;

	internal static int CopiesRefused;

	internal static int SavesConfirmed;

	internal static int BoardsSeen;

	internal static string LastSavedWhy = "";

	internal static string LastProblem = "";

	internal static bool FromArgs => Args.Has("--br-versus");

	internal static bool Ended => Rules != null && Rules.ended;

	internal static bool LocalReady => localReady;

	internal static bool ShopUp => shopUp;

	/// A joiner on the game's new-game screens for their own shop.
	internal static bool Picking => picking && !loadStarted;

	/// The host's own trade, perks, dealt at random (its own choice; the rules' force applies to the others).
	private static bool hostRandomTrade;

	private static bool hostRandomPerks;

	internal static string RunFolder => runFolder;

	internal static IReadOnlyList<VsRow> Board => board;

	internal static int SecondsLeft
	{
		get
		{
			if (boardSecondsLeft < 0)
			{
				return -1;
			}
			return Math.Max(0, boardSecondsLeft - (int)(Time.realtimeSinceStartup - boardAt));
		}
	}

	internal static VsRow Row(int player)
	{
		if (Net.LinkServer)
		{
			rows.TryGetValue(player, out var value);
			return value;
		}
		return board.FirstOrDefault((VsRow r) => r.player == player);
	}

	internal static bool Owns(string kind)
	{
		return kind != null && kind.StartsWith("vs.", StringComparison.Ordinal);
	}

	// ---- Starting

	internal static void StartFromArgs()
	{
		ServerConfig cfg = ServerConfig.Load();
		cfg.versus = true;
		string why = HostBegin(cfg, cfg.newRun.difficulty);
		if (why != null)
		{
			Log.Error("versus: the run could not start: " + why);
			return;
		}
		Rules.randomTrade = cfg.forceRandomTrade;
		Rules.randomPerks = cfg.forceRandomPerks;
		SetHostRandom(cfg.newRun.randomTrade || cfg.forceRandomTrade, cfg.newRun.randomPerks || cfg.forceRandomPerks);
		WriteRun();
		autoScenario = cfg.newRun.scenario;
		autoPerks = Args.Get("--br-versus-perks");
	}

	internal static void SetHostRandom(bool trade, bool perks)
	{
		hostRandomTrade = trade;
		hostRandomPerks = perks;
	}

	private static string autoScenario;

	private static string autoPerks;

	private static float menuSince = -1f;

	/// Host: the session for a new versus run. The host's own shop starts after this, on the game's own new-game path.
	internal static string HostBegin(ServerConfig cfg, string difficulty, string baseFolder = null, string restoreTo = null)
	{
		bool flag = baseFolder == null;
		string text0 = Paths.Override;
		Reset();
		hostRandomTrade = false;
		hostRandomPerks = false;
		Rules = new VsRules
		{
			run = "vs-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture),
			name = cfg.name ?? "",
			days = cfg.versusDays,
			bedSeconds = cfg.readyTimeoutSeconds,
			difficulty = string.IsNullOrEmpty(difficulty) ? "normal" : difficulty
		};
		if (!UseRunFolder(baseFolder ?? Paths.Root, flag ? text0 : restoreTo))
		{
			return "the run's folder could not be made";
		}
		Net.Versus = true;
		Net.Role = Role.Host;
		Net.LocalPlayer = 0;
		if (!Server.Start(cfg))
		{
			Leave("could not listen");
			return "port " + cfg.port + " is not usable";
		}
		Server.ShopReady = true;
		PlayerInfo host = Server.AddHost(ServerMode.HostName(), ServerMode.HostSteamId()).Info;
		OnJoined(host);
		WriteRun();
		Log.Info("versus: run " + Rules.run + " '" + Rules.name + "' is open on port " + cfg.port + ": " + Rules.days + " day(s), bedtime timer " + Rules.bedSeconds + " s, difficulty " + Rules.difficulty + "; folder " + runFolder);
		return null;
	}

	/// Points the game's saves at the run's folder under baseFolder; restoreTo is the folder in use after the run.
	private static bool UseRunFolder(string baseFolder, string restoreTo)
	{
		if (!foldersSet)
		{
			prevOverride = restoreTo;
		}
		if (!Paths.SetOverride(baseFolder + "/versus/" + Rules.run))
		{
			return false;
		}
		foldersSet = true;
		Paths.KeepSettings = true;
		runFolder = Paths.Root;
		return true;
	}

	internal static JToken RulesFor(PlayerInfo p)
	{
		return JToken.FromObject(Rules, Wire.Serializer);
	}

	/// A joiner: the server runs a versus run. Pick a trade and perks on the game's screens and start a shop of one's own.
	internal static void ClientWelcome(JToken rules)
	{
		Reset();
		try
		{
			Rules = rules.ToObject<VsRules>(Wire.Serializer) ?? new VsRules();
		}
		catch (Exception ex)
		{
			Log.Warn("versus: the run's rules are unreadable (" + ex.Message + "); defaults");
			Rules = new VsRules();
		}
		if (string.IsNullOrEmpty(Rules.run))
		{
			Rules.run = "vs-unnamed";
		}
		UseRunFolder(Paths.Root, Paths.Override);
		RandomStarts.ForceTrade = Rules.randomTrade;
		RandomStarts.ForcePerks = Rules.randomPerks;
		ProbablyTogether.UI.RandomBoxes.ResetJoin();
		try
		{
			ProbablyTogether.UI.JoinMenu.OnJoined();
		}
		catch
		{
		}
		Log.Info("versus: joined run " + Rules.run + " '" + Rules.name + "' on lobby day " + Rules.lobbyDay + " of " + Rules.days + " (difficulty " + Rules.difficulty + ", bedtime timer " + Rules.bedSeconds + " s" + (Rules.randomTrade ? ", random trades" : "") + (Rules.randomPerks ? ", random perks" : "") + "); folder " + runFolder);
		string text = Args.Get("--br-versus-scenario");
		if (!string.IsNullOrEmpty(text))
		{
			autoScenario = text;
			autoPerks = Args.Get("--br-versus-perks");
			return;
		}
		OpenPicking();
	}

	private static void OpenPicking()
	{
		try
		{
			NewGameData instance = NewGameData.Instance;
			instance.saveSlot = -1;
			instance.saveFile = null;
			instance.startingPerks = new List<StartingPerk>();
			NewGameSpecializationUI.Open();
			picking = true;
			pickIdleSince = -1f;
			ProbablyTogether.UI.Notices.Show(Lang.Trf("Versus run '{0}': pick your trade and perks for your own shop. The difficulty is the host's ({1}).", Rules.name, Rules.difficulty), "versus");
			Log.Info("versus: the game's new-game screens are up for our own shop");
		}
		catch (Exception ex)
		{
			LastProblem = "new game screens: " + ex.Message;
			Log.Error("versus: the new-game screens did not open: " + ex);
			Leave("the new-game screens did not open");
		}
	}

	/// The game's Start on its new-game screens. True: let the game start the run itself.
	internal static bool OnStartGame()
	{
		if (!Net.Versus || loadStarted || !ProbablyTogether.UI.Ui.AtMenu)
		{
			return false;
		}
		if (!picking && !Net.LinkHost)
		{
			return false;
		}
		NewGameData instance = NewGameData.Instance;
		instance.saveSlot = -1;
		instance.saveFile = null;
		instance.isLoadFromMainMenu = false;
		instance.disableTutorialHint = true;
		instance.difficultyId = Rules.difficulty;
		bool flag = Net.LinkHost ? hostRandomTrade : (ProbablyTogether.UI.RandomBoxes.TradeOn || Rules.randomTrade);
		bool flag2 = Net.LinkHost ? hostRandomPerks : (ProbablyTogether.UI.RandomBoxes.PerksOn || Rules.randomPerks);
		RandomStarts.DealOwn(instance, flag, flag2);
		DealtTrade = flag ? instance.startType.ToString() : null;
		DealtPerks = flag2 ? (instance.startingPerks ?? new List<StartingPerk>()).Select((StartingPerk p) => p.id).ToList() : null;
		picking = false;
		loadStarted = true;
		Log.Info("versus: our own shop starts (" + instance.startType.ToString() + ", " + instance.difficultyId + ", " + ((instance.startingPerks != null) ? instance.startingPerks.Count : 0) + " perk(s))");
		return true;
	}

	private static void AutoStart()
	{
		NewGameData instance = NewGameData.Instance;
		if (instance == null || !Enum.TryParse<NewGameData.StartType>(autoScenario ?? "", ignoreCase: true, out NewGameData.StartType result) || (int)result == 0)
		{
			Log.Warn("versus: unknown trade '" + autoScenario + "', the water merchant instead");
			result = (NewGameData.StartType)1;
		}
		instance.startingPerks = new List<StartingPerk>();
		foreach (string item in (autoPerks ?? "").Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries))
		{
			StartingPerk startingPerk = Perks.Def(item.Trim());
			if (startingPerk != null)
			{
				instance.startingPerks.Add(startingPerk);
			}
		}
		instance.startType = result;
		instance.startScenarioId = "";
		instance.skipIntro = true;
		instance.endlessMode = false;
		autoScenario = null;
		picking = true;
		OnStartGame();
		EmporiumEntry.sceneLoadStartTime = Time.realtimeSinceStartup;
		SceneManager.LoadScene("InventoryScene", (LoadSceneMode)0);
	}

	// ---- Joins and leaves (host)

	internal static void OnJoined(PlayerInfo p)
	{
		if (!Net.LinkServer || p == null)
		{
			return;
		}
		if (!rows.TryGetValue(p.Id, out var value))
		{
			value = new VsRow
			{
				player = p.Id
			};
			rows[p.Id] = value;
		}
		value.name = p.Name;
		value.steam = p.SteamId;
		value.state = "starting";
		ReadCopyRecord(value);
		boardDirty = true;
	}

	internal static void OnLeft(PlayerInfo p)
	{
		if (p != null && rows.TryGetValue(p.Id, out var value))
		{
			value.state = "offline";
			value.ready = false;
			uploads.Remove(p.Id);
			boardDirty = true;
			Log.Info("versus: " + p.Name + " left the run; their row stays on the board, offline");
		}
	}

	internal static void ClientLost(string why)
	{
		if (Rules != null)
		{
			Log.Info("versus: the link to the host is gone (" + why + "); our shop goes on as a game of its own");
			if (shopUp)
			{
				ProbablyTogether.UI.Notices.Show(Lang.Tr("The versus host is gone. Your shop is your own save and carries on."), "versus");
			}
		}
		picking = false;
		localReady = false;
		nightPending = false;
	}

	/// Ends this copy's part in the run: the host closes the lobby, a player disconnects.
	internal static void Leave(string why)
	{
		Log.Info("versus: leaving the run (" + why + ")");
		if (Net.LinkHost)
		{
			Server.Stopping = true;
			Server.Goodbye(why);
			Server.StopListening();
			Server.ResetAfterHost();
			Net.Players.Clear();
			Net.LocalPlayer = 0;
			Net.Role = Role.Off;
			Net.Versus = false;
		}
		else if (Net.LinkOnline)
		{
			Client.Disconnect(why);
		}
		Net.Versus = false;
		picking = false;
	}

	private static void RestoreFolders()
	{
		if (!foldersSet)
		{
			return;
		}
		foldersSet = false;
		if (prevOverride != null)
		{
			Paths.SetOverride(prevOverride);
		}
		else
		{
			Paths.ClearOverride();
			Paths.KeepSettings = false;
		}
		prevOverride = null;
	}

	private static void Reset()
	{
		rows.Clear();
		board = new List<VsRow>();
		uploads.Clear();
		boardSecondsLeft = -1;
		bedDeadlineMs = 0L;
		picking = false;
		loadStarted = false;
		shopUp = false;
		localReady = false;
		nightPending = false;
		closeWaitUntil = -1f;
		saveDirty = false;
		lastSaveHash = "";
		lastSaveDay = 0;
		lastStatus = "";
		NightAllowed = false;
	}

	// ---- Frames

	internal static void OnServerFrame(int player, string kind, JToken d)
	{
		try
		{
			switch (kind)
			{
			case KStatus:
				ServerStatus(player, Wire.To<MsgVsStatus>(d));
				break;
			case KBed:
				ServerBed(player, Wire.To<MsgVsBed>(d)?.ready ?? false);
				break;
			case KSave:
				ServerSave(player, Wire.To<MsgVsSave>(d));
				break;
			default:
				Log.Warn("versus: unexpected " + kind + " from " + Net.PlayerName(player));
				break;
			}
		}
		catch (Exception ex)
		{
			Log.Error("versus: " + kind + " from " + Net.PlayerName(player) + " threw: " + ex);
		}
	}

	internal static void OnClientFrame(string kind, JToken d)
	{
		try
		{
			switch (kind)
			{
			case KBoard:
				ApplyBoard(Wire.To<MsgVsBoard>(d));
				break;
			case KNight:
				OnNight(Wire.To<MsgVsNight>(d));
				break;
			case KSaved:
				OnSaved(Wire.To<MsgVsSaved>(d));
				break;
			case KEnd:
				ApplyBoard(Wire.To<MsgVsBoard>(d));
				OnEnd();
				break;
			default:
				Log.Warn("versus: unexpected " + kind + " from the host");
				break;
			}
		}
		catch (Exception ex)
		{
			Log.Error("versus: " + kind + " threw: " + ex);
		}
	}

	private static void ToHost(string kind, object msg)
	{
		if (Net.LinkHost)
		{
			OnServerFrame(Net.LocalPlayer, kind, JToken.FromObject(msg, Wire.Serializer));
		}
		else if (Net.LinkOnline && Client.Connected)
		{
			Client.Send(Wire.Encode(kind, msg));
		}
	}

	private static void ToPlayer(int player, string kind, object msg)
	{
		if (player == Net.LocalPlayer)
		{
			OnClientFrame(kind, JToken.FromObject(msg, Wire.Serializer));
		}
		else if (Server.ByPlayer.TryGetValue(player, out var value))
		{
			value.Conn.Send(Wire.Encode(kind, msg));
		}
	}

	private static void ToAll(string kind, object msg)
	{
		Server.SendToJoined(Wire.Encode(kind, msg), -1);
		OnClientFrame(kind, JToken.FromObject(msg, Wire.Serializer));
	}

	// ---- Status and board

	private static MsgVsStatus ReadStatus()
	{
		MsgVsStatus msgVsStatus = new MsgVsStatus
		{
			saveDay = lastSaveDay,
			saveHash = lastSaveHash
		};
		PlayerStore store = Game.Store;
		if (!shopUp || store == null || !Game.ShopLoaded)
		{
			msgVsStatus.state = (shopUp ? "loading" : "starting");
			return msgVsStatus;
		}
		msgVsStatus.spec = store.startType.ToString();
		msgVsStatus.day = Game.Day;
		msgVsStatus.cash = store.playerCash;
		msgVsStatus.debt = Debt(store);
		if (store.storeReputations != null)
		{
			foreach (StoreReputation storeReputation in store.storeReputations)
			{
				if (storeReputation != null && !string.IsNullOrEmpty(storeReputation.factionId))
				{
					msgVsStatus.rep[storeReputation.factionId] = storeReputation.influence;
				}
			}
		}
		msgVsStatus.state = LocalState(store);
		return msgVsStatus;
	}

	/// What the shop still owes: the game's own repayment schedule (weeks left times the weekly payment).
	internal static int Debt(PlayerStore ps)
	{
		if (ps == null || ps.mortgageWeekLeft <= 0 || ps.mortgageWeeklyPayment <= 0)
		{
			return 0;
		}
		return ps.mortgageWeekLeft * ps.mortgageWeeklyPayment;
	}

	private static string LocalState(PlayerStore ps)
	{
		try
		{
			GameOverUIManager instance = GameOverUIManager.Instance;
			if ((Object)(object)instance != (Object)null && (Object)(object)instance.uiPanel != (Object)null && instance.uiPanel.activeSelf)
			{
				return "over";
			}
		}
		catch
		{
		}
		if (NightAllowed || nightPending)
		{
			return "night";
		}
		switch ((int)ps.storeState)
		{
		case 1:
			return "open";
		case 2:
			return "morning";
		default:
			if (!AtHome(ps))
			{
				return "out";
			}
			return "closed";
		}
	}

	private static bool AtHome(PlayerStore ps)
	{
		if ((int)ps.playerLocation != 0)
		{
			return false;
		}
		return !DayNightOuting.IsOut;
	}

	private static void SendStatus(bool force)
	{
		MsgVsStatus msgVsStatus = ReadStatus();
		string text = Wire.Encode(KStatus, msgVsStatus, out var json) != null ? json : "";
		if (!force && text == lastStatus && Time.realtimeSinceStartup - statusAt < 10f)
		{
			return;
		}
		lastStatus = text;
		statusAt = Time.realtimeSinceStartup;
		ToHost(KStatus, msgVsStatus);
	}

	private static void ServerStatus(int player, MsgVsStatus m)
	{
		if (m == null || !rows.TryGetValue(player, out var value))
		{
			return;
		}
		string state = value.state;
		value.spec = m.spec ?? "";
		value.day = m.day;
		value.cash = m.cash;
		value.debt = Math.Max(0, m.debt);
		value.rep = m.rep ?? new Dictionary<string, int>();
		value.state = m.state ?? "";
		value.saveDay = m.saveDay;
		value.saveHash = m.saveHash ?? "";
		if (value.state != "closed" && value.ready && !NightAllowed)
		{
			value.ready = false;
		}
		if (state != value.state)
		{
			Log.Info("versus: " + value.name + " is now " + value.state + " (day " + value.day + ", cash " + value.cash + ", debt " + value.debt + ")");
		}
		boardDirty = true;
	}

	private static MsgVsBoard BuildBoard()
	{
		return new MsgVsBoard
		{
			rules = Rules,
			rows = Ranked(rows.Values),
			secondsLeft = (bedDeadlineMs > 0) ? ((int)Math.Max(0L, (bedDeadlineMs - Clock.Ms + 999) / 1000)) : (-1)
		};
	}

	internal static List<VsRow> Ranked(IEnumerable<VsRow> list)
	{
		return list.OrderByDescending((VsRow r) => r.Worth).ThenBy((VsRow r) => r.player).ToList();
	}

	private static void ApplyBoard(MsgVsBoard m)
	{
		if (m == null)
		{
			return;
		}
		if (m.rules != null && !Net.LinkServer)
		{
			Rules = m.rules;
		}
		board = m.rows ?? new List<VsRow>();
		boardSecondsLeft = m.secondsLeft;
		boardAt = Time.realtimeSinceStartup;
		BoardsSeen++;
		VsRow vsRow = board.FirstOrDefault((VsRow r) => r.player == Net.LocalPlayer);
		if (vsRow != null && !vsRow.ready && localReady && !nightPending)
		{
			localReady = false;
		}
	}

	internal static string BoardLine()
	{
		List<string> list = new List<string>();
		int num = 1;
		foreach (VsRow item in board)
		{
			list.Add(num++ + ". " + item.name + " " + item.Worth + " (" + item.state + ", day " + item.day + ((item.ready ? ", ready" : "") + (item.InSync ? ", copy ok" : "")) + ")");
		}
		return string.Join("; ", list);
	}

	// ---- Bedtime

	/// The local player asks to go to bed, or takes it back.
	internal static void PressBed(bool on)
	{
		if (!Net.Versus || Ended)
		{
			return;
		}
		if (on)
		{
			PlayerStore store = Game.Store;
			string text = (store == null) ? "The shop is not up." : (((int)store.storeState != 0) ? "Close the shop first." : ((!AtHome(store)) ? "Come back from tonight's trip first." : null));
			if (text != null)
			{
				ProbablyTogether.UI.Notices.Show(Lang.Tr(text), "versus");
				return;
			}
		}
		if (localReady == on)
		{
			return;
		}
		localReady = on;
		SendStatus(force: true);
		ToHost(KBed, new MsgVsBed
		{
			ready = on
		});
		if (on)
		{
			ProbablyTogether.UI.Notices.Show(Lang.Tr("Ready for bed. The night starts when every shop is ready."), "versus");
		}
	}

	private static void ServerBed(int player, bool on)
	{
		if (Ended || !rows.TryGetValue(player, out var value) || value.ready == on)
		{
			return;
		}
		value.ready = on;
		if (on && bedDeadlineMs <= 0)
		{
			bedDeadlineMs = Clock.Ms + (long)Rules.bedSeconds * 1000L;
		}
		if (!rows.Values.Any((VsRow r) => r.ready))
		{
			bedDeadlineMs = 0L;
		}
		Log.Info("versus: " + value.name + (on ? " is ready for bed" : " is not ready any more") + "; ready " + rows.Values.Count((VsRow r) => r.ready) + " of " + Awaited().Count);
		boardDirty = true;
		EvaluateBed();
	}

	/// The shops bedtime waits for: connected, with a shop up and the run not over for them.
	private static List<VsRow> Awaited()
	{
		return rows.Values.Where((VsRow r) => Server.ByPlayer.ContainsKey(r.player) && r.state != "starting" && r.state != "loading" && r.state != "over" && r.state != "offline").ToList();
	}

	private static void EvaluateBed()
	{
		if (!Net.LinkServer || Ended || Rules == null)
		{
			return;
		}
		List<VsRow> list = Awaited();
		if (list.Count == 0)
		{
			return;
		}
		bool flag = list.All((VsRow r) => r.ready);
		bool flag2 = bedDeadlineMs > 0 && Clock.Ms >= bedDeadlineMs;
		if (!flag && !flag2)
		{
			return;
		}
		int lobbyDay = Rules.lobbyDay;
		string text = flag ? ("every shop is ready (" + list.Count + ")") : ("the bedtime timer ran out (" + list.Count((VsRow r) => r.ready) + " of " + list.Count + " ready after " + Rules.bedSeconds + " s)");
		foreach (VsRow value in rows.Values)
		{
			value.ready = false;
		}
		bedDeadlineMs = 0L;
		if (lobbyDay >= Rules.days)
		{
			Rules.ended = true;
			WriteRun();
			Log.Info("versus: day " + lobbyDay + " of " + Rules.days + " is over (" + text + "): the run ends; " + BoardText(Ranked(rows.Values)));
			ToAll(KEnd, BuildBoard());
			return;
		}
		Rules.lobbyDay = lobbyDay + 1;
		WriteRun();
		Log.Info("versus: bedtime of lobby day " + lobbyDay + ": " + text + "; every shop sleeps now");
		ToAll(KNight, new MsgVsNight
		{
			day = lobbyDay,
			timeout = !flag
		});
		boardDirty = true;
	}

	internal static string BoardText(List<VsRow> list)
	{
		return string.Join(", ", list.Select((VsRow r, int i) => i + 1 + ". " + r.name + " " + r.Worth));
	}

	private static void OnNight(MsgVsNight m)
	{
		if (m == null)
		{
			return;
		}
		localReady = false;
		nightPending = true;
		nightFromDay = Game.Day;
		closeWaitUntil = -1f;
		Log.Info("versus: bedtime of lobby day " + m.day + (m.timeout ? " (the timer ran out)" : "") + ": our day " + nightFromDay + " ends");
		if (m.timeout)
		{
			ProbablyTogether.UI.Notices.Show(Lang.Tr("Bedtime: the timer ran out and the night starts."), "versus");
		}
	}

	/// Runs the night the host called, once the shop can sleep: an open shop is closed first, a trip comes home first.
	private static void NightTick()
	{
		if (!nightPending)
		{
			return;
		}
		PlayerStore store = Game.Store;
		if (store == null || !Game.ShopLoaded)
		{
			return;
		}
		if (LocalState(store) == "over" && !NightAllowed)
		{
			nightPending = false;
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if ((int)store.storeState != 0)
		{
			if (closeWaitUntil < 0f)
			{
				closeWaitUntil = realtimeSinceStartup + 3f;
				NightsForced++;
				Log.Info("versus: the shop is still " + (((int)store.storeState == 1) ? "open" : "in its morning") + " at bedtime: the game closes it");
				try
				{
					StoreShutterButton.Instance.EndAndClose();
				}
				catch (Exception ex)
				{
					Log.Error("versus: closing the shop threw: " + ex);
				}
			}
			return;
		}
		if (closeWaitUntil > 0f && realtimeSinceStartup < closeWaitUntil)
		{
			return;
		}
		if (!AtHome(store))
		{
			return;
		}
		EndOfDayUIManager instance = EndOfDayUIManager.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		nightPending = false;
		closeWaitUntil = -1f;
		NightAllowed = true;
		try
		{
			instance.OnEndDayButtonClick();
			NightsRun++;
		}
		catch (Exception ex2)
		{
			Log.Error("versus: the end-of-day button threw: " + ex2);
		}
		finally
		{
			NightAllowed = false;
		}
	}

	private static void OnEnd()
	{
		localReady = false;
		nightPending = false;
		Log.Info("versus: the run is over: " + BoardText(board));
		VsRow vsRow = board.FirstOrDefault();
		if (vsRow != null)
		{
			ProbablyTogether.UI.Notices.Show(Lang.Trf("The versus run is over. {0} wins with {1}.", vsRow.name, vsRow.Worth.ToString(CultureInfo.InvariantCulture)), "versus");
		}
	}

	// ---- Save copies

	/// The game wrote this copy's save (Versus.SavedPatch).
	internal static void OnSlotWritten(int slot)
	{
		if (Net.Versus && slot >= 0)
		{
			savedSlot = slot;
			saveDirty = true;
		}
	}

	private static void SaveTick()
	{
		if (!saveDirty || savedSlot < 0)
		{
			return;
		}
		saveDirty = false;
		string text = Paths.Under(SaveFiles.Slot(savedSlot));
		byte[] array;
		try
		{
			array = File.ReadAllBytes(text);
		}
		catch (Exception ex)
		{
			Log.Warn("versus: our save " + text + " could not be read for the host's copy: " + ex.Message);
			return;
		}
		string text2 = Hash(array);
		int day = Game.Day;
		lastSaveHash = text2;
		lastSaveDay = day;
		if (Net.LinkHost)
		{
			StoreCopy(Net.LocalPlayer, day, text2, array);
			SendStatus(force: true);
			return;
		}
		string text3 = Convert.ToBase64String(Gzip(array));
		int num = Math.Max(1, (text3.Length + PartChars - 1) / PartChars);
		for (int i = 0; i < num; i++)
		{
			int num2 = i * PartChars;
			ToHost(KSave, new MsgVsSave
			{
				day = day,
				hash = text2,
				part = i,
				parts = num,
				data = text3.Substring(num2, Math.Min(PartChars, text3.Length - num2))
			});
		}
		UploadsSent++;
		Log.Info("versus: our day " + day + " save (" + array.Length / 1024 + " KB, " + text2.Substring(0, 12) + ") goes to the host in " + num + " part(s)");
		SendStatus(force: true);
	}

	private static void ServerSave(int player, MsgVsSave m)
	{
		if (m == null || m.parts < 1 || m.parts > 64 || m.part < 0 || m.part >= m.parts || string.IsNullOrEmpty(m.hash))
		{
			return;
		}
		if (!uploads.TryGetValue(player, out var value) || value.Hash != m.hash || value.Parts.Length != m.parts)
		{
			value = new Upload
			{
				Day = m.day,
				Hash = m.hash,
				Parts = new string[m.parts]
			};
			uploads[player] = value;
		}
		if (value.Parts[m.part] == null)
		{
			value.Parts[m.part] = m.data ?? "";
			value.Got++;
		}
		if (value.Got < value.Parts.Length)
		{
			return;
		}
		uploads.Remove(player);
		byte[] array;
		try
		{
			array = Gunzip(Convert.FromBase64String(string.Concat(value.Parts)));
		}
		catch (Exception ex)
		{
			CopiesRefused++;
			Log.Warn("versus: " + Net.PlayerName(player) + "'s save did not unpack: " + ex.Message);
			ToPlayer(player, KSaved, new MsgVsSaved
			{
				day = value.Day,
				hash = value.Hash,
				ok = false,
				why = "unreadable"
			});
			return;
		}
		string text = Hash(array);
		if (text != value.Hash)
		{
			CopiesRefused++;
			Log.Warn("versus: " + Net.PlayerName(player) + "'s save arrived as " + text.Substring(0, 12) + ", not the " + value.Hash.Substring(0, Math.Min(12, value.Hash.Length)) + " they sent: not kept");
			ToPlayer(player, KSaved, new MsgVsSaved
			{
				day = value.Day,
				hash = value.Hash,
				ok = false,
				why = "hash mismatch"
			});
			return;
		}
		StoreCopy(player, value.Day, text, array);
	}

	private static void StoreCopy(int player, int day, string hash, byte[] bytes)
	{
		if (!rows.TryGetValue(player, out var value))
		{
			return;
		}
		string text = CopyFolder(value);
		try
		{
			System.IO.Directory.CreateDirectory(text);
			File.WriteAllBytes(text + "/save.es3", bytes);
			JObject jObject = new JObject
			{
				["player"] = value.name,
				["steam"] = value.steam.ToString(CultureInfo.InvariantCulture),
				["day"] = day,
				["hash"] = hash,
				["bytes"] = bytes.Length,
				["at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
			};
			File.WriteAllText(text + "/copy.json", jObject.ToString());
		}
		catch (Exception ex)
		{
			CopiesRefused++;
			Log.Error("versus: the copy of " + value.name + "'s save could not be written: " + ex.Message);
			ToPlayer(player, KSaved, new MsgVsSaved
			{
				day = day,
				hash = hash,
				ok = false,
				why = "the host could not write it"
			});
			return;
		}
		value.copyDay = day;
		value.copyHash = hash;
		CopiesStored++;
		boardDirty = true;
		Log.Info("versus: copy of " + value.name + "'s day " + day + " save kept (" + bytes.Length / 1024 + " KB, " + hash.Substring(0, 12) + ")");
		ToPlayer(player, KSaved, new MsgVsSaved
		{
			day = day,
			hash = hash,
			ok = true
		});
	}

	private static void OnSaved(MsgVsSaved m)
	{
		if (m != null)
		{
			LastSavedWhy = (m.ok ? "ok" : m.why);
			if (m.ok)
			{
				SavesConfirmed++;
			}
			else
			{
				Log.Warn("versus: the host did not keep our day " + m.day + " save: " + m.why + "; it is sent again");
				if (m.hash == lastSaveHash)
				{
					saveDirty = true;
				}
			}
		}
	}

	private static string CopyFolder(VsRow r)
	{
		return runFolder + "/players/" + ((r.steam != 0L) ? r.steam.ToString(CultureInfo.InvariantCulture) : ("player" + r.player));
	}

	private static void ReadCopyRecord(VsRow r)
	{
		try
		{
			string path = CopyFolder(r) + "/copy.json";
			if (File.Exists(path))
			{
				JObject jObject = JObject.Parse(File.ReadAllText(path));
				r.copyDay = ((int?)jObject["day"]) ?? 0;
				r.copyHash = ((string)jObject["hash"]) ?? "";
			}
		}
		catch
		{
		}
	}

	private static void WriteRun()
	{
		if (!Net.LinkServer || runFolder == null)
		{
			return;
		}
		try
		{
			JObject jObject = JObject.FromObject(Rules, Wire.Serializer);
			jObject["players"] = JArray.FromObject(Ranked(rows.Values).Select((VsRow r) => new { r.player, r.name, steam = r.steam.ToString(CultureInfo.InvariantCulture), r.spec, r.day, r.cash, r.debt, worth = r.Worth, r.state }), Wire.Serializer);
			File.WriteAllText(runFolder + "/run.json", jObject.ToString());
		}
		catch (Exception ex)
		{
			Log.Warn("versus: run.json could not be written: " + ex.Message);
		}
	}

	internal static string Hash(byte[] bytes)
	{
		using SHA256 sHA = SHA256.Create();
		return BitConverter.ToString(sHA.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
	}

	private static byte[] Gzip(byte[] bytes)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using (GZipStream gZipStream = new GZipStream(memoryStream, System.IO.Compression.CompressionLevel.Optimal))
		{
			gZipStream.Write(bytes, 0, bytes.Length);
		}
		return memoryStream.ToArray();
	}

	private static byte[] Gunzip(byte[] bytes)
	{
		using MemoryStream stream = new MemoryStream(bytes);
		using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress);
		using MemoryStream memoryStream = new MemoryStream();
		gZipStream.CopyTo(memoryStream);
		return memoryStream.ToArray();
	}

	// ---- Tick

	/// Core.Tick, while in a versus session.
	internal static void Tick()
	{
		try
		{
			if (autoScenario != null && ProbablyTogether.UI.Ui.AtMenu && (Object)(object)NewGameData.Instance != (Object)null)
			{
				// The menu's own jobs (the bot runner among them) start after 2 s on the menu.
				if (menuSince < 0f)
				{
					menuSince = Time.realtimeSinceStartup;
				}
				else if (Time.realtimeSinceStartup - menuSince > 3f)
				{
					menuSince = -1f;
					AutoStart();
				}
			}
			PickTick();
			if (!shopUp && loadStarted && Game.ShopLoaded)
			{
				shopUp = true;
				Log.Info("versus: our own shop is up (" + Game.Store.startType.ToString() + ", day " + Game.Day + ", cash " + Game.Store.playerCash + ", slot " + Game.Store.saveSlotId + ")");
				SendStatus(force: true);
				if (Game.Store.saveSlotId >= 0 && File.Exists(Paths.Under(SaveFiles.Slot(Game.Store.saveSlotId))))
				{
					OnSlotWritten(Game.Store.saveSlotId);
				}
			}
			if (shopUp)
			{
				NightTick();
				SaveTick();
				if (Time.realtimeSinceStartup - statusAt >= 1f)
				{
					SendStatus(force: false);
				}
			}
			if (Net.LinkServer)
			{
				EvaluateBed();
				if (boardDirty && Time.realtimeSinceStartup - boardSentAt >= 0.5f)
				{
					boardDirty = false;
					boardSentAt = Time.realtimeSinceStartup;
					ToAll(KBoard, BuildBoard());
				}
				else if (bedDeadlineMs > 0 && Time.realtimeSinceStartup - boardSentAt >= 5f)
				{
					boardDirty = true;
				}
			}
		}
		catch (Exception ex)
		{
			if (Log.Allow("versus|tick", 10000L, out var skipped))
			{
				Log.Error("versus: tick threw" + Log.More(skipped) + ": " + ex);
			}
		}
	}

	/// A joiner who closes the game's new-game screens leaves the run.
	private static void PickTick()
	{
		if (!picking || loadStarted)
		{
			return;
		}
		if (!ProbablyTogether.UI.Ui.AtMenu)
		{
			return;
		}
		if (NewGameScreenOpen)
		{
			pickIdleSince = -1f;
			return;
		}
		if (pickIdleSince < 0f)
		{
			pickIdleSince = Time.realtimeSinceStartup;
		}
		else if (Time.realtimeSinceStartup - pickIdleSince > 1.5f)
		{
			Log.Info("versus: the new-game screens were closed before the shop started");
			Leave("closed the new-game screens");
		}
	}

	private static bool NewGameScreenOpen
	{
		get
		{
			try
			{
				CustomUIManager instance = CustomUIManager.Instance;
				if ((Object)(object)instance == (Object)null)
				{
					return false;
				}
				foreach (string item in new string[3] { "new_game_specialization", "new_game_perks", "new_game_difficulty" })
				{
					CustomUIWindow window = instance.GetWindow(item);
					if (window != null && window.IsAlive)
					{
						return true;
					}
				}
			}
			catch
			{
			}
			return false;
		}
	}

	/// Every frame: back on the menu after playing a versus shop, the folders go back.
	[SystemTick]
	private static void MenuTick()
	{
		if (foldersSet && ProbablyTogether.UI.Ui.AtMenu && (shopUp || !Net.Versus))
		{
			if (Net.Versus)
			{
				Leave("back on the menu");
			}
			RestoreFolders();
			Reset();
			Rules = null;
			RandomStarts.ForceTrade = false;
			RandomStarts.ForcePerks = false;
		}
	}
}
