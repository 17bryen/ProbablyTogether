using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

internal static class ReplayLab
{
	public sealed class Seen
	{
		public int n;

		public bool replayed;

		public bool ok;

		public string detail;
	}

	public const string KTest = "replay.bot.test";

	public const string KCue = "replay.bot.cue";

	public static readonly List<Seen> seen = new List<Seen>();

	public static int loads;

	public static bool askAgain;

	public static string askAgainResult = "";

	private static bool lastWasReplay;

	private static int lastReplayIndex = -1;

	public static int sent;

	public static int left;

	public static readonly Dictionary<string, int> cues = new Dictionary<string, int>();

	public static string lastServer = "";

	private static bool registered;

	internal static void Register()
	{
		if (registered)
		{
			return;
		}
		registered = true;
		Net.On<ReplayTestMsg>("replay.bot.test", null, OnTest);
		Net.On<ReplayCueMsg>("replay.bot.cue", OnCue, null);
		Snapshot.Loaded += delegate
		{
			loads++;
		};
		Net.PlayerLeft += delegate
		{
			if (Net.IsServer)
			{
				left++;
			}
		};
	}

	private static ReplayRolls Run()
	{
		PlayerStore store = Game.Store;
		EmporiumEntry instance = EmporiumEntry.Instance;
		ReplayRolls replayRolls = new ReplayRolls();
		replayRolls.a = RNG.GetRandomInt(1, 1000);
		replayRolls.b = Random.Range(0, 1000000);
		replayRolls.c = (RNG.Roll(50) ? 1 : 0);
		replayRolls.d = (int)RNG.GetRandomDouble(0.0, 1000000.0);
		StoreClientManager storeClientManager = store.storeClientManager;
		if (storeClientManager != null)
		{
			replayRolls.faction = storeClientManager.ComputeClientType() ?? "";
			replayRolls.pick = storeClientManager.PickRandomValidClient(replayRolls.faction)?.identifier ?? "";
		}
		GameItem val = DirectoryMaster.Item("newspaper", true);
		replayRolls.placed = GraphUtils.TryAcceptAll((GraphNodeStorage)(object)instance.invElement, val, -1);
		replayRolls.uid = val.uniqueId;
		store.playerCash += 1 + replayRolls.a % 10;
		replayRolls.cash = store.playerCash;
		return replayRolls;
	}

	public static string Send(int except = -1)
	{
		ReplayTestMsg replayTestMsg = new ReplayTestMsg
		{
			n = ++sent
		};
		using (Replay.Server(out replayTestMsg.seed))
		{
			replayTestMsg.r = Run();
		}
		if (except >= 0)
		{
			Net.ToAllExcept(except, "replay.bot.test", replayTestMsg);
		}
		else
		{
			Net.ToAll("replay.bot.test", replayTestMsg);
		}
		lastServer = "#" + replayTestMsg.n + " " + replayTestMsg.r?.ToString() + " digest " + replayTestMsg.seed.After.ToString("x16") + " uidAfter " + replayTestMsg.seed.UidAfter;
		Log.Info("replay-bot: server test " + lastServer + "; day log " + DayLog.Count);
		return lastServer;
	}

	private static void OnTest(ReplayTestMsg m)
	{
		if (DayLog.Replaying)
		{
			if (!lastWasReplay || DayLog.ReplayIndex <= lastReplayIndex)
			{
				seen.Clear();
			}
			lastWasReplay = true;
			lastReplayIndex = DayLog.ReplayIndex;
		}
		else
		{
			lastWasReplay = false;
		}
		if (!Game.ShopLoaded)
		{
			seen.Add(new Seen
			{
				n = m.n,
				replayed = DayLog.Replaying,
				ok = false,
				detail = "no shop"
			});
			return;
		}
		int checksBad = Replay.ChecksBad;
		ReplayRolls replayRolls;
		using (Replay.Client(m.seed))
		{
			replayRolls = Run();
		}
		ReplayRolls replayRolls2 = m.r ?? new ReplayRolls();
		bool flag = replayRolls2.a == replayRolls.a && replayRolls2.b == replayRolls.b && replayRolls2.c == replayRolls.c && replayRolls2.d == replayRolls.d && replayRolls2.faction == replayRolls.faction && replayRolls2.pick == replayRolls.pick && replayRolls2.uid == replayRolls.uid && replayRolls2.placed == replayRolls.placed;
		bool flag2 = Replay.ChecksBad == checksBad;
		if (!flag2 && askAgain && !DayLog.Replaying)
		{
			askAgain = false;
			Snapshot.RequestResync();
			askAgainResult = "second ask in the same frame, client state " + Net.ClientState;
		}
		string text = (flag ? replayRolls.ToString() : ("server " + replayRolls2?.ToString() + " / here " + replayRolls));
		seen.Add(new Seen
		{
			n = m.n,
			replayed = DayLog.Replaying,
			ok = flag,
			detail = text + (flag2 ? "" : ("; replay check: " + Replay.LastMismatch))
		});
		Log.Info("replay-bot: test #" + m.n + (DayLog.Replaying ? (" (replayed, log #" + (DayLog.ReplayIndex + 1) + ")") : " (live)") + ": " + (flag ? "same rolls as the server: " : "DIFFERENT rolls: ") + text + "; replay check " + (flag2 ? "ok" : "DIFFERS"));
	}

	private static void OnCue(ReplayCueMsg m, int player)
	{
		string text = (((m?.stage ?? "").Length > 32) ? m.stage.Substring(0, 32) : (m?.stage ?? ""));
		cues.TryGetValue(text, out var value);
		cues[text] = value + 1;
		Log.Info("replay-bot: cue '" + text + "' from " + Net.PlayerName(player));
	}

	public static void Cue(string stage)
	{
		Net.ToServer("replay.bot.cue", new ReplayCueMsg
		{
			stage = stage
		});
	}

	public static int CueCount(string stage)
	{
		if (!cues.TryGetValue(stage, out var value))
		{
			return 0;
		}
		return value;
	}

	public static Server.Peer PeerIn(Server.PeerState s)
	{
		return Server.ByPlayer.Values.FirstOrDefault((Server.Peer p) => p.State == s);
	}

	public static int PeersIn(Server.PeerState s)
	{
		return Server.ByPlayer.Values.Count((Server.Peer p) => p.State == s);
	}

	public static string SeenText()
	{
		if (seen.Count != 0)
		{
			return string.Join(", ", seen.Select((Seen x) => "#" + x.n + (x.replayed ? "r" : "l") + (x.ok ? "" : "!")));
		}
		return "none";
	}

	public static Seen Find(int n)
	{
		return seen.LastOrDefault((Seen x) => x.n == n);
	}

	public static string Hex(long v)
	{
		return v.ToString("x16");
	}
}
