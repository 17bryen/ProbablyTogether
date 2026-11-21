using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ProbablyTogether.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

namespace ProbablyTogether;

public static class Snapshot
{
	public const int ReservedSlot = 990077;

	public const string SessionFile = "probably_together/session.es3";

	internal const string ServerFile = "probably_together/snapshot.es3";

	internal static bool SuppressRotate;

	internal static Exception CaptureError;

	public static Func<bool> SaveBlocked;

	private static bool saveBlockedThrew;

	private static int seq;

	private static MsgSnapshot morning;

	private static byte[] morningFrame;

	private static long lastResyncSweepMs;

	internal const int StuckAfterLoads = 3;

	private static MsgSnapshot loading;

	private static int replayCount;

	private static readonly List<KeyValuePair<string, JToken>> held = new List<KeyValuePair<string, JToken>>();

	private static long loadStartMs;

	private static int shopFrames;

	private static long logWaitMs;

	private const long LogWaitMaxMs = 120000L;

	private static bool sceneRequested;

	private static bool menuReturnPending;

	private static long askedMs;

	/// Resyncs asked soon after a load, in a row: each doubles the wait before the next.
	private static int quickAsks;

	private static long loadedMs;

	/// 30 s after the last resync, doubled for each quick re-ask after a load, up to 4 minutes.
	internal static long AskWaitMs => Math.Min(240000L, 30000L << Math.Min(quickAsks, 3));

	private static Dictionary<long, string> askTable;

	private static string askReason;

	private static int askDay;

	private const int NoteItems = 6;

	internal static string LastReloadReport;

	public static bool IsLoading
	{
		get
		{
			if (Net.IsClient)
			{
				if (Client.State != ClientState.Loading)
				{
					return Client.State == ClientState.AwaitSnapshot;
				}
				return true;
			}
			return false;
		}
	}

	public static bool Capturing { get; internal set; }

	public static bool SaveIsBlocked
	{
		get
		{
			Func<bool> saveBlocked = SaveBlocked;
			if (saveBlocked == null || Capturing)
			{
				return false;
			}
			Delegate[] invocationList = saveBlocked.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				try
				{
					if (((Func<bool>)obj)())
					{
						return true;
					}
				}
				catch (Exception ex)
				{
					if (!saveBlockedThrew)
					{
						saveBlockedThrew = true;
						Log.Error("Snapshot.SaveBlocked threw (save allowed): " + ex);
					}
				}
			}
			return false;
		}
	}

	internal static int AskCount { get; private set; }

	internal static int Loads { get; private set; }

	internal static DayLog.ReplayStats LastReplay { get; private set; }

	internal static bool SessionOpen
	{
		get
		{
			if (Game.Store != null)
			{
				return Game.Store.saveSlotId == 990077;
			}
			return false;
		}
	}

	public static event Action Loaded;

	public static event Action<int> ClientReady;

	internal static event Action<string> BeforeMorningCapture;

	public static void SendTo(int player)
	{
		SendTo(player, "resync");
	}

	public static void SendToAll()
	{
		SendToAll("resync");
	}

	internal static void SendToAll(string reason)
	{
		if (!Net.IsServer)
		{
			return;
		}
		foreach (int item in new List<int>(Server.ByPlayer.Keys))
		{
			SendTo(item, reason);
		}
	}

	internal static void SendTo(int player, string reason)
	{
		if (!Net.IsServer || !Server.ByPlayer.TryGetValue(player, out var value))
		{
			return;
		}
		if (value.Conn.IsLoopback)
		{
			value.State = Server.PeerState.Ready;
			return;
		}
		if (morningFrame == null && !NewMorning("first join"))
		{
			Log.Error("server: no morning snapshot for " + value.Info.Name + "; disconnecting them");
			value.Conn.CloseAfterFlush("snapshot failed");
			return;
		}
		value.SnapshotSeq = morning.seq;
		value.State = Server.PeerState.Syncing;
		value.LastSnapshotMs = Clock.Ms;
		value.ResyncAtMs = 0L;
		value.Conn.Send(morningFrame);
		value.Conn.Send(Wire.Encode("sys.replay", new MsgReplay
		{
			count = DayLog.Count,
			locks = Locks.Table()
		}));
		long num = 0L;
		foreach (byte[] frame in DayLog.Frames)
		{
			value.Conn.Send(frame);
			num += frame.Length;
		}
		Log.Info("server: morning snapshot #" + morning.seq + " (" + reason + ") to " + value.Info.Name + ": " + morningFrame.Length / 1024 + " KB + " + DayLog.Count + " log message(s) (" + num / 1024 + " KB), day " + morning.day);
	}

	internal static bool NewMorning(string reason, bool capRestart = false)
	{
		if (Net.IsServer && Game.ShopLoaded && Snapshot.BeforeMorningCapture != null)
		{
			Delegate[] invocationList = Snapshot.BeforeMorningCapture.GetInvocationList();
			for (int i = 0; i < invocationList.Length; i++)
			{
				Action<string> action = (Action<string>)invocationList[i];
				try
				{
					action(reason);
				}
				catch (Exception ex)
				{
					Log.Warn("server: before-morning step failed (" + action.Method.DeclaringType?.Name + "): " + ex.Message);
				}
			}
		}
		MsgSnapshot msgSnapshot = Capture(reason);
		if (msgSnapshot == null)
		{
			return false;
		}
		morning = msgSnapshot;
		morningFrame = Wire.Encode("sys.snapshot", msgSnapshot);
		DayLog.Restart(msgSnapshot.day, msgSnapshot.seq, capRestart);
		return true;
	}

	internal static MsgSnapshot Capture(string reason)
	{
		if (!Game.ShopLoaded)
		{
			return null;
		}
		PlayerStore store = Game.Store;
		Ids.Normalize();
		CaptureError = null;
		Capturing = true;
		long ms = Clock.Ms;
		try
		{
			store.SaveGame();
		}
		catch (Exception captureError)
		{
			CaptureError = captureError;
		}
		finally
		{
			Capturing = false;
		}
		if (CaptureError != null)
		{
			Log.Error("server: snapshot capture failed: " + CaptureError);
			return null;
		}
		byte[] array;
		try
		{
			array = File.ReadAllBytes(Paths.Under("probably_together/snapshot.es3"));
		}
		catch (Exception ex)
		{
			Log.Error("server: snapshot file unreadable: " + ex.Message);
			return null;
		}
		MsgDigest msgDigest = Digest.Compute();
		MsgSnapshot msgSnapshot = new MsgSnapshot
		{
			seq = ++seq,
			reason = reason,
			day = Game.Day,
			slot = store.saveSlotId,
			uidCounter = store.currentUniqueId,
			items = msgDigest.items,
			locks = Locks.Table()
		};
		if (LooksLikeText(array))
		{
			msgSnapshot.save = Encoding.UTF8.GetString(array);
		}
		else
		{
			msgSnapshot.saveB64 = Convert.ToBase64String(array);
		}
		Log.Info("server: captured morning snapshot #" + msgSnapshot.seq + " (" + reason + ") in " + (Clock.Ms - ms) + " ms: save " + array.Length / 1024 + " KB, uid counter " + msgSnapshot.uidCounter + "; " + msgDigest.Describe());
		return msgSnapshot;
	}

	private static bool LooksLikeText(byte[] b)
	{
		if (b.Length == 0 || b[0] != 123)
		{
			return false;
		}
		try
		{
			new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(b);
			return true;
		}
		catch
		{
			return false;
		}
	}

	internal static void OnReady(Server.Peer p, MsgReady r)
	{
		if (r == null || p.State != Server.PeerState.Syncing)
		{
			return;
		}
		if (r.seq != p.SnapshotSeq)
		{
			Log.Info("server: " + p.Info.Name + " finished an older snapshot #" + r.seq + " (latest #" + p.SnapshotSeq + "); waiting");
			return;
		}
		p.State = Server.PeerState.Ready;
		p.ReadyMs = Clock.Ms;
		if (p.ModsNote != null)
		{
			Log.Info("server: told " + p.Info.Name + ": " + p.ModsNote);
			Net.ToPlayer(p.Id, "chat.notice", new MsgChatNotice
			{
				text = p.ModsNote
			});
			p.ModsNote = null;
		}
		MsgDigest msgDigest = Digest.Compute();
		string text = Digest.Diff(msgDigest, r.digest);
		string s = null;
		if (text.Length > 0)
		{
			try
			{
				s = r.digest?.Describe();
			}
			catch (Exception)
			{
				s = "?";
			}
		}
		Log.Info("server: " + p.Info.Name + " is live on snapshot #" + r.seq + " after " + (p.ReadyMs - p.LastSnapshotMs) + " ms; " + ((text.Length == 0) ? ("their shop MATCHES ours: " + msgDigest.Describe()) : ("their shop DIFFERS (" + Text.Clip(text, 300) + "): ours " + msgDigest.Describe() + " / theirs " + Text.Clip(s, 300))) + EconAreas(r));
		string text2 = Digest.AreasOf(text);
		if (text2.Length == 0)
		{
			p.StuckAreas = null;
			p.StuckLoads = 0;
		}
		else if (text2 == p.StuckAreas)
		{
			p.StuckLoads++;
		}
		else
		{
			p.StuckAreas = text2;
			p.StuckLoads = 1;
		}
		try
		{
			Snapshot.ClientReady?.Invoke(p.Id);
		}
		catch (Exception ex2)
		{
			Log.Error("Snapshot.ClientReady handler: " + ex2);
		}
	}

	private static string EconAreas(MsgReady r)
	{
		if (r?.econ == null || r.econ.Count == 0)
		{
			return "";
		}
		try
		{
			Dictionary<string, string> dictionary = Economy.Parts();
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, string> item in dictionary)
			{
				r.econ.TryGetValue(item.Key, out var value);
				if (value != item.Value)
				{
					list.Add(item.Key);
				}
			}
			foreach (string key in r.econ.Keys)
			{
				if (!dictionary.ContainsKey(key))
				{
					list.Add(Text.Clip(key, 20));
				}
			}
			return (list.Count == 0) ? "" : ("; economy differs in: " + Text.Clip(string.Join(", ", list), 200));
		}
		catch (Exception ex)
		{
			return "; economy areas not compared: " + ex.Message;
		}
	}

	private static string AreaWords(string areas)
	{
		List<string> list = new List<string>();
		string[] array = areas.Split(new string[1] { ", " }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array)
		{
			string item = text switch
			{
				"daynight" => "the day", 
				"customers" => "customers", 
				"story" => "the story", 
				"economy" => "money and prices", 
				_ => (text.StartsWith("items") || text == "item hash") ? "items" : text, 
			};
			if (!list.Contains(item))
			{
				list.Add(item);
			}
		}
		if (list.Count != 0)
		{
			return string.Join(", ", list);
		}
		return "the shop";
	}

	internal static void OnResyncRequest(Server.Peer p, string reason)
	{
		reason = Text.Clip(reason, 200);
		if (p.StuckLoads >= 3 && p.StuckAreas != null && reason.StartsWith("digest mismatch: ", StringComparison.Ordinal) && Covered(Digest.AreasOf(reason.Substring("digest mismatch: ".Length)), p.StuckAreas))
		{
			p.StuckIgnored++;
			if (!p.StuckTold)
			{
				p.StuckTold = true;
				Log.Info("server: " + p.Info.Name + "'s shop differed in " + p.StuckAreas + " right after " + p.StuckLoads + " loads in a row; not reloading it for that any more (likely another mod or another game build on their side; the server's numbers count)");
				Net.ToPlayer(p.Id, "chat.notice", new MsgChatNotice
				{
					text = "Your game keeps disagreeing with the server (" + AreaWords(p.StuckAreas) + "), so the shop will stop reloading for that. The server's numbers count. This usually comes from another mod, or the game's beta, on your side."
				});
			}
			return;
		}
		long num = 10000 - (Clock.Ms - p.LastSnapshotMs);
		if (num > 0)
		{
			Log.Info("server: " + p.Info.Name + " asked for a resync (" + reason + "); last one was " + (Clock.Ms - p.LastSnapshotMs) + " ms ago, sending in " + num + " ms");
			p.ResyncAtMs = Clock.Ms + num;
			p.ResyncReason = reason;
		}
		else
		{
			SendTo(p.Id, "resync: " + reason);
		}
	}

	private static bool Covered(string asked, string stuck)
	{
		if (asked.Length == 0)
		{
			return false;
		}
		HashSet<string> hashSet = new HashSet<string>(stuck.Split(new string[1] { ", " }, StringSplitOptions.RemoveEmptyEntries));
		string[] array = asked.Split(new string[1] { ", " }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string item in array)
		{
			if (!hashSet.Contains(item))
			{
				return false;
			}
		}
		return true;
	}

	internal static void ServerTick()
	{
		DayLog.CapTick();
		long ms = Clock.Ms;
		if (ms - lastResyncSweepMs < 500)
		{
			return;
		}
		lastResyncSweepMs = ms;
		foreach (Server.Peer item in new List<Server.Peer>(Server.ByPlayer.Values))
		{
			if (item.ResyncAtMs > 0 && ms >= item.ResyncAtMs)
			{
				SendTo(item.Id, "resync: " + item.ResyncReason);
			}
		}
	}

	public static void RequestResync(string why = null)
	{
		Ask(string.IsNullOrEmpty(why) ? "asked by code" : Text.Clip(why, 180));
	}

	internal static void Ask(string reason)
	{
		if (Net.IsClient && Client.State == ClientState.Ready)
		{
			if (askedMs > 0 && Clock.Ms - askedMs < AskWaitMs)
			{
				Log.Info("client: resync already asked " + (Clock.Ms - askedMs) + " ms ago; not asking again (" + reason + ")");
			}
			else
			{
				// Asked again within two minutes of a load: the reload did not cure it; wait longer next time.
				quickAsks = (loadedMs > 0 && Clock.Ms - loadedMs < 120000) ? (quickAsks + 1) : 0;
				askedMs = Clock.Ms;
				AskCount++;
				Log.Info("client: asking for a resync (" + reason + ")");
				Client.Send(Wire.Encode("sys.resync", new MsgResync
				{
					reason = reason,
					report = DesyncReport.Build(reason)
				}));
				askTable = ItemTable();
				askReason = reason;
				askDay = Game.Station?.dayCounter ?? 0;
			}
		}
	}

	internal static Dictionary<long, string> ItemTable()
	{
		try
		{
			if (!Items.Ready)
			{
				return null;
			}
			Dictionary<long, string> dictionary = new Dictionary<long, string>();
			foreach (GameItem item in Items.All())
			{
				if (item != null && !Items.IsGone(item))
				{
					long num = Ids.Of(item);
					if (num != 0L && !dictionary.ContainsKey(num))
					{
						GridShape modifiedShape = item.modifiedShape;
						dictionary[num] = (Items.RefOf((GraphNodeStorage)(object)item.parentInventory)?.ToString() ?? "-") + "@" + ((modifiedShape != null) ? (modifiedShape.minX + "," + modifiedShape.minY + ((modifiedShape.orientation != 0) ? (" r" + modifiedShape.orientation) : "") + (modifiedShape.flipped ? " f" : "")) : "?") + ((item.unitCount != 1) ? (" x" + item.unitCount) : "") + "|" + item.identifier;
					}
				}
			}
			return dictionary;
		}
		catch (Exception ex)
		{
			Log.Warn("client: item table for the reload report failed: " + ex.Message);
			return null;
		}
	}

	internal static string TableDiff(Dictionary<long, string> before, Dictionary<long, string> after, out int changed)
	{
		changed = 0;
		List<string> list = new List<string>();
		foreach (KeyValuePair<long, string> item in before.OrderBy((KeyValuePair<long, string> k) => k.Key))
		{
			after.TryGetValue(item.Key, out var value);
			if (!(value == item.Value))
			{
				changed++;
				if (list.Count < 6)
				{
					list.Add(Name(item.Value) + "#" + item.Key + " " + Place(item.Value) + " > " + ((value != null) ? Place(value) : "gone"));
				}
			}
		}
		foreach (KeyValuePair<long, string> item2 in after.OrderBy((KeyValuePair<long, string> k) => k.Key))
		{
			if (!before.ContainsKey(item2.Key))
			{
				changed++;
				if (list.Count < 6)
				{
					list.Add(Name(item2.Value) + "#" + item2.Key + " new > " + Place(item2.Value));
				}
			}
		}
		if (changed > list.Count)
		{
			list.Add("+" + (changed - list.Count) + " more");
		}
		return string.Join("; ", list);
	}

	private static string Name(string entry)
	{
		int num = entry.LastIndexOf('|');
		if (num < 0)
		{
			return "?";
		}
		return entry.Substring(num + 1);
	}

	private static string Place(string entry)
	{
		int num = entry.LastIndexOf('|');
		if (num < 0)
		{
			return entry;
		}
		return entry.Substring(0, num);
	}

	private static void ReportReload(MsgSnapshot m)
	{
		Dictionary<long, string> dictionary = askTable;
		string text = askReason;
		askTable = null;
		askReason = null;
		if (dictionary == null || m == null || m.day != askDay)
		{
			return;
		}
		Dictionary<long, string> dictionary2 = ItemTable();
		if (dictionary2 == null)
		{
			return;
		}
		int changed;
		string text2 = TableDiff(dictionary, dictionary2, out changed);
		LastReloadReport = "(" + text + ") " + changed + ": " + text2;
		Log.Info("client: the reload (" + text + ") moved " + changed + " item(s) here" + ((changed > 0) ? (": " + text2) : ""));
		try
		{
			Client.Send(Wire.Encode("sys.resyncnote", new MsgResyncNote
			{
				reason = Text.Clip(text, 120),
				changed = changed,
				items = Text.Clip(text2, 400)
			}));
		}
		catch (Exception ex)
		{
			Log.Warn("client: reload report not sent: " + ex.Message);
		}
	}

	internal static void OnSnapshot(MsgSnapshot m)
	{
		ProbablyTogether.UI.WindowMemory.BeforeLoad();
		Systems.Outings.OnSnapshot();
		Systems.Standing.NewScene();
		Systems.TradeVisits.NewScene();
		if (m == null || (m.save == null && m.saveB64 == null))
		{
			Client.Disconnect("the server sent an empty snapshot");
			return;
		}
		// Not reset here: the wait counts from the last ask.
		held.Clear();
		byte[] bytes;
		try
		{
			bytes = ((m.save != null) ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(m.save) : Convert.FromBase64String(m.saveB64));
		}
		catch (Exception)
		{
			Client.Disconnect("the server sent a save this game will not load");
			return;
		}
		string text;
		int types;
		try
		{
			text = SaveCheck.TypesProblem(SaveCheck.AsEs3Reads(bytes), out types);
		}
		catch (Exception ex2)
		{
			text = "unreadable (" + ex2.GetType().Name + ")";
			types = 0;
		}
		if (text != null)
		{
			Log.Warn("client: refused the server's snapshot #" + m.seq + ": " + text);
			Client.Disconnect("the server sent a save this game will not load");
			return;
		}
		try
		{
			string text2 = Paths.Under("probably_together/session.es3");
			System.IO.Directory.CreateDirectory(Path.GetDirectoryName(text2));
			File.WriteAllBytes(text2 + ".tmp", bytes);
			if (File.Exists(text2))
			{
				File.Delete(text2);
			}
			File.Move(text2 + ".tmp", text2);
		}
		catch (Exception ex3)
		{
			Log.Error("client: could not write the session file: " + ex3.Message);
			Client.Disconnect("could not write the shop file");
			return;
		}
		loading = m;
		replayCount = 0;
		Locks.ClientReset(m.locks);
		Digest.ResetClient();
		Client.State = ClientState.Loading;
		loadStartMs = Clock.Ms;
		shopFrames = 0;
		logWaitMs = 0L;
		sceneRequested = false;
		Log.Info("client: morning snapshot #" + m.seq + " (" + m.reason + ") received: day " + m.day + ", " + m.items + " items, save types checked (" + types + "); loading the shop");
	}

	internal static void OnReplayHeader(MsgReplay r)
	{
		if (r != null && Client.State == ClientState.Loading)
		{
			replayCount = Math.Min(Math.Max(0, r.count), 200000);
			Locks.ClientReset(r.locks);
		}
	}

	internal static void Hold(string kind, JToken d)
	{
		if (held.Count > replayCount + 20000)
		{
			Client.Disconnect("too many messages while loading");
		}
		else
		{
			held.Add(new KeyValuePair<string, JToken>(kind, d));
		}
	}

	internal static void ClientTick()
	{
		if (menuReturnPending && !Net.IsClient)
		{
			if (Game.ShopLoaded && Game.Store.saveSlotId == 990077)
			{
				menuReturnPending = false;
				Game.ToMainMenu("disconnected from the server");
			}
			else if (Boot.Scene == "EmporiumMenu")
			{
				menuReturnPending = false;
			}
		}
		if (loading == null || Client.State != ClientState.Loading)
		{
			return;
		}
		if (!sceneRequested)
		{
			NewGameData instance = NewGameData.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				if (Clock.Ms - loadStartMs > 30000)
				{
					Fail("the game is not ready to load (no NewGameData)");
				}
				return;
			}
			sceneRequested = true;
			instance.saveSlot = 990077;
			instance.saveFile = "probably_together/session.es3";
			instance.isLoadFromMainMenu = false;
			if (Boot.Scene == "InventoryScene")
			{
				try
				{
					PlayerStore store = Game.Store;
					if (store != null)
					{
						store.Reset();
					}
				}
				catch
				{
				}
				Game.ResetShopStatics();
			}
			EmporiumEntry.sceneLoadStartTime = Time.realtimeSinceStartup;
			SceneManager.LoadScene("InventoryScene", (LoadSceneMode)0);
		}
		else if (Game.ShopLoaded && Game.Store.saveSlotId == 990077)
		{
			if (++shopFrames < 3)
			{
				return;
			}
			if (held.Count >= replayCount)
			{
				Finish();
				return;
			}
			long ms = Clock.Ms;
			if (logWaitMs == 0L)
			{
				logWaitMs = ms;
				Log.Info("client: shop loaded; waiting for today's log (" + held.Count + "/" + replayCount + " message(s) here)");
			}
			else if (ms - logWaitMs > 120000)
			{
				Log.Warn("client: today's log did not all arrive in " + 120L + " s (" + held.Count + "/" + replayCount + "); going on with what is here");
				Finish();
			}
		}
		else if (Clock.Ms - loadStartMs > 180000)
		{
			Fail("loading the shop took over 3 minutes");
		}
	}

	private static void Fail(string why)
	{
		Log.Error("client: " + why);
		Client.Disconnect(why);
	}

	private static void Finish()
	{
		MsgSnapshot msgSnapshot = loading;
		loading = null;
		long num = ((logWaitMs > 0) ? (Clock.Ms - logWaitMs) : 0);
		logWaitMs = 0L;
		PlayerStore store = Game.Store;
		if (store != null && msgSnapshot.uidCounter > 0)
		{
			store.currentUniqueId = msgSnapshot.uidCounter;
		}
		Ids.AfterLoad();
		Client.State = ClientState.Ready;
		long num2 = Clock.Ms - loadStartMs;
		List<KeyValuePair<string, JToken>> queue = new List<KeyValuePair<string, JToken>>(held);
		held.Clear();
		Systems.Bays.OpenAll();
		DayLog.ReplayStats replayStats = DayLog.ReplayHeld(queue, replayCount);
		if (Client.State != ClientState.Ready)
		{
			return;
		}
		Security.AfterLoad();
		ProbablyTogether.UI.WindowMemory.AfterLoad();
		Loads++;
		loadedMs = Clock.Ms;
		LastReplay = replayStats;
		MsgDigest msgDigest = Digest.Compute();
		Log.Info("client: morning snapshot #" + msgSnapshot.seq + " loaded in " + num2 + " ms" + ((num > 0) ? (" (" + num + " ms of it waiting for the log)") : "") + ", then " + replayStats?.ToString() + ": " + msgDigest.Describe() + "; " + Ids.Count + " ids indexed");
		Dictionary<string, string> econ = null;
		try
		{
			econ = Economy.Parts();
		}
		catch (Exception ex)
		{
			Log.Warn("client: economy areas not sent: " + ex.Message);
		}
		Client.Send(Wire.Encode("sys.ready", new MsgReady
		{
			seq = msgSnapshot.seq,
			digest = msgDigest,
			econ = econ
		}));
		ReportReload(msgSnapshot);
		try
		{
			Snapshot.Loaded?.Invoke();
		}
		catch (Exception ex2)
		{
			Log.Error("Snapshot.Loaded handler: " + ex2);
		}
	}

	internal static void ClientAbort()
	{
		loading = null;
		logWaitMs = 0L;
		held.Clear();
		replayCount = 0;
		askedMs = 0L;
		quickAsks = 0;
		loadedMs = 0L;
		askTable = null;
		askReason = null;
		DayLog.ClientAbort();
		menuReturnPending = true;
	}
}
