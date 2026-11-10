using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ProbablyTogether;

public static class DayLog
{
	public sealed class ReplayStats
	{
		public int Expected;

		public int Replayed;

		public int Live;

		public long Ms;

		public int ChecksOk;

		public int ChecksBad;

		public string FirstBad = "";

		public override string ToString()
		{
			return Replayed + "/" + Expected + " log message(s) replayed and " + Live + " live one(s) applied in " + Ms + " ms; seeded checks " + ChecksOk + " ok, " + ChecksBad + " differ" + ((ChecksBad > 0) ? (" (first: " + FirstBad + ")") : "");
		}
	}

	private static readonly List<byte[]> frames = new List<byte[]>();

	private static long bytes;

	private static int seq;

	private static StreamWriter file;

	private static bool warnedBig;

	private const long WarnBytes = 67108864L;

	internal const int MaxFrames = 200000;

	internal const long MaxBytes = 134217728L;

	private static bool fileOff;

	private static long capRetryMs;

	private static ReplayStats current;

	private static int capFrames;

	internal static int CapSnapshots;

	public static bool Replaying { get; internal set; }

	public static int ReplayIndex { get; private set; } = -1;

	public static string ReplayKind { get; private set; } = "";

	public static int Count => frames.Count;

	public static int Day { get; private set; }

	public static int MorningSnapshot { get; private set; }

	public static ReplayStats Last { get; private set; }

	private static int CapFrames
	{
		get
		{
			if (capFrames > 0)
			{
				return capFrames;
			}
			capFrames = 200000;
			string text = Args.Get("--br-daylog-cap");
			if (text != null && int.TryParse(text, out var result) && result > 0 && result < 200000 && (Args.Data ?? "").IndexOf("SSLabs", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				capFrames = result;
				Log.Warn("lab: today's log is capped at " + result + " messages (--br-daylog-cap)");
			}
			return capFrames;
		}
	}

	internal static bool CapHit { get; private set; }

	internal static long Bytes => bytes;

	internal static IReadOnlyList<byte[]> Frames => frames;

	internal static int CurrentChecksBad => current?.ChecksBad ?? 0;

	internal static int Record(string kind, string json, byte[] frame, int except)
	{
		Append(json, frame);
		if (except >= 0 && Server.ByPlayer.TryGetValue(except, out var value) && value.State == Server.PeerState.Syncing)
		{
			return -1;
		}
		return except;
	}

	private static void Append(string json, byte[] frame)
	{
		frames.Add(frame);
		bytes += frame.Length;
		seq++;
		if (!warnedBig && bytes > 67108864)
		{
			warnedBig = true;
			Log.Warn("server: today's log is over " + 64L + " MB (" + frames.Count + " messages); joins replay all of it");
		}
		if (!CapHit && (frames.Count >= CapFrames || bytes >= 134217728))
		{
			CapHit = true;
			fileOff = true;
			CloseFile();
			Log.Error("server: today's log reached its cap (" + frames.Count + " messages, " + (bytes >> 20) + " MB; the cap is " + CapFrames + " or " + 128L + " MB): daylog.jsonl is off until the next morning, and everyone gets a fresh snapshot now instead of a longer log");
		}
		try
		{
			if (file != null)
			{
				file.Write("{\"seq\":" + seq + ",\"t\":\"" + DateTime.UtcNow.ToString("HH:mm:ss.fff") + "\",\"m\":");
				file.Write(json);
				file.Write("}\n");
				file.Flush();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("daylog.jsonl write failed, file logging off: " + ex.Message);
			CloseFile();
		}
	}

	internal static void CapTick()
	{
		if (!CapHit || Net.Applying || !Server.ShopReady)
		{
			return;
		}
		long ms = Clock.Ms;
		if (ms >= capRetryMs)
		{
			capRetryMs = ms + 10000;
			int count = frames.Count;
			if (!Snapshot.NewMorning("day log full", capRestart: true))
			{
				Log.Error("server: the fresh snapshot for the full day log failed; trying again in 10 s");
				return;
			}
			CapSnapshots++;
			Log.Info("server: fresh snapshot instead of a day log of " + count + " message(s); sending it to " + Server.PlayerCount + " player(s)");
			Snapshot.SendToAll("day log full");
		}
	}

	internal static void Restart(int day, int snapshotSeq, bool capRestart = false)
	{
		int count = frames.Count;
		frames.Clear();
		bytes = 0L;
		seq = 0;
		warnedBig = false;
		CapHit = false;
		if (!capRestart)
		{
			fileOff = false;
		}
		Day = day;
		MorningSnapshot = snapshotSeq;
		CloseFile();
		if (fileOff)
		{
			Log.Info("server: day log restarted (fresh snapshot, day " + day + "); daylog.jsonl stays off until the next morning");
			return;
		}
		try
		{
			string text = Paths.Under("daylog.jsonl");
			if (File.Exists(text))
			{
				string text2 = Paths.Under("daylog-prev.jsonl");
				if (File.Exists(text2))
				{
					File.Delete(text2);
				}
				File.Move(text, text2);
			}
			file = new StreamWriter(text, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			file.Write("{\"morning\":{\"day\":" + day + ",\"snapshot\":" + snapshotSeq + ",\"t\":\"" + DateTime.UtcNow.ToString("o") + "\"}}\n");
			file.Flush();
		}
		catch (Exception ex)
		{
			Log.Warn("daylog.jsonl not writable: " + ex.Message);
			CloseFile();
		}
		if (count > 0)
		{
			Log.Info("server: day log restarted (morning of day " + day + "); yesterday's had " + count + " message(s)");
		}
	}

	internal static void CloseFile()
	{
		try
		{
			file?.Dispose();
		}
		catch
		{
		}
		file = null;
	}

	internal static ReplayStats ReplayHeld(List<KeyValuePair<string, JToken>> queue, int replayCount)
	{
		ReplayStats replayStats = (current = new ReplayStats
		{
			Expected = replayCount
		});
		long ms = Clock.Ms;
		try
		{
			for (int i = 0; i < queue.Count; i++)
			{
				if (Client.State != ClientState.Ready)
				{
					break;
				}
				bool flag = (Replaying = i < replayCount);
				ReplayIndex = (flag ? i : (-1));
				ReplayKind = (flag ? queue[i].Key : "");
				try
				{
					Dispatch.OnClientFrame(queue[i].Key, queue[i].Value);
				}
				finally
				{
					Replaying = false;
					ReplayIndex = -1;
					ReplayKind = "";
				}
				if (flag)
				{
					replayStats.Replayed++;
				}
				else
				{
					replayStats.Live++;
				}
			}
		}
		finally
		{
			Replaying = false;
			ReplayIndex = -1;
			ReplayKind = "";
			current = null;
			replayStats.Ms = Clock.Ms - ms;
			Last = replayStats;
		}
		if (replayStats.Replayed < replayStats.Expected && Client.State == ClientState.Ready)
		{
			Log.Warn("client: the server announced " + replayStats.Expected + " log message(s) but only " + replayStats.Replayed + " arrived before the load finished");
		}
		if (replayStats.ChecksBad > 0)
		{
			Log.Warn("client: day log replay: " + replayStats);
		}
		return replayStats;
	}

	internal static void NoteCheck(bool ok, string detail)
	{
		ReplayStats replayStats = current;
		if (replayStats == null)
		{
			return;
		}
		if (ok)
		{
			replayStats.ChecksOk++;
			return;
		}
		if (replayStats.ChecksBad == 0)
		{
			replayStats.FirstBad = detail ?? "";
		}
		replayStats.ChecksBad++;
	}

	internal static void ClientAbort()
	{
		Replaying = false;
		ReplayIndex = -1;
		ReplayKind = "";
		current = null;
	}
}
