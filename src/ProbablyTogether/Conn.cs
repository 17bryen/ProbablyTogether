using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace ProbablyTogether;

internal sealed class Conn
{
	private enum Phase
	{
		Greeting,
		Status,
		PreJoin,
		Joined
	}

	private static class Watchdog
	{
		private static readonly ConcurrentDictionary<int, Conn> live = new ConcurrentDictionary<int, Conn>();

		private static bool started;

		private static readonly object startGate = new object();

		internal static IEnumerable<Conn> All => live.Values;

		internal static void Add(Conn c)
		{
			live[c.Id] = c;
			if (started)
			{
				return;
			}
			lock (startGate)
			{
				if (!started)
				{
					started = true;
					Background.Run("watchdog", Loop);
				}
			}
		}

		internal static void Remove(Conn c)
		{
			live.TryRemove(c.Id, out var _);
		}

		private static void Loop()
		{
			long num = Clock.Ms;
			while (true)
			{
				try
				{
					Thread.Sleep(200);
					long ms = Clock.Ms;
					foreach (Conn value in live.Values)
					{
						try
						{
							value.CheckDeadlines(ms);
						}
						catch
						{
						}
					}
					if (ms - num >= 60000)
					{
						num = ms;
						Summary();
						PruneIps(ms);
					}
				}
				catch
				{
				}
			}
		}
	}

	private sealed class IpState
	{
		public int Live;

		public int PreJoin;

		public double Tokens = 20.0;

		public long TokensMs = Clock.Ms;

		public int PwFails;

		public long PwWindowStartMs;

		public long BlockedUntilMs;

		public int BlockLevel;

		public long LastBadMs;

		public long LastLogMs;

		public int Suppressed;
	}

	internal static class Stats
	{
		internal static int Refused;

		internal static int Evicted;

		internal static int Blocks;

		internal static int Timeouts;

		internal static int Floods;

		internal static int BadFrames;

		internal static int Dropped;

		internal static int DroppedAfterAbuse;

		internal static int Backpressure;

		internal static int SendOverflow;

		internal static int StatusAnswered;

		internal static int LogsSuppressed;

		internal static string Text()
		{
			return "refused " + Refused + ", evicted " + Evicted + ", blocks " + Blocks + ", timeouts " + Timeouts + ", floods " + Floods + ", bad " + BadFrames + ", dropped " + Dropped + "+" + DroppedAfterAbuse + ", backpressure " + Backpressure + ", send overflow " + SendOverflow + ", status " + StatusAnswered + ", logs held back " + LogsSuppressed;
		}
	}

	private sealed class Bucket
	{
		private readonly double rate;

		private readonly double burst;

		private double tokens;

		private long lastMs;

		internal Bucket(double perSecond, double burst)
		{
			rate = perSecond;
			this.burst = burst;
			tokens = burst;
			lastMs = Clock.Ms;
		}

		private void Refill(long now)
		{
			tokens = Math.Min(burst, tokens + (double)(now - lastMs) * rate / 1000.0);
			lastMs = now;
		}

		internal double Available(long now)
		{
			Refill(now);
			return tokens;
		}

		internal bool Take(double n, long now)
		{
			Refill(now);
			if (tokens < n)
			{
				return false;
			}
			tokens -= n;
			return true;
		}
	}

	internal const int MaxLive = 64;

	internal const int MaxLivePerIp = 8;

	internal const int MaxPreJoin = 8;

	internal const int MaxPreJoinPerIp = 4;

	internal const int EvictAfterMs = 2000;

	internal const double ConnPerSecPerIp = 2.0;

	internal const double ConnBurstPerIp = 20.0;

	internal const int PwFailsToBlock = 5;

	internal const int PwWindowMs = 60000;

	internal const int PwBlockMs = 60000;

	internal const int PwBlockMaxMs = 900000;

	internal const int GreetingMs = 5000;

	internal const int StatusLineMs = 3000;

	internal const int JoinAfterHelloMs = 15000;

	internal const int JoinMaxMs = 30000;

	internal const int FrameMs = 30000;

	internal const int ClientFrameMs = 180000;

	internal const int FlushMs = 5000;

	internal const int WriteStallMs = 30000;

	internal const int PreJoinMaxFrames = 16;

	internal const int PreJoinMaxBytes = 32768;

	internal const double FramesPerSec = 60.0;

	internal const double FramesBurst = 300.0;

	internal const double ActionsPerSec = 20.0;

	internal const double ActionsBurst = 120.0;

	internal const double WirePerSec = 262144.0;

	internal const double WireBurst = 2097152.0;

	internal const double JsonPerSec = 1048576.0;

	internal const double JsonBurst = 8388608.0;

	internal const int MaxKinds = 128;

	internal const int BacklogFrames = 1024;

	internal const int BacklogBytes = 16777216;

	internal const int ClientBacklogFrames = 200000;

	internal const int ClientBacklogBytes = 536870912;

	internal const long MaxQueuedBytes = 167772160L;

	internal const int MaxQueuedFrames = 250000;

	private const int WriteChunk = 65536;

	private static readonly HashSet<string> ClientSysKinds = new HashSet<string>(StringComparer.Ordinal) { "sys.hello", "sys.ping", "sys.ready", "sys.resync", "sys.resyncnote", "sys.echo" };

	private static int nextId;

	internal readonly int Id = Interlocked.Increment(ref nextId);

	internal readonly bool ServerSide;

	private readonly TcpClient tcp;

	private NetworkStream stream;

	private readonly BlockingCollection<byte[]> outq = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());

	private long queuedBytes;

	private int queuedFrames;

	private int closed;

	private volatile bool closing;

	internal string CloseReason;

	internal string Remote = "?";

	internal long LastRecvMs = Clock.Ms;

	private readonly Wire.Limits limits;

	private readonly string ip;

	private readonly long acceptedMs = Clock.Ms;

	private volatile int phase;

	private bool countedPreJoin;

	private bool countedLive;

	private long serverHelloMs;

	private long flushDeadlineMs;

	private long frameStartMs;

	private long writingSinceMs;

	private bool opened;

	private readonly object stateGate = new object();

	private readonly ManualResetEventSlim readerDone = new ManualResetEventSlim(initialState: false);

	private Bucket frames = new Bucket(60.0, 300.0);

	private Bucket wire = new Bucket(262144.0, 2097152.0);

	private Bucket jsonB = new Bucket(1048576.0, 8388608.0);

	private Bucket sysReady = new Bucket(1.0, 3.0);

	private Bucket sysResync = new Bucket(0.5, 2.0);

	private Bucket sysNote = new Bucket(0.2, 2.0);

	private Bucket sysEcho = new Bucket(2.0, 4.0);

	private Bucket actions = new Bucket(20.0, 120.0);

	private int preJoinFrames;

	private int preJoinBytes;

	private bool helloSeen;

	private HashSet<string> kinds;

	internal readonly Queue<InboxItem> InboxPending = new Queue<InboxItem>();

	internal bool InTurns;

	internal int PendingFrames;

	internal long PendingBytes;

	internal volatile bool Abusive;

	private int strikes;

	private long strikeWindowMs;

	internal static volatile string StatusJson = "{}";

	private const string FloodText = "Your game sent the server too many messages too fast, so the server closed the connection.";

	private static readonly object ipGate = new object();

	private static readonly Dictionary<string, IpState> ips = new Dictionary<string, IpState>(StringComparer.Ordinal);

	private static int liveTotal;

	private static int preJoinTotal;

	private static long globalLogWindowMs;

	private static int globalLogLines;

	private static string lastSummary = "";

	internal bool Closed => Volatile.Read(ref closed) != 0;

	internal bool Joined => phase == 3;

	/// Host loopback connection: no socket, frames are dropped, kind-addressed sends go to HostLoop.Sys.
	internal Conn(string loopbackName)
	{
		tcp = null;
		ServerSide = true;
		limits = Wire.FromClient;
		phase = 3;
		Remote = loopbackName ?? "host";
		ip = "";
	}

	internal bool IsLoopback => tcp == null;

	internal Conn(TcpClient tcp, bool serverSide)
	{
		this.tcp = tcp;
		ServerSide = serverSide;
		limits = (serverSide ? Wire.FromClient : Wire.FromServer);
		phase = ((!serverSide) ? 3 : 0);
		try
		{
			Remote = tcp.Client.RemoteEndPoint?.ToString() ?? "?";
		}
		catch
		{
		}
		ip = (serverSide ? IpKey(tcp) : "");
	}

	internal void Start()
	{
		if (ServerSide && !Admit())
		{
			Interlocked.Exchange(ref closed, 1);
			try
			{
				tcp.Client?.Close(0);
			}
			catch
			{
			}
			try
			{
				tcp.Close();
				return;
			}
			catch
			{
				return;
			}
		}
		try
		{
			tcp.NoDelay = true;
			tcp.SendTimeout = 30000;
			stream = tcp.GetStream();
		}
		catch (Exception e)
		{
			Close("could not start: " + Short(e));
			return;
		}
		Watchdog.Add(this);
		if (!ServerSide)
		{
			Enqueue(Wire.Magic);
		}
		Background.Run("read " + Id, ReadLoop);
		Background.Run("write " + Id, WriteLoop);
	}

	internal void Send(byte[] frame)
	{
		if (IsLoopback)
		{
			return;
		}
		if (!Closed && frame != null)
		{
			if (!ServerSide && frame.Length - 4 > 1048576)
			{
				LogLimited("send-big", "client: a frame of " + (frame.Length >> 10) + " KB was not sent: the server accepts at most " + 1024 + " KB");
			}
			else
			{
				Enqueue(frame);
			}
		}
	}

	private void Enqueue(byte[] frame)
	{
		if (Interlocked.Add(ref queuedBytes, frame.Length) > 167772160 || Interlocked.Increment(ref queuedFrames) > 250000)
		{
			Stats.SendOverflow++;
			Close("send queue overflow (peer not reading)");
			return;
		}
		try
		{
			outq.Add(frame);
		}
		catch (InvalidOperationException)
		{
			Interlocked.Add(ref queuedBytes, -frame.Length);
			Interlocked.Decrement(ref queuedFrames);
		}
	}

	internal void Send(string kind, object payload)
	{
		if (IsLoopback)
		{
			HostLoop.Sys(kind, payload);
			return;
		}
		if (ServerSide)
		{
			Observe(kind, payload);
		}
		Send(Wire.Encode(kind, payload));
	}

	private void Observe(string kind, object payload)
	{
		switch (kind)
		{
		case "sys.server":
			if (Interlocked.Read(ref serverHelloMs) == 0L)
			{
				Interlocked.Exchange(ref serverHelloMs, Clock.Ms);
			}
			break;
		case "sys.welcome":
			MarkJoined();
			break;
		case "sys.refuse":
			if (payload is MsgRefuse { code: "password" })
			{
				NotePasswordFail(ip, Remote);
			}
			break;
		}
	}

	internal void MarkJoined()
	{
		if (!ServerSide || phase == 3)
		{
			return;
		}
		phase = 3;
		lock (ipGate)
		{
			if (countedPreJoin)
			{
				countedPreJoin = false;
				preJoinTotal--;
				State(ip).PreJoin--;
			}
		}
	}

	internal void CloseAfterFlush(string reason)
	{
		if (Closed)
		{
			return;
		}
		if (IsLoopback)
		{
			Shut(reason);
			return;
		}
		closing = true;
		if (CloseReason == null)
		{
			CloseReason = reason;
		}
		Interlocked.CompareExchange(ref flushDeadlineMs, Clock.Ms + 5000, 0L);
		try
		{
			outq.CompleteAdding();
		}
		catch
		{
		}
	}

	internal void Close(string reason)
	{
		Shut(reason);
	}

	private void Quiet(string why)
	{
		Shut(why);
	}

	private void Shut(string reason)
	{
		bool flag;
		lock (stateGate)
		{
			if (Volatile.Read(ref closed) != 0)
			{
				return;
			}
			Volatile.Write(ref closed, 1);
			if (CloseReason == null)
			{
				CloseReason = reason;
			}
			flag = !ServerSide || opened;
		}
		closing = true;
		try
		{
			outq.CompleteAdding();
		}
		catch
		{
		}
		if (IsLoopback)
		{
			Log.Warn("host: the host peer connection was closed (" + reason + ")");
			return;
		}
		try
		{
			tcp.Client?.Shutdown(SocketShutdown.Both);
		}
		catch
		{
		}
		try
		{
			tcp.Close();
		}
		catch
		{
		}
		Release();
		Watchdog.Remove(this);
		if (flag)
		{
			Inbox.Post(new InboxItem
			{
				Event = InboxEvent.Closed,
				Conn = this,
				Text = CloseReason
			});
		}
	}

	private bool PostOpened()
	{
		lock (stateGate)
		{
			if (Volatile.Read(ref closed) != 0)
			{
				return false;
			}
			opened = true;
			Inbox.Post(new InboxItem
			{
				Event = InboxEvent.Opened,
				Conn = this
			});
			return true;
		}
	}

	internal void Refuse(string code, string reason, string logWhy, bool abusive)
	{
		if (Closed || closing)
		{
			return;
		}
		if (IsLoopback)
		{
			Log.Warn("host: refuse (" + code + ") on the host peer ignored: " + logWhy);
			return;
		}
		if (abusive)
		{
			Abusive = true;
		}
		if (code == "flood")
		{
			Stats.Floods++;
		}
		LogLimited(code, "server: " + Remote + " refused (" + code + "): " + logWhy);
		if (ServerSide)
		{
			try
			{
				Enqueue(Wire.Encode("sys.refuse", new MsgRefuse
				{
					code = code,
					reason = reason
				}));
			}
			catch
			{
			}
			CloseAfterFlush("refused: " + code + " (" + logWhy + ")");
		}
		else
		{
			Close(logWhy);
		}
	}

	internal void Strike(string why)
	{
		if (IsLoopback)
		{
			Log.Warn("host: strike on the host peer ignored: " + why);
			return;
		}
		if (ServerSide && !Closed)
		{
			long ms = Clock.Ms;
			if (ms - strikeWindowMs > 60000)
			{
				strikeWindowMs = ms;
				strikes = 0;
			}
			if (++strikes >= 20)
			{
				Refuse("bad", "Your game sent the server too many messages it could not use.", "20 bad messages in 60 s (last: " + why + ")", abusive: true);
			}
		}
	}

	private void WriteLoop()
	{
		try
		{
			foreach (byte[] item in outq.GetConsumingEnumerable())
			{
				for (int i = 0; i < item.Length; i += 65536)
				{
					Interlocked.Exchange(ref writingSinceMs, Clock.Ms);
					stream.Write(item, i, Math.Min(65536, item.Length - i));
				}
				Interlocked.Exchange(ref writingSinceMs, 0L);
				Interlocked.Add(ref queuedBytes, -item.Length);
				Interlocked.Decrement(ref queuedFrames);
			}
			if (!Closed)
			{
				try
				{
					stream.Flush();
				}
				catch
				{
				}
				try
				{
					tcp.Client.Shutdown(SocketShutdown.Send);
				}
				catch
				{
				}
				long num = Interlocked.Read(ref flushDeadlineMs);
				int num2 = (int)((num > 0) ? Math.Max(0L, num - Clock.Ms) : 0);
				if (num2 > 0)
				{
					readerDone.Wait(num2);
				}
				Close(CloseReason ?? "closed");
			}
		}
		catch (Exception e)
		{
			Close(Closed ? CloseReason : ("send failed: " + Short(e)));
		}
	}

	private void ReadLoop()
	{
		byte[] array = new byte[4];
		try
		{
			if (ServerSide)
			{
				stream.ReadTimeout = 5000;
				if (!ReadExactly(array, 1))
				{
					Close("closed before saying anything");
					return;
				}
				if (array[0] == 83)
				{
					phase = 1;
					AnswerStatus();
					return;
				}
				if (!ReadExactly(array, 3, 1))
				{
					Close("closed");
					return;
				}
				if (array[0] != Wire.Magic[0] || array[1] != Wire.Magic[1] || array[2] != Wire.Magic[2] || array[3] != Wire.Magic[3])
				{
					Quiet("not a ProbablyTogether client");
					return;
				}
				stream.ReadTimeout = -1;
				phase = 2;
			}
			if (!PostOpened())
			{
				return;
			}
			while (!Closed)
			{
				if (!ReadExactly(array, 4))
				{
					if (!closing)
					{
						Close(ServerSide ? "player closed the connection" : "server closed the connection");
					}
					break;
				}
				Wire.ParseHeader(array, out var length, out var gz);
				bool flag = phase == 3;
				if (ServerSide && !flag)
				{
					if (gz || length <= 0 || length > 16384)
					{
						Refuse("bad", "Bad join request.", "a " + length + "-byte" + (gz ? " gzip" : "") + " frame before joining", abusive: true);
						break;
					}
					preJoinBytes += length;
					if (++preJoinFrames > 16 || preJoinBytes > 32768)
					{
						Refuse("flood", "Too many messages before joining.", preJoinFrames + " frames / " + preJoinBytes + " bytes before joining", abusive: true);
						break;
					}
				}
				else
				{
					if (length <= 0 || length > limits.MaxFrame)
					{
						Refuse("bad", "Your game sent a message bigger than the server accepts.", "frame of " + length + " bytes (limit " + limits.MaxFrame + ")", abusive: true);
						break;
					}
					if (ServerSide)
					{
						long ms = Clock.Ms;
						if (!frames.Take(1.0, ms))
						{
							Refuse("flood", "Your game sent the server too many messages too fast, so the server closed the connection.", "over " + 60.0 + " frames/s (burst " + 300.0 + ")", abusive: true);
							break;
						}
						if (!wire.Take(length, ms))
						{
							Refuse("flood", "Your game sent the server too many messages too fast, so the server closed the connection.", "over " + 256.0 + " KB/s on the wire (burst " + 2.0 + " MB)", abusive: true);
							break;
						}
					}
				}
				Interlocked.Exchange(ref frameStartMs, Clock.Ms);
				byte[] array2 = ReadBody(length);
				Interlocked.Exchange(ref frameStartMs, 0L);
				if (array2 == null)
				{
					if (!closing)
					{
						Close("connection lost mid-frame");
					}
					break;
				}
				LastRecvMs = Clock.Ms;
				if (closing)
				{
					break;
				}
				int num = limits.MaxJson;
				if (ServerSide && flag)
				{
					num = (int)Math.Min(num, Math.Max(0.0, jsonB.Available(Clock.Ms)));
				}
				string kind;
				JToken data;
				int jsonBytes;
				try
				{
					Wire.Decode(array2, length, gz, limits, num, out kind, out data, out jsonBytes);
				}
				catch (Wire.OverBudget)
				{
					Refuse("flood", "Your game sent the server too many messages too fast, so the server closed the connection.", "over " + 1.0 + " MB/s unzipped (burst " + 8.0 + " MB)", abusive: true);
					break;
				}
				catch (Exception e)
				{
					Stats.BadFrames++;
					if (ServerSide)
					{
						Refuse("bad", "Your game sent a message the server could not read.", "unreadable frame: " + Short(e), abusive: true);
					}
					else
					{
						Close("the server sent a message this game could not read: " + Short(e));
					}
					break;
				}
				if (ServerSide)
				{
					if (flag)
					{
						jsonB.Take(jsonBytes, Clock.Ms);
					}
					if (!Admissible(kind, flag))
					{
						if (closing)
						{
							break;
						}
						continue;
					}
				}
				WaitForBacklog();
				if (Closed || closing)
				{
					break;
				}
				Inbox.Post(new InboxItem
				{
					Event = InboxEvent.Frame,
					Conn = this,
					Kind = kind,
					Data = data,
					Cost = jsonBytes
				});
			}
		}
		catch (Exception e2)
		{
			Close(Closed ? CloseReason : ("read failed: " + Short(e2)));
		}
		finally
		{
			if (closing && !Closed)
			{
				Drain();
			}
			readerDone.Set();
		}
	}

	private void Drain()
	{
		byte[] array = new byte[16384];
		long num = 0L;
		try
		{
			stream.ReadTimeout = 5000;
			while (!Closed && num < 8388608)
			{
				int num2 = stream.Read(array, 0, array.Length);
				if (num2 > 0)
				{
					num += num2;
					continue;
				}
				break;
			}
		}
		catch
		{
		}
	}

	private bool Admissible(string kind, bool joined)
	{
		bool flag = kind.StartsWith("sys.", StringComparison.Ordinal);
		if (!joined)
		{
			if (kind == "sys.ping")
			{
				return true;
			}
			if (kind == "sys.hello")
			{
				if (helloSeen)
				{
					Stats.Dropped++;
					return false;
				}
				helloSeen = true;
				return true;
			}
			Refuse("protocol", "Expected a join request first.", kind + " before joining", abusive: true);
			return false;
		}
		if (kinds == null)
		{
			kinds = new HashSet<string>(StringComparer.Ordinal);
		}
		if (kinds.Count >= 128 && !kinds.Contains(kind))
		{
			Refuse("bad", "Your game sent messages the server does not know.", "more than " + 128 + " different kinds", abusive: true);
			return false;
		}
		kinds.Add(kind);
		if (!flag && !kind.StartsWith("presence.", StringComparison.Ordinal) && !kind.StartsWith("chat.", StringComparison.Ordinal) && !kind.StartsWith("voice.", StringComparison.Ordinal) && !actions.Take(1.0, Clock.Ms))
		{
			Refuse("flood", "Your game sent the server too many messages too fast, so the server closed the connection.", "over " + 20.0 + " game actions/s (burst " + 120.0 + ")", abusive: true);
			return false;
		}
		if (flag)
		{
			if (!ClientSysKinds.Contains(kind) || kind == "sys.hello")
			{
				Stats.Dropped++;
				LogLimited("sys-drop", "server: dropped " + kind + " from " + Remote + " (not a kind a client sends)");
				return false;
			}
			long ms = Clock.Ms;
			Bucket bucket = null;
			switch (kind)
			{
			case "sys.ready":
				bucket = sysReady;
				break;
			case "sys.resync":
				bucket = sysResync;
				break;
			case "sys.resyncnote":
				bucket = sysNote;
				break;
			case "sys.echo":
				bucket = sysEcho;
				break;
			}
			if (bucket != null && !bucket.Take(1.0, ms))
			{
				Stats.Dropped++;
				return false;
			}
			return true;
		}
		return true;
	}

	private void WaitForBacklog()
	{
		int num = (ServerSide ? 1024 : 200000);
		long num2 = (ServerSide ? 16777216 : 536870912);
		bool flag = false;
		while (!Closed && !closing && (Volatile.Read(ref PendingFrames) >= num || Interlocked.Read(ref PendingBytes) >= num2))
		{
			if (!flag)
			{
				flag = true;
				Stats.Backpressure++;
			}
			Thread.Sleep(10);
		}
	}

	private byte[] ReadBody(int len)
	{
		if (len <= 65536)
		{
			byte[] array = new byte[len];
			if (!ReadExactly(array, len))
			{
				return null;
			}
			return array;
		}
		byte[] array2 = new byte[65536];
		int num;
		for (int i = 0; i < len; i += num)
		{
			if (i == array2.Length)
			{
				Array.Resize(ref array2, (int)Math.Min((long)array2.Length * 2L, len));
			}
			num = stream.Read(array2, i, Math.Min(array2.Length, len) - i);
			if (num <= 0)
			{
				return null;
			}
		}
		return array2;
	}

	private void AnswerStatus()
	{
		StringBuilder stringBuilder = new StringBuilder("S");
		byte[] array = new byte[1];
		stream.ReadTimeout = 3000;
		while (stringBuilder.Length < 16 && ReadExactly(array, 1) && array[0] != 10)
		{
			stringBuilder.Append((char)array[0]);
		}
		if (stringBuilder.ToString().TrimEnd('\r') == "STATUS")
		{
			byte[] bytes = Encoding.UTF8.GetBytes(StatusJson + "\n");
			try
			{
				stream.Write(bytes, 0, bytes.Length);
				stream.Flush();
			}
			catch
			{
			}
			Stats.StatusAnswered++;
		}
		Quiet("status answered");
	}

	private bool ReadExactly(byte[] buf, int count, int offset = 0)
	{
		int num;
		for (int i = 0; i < count; i += num)
		{
			num = stream.Read(buf, offset + i, count - i);
			if (num <= 0)
			{
				return false;
			}
		}
		return true;
	}

	internal static string Short(Exception e)
	{
		Exception baseException = e.GetBaseException();
		return Text.Clip((baseException is IOException || baseException is SocketException || baseException is ObjectDisposedException || baseException is InvalidDataException) ? baseException.Message : (baseException.GetType().Name + ": " + baseException.Message), 240);
	}

	internal void CheckDeadlines(long now)
	{
		if (Closed)
		{
			return;
		}
		long num = Interlocked.Read(ref flushDeadlineMs);
		if (num > 0 && now > num)
		{
			Close(CloseReason ?? "closed (flush timed out)");
			return;
		}
		long num2 = Interlocked.Read(ref writingSinceMs);
		if (num2 > 0 && now - num2 > 30000)
		{
			Stats.Timeouts++;
			Close("peer not reading for " + 30 + " s");
			return;
		}
		long num3 = Interlocked.Read(ref frameStartMs);
		int num4 = (ServerSide ? 30000 : 180000);
		if (num3 > 0 && now - num3 > num4)
		{
			Stats.Timeouts++;
			Close("a message took over " + num4 / 1000 + " s to arrive");
		}
		else
		{
			if (!ServerSide)
			{
				return;
			}
			switch ((Phase)phase)
			{
			case Phase.Greeting:
				if (now - acceptedMs > 5000)
				{
					Stats.Timeouts++;
					Quiet("said nothing useful in " + 5 + " s");
				}
				break;
			case Phase.Status:
				if (now - acceptedMs > 8000)
				{
					Stats.Timeouts++;
					Quiet("STATUS line not finished in time");
				}
				break;
			case Phase.PreJoin:
			{
				long num5 = Interlocked.Read(ref serverHelloMs);
				if ((num5 > 0 && now - num5 > 15000) || now - acceptedMs > 30000)
				{
					Stats.Timeouts++;
					LogLimited("join-timeout", "server: " + Remote + " did not finish joining in time; closed");
					Close("no join request in time");
				}
				break;
			}
			}
		}
	}

	private static IpState State(string key)
	{
		if (!ips.TryGetValue(key, out var value))
		{
			value = (ips[key] = new IpState());
		}
		return value;
	}

	private bool Admit()
	{
		Conn conn = null;
		string text = null;
		lock (ipGate)
		{
			long ms = Clock.Ms;
			IpState ipState = State(ip);
			ipState.Tokens = Math.Min(20.0, ipState.Tokens + (double)(ms - ipState.TokensMs) * 2.0 / 1000.0);
			ipState.TokensMs = ms;
			if (ipState.BlockedUntilMs > ms)
			{
				text = "blocked after failed passwords for " + ((ipState.BlockedUntilMs - ms) / 1000 + 1) + " more s";
			}
			else if (ipState.Tokens < 1.0)
			{
				text = "connecting too often (over " + 2.0 + "/s)";
			}
			else if (liveTotal >= 64)
			{
				text = "server at " + 64 + " connections";
			}
			else if (ipState.Live >= 8)
			{
				text = "address at " + 8 + " connections";
			}
			else if (ipState.PreJoin >= 4)
			{
				text = "address at " + 4 + " connections that have not joined";
			}
			else if (preJoinTotal >= 8)
			{
				conn = EvictionVictim(ms - 2000);
				if (conn == null)
				{
					text = "server at " + 8 + " connections that have not joined";
				}
			}
			if (text == null)
			{
				ipState.Tokens -= 1.0;
				ipState.Live++;
				liveTotal++;
				countedLive = true;
				ipState.PreJoin++;
				preJoinTotal++;
				countedPreJoin = true;
			}
			else
			{
				ipState.Tokens = Math.Max(-10.0, ipState.Tokens - 1.0);
				Stats.Refused++;
			}
		}
		if (conn != null)
		{
			Stats.Evicted++;
			conn.LogLimited("evict", "server: dropped " + conn.Remote + " (not joined after " + (Clock.Ms - conn.acceptedMs) / 1000 + " s) to make room for " + Remote);
			conn.Close("dropped: too many connections waiting to join");
		}
		if (text != null)
		{
			LogLimited("admit", "server: refused a connection from " + Remote + ": " + text);
			return false;
		}
		return true;
	}

	private static Conn EvictionVictim(long olderThanMs)
	{
		Conn conn = null;
		int num = 0;
		foreach (Conn item in Watchdog.All)
		{
			if (item.ServerSide && !item.Closed && item.countedPreJoin && item.acceptedMs < olderThanMs)
			{
				IpState value;
				int num2 = (ips.TryGetValue(item.ip, out value) ? value.PreJoin : 0);
				if (conn == null || num2 > num || (num2 == num && item.acceptedMs < conn.acceptedMs))
				{
					conn = item;
					num = num2;
				}
			}
		}
		return conn;
	}

	private void Release()
	{
		if (!ServerSide)
		{
			return;
		}
		lock (ipGate)
		{
			if (ips.TryGetValue(ip, out var value))
			{
				if (countedLive)
				{
					countedLive = false;
					value.Live--;
					liveTotal--;
				}
				if (countedPreJoin)
				{
					countedPreJoin = false;
					value.PreJoin--;
					preJoinTotal--;
				}
			}
		}
	}

	private static void NotePasswordFail(string key, string remote)
	{
		string text = null;
		lock (ipGate)
		{
			long ms = Clock.Ms;
			IpState ipState = State(key);
			ipState.LastBadMs = ms;
			if (ms - ipState.PwWindowStartMs > 60000)
			{
				ipState.PwWindowStartMs = ms;
				ipState.PwFails = 0;
			}
			if (++ipState.PwFails >= 5)
			{
				long num = Math.Min(900000L, 60000L << Math.Min(ipState.BlockLevel, 8));
				ipState.BlockLevel++;
				ipState.BlockedUntilMs = ms + num;
				ipState.PwFails = 0;
				text = "server: " + remote + " failed the password " + 5 + " times; its address is blocked for " + num / 1000 + " s";
			}
		}
		if (text != null)
		{
			Stats.Blocks++;
			Log.Warn(text);
		}
	}

	private static void PruneIps(long now)
	{
		lock (ipGate)
		{
			List<string> list = null;
			foreach (KeyValuePair<string, IpState> ip in ips)
			{
				IpState value = ip.Value;
				bool flag = value.Live == 0 && value.PreJoin == 0 && value.BlockedUntilMs < now && now - value.TokensMs > 120000;
				if (flag && value.BlockLevel > 0 && now - value.LastBadMs < 3600000)
				{
					flag = false;
				}
				if (flag)
				{
					(list ?? (list = new List<string>())).Add(ip.Key);
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (string item in list)
			{
				ips.Remove(item);
			}
		}
	}

	private static string IpKey(TcpClient t)
	{
		try
		{
			if (!(t.Client.RemoteEndPoint is IPEndPoint iPEndPoint))
			{
				return "?";
			}
			IPAddress iPAddress = iPEndPoint.Address;
			if (iPAddress.IsIPv4MappedToIPv6)
			{
				iPAddress = iPAddress.MapToIPv4();
			}
			if (iPAddress.AddressFamily == AddressFamily.InterNetworkV6)
			{
				return BitConverter.ToString(iPAddress.GetAddressBytes(), 0, 8) + "::/64";
			}
			return iPAddress.ToString();
		}
		catch
		{
			return "?";
		}
	}

	private void LogLimited(string topic, string text)
	{
		if (!ServerSide)
		{
			Log.Warn(text);
			return;
		}
		string text2 = null;
		lock (ipGate)
		{
			long ms = Clock.Ms;
			IpState ipState = State(ip + "|" + topic);
			if (ms - globalLogWindowMs > 60000)
			{
				globalLogWindowMs = ms;
				globalLogLines = 0;
			}
			if (ms - ipState.LastLogMs >= 10000 && globalLogLines < 30)
			{
				ipState.LastLogMs = ms;
				globalLogLines++;
				text2 = text + ((ipState.Suppressed > 0) ? (" (+" + ipState.Suppressed + " more like it not logged)") : "");
				ipState.Suppressed = 0;
			}
			else
			{
				ipState.Suppressed++;
				Stats.LogsSuppressed++;
			}
		}
		if (text2 != null)
		{
			Log.Warn(text2);
		}
	}

	private static void Summary()
	{
		int num = 0;
		int num2 = 0;
		lock (ipGate)
		{
			num = liveTotal;
			num2 = preJoinTotal;
		}
		string text = Stats.Text();
		if (!(text == lastSummary))
		{
			lastSummary = text;
			Log.Info("harden: since start: " + text + "; now " + num + " connection(s), " + num2 + " not joined");
		}
	}
}
