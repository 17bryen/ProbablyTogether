using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

public static class DayNightRun
{
	public const string KRunEnded = "dayNight.runEnded";

	public const string KNewRun = "dayNight.newRun";

	public const string KNewRunStarting = "dayNight.newRunStarting";

	public const string KNewRunVotes = "dayNight.newRunVotes";

	private static readonly List<int> votes = new List<int>();

	private static readonly List<int> voteWaiting = new List<int>();

	private static long voteDeadlineMs;

	private static bool pendingVote;

	public static string LastNewRunWhy = "";

	private static bool pendingFinal;

	private static bool endedAtNight;

	private static readonly List<string> pendingContinue = new List<string>();

	private static bool loggedRun;

	private static bool lastShopReady;

	private static int startingBy;

	private static bool startingSeen;

	private static string shownKey = "";

	private static bool newRunExpected;

	public static int ShownCount;

	public static int DroppedLocal;

	public const string NewRunFailedWhy = "The server could not start a new run. The server owner can check the server's settings and log.";

	public const string EndedLine = "The run has ended. A new run starts when everyone here presses Start a new run, or when the server owner starts one.";

	public const string GatedWhy = "The run has ended. Everyone can vote to start a new run.";

	public static bool RunOver { get; private set; }

	public static string Ending { get; private set; } = "";

	public static bool Endless => Game.Store?.endlessMode ?? false;

	public static IReadOnlyList<int> NewRunVotes => votes;

	public static int NewRunNeeded
	{
		get
		{
			if (Net.IsServer)
			{
				return DayNight.LivePlayers().Count;
			}
			int num = 0;
			foreach (int vote in votes)
			{
				if (Net.Players.ContainsKey(vote))
				{
					num++;
				}
			}
			foreach (int item in voteWaiting)
			{
				if (Net.Players.ContainsKey(item))
				{
					num++;
				}
			}
			if (num <= 0)
			{
				return Net.Players.Count;
			}
			return num;
		}
	}

	public static int NewRunSecondsLeft
	{
		get
		{
			if (voteDeadlineMs > 0)
			{
				return (int)Math.Max(0L, (voteDeadlineMs - Clock.Ms + 999) / 1000);
			}
			return -1;
		}
	}

	public static IReadOnlyList<int> NewRunWaiting => voteWaiting;

	public static bool LocalVoted
	{
		get
		{
			if (Net.IsClient)
			{
				if (!pendingVote)
				{
					return votes.Contains(Net.LocalPlayer);
				}
				return true;
			}
			return false;
		}
	}

	private static bool NewRunLoading => ServerMode.NewRunLoading;

	internal static bool BlockSaves
	{
		get
		{
			if (Net.IsServer && RunOver && !ServerMode.NewRunLoading)
			{
				return Server.ShopReady;
			}
			return false;
		}
	}

	public static string LastShown => shownKey;

	public static event Action<string, bool> RunEnded;

	public static event Action<int> NewRunStarting;

	public static event Action NewRunVotesChanged;

	internal static void Register()
	{
		Net.On<MsgDnRunEnded>("dayNight.runEnded", null, ClientRunEnded);
		Net.On<MsgDnVote>("dayNight.newRun", ServerVote, null);
		Net.On<MsgDnNewRun>("dayNight.newRunStarting", null, ClientNewRunStarting);
		Net.On<MsgDnVotes>("dayNight.newRunVotes", null, ClientVotes);
		Snapshot.Loaded += delegate
		{
			if (newRunExpected)
			{
				newRunExpected = false;
				RunOver = false;
				Ending = "";
				ClearVotesView();
			}
		};
		Net.AddServerGate(Gate);
		Snapshot.SaveBlocked = (Func<bool>)Delegate.Combine(Snapshot.SaveBlocked, (Func<bool>)(() => BlockSaves));
		ServerMode.NewRunStarting += OnNewRunStarting;
	}

	internal static void OnGameOverServer(string ending)
	{
		if (!Net.IsServer)
		{
			return;
		}
		if (string.IsNullOrEmpty(ending))
		{
			ending = "rent";
		}
		if (RunOver)
		{
			Log.Info("dayNight: another ending (" + ending + ") after the run ended (" + Ending + "): ignored");
		}
		else if (ending == "buyout" || ending == "buyout_mortgage")
		{
			try
			{
				if ((Object)(object)GameOverUIManager.Instance != (Object)null)
				{
					GameOverUIManager.Instance.ContinuePlayClick();
				}
			}
			catch (Exception ex)
			{
				Log.Warn("dayNight: hiding the server's ending panel: " + ex.Message);
			}
			pendingContinue.Add(ending);
			Log.Info("dayNight: ending " + ending + " on day " + Game.Day + ": the run continues");
		}
		else
		{
			RunOver = true;
			Ending = ending;
			pendingFinal = true;
			endedAtNight = DayNight.NightRunning;
			Log.Info("dayNight: the run ended on day " + Game.Day + " (" + ending + (endedAtNight ? ", during the night" : "") + "); no more actions or saves");
		}
	}

	internal static void AfterNight()
	{
	}

	internal static void ServerTick()
	{
		bool shopReady = Server.ShopReady;
		if (shopReady && !lastShopReady)
		{
			OnShopUp();
		}
		lastShopReady = shopReady;
		if (!loggedRun && Server.ShopReady && Game.ShopLoaded)
		{
			loggedRun = true;
			ServerConfig config = ServerMode.Config;
			Log.Info("dayNight: run on day " + Game.Day + ", endless " + Endless + ((config != null && config.endless != Endless) ? (" (server.json says endless " + config.endless + ": it applies to the next new run)") : ""));
		}
		if (DayNight.NightRunning)
		{
			return;
		}
		if (pendingContinue.Count > 0)
		{
			foreach (string item in pendingContinue)
			{
				Net.ToAll("dayNight.runEnded", new MsgDnRunEnded
				{
					ending = item,
					day = Game.Day,
					final = false,
					slotFile = ""
				});
				DayNight.Raise(DayNightRun.RunEnded, item, b: false);
			}
			pendingContinue.Clear();
		}
		if (pendingFinal)
		{
			pendingFinal = false;
			FinishEnding();
		}
		if (voteDeadlineMs > 0)
		{
			EvaluateVotes();
		}
	}

	private static void OnShopUp()
	{
		if (RunOver)
		{
			Log.Info("dayNight: a new run is up (day " + Game.Day + "); the ended run (" + Ending + ") is behind us");
		}
		RunOver = false;
		Ending = "";
		pendingFinal = false;
		pendingContinue.Clear();
		votes.Clear();
		voteDeadlineMs = 0L;
		loggedRun = false;
		DayNight.ResetForNewShop();
	}

	private static void FinishEnding()
	{
		string text = RenameSlot();
		if (endedAtNight && Snapshot.NewMorning("run ended"))
		{
			Snapshot.SendToAll("run ended");
		}
		foreach (KeyValuePair<string, int> item in Locks.Table())
		{
			Locks.Release(item.Key, -1);
		}
		DayNight.ClearReady("run ended");
		votes.Clear();
		voteDeadlineMs = 0L;
		MsgDnRunEnded msgDnRunEnded = new MsgDnRunEnded
		{
			ending = Ending,
			day = Game.Day,
			final = true,
			slotFile = text
		};
		Net.ToAll("dayNight.runEnded", msgDnRunEnded);
		ServerMode.SetRunEnded(Ending);
		Log.Info("dayNight: run over (" + Ending + ", day " + msgDnRunEnded.day + "); slot file " + ((text.Length > 0) ? ("renamed to " + text) : "not found") + "; a new run starts when every player votes for it, or from the panel");
		DayNight.Raise(DayNightRun.RunEnded, Ending, b: true);
		BroadcastVotes(-1, "run ended");
	}

	private static string RenameSlot()
	{
		int num = Game.Store?.saveSlotId ?? ServerMode.Config?.saveSlot ?? 1;
		if (num < 0 || num == 990077)
		{
			num = ServerMode.Config?.saveSlot ?? 1;
		}
		string text = SaveFiles.Slot(num);
		string text2 = Paths.Under(text);
		try
		{
			if (!File.Exists(text2))
			{
				Log.Warn("dayNight: no slot file to move aside at " + text2);
				return "";
			}
			string text3 = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
			string text4 = "ended_" + text3 + "_" + Path.GetFileName(text);
			File.Move(text2, Paths.Under(text4));
			ServerMode.EndedStamp = text3;
			return text4;
		}
		catch (Exception ex)
		{
			Log.Error("dayNight: could not move the ended run's slot file aside (" + text2 + "): " + ex.Message);
			return "";
		}
	}

	private static void ServerVote(MsgDnVote m, int from)
	{
		if (m == null)
		{
			return;
		}
		if (!RunOver)
		{
			DayNight.Refuse(from, "newRun", "The run is still going. A new run can be voted for after it ends.");
			return;
		}
		if (NewRunLoading)
		{
			DayNight.Refuse(from, "newRun", "A new run is already loading.");
			return;
		}
		if (!Net.IsPlayerLive(from))
		{
			DayNight.Refuse(from, "newRun", "Still loading: try again in a moment.");
			return;
		}
		if (m.vote)
		{
			if (!votes.Contains(from))
			{
				votes.Add(from);
				if (voteDeadlineMs <= 0)
				{
					voteDeadlineMs = Clock.Ms + (long)DayNight.ReadyTimeoutSeconds * 1000L;
				}
			}
		}
		else
		{
			if (!votes.Remove(from))
			{
				SendVotesTo(from, "resend");
				return;
			}
			if (votes.Count == 0)
			{
				voteDeadlineMs = 0L;
			}
		}
		BroadcastVotes(from, m.vote ? "vote" : "unvote");
		EvaluateVotes();
	}

	internal static MsgDnVotes VotesState(int by, string why)
	{
		List<int> source = DayNight.LivePlayers();
		return new MsgDnVotes
		{
			votes = votes.OrderBy((int x) => x).ToArray(),
			waiting = source.Where((int x) => !votes.Contains(x)).ToArray(),
			secondsLeft = NewRunSecondsLeft,
			by = by,
			why = why
		};
	}

	private static void BroadcastVotes(int by, string why)
	{
		MsgDnVotes msgDnVotes = VotesState(by, why);
		voteWaiting.Clear();
		voteWaiting.AddRange(msgDnVotes.waiting);
		Net.ToAll("dayNight.newRunVotes", msgDnVotes);
		Log.Info("dayNight: new-run votes " + Names(msgDnVotes.votes) + ", waiting for " + Names(msgDnVotes.waiting) + ((msgDnVotes.secondsLeft >= 0) ? (", new run in at most " + msgDnVotes.secondsLeft + " s") : "") + " (" + why + ((by > 0) ? (" by " + Net.PlayerName(by)) : "") + ")");
		RaiseVotes();
	}

	private static void SendVotesTo(int player, string why)
	{
		if (player > 0)
		{
			Net.ToPlayer(player, "dayNight.newRunVotes", VotesState(-1, why));
		}
	}

	private static string Names(int[] a)
	{
		if (a != null && a.Length != 0)
		{
			return string.Join(", ", a.Select(Net.PlayerName));
		}
		return "nobody";
	}

	private static void EvaluateVotes()
	{
		if (!Net.IsServer || !RunOver || NewRunLoading || votes.Count == 0)
		{
			return;
		}
		if (votes.RemoveAll((int id) => !Server.ByPlayer.ContainsKey(id)) > 0 && votes.Count == 0)
		{
			voteDeadlineMs = 0L;
			BroadcastVotes(-1, "left");
			return;
		}
		List<int> list = DayNight.LivePlayers();
		bool flag = list.Count > 0 && list.All(votes.Contains);
		bool flag2 = voteDeadlineMs > 0 && Clock.Ms >= voteDeadlineMs;
		if (flag || flag2)
		{
			string text = (flag ? ("everyone voted (" + votes.Count + ")") : ("vote timeout (" + votes.Count(list.Contains) + " of " + list.Count + " voted after " + DayNight.ReadyTimeoutSeconds + " s)"));
			Log.Info("dayNight: the new-run vote passed: " + text);
			BeginNewRun(0, text);
		}
	}

	internal static void OnPlayerLeft(int player)
	{
		if (RunOver && !NewRunLoading)
		{
			votes.Remove(player);
			if (votes.Count == 0)
			{
				voteDeadlineMs = 0L;
			}
			BroadcastVotes(-1, "left");
			EvaluateVotes();
		}
	}

	internal static void OnClientReady(int player)
	{
		if (RunOver && !NewRunLoading && !pendingFinal)
		{
			BroadcastVotes(-1, "joined");
		}
	}

	internal static void BeginNewRun(int by, string why)
	{
		if (!Net.IsServer || NewRunLoading)
		{
			return;
		}
		startingBy = by;
		startingSeen = false;
		try
		{
			ServerMode.StartNewRun(why);
		}
		finally
		{
			startingBy = 0;
		}
		if (!startingSeen)
		{
			Log.Warn("dayNight: the core did not start the new run (" + why + "); the vote starts over");
			int[] array = votes.ToArray();
			votes.Clear();
			voteDeadlineMs = 0L;
			int[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				DayNight.Refuse(array2[i], "newRun", "The server could not start a new run. The server owner can check the server's settings and log.");
			}
			if (RunOver)
			{
				BroadcastVotes(-1, "refused");
			}
		}
	}

	private static void OnNewRunStarting(string why)
	{
		if (Net.IsServer)
		{
			startingSeen = true;
			int num = startingBy;
			LastNewRunWhy = why ?? "";
			Log.Info("dayNight: a new run is loading (" + why + ")" + (RunOver ? ("; the ended run (" + Ending + ") is behind us") : "; the run was still going"));
			RunOver = false;
			Ending = "";
			pendingFinal = false;
			pendingContinue.Clear();
			votes.Clear();
			voteWaiting.Clear();
			voteDeadlineMs = 0L;
			Net.ToAll("dayNight.newRunStarting", new MsgDnNewRun
			{
				by = num,
				why = why
			});
			DayNight.ClearReady("new run");
			DayNightOuting.SetMapView(open: false);
			DayNight.Raise(DayNightRun.NewRunStarting, num);
			RaiseVotes();
		}
	}

	public static void RequestNewRun()
	{
		if (Net.IsClient)
		{
			pendingVote = true;
			DayNight.Send("dayNight.newRun", new MsgDnVote
			{
				vote = true
			});
			Log.Info("dayNight: voted for a new run");
			RaiseVotes();
		}
	}

	public static void CancelNewRunVote()
	{
		if (Net.IsClient && LocalVoted)
		{
			pendingVote = false;
			DayNight.Send("dayNight.newRun", new MsgDnVote
			{
				vote = false
			});
			Log.Info("dayNight: took the new-run vote back");
			RaiseVotes();
		}
	}

	internal static void ClearPendingVote()
	{
		if (pendingVote)
		{
			pendingVote = false;
			RaiseVotes();
		}
	}

	private static void ClientVotes(MsgDnVotes m)
	{
		ApplyVotesView(m);
	}

	private static void ApplyVotesView(MsgDnVotes m)
	{
		if (m != null)
		{
			votes.Clear();
			if (m.votes != null)
			{
				votes.AddRange(m.votes);
			}
			voteWaiting.Clear();
			if (m.waiting != null)
			{
				voteWaiting.AddRange(m.waiting);
			}
			voteDeadlineMs = ((m.secondsLeft >= 0 && !DayLog.Replaying) ? (Clock.Ms + (long)m.secondsLeft * 1000L) : 0);
			if (votes.Contains(Net.LocalPlayer) || m.by == Net.LocalPlayer)
			{
				pendingVote = false;
			}
			if (m.why == "vote" && m.by > 0 && m.by != Net.LocalPlayer)
			{
				DayNight.Notice(Net.PlayerName(m.by) + " wants to start a new run.");
			}
			RaiseVotes();
		}
	}

	private static void ClearVotesView()
	{
		votes.Clear();
		voteWaiting.Clear();
		voteDeadlineMs = 0L;
		pendingVote = false;
		RaiseVotes();
	}

	private static void RaiseVotes()
	{
		try
		{
			DayNightRun.NewRunVotesChanged?.Invoke();
		}
		catch (Exception ex)
		{
			Log.Error("DayNightRun.NewRunVotesChanged handler: " + ex);
		}
	}

	internal static void NoteShown(string ending)
	{
		shownKey = ending + "@" + Game.Day;
		ShownCount++;
	}

	internal static void NoteDropped(string ending)
	{
		DroppedLocal++;
	}

	private static void ClientRunEnded(MsgDnRunEnded m)
	{
		if (m != null)
		{
			if (m.final)
			{
				RunOver = true;
				Ending = m.ending ?? "";
			}
			DayNight.ClearBlackout();
			ShowEnding(m.ending, m.final, m.day);
			Log.Info("dayNight: ending " + m.ending + " on day " + m.day + (m.final ? ": the run is over" : ": the run continues") + (DayLog.Replaying ? " (replayed)" : ""));
			DayNight.Raise(DayNightRun.RunEnded, m.ending ?? "", m.final);
		}
	}

	private static void ShowEnding(string ending, bool final, int day)
	{
		GameOverUIManager instance = GameOverUIManager.Instance;
		PlayerStore store = Game.Store;
		if ((Object)(object)instance == (Object)null || store == null || string.IsNullOrEmpty(ending))
		{
			return;
		}
		bool isShowing = instance.IsShowing;
		string text = ending + "@" + day;
		bool num;
		if (!final)
		{
			if (DayLog.Replaying || !(shownKey != text))
			{
				goto IL_00ab;
			}
			num = !isShowing;
		}
		else
		{
			if (!isShowing)
			{
				goto IL_006e;
			}
			num = shownKey != text;
		}
		if (num)
		{
			goto IL_006e;
		}
		goto IL_00ab;
		IL_00ab:
		if (final)
		{
			AddEndedLine(instance);
		}
		return;
		IL_006e:
		using (Net.Apply())
		{
			try
			{
				store.ExecuteGameOver(ending);
			}
			catch (Exception ex)
			{
				Log.Error("dayNight: showing the ending threw: " + ex);
			}
		}
		goto IL_00ab;
	}

	private static void AddEndedLine(GameOverUIManager g)
	{
		try
		{
			if ((Object)(object)g.continuePlayButton != (Object)null)
			{
				g.continuePlayButton.SetActive(false);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("dayNight: ending line: " + ex.Message);
		}
	}

	internal static void ClientTick()
	{
		if (newRunExpected && Snapshot.IsLoading)
		{
			newRunExpected = false;
			RunOver = false;
			Ending = "";
			ClearVotesView();
			Log.Info("dayNight: the new run's morning is loading");
		}
	}

	private static void ClientNewRunStarting(MsgDnNewRun m)
	{
		LastNewRunWhy = m?.why ?? "";
		if (!DayLog.Replaying)
		{
			newRunExpected = true;
			Log.Info("dayNight: the server is starting a new run (" + m?.why + ")");
			DayNight.Notice("The server is starting a new run...");
			DayNight.Raise(DayNightRun.NewRunStarting, m?.by ?? 0);
		}
	}

	internal static void ApplyState(MsgDnState m)
	{
		RunOver = m.runOver;
		Ending = m.ending ?? "";
		if (m.runOver)
		{
			ShowEnding(Ending, final: true, Game.Day);
			ApplyVotesView(m.votes);
		}
		else
		{
			ClearVotesView();
		}
	}

	internal static void ResetClient()
	{
		RunOver = false;
		Ending = "";
		shownKey = "";
		newRunExpected = false;
		votes.Clear();
		voteWaiting.Clear();
		voteDeadlineMs = 0L;
		pendingVote = false;
	}

	internal static string Gate(int player, string kind)
	{
		if (!RunOver || kind == null || Allowed(kind))
		{
			return null;
		}
		DayNight.NoteRefused(kind);
		return "The run has ended. Everyone can vote to start a new run.";
	}

	internal static bool Allowed(string kind)
	{
		if (!kind.StartsWith("dayNight.", StringComparison.Ordinal) && !kind.StartsWith("chat.", StringComparison.Ordinal) && !kind.StartsWith("voice.", StringComparison.Ordinal) && !kind.StartsWith("presence.", StringComparison.Ordinal) && !kind.StartsWith("bot.", StringComparison.Ordinal))
		{
			return kind.StartsWith("selftest.", StringComparison.Ordinal);
		}
		return true;
	}
}
