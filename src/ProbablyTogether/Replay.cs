using System;
using System.Globalization;
using System.Security.Cryptography;
using UnityEngine;

namespace ProbablyTogether;

public static class Replay
{
	private sealed class Scope : IDisposable
	{
		private readonly IDisposable apply;

		private readonly System.Random oldSys;

		private readonly Seed seed;

		private readonly bool client;

		private readonly bool installed;

		private bool done;

		internal Scope(Seed s, bool isClient, int actor)
		{
			apply = Net.Apply(actor);
			depth++;
			seed = s;
			client = isClient;
			oldSys = RNG.random;
			if (s == null)
			{
				if (!warnedNull)
				{
					warnedNull = true;
					Log.Warn("Replay without a seed: plain apply, rolls will differ (warned once)");
				}
			}
			else
			{
				if (s.IsEmpty)
				{
					return;
				}
				if (client)
				{
					PlayerStore store = Game.Store;
					if (store != null && s.UidBase > 0)
					{
						store.currentUniqueId = s.UidBase;
					}
				}
				bool ok;
				int num = UnitySeed(s, out ok);
				if (ok)
				{
					UnityEngine.Random.InitState(num);
				}
				else if (!string.IsNullOrEmpty(s.RandomState))
				{
					Log.Warn("Replay: unknown RandomState '" + s.RandomState + "' (Random left alone)");
				}
				RNG.random = new System.Random(s.SysSeed);
				installed = true;
			}
		}

		public void Dispose()
		{
			if (done)
			{
				return;
			}
			done = true;
			try
			{
				if (!installed)
				{
					return;
				}
				RNG.random = oldSys;
				if (!client)
				{
					if (Net.IsServer && Game.ShopLoaded)
					{
						seed.UidAfter = Game.Store?.currentUniqueId ?? 0;
						seed.After = Digest();
					}
				}
				else
				{
					AfterClient(seed);
				}
			}
			catch (Exception ex)
			{
				Log.Error("Replay scope end: " + ex);
			}
			finally
			{
				depth--;
				apply.Dispose();
			}
		}
	}

	public static bool ResyncOnMismatch = true;

	private static readonly object gate = new object();

	private static System.Random seeds;

	private static int depth;

	private static long lastAskMs = -1000000L;

	private static bool warnedNull;

	internal const int AskEveryMs = 60000;

	public static bool InScope => depth > 0;

	public static int ChecksOk { get; private set; }

	public static int ChecksBad { get; private set; }

	public static int UidDrift { get; private set; }

	public static string LastMismatch { get; private set; } = "";

	public static IDisposable Server(out Seed seed)
	{
		return Server(out seed, Net.Actor);
	}

	public static IDisposable Server(out Seed seed, int actor)
	{
		int num;
		int sysSeed;
		lock (gate)
		{
			if (seeds == null)
			{
				seeds = new System.Random(PrivateSeed());
			}
			num = seeds.Next();
			sysSeed = seeds.Next();
		}
		PlayerStore store = Game.Store;
		seed = new Seed
		{
			RandomState = "i:" + num,
			SysSeed = sysSeed,
			UidBase = (store?.currentUniqueId ?? 0),
			Actor = ((actor < 0) ? (-1) : actor)
		};
		return new Scope(seed, isClient: false, seed.Actor);
	}

	public static IDisposable Client(Seed seed)
	{
		return new Scope(seed, isClient: true, (seed != null && seed.Actor >= 0) ? seed.Actor : Net.Actor);
	}

	public static IDisposable Client(Seed seed, int actor)
	{
		return new Scope(seed, isClient: true, actor);
	}

	public static long Digest()
	{
		MsgDigest msgDigest = ProbablyTogether.Digest.Compute();
		ulong h = 14695981039346656037uL;
		Mix(msgDigest.day);
		Mix(msgDigest.cash);
		Mix(msgDigest.favor);
		Mix(msgDigest.items);
		Mix(msgDigest.queue);
		Mix((long)(ulong.TryParse(msgDigest.hash, NumberStyles.HexNumber, null, out var result) ? result : 0));
		string text = msgDigest.state + "|" + msgDigest.client;
		foreach (char c in text)
		{
			h ^= c;
			h *= 1099511628211uL;
		}
		return (long)h;
		void Mix(long v)
		{
			for (int j = 0; j < 8; j++)
			{
				h ^= (byte)(v >> j * 8);
				h *= 1099511628211uL;
			}
		}
	}

	public static bool Check(long serverDigest, string what)
	{
		if (!Net.IsClient || serverDigest == 0L || !Game.ShopLoaded)
		{
			return true;
		}
		long num = Digest();
		if (num == serverDigest)
		{
			ChecksOk++;
			DayLog.NoteCheck(ok: true, null);
			return true;
		}
		Mismatch(what + ": digest server " + serverDigest.ToString("x16") + " / here " + num.ToString("x16"));
		return false;
	}

	private static int PrivateSeed()
	{
		try
		{
			byte[] array = new byte[4];
			using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
			{
				randomNumberGenerator.GetBytes(array);
			}
			return BitConverter.ToInt32(array, 0);
		}
		catch
		{
			return Environment.TickCount ^ (int)DateTime.UtcNow.Ticks;
		}
	}

	private static int UnitySeed(Seed s, out bool ok)
	{
		string text = s?.RandomState ?? "";
		ok = true;
		if (text.StartsWith("i:", StringComparison.Ordinal) && int.TryParse(text.Substring(2), out var result))
		{
			return result;
		}
		ok = false;
		return 0;
	}

	private static void AfterClient(Seed seed)
	{
		if (!Net.IsClient || (seed.After == 0L && seed.UidAfter == 0))
		{
			return;
		}
		PlayerStore store = Game.Store;
		if (store == null || !Game.ShopLoaded)
		{
			return;
		}
		string text = null;
		if (seed.UidAfter > 0 && store.currentUniqueId != seed.UidAfter)
		{
			text = "uid counter server " + seed.UidAfter + " / here " + store.currentUniqueId + " (base " + seed.UidBase + ")";
			store.currentUniqueId = seed.UidAfter;
		}
		long num = ((seed.After != 0L) ? Digest() : 0);
		if (seed.After != 0L && num != seed.After)
		{
			Mismatch("digest server " + seed.After.ToString("x16") + " / here " + num.ToString("x16") + ((text != null) ? ("; " + text) : ""));
			return;
		}
		ChecksOk++;
		DayLog.NoteCheck(ok: true, null);
		if (text != null)
		{
			UidDrift++;
			if (UidDrift <= 3 || UidDrift % 50 == 0)
			{
				Log.Warn("client: a seeded action used a different number of item ids than the server (" + text + "); no live item differs, counter set to the server's (" + UidDrift + " so far)");
			}
		}
	}

	private static void Mismatch(string detail)
	{
		ChecksBad++;
		LastMismatch = (DayLog.Replaying ? ("day log #" + (DayLog.ReplayIndex + 1) + " " + DayLog.ReplayKind) : "live") + ": " + detail;
		DayLog.NoteCheck(ok: false, LastMismatch);
		if (DayLog.Replaying)
		{
			if (DayLog.CurrentChecksBad <= 3)
			{
				Log.Warn("client: seeded action ended differently from the server (" + LastMismatch + ")");
			}
			return;
		}
		string text = "a seeded action ended differently from the server (" + LastMismatch + ")";
		if (!ResyncOnMismatch || Net.ClientState != ClientState.Ready)
		{
			Log.Warn("client: " + text);
			return;
		}
		if (Locks.LocalHoldsAny())
		{
			Log.Warn("client: " + text + "; resync waits: this player holds a lock (the 10 s digest retries)");
			return;
		}
		long ms = Clock.Ms;
		if (ms - lastAskMs < 60000)
		{
			Log.Warn("client: " + text + "; a resync was asked " + (ms - lastAskMs) / 1000 + " s ago");
		}
		else
		{
			lastAskMs = ms;
			Log.Warn("client: " + text + "; asking for a resync");
			Snapshot.Ask("replay check: " + LastMismatch);
		}
	}
}
