using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json.Linq;
using Steamworks;
using UnityEngine;

namespace ProbablyTogether;

internal static class Client
{
	internal static ClientState State = ClientState.Idle;

	private static Conn conn;

	private static string password = "";

	private static string wantName = "";

	internal static string Address = "";

	internal static string ServerName = "";

	internal static string ServerMod = "";

	internal static int MaxPlayers;

	internal static string LastRefusal;

	internal static string LastRefuseCode;

	internal static string LastRefuseReason;

	internal static List<ModList.Info> LastRefuseMods;

	internal static HashSet<string> ServerMods;

	internal static string ServerBuild = "";

	private static long lastPingMs;

	private static int attempt;

	internal static string LabHelloMod;

	internal static bool Connected
	{
		get
		{
			if (conn != null)
			{
				return !conn.Closed;
			}
			return false;
		}
	}

	internal static void Connect(string address, string pw, string name)
	{
		Disconnect("reconnecting");
		if (!TryParse(address, out var host, out var port))
		{
			Log.Error("client: bad server address '" + address + "' (host:port)");
			return;
		}
		Address = host + ":" + port;
		password = pw ?? "";
		wantName = ChooseName(name);
		LastRefusal = null;
		LastRefuseCode = null;
		LastRefuseReason = null;
		LastRefuseMods = null;
		ServerMods = null;
		ServerBuild = "";
		Net.LastDeniedKind = null;
		Net.LastDeniedReason = null;
		State = ClientState.Connecting;
		int my = ++attempt;
		Log.Info("client: connecting to " + Address + " as '" + wantName + "'");
		Thread thread = new Thread((ThreadStart)delegate
		{
			TcpClient tcpClient = new TcpClient();
			try
			{
				IAsyncResult asyncResult = tcpClient.BeginConnect(host, port, null, null);
				if (!asyncResult.AsyncWaitHandle.WaitOne(10000))
				{
					throw new TimeoutException("no answer in 10 s");
				}
				tcpClient.EndConnect(asyncResult);
				Conn conn = new Conn(tcpClient, serverSide: false);
				if (my != Volatile.Read(ref attempt))
				{
					tcpClient.Close();
				}
				else
				{
					Client.conn = conn;
					conn.Start();
				}
			}
			catch (Exception e)
			{
				try
				{
					tcpClient.Close();
				}
				catch
				{
				}
				Inbox.Post(new InboxItem
				{
					Event = InboxEvent.ConnectFailed,
					Text = Conn.Short(e)
				});
			}
		});
		thread.IsBackground = true;
		thread.Name = "ProbablyTogether connect";
		thread.Start();
	}

	internal static void Disconnect(string why)
	{
		Interlocked.Increment(ref attempt);
		Conn conn = Client.conn;
		Client.conn = null;
		if (conn != null && !conn.Closed)
		{
			conn.Close(why);
		}
		if (State != ClientState.Idle)
		{
			Lost(why, log: false);
		}
	}

	private static bool TryParse(string address, out string host, out int port)
	{
		host = null;
		port = 27700;
		if (string.IsNullOrWhiteSpace(address))
		{
			return false;
		}
		address = address.Trim();
		int num = address.LastIndexOf(':');
		if (num > 0 && address.IndexOf(':') == num)
		{
			host = address.Substring(0, num);
			if (int.TryParse(address.Substring(num + 1), out port) && port > 0)
			{
				return port < 65536;
			}
			return false;
		}
		host = address;
		return true;
	}

	internal static string ChooseName(string given)
	{
		string text = SteamName();
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		if (!string.IsNullOrWhiteSpace(given))
		{
			return given;
		}
		return "Player";
	}

	private static string SteamName()
	{
		try
		{
			if (SteamManager.Initialized)
			{
				return SteamFriends.GetPersonaName();
			}
		}
		catch
		{
		}
		return null;
	}

	private static string HelloModVersion()
	{
		string text = ((Args.Bot != null) ? (LabHelloMod ?? Args.Get("--br-hello-mod")) : null);
		if (string.IsNullOrEmpty(text) || text.Length > 12)
		{
			return Mod.Version;
		}
		return text;
	}

	private static string SteamIdString()
	{
		// All copies on one machine share a Steam account; a bot takes its identity from --br-steamid.
		if (Args.Bot != null && !string.IsNullOrEmpty(Args.Get("--br-steamid")))
		{
			return Args.Get("--br-steamid");
		}
		try
		{
			if (SteamManager.Initialized)
			{
				return SteamUser.GetSteamID().m_SteamID.ToString();
			}
		}
		catch
		{
		}
		return "0";
	}

	internal static void OnConnectFailed(string why)
	{
		if (State == ClientState.Connecting)
		{
			Log.Warn("client: could not reach " + Address + ": " + why);
			Lost("could not reach the server: " + why, log: true);
		}
	}

	internal static void OnOpened(Conn c)
	{
		if (c == conn)
		{
			State = ClientState.Handshake;
		}
	}

	internal static void OnClosed(Conn c, string why)
	{
		if (c == conn)
		{
			conn = null;
			Lost(LastRefusal ?? why, log: true);
		}
	}

	private static void Lost(string why, bool log)
	{
		ClientState state = State;
		State = ClientState.Idle;
		if (log)
		{
			Log.Info("client: disconnected from " + Address + ": " + why);
		}
		Snapshot.ClientAbort();
		Net.LeaveSession();
		if (state != ClientState.Idle)
		{
			Net.RaiseDisconnected(why);
		}
	}

	internal static void OnFrame(Conn c, string kind, JToken d)
	{
		if (c != conn)
		{
			return;
		}
		switch (kind)
		{
		case "sys.server":
		{
			MsgServerHello msgServerHello = Wire.To<MsgServerHello>(d) ?? new MsgServerHello();
			ServerName = msgServerHello.name ?? "";
			ServerMod = Text.Clip((msgServerHello.mod ?? "").Trim(), 24);
			MaxPlayers = msgServerHello.maxPlayers;
			Log.Info("client: server '" + Text.Clip(ServerName, 64) + "' protocol " + msgServerHello.protocol + " mod " + Text.Clip(msgServerHello.mod, 40) + " game " + Text.Clip(msgServerHello.game, 40) + ", " + msgServerHello.players + "/" + msgServerHello.maxPlayers + " players" + (msgServerHello.passworded ? ", password" : ""));
			c.Send("sys.hello", new MsgHello
			{
				protocol = Protocol.Version,
				mod = HelloModVersion(),
				game = Application.version,
				name = wantName,
				steamId = SteamIdString(),
				auth = (msgServerHello.passworded ? Auth.Proof(Auth.DeriveKey(password, msgServerHello.salt), msgServerHello.nonce) : ""),
				mods = (JToken)(object)ModList.Describe(),
				build = GameBuild.Id,
				beta = GameBuild.Beta
			});
			break;
		}
		case "sys.refuse":
		{
			MsgRefuse msgRefuse = Wire.To<MsgRefuse>(d) ?? new MsgRefuse();
			LastRefuseCode = msgRefuse.code ?? "";
			LastRefuseReason = msgRefuse.reason ?? "";
			LastRefuseMods = ModList.Read(msgRefuse.mods);
			LastRefusal = "refused (" + msgRefuse.code + "): " + msgRefuse.reason;
			Log.Info("client: " + Text.Clip(LastRefusal, 400));
			break;
		}
		case "sys.welcome":
		{
			MsgWelcome msgWelcome = Wire.To<MsgWelcome>(d);
			if (msgWelcome == null)
			{
				Disconnect("bad welcome");
				break;
			}
			ServerName = msgWelcome.server ?? ServerName;
			MaxPlayers = msgWelcome.maxPlayers;
			ServerMods = ModList.ReadServerMods(msgWelcome.mods);
			ServerBuild = GameBuild.CleanId(msgWelcome.build);
			Locks.TakeoverSeconds = msgWelcome.takeover;
			Systems.Outings.Individual = msgWelcome.outings;
			Systems.Bays.Enabled = msgWelcome.ownership;
			Net.EnterSession(msgWelcome.you, msgWelcome.players);
			State = ClientState.AwaitSnapshot;
			Log.Info("client: joined '" + ServerName + "' as player " + msgWelcome.you + "; players: " + Names(msgWelcome.players) + ((ServerMods == null) ? "; the server does not list its mods" : ("; server mods: " + ((ServerMods.Count == 0) ? "-" : Text.Clip(string.Join(", ", ServerMods), 400)))));
			break;
		}
		case "sys.roster":
		{
			MsgRoster msgRoster = Wire.To<MsgRoster>(d);
			if (msgRoster != null)
			{
				Net.ApplyRoster(msgRoster);
			}
			break;
		}
		case "sys.ping":
			break;
		case "sys.bye":
			LastRefusal = "server stopping: " + Wire.To<MsgBye>(d)?.reason;
			Log.Info("client: " + LastRefusal);
			break;
		case "sys.snapshot":
			Snapshot.OnSnapshot(Wire.To<MsgSnapshot>(d));
			break;
		case "sys.replay":
			Snapshot.OnReplayHeader(Wire.To<MsgReplay>(d));
			break;
		case "sys.lock":
		{
			MsgLock msgLock = Wire.To<MsgLock>(d);
			if (msgLock != null)
			{
				Locks.ClientSet(msgLock.name, msgLock.holder);
			}
			break;
		}
		case "sys.digest":
			if (State == ClientState.Ready)
			{
				Digest.Compare(Wire.To<MsgDigest>(d));
			}
			break;
		case "sys.denied":
		{
			MsgDenied msgDenied = Wire.To<MsgDenied>(d);
			if (msgDenied != null)
			{
				if (Log.Allow("c|denied|" + msgDenied.kind, 2000L, out var skipped))
				{
					Log.Info("client: the server refused " + Text.Clip(msgDenied.kind, 64) + ": " + Text.Clip(msgDenied.reason, 300) + Log.More(skipped));
				}
				Net.RaiseDenied(msgDenied.kind, msgDenied.reason);
			}
			break;
		}
		case "sys.echo":
		{
			MsgEcho msgEcho = Wire.To<MsgEcho>(d);
			if (msgEcho != null)
			{
				Log.Info("client: echo '" + msgEcho.text + "' came back in " + (Clock.Ms - msgEcho.sentMs) + " ms");
			}
			break;
		}
		default:
			if (kind.StartsWith("sys.", StringComparison.Ordinal))
			{
				Log.Warn("client: unexpected " + kind);
			}
			else if (State == ClientState.Loading || State == ClientState.AwaitSnapshot)
			{
				Snapshot.Hold(kind, d);
			}
			else
			{
				Dispatch.OnClientFrame(kind, d);
			}
			break;
		}
	}

	internal static void Send(byte[] frame)
	{
		conn?.Send(frame);
	}

	internal static void Tick()
	{
		Conn conn = Client.conn;
		if (conn != null)
		{
			long ms = Clock.Ms;
			if (State >= ClientState.AwaitSnapshot && ms - lastPingMs > 5000)
			{
				lastPingMs = ms;
				conn.Send("sys.ping", new MsgPing
				{
					t = ms
				});
			}
			if (State >= ClientState.Handshake && ms - conn.LastRecvMs > 30000)
			{
				conn.Close("server silent for 30 s");
			}
		}
	}

	internal static string Names(List<PlayerInfo> l)
	{
		if (l == null)
		{
			return "-";
		}
		List<string> list = new List<string>();
		foreach (PlayerInfo item in l)
		{
			list.Add(item.Name + "#" + item.Id);
		}
		return string.Join(", ", list);
	}
}
