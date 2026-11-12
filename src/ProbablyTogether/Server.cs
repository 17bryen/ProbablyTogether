using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ProbablyTogether.Systems;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ProbablyTogether;

internal static class Server
{
	internal enum PeerState
	{
		Handshake,
		Syncing,
		Ready
	}

	internal sealed class Peer
	{
		public Conn Conn;

		public PeerState State;

		public string Nonce;

		public PlayerInfo Info;

		public long OpenedMs;

		public int SnapshotSeq;

		public long LastSnapshotMs = -100000L;

		/// Set once the host's seat is looked up; the host has no join handshake.
		public bool SeatAsked;

		public long ReadyMs;

		public long ResyncAtMs;

		public string ResyncReason;

		public string StuckAreas;

		public int StuckLoads;

		public bool StuckTold;

		public int StuckIgnored;

		public string ModsNote;

		public string ModVersion = "";

		public int Id => Info?.Id ?? 0;
	}

	internal static ServerConfig Config;

	private static TcpListener listener;

	private static Thread acceptThread;

	private static volatile bool listening;

	internal static readonly Dictionary<int, Peer> ByConn = new Dictionary<int, Peer>();

	internal static readonly Dictionary<int, Peer> ByPlayer = new Dictionary<int, Peer>();

	private static int nextPlayerId = 1;

	private static long lastPingMs;

	private static long lastStatusMs;

	private static bool shopReady;

	private static readonly HashSet<string> droppedWhileLoading = new HashSet<string>();

	private static string salt = "";

	private static byte[] passwordKey;

	private static string keyPassword = "";

	internal static bool Stopping;

	internal const string KickedReason = "Removed by the server owner.";

	internal static bool ShopReady
	{
		get
		{
			return shopReady;
		}
		set
		{
			if (value && !shopReady)
			{
				droppedWhileLoading.Clear();
			}
			shopReady = value;
		}
	}

	internal static int PlayerCount => ByPlayer.Count;

	internal static bool Listening => listening;

	internal static bool Start(ServerConfig cfg)
	{
		Config = cfg;
		salt = Auth.Random16();
		passwordKey = null;
		keyPassword = "";
		PasswordKey();
		UpdateStatus();
		try
		{
			listener = new TcpListener(IPAddress.Any, cfg.port);
			listener.Start(32);
			listening = true;
			acceptThread = new Thread(AcceptLoop)
			{
				IsBackground = true,
				Name = "ProbablyTogether accept"
			};
			acceptThread.Start();
			Log.Info("server: listening on TCP " + cfg.port);
			return true;
		}
		catch (Exception e)
		{
			try
			{
				listener?.Stop();
			}
			catch
			{
			}
			Log.Error("server: could not listen on TCP " + cfg.port);
			Log.Error("server: the game port " + cfg.port + " is not usable (" + Conn.Short(e) + "); another program may hold it; quitting");
			return false;
		}
	}

	private static void AcceptLoop()
	{
		while (listening)
		{
			TcpClient tcpClient;
			try
			{
				tcpClient = listener.AcceptTcpClient();
			}
			catch (Exception e)
			{
				if (listening)
				{
					Log.Warn("server: accept failed: " + Conn.Short(e));
				}
				Thread.Sleep(200);
				continue;
			}
			try
			{
				new Conn(tcpClient, serverSide: true).Start();
			}
			catch (Exception e2)
			{
				Log.Warn("server: could not start a connection: " + Conn.Short(e2));
				try
				{
					tcpClient.Close();
				}
				catch
				{
				}
			}
		}
	}

	/// Forgets every peer so the same game can host again.
	internal static void ResetAfterHost()
	{
		foreach (Peer value in new List<Peer>(ByConn.Values))
		{
			try
			{
				value.Conn.Close("the host left");
			}
			catch
			{
			}
		}
		ByConn.Clear();
		ByPlayer.Clear();
		nextPlayerId = 1;
		ShopReady = false;
		Stopping = false;
		listener = null;
		Config = null;
	}

	internal static void StopListening()
	{
		listening = false;
		try
		{
			listener?.Stop();
		}
		catch
		{
		}
	}

	internal static void OnOpened(Conn c)
	{
		if (ByConn.Count >= Config.maxPlayers + 8 + 2)
		{
			c.Refuse("full", "The server is full (" + PlayerCount + "/" + Config.maxPlayers + " players). Try again in a minute.", "connection table full (" + ByConn.Count + " entries)", abusive: false);
			return;
		}
		Peer peer = new Peer
		{
			Conn = c,
			State = PeerState.Handshake,
			Nonce = Auth.Random16(),
			OpenedMs = Clock.Ms
		};
		ByConn[c.Id] = peer;
		c.Send("sys.server", new MsgServerHello
		{
			protocol = Protocol.Version,
			mod = GreetingModVersion(),
			game = Application.version,
			name = Config.name,
			nonce = peer.Nonce,
			salt = salt,
			passworded = (Config.password.Length > 0),
			players = PlayerCount,
			maxPlayers = Config.maxPlayers
		});
	}

	internal static void OnClosed(Conn c, string why)
	{
		if (ByConn.TryGetValue(c.Id, out var value))
		{
			ByConn.Remove(c.Id);
			if (value.Info != null)
			{
				ByPlayer.Remove(value.Info.Id);
				Dispatch.Forget(value.Info.Id);
				Log.ForgetPrefix("s" + value.Info.Id + "|");
				Net.Players.Remove(value.Info.Id);
				Log.Info("server: " + value.Info.Name + " (player " + value.Info.Id + ") left: " + why + "; " + PlayerCount + "/" + Config.maxPlayers + " players");
				Locks.ReleaseAll(value.Info.Id);
				BroadcastRoster(-1, value.Info.Id, "left");
				Net.RaisePlayerLeft(value.Info);
				PanelLink.MarkDirty("leave");
			}
		}
	}

	internal static void OnFrame(Conn c, string kind, JToken d)
	{
		if (!ByConn.TryGetValue(c.Id, out var value))
		{
			return;
		}
		if (value.State == PeerState.Handshake)
		{
			if (kind == "sys.hello")
			{
				if (!TryRead<MsgHello>(value, kind, d, out var msg))
				{
					Refuse(value, "protocol", "Bad join request.");
				}
				else
				{
					Hello(value, msg);
				}
			}
			else if (kind != "sys.ping")
			{
				Refuse(value, "protocol", "Expected a join request first.");
			}
			return;
		}
		switch (kind)
		{
		case "sys.ready":
		{
			if (TryRead<MsgReady>(value, kind, d, out var msg5))
			{
				Snapshot.OnReady(value, msg5);
			}
			return;
		}
		case "sys.resync":
		{
			if (TryRead<MsgResync>(value, kind, d, out var msg4))
			{
				DesyncReport.Receive(value.Info.Name, msg4?.reason ?? "asked", msg4?.report, msg4 != null && msg4.hold);
				if (msg4 == null || !msg4.hold)
				{
					Snapshot.OnResyncRequest(value, msg4?.reason ?? "asked");
				}
			}
			return;
		}
		case "sys.resyncnote":
		{
			if (TryRead<MsgResyncNote>(value, kind, d, out var msg2) && msg2 != null && Log.Allow("s" + value.Id + "|resyncnote", 5000L, out var skipped))
			{
				Log.Info("server: " + value.Info.Name + "'s reload (" + Text.Clip(msg2.reason, 120) + ") moved " + Math.Max(0, msg2.changed) + " item(s) on their side" + ((msg2.changed > 0) ? (": " + Text.Clip(msg2.items, 400)) : "") + Log.More(skipped));
			}
			return;
		}
		case "sys.echo":
		{
			if (TryRead<MsgEcho>(value, kind, d, out var msg3))
			{
				msg3 = msg3 ?? new MsgEcho();
				msg3.text = ((msg3.text == null) ? null : Text.Clip(msg3.text, 200));
				msg3.from = value.Id;
				msg3.serverMs = Clock.Ms;
				if (Log.Allow("s" + value.Id + "|echo", 1000L, out var skipped2))
				{
					Log.Info("server: echo from " + value.Info.Name + ": '" + msg3.text + "'" + Log.More(skipped2));
				}
				value.Conn.Send("sys.echo", msg3);
			}
			return;
		}
		}
		if (kind == "sys.ping")
		{
			return;
		}
		if (kind.StartsWith("sys.", StringComparison.Ordinal))
		{
			if (Log.Allow("s" + value.Id + "|sys|" + kind, 10000L, out var skipped3))
			{
				Log.Warn("server: unexpected " + kind + " from " + value.Info.Name + Log.More(skipped3));
			}
		}
		else if (value.State != PeerState.Ready)
		{
			if (Log.Allow("s" + value.Id + "|loading|" + kind, 10000L, out var skipped4))
			{
				Log.Info("server: " + kind + " from " + value.Info.Name + " while still loading: dropped" + Log.More(skipped4));
			}
		}
		else if (!ShopReady && !kind.StartsWith("bot.", StringComparison.Ordinal))
		{
			if (droppedWhileLoading.Count < 256 && droppedWhileLoading.Add(kind))
			{
				Log.Info("server: " + kind + " from " + value.Info.Name + " while the server loads a new run: dropped (logged once per kind per load)");
			}
		}
		else
		{
			Dispatch.OnServerFrame(value.Id, kind, d);
		}
	}

	private static bool TryRead<T>(Peer p, string kind, JToken d, out T msg) where T : class
	{
		try
		{
			msg = Wire.To<T>(d);
			return true;
		}
		catch (Exception ex)
		{
			msg = null;
			if (Log.Allow("c" + p.Conn.Id + "|bad|" + kind, 10000L, out var skipped))
			{
				Log.Warn("server: unreadable " + kind + " from " + (p.Info?.Name ?? p.Conn.Remote) + ": " + Text.Clip(ex.Message, 160) + Log.More(skipped));
			}
			p.Conn.Strike("unreadable " + kind);
			return false;
		}
	}

	private static string GreetingModVersion()
	{
		string text = ((Args.Bot != null) ? Args.Get("--br-hello-mod") : null);
		if (string.IsNullOrEmpty(text) || text.Length > 12)
		{
			return Mod.Version;
		}
		return text;
	}

	private static void Hello(Peer p, MsgHello h)
	{
		if (h == null)
		{
			Refuse(p, "protocol", "Bad join request.");
		}
		else if (Stopping)
		{
			Refuse(p, "stopping", "The server is stopping. Try again in a minute.");
		}
		else if (h.protocol != Protocol.Version)
		{
			Refuse(p, "protocol", "Version mismatch: the server runs " + Mod.Name + " protocol " + Protocol.Version + " (mod " + Mod.Version + "), you have protocol " + h.protocol + " (mod " + Text.Clip(h.mod ?? "?", 40) + "). Update the mod to match the server.");
		}
		else if (!string.Equals(h.game, Application.version, StringComparison.Ordinal))
		{
			Refuse(p, "game", "Game version mismatch: the server runs Probably Stolen " + Application.version + ", you run " + Text.Clip(h.game ?? "?", 40) + ". Update the game (or ask the owner to update the server).");
		}
		else if (Config.password.Length > 0 && !Auth.Matches(PasswordKey(), p.Nonce, h.auth))
		{
			Refuse(p, "password", "Wrong password.");
		}
		else
		{
			if (!BuildOk(p, h) || !ModsOk(p, h))
			{
				return;
			}
			if (!ShopReady)
			{
				Refuse(p, "starting", "The server is still loading the shop. Try again in a minute.");
				return;
			}
			if (!CrewCustomers.JoinOk(h.mod, out var why))
			{
				Refuse(p, "protocol", why);
				return;
			}
			if (PlayerCount >= Config.maxPlayers)
			{
				Refuse(p, "full", "The server is full (" + PlayerCount + "/" + Config.maxPlayers + " players).");
				return;
			}
			string text = CleanName(h.name);
			if (text.Length == 0)
			{
				text = "Player";
			}
			string text2 = text;
			int num = 2;
			while (NameTaken(text))
			{
				text = text2 + " (" + num + ")";
				num++;
			}
			ulong.TryParse(h.steamId ?? "", out var result);
			if (!Systems.Members.Admit(result, text, out var seat, out var why2))
			{
				Refuse(p, "full", why2);
				return;
			}
			p.Info = new PlayerInfo
			{
				Id = nextPlayerId++,
				Name = text,
				SteamId = result,
				Seat = seat
			};
			p.State = PeerState.Syncing;
			p.ModVersion = Text.Clip((h.mod ?? "").Trim(), 24);
			ByPlayer[p.Info.Id] = p;
			Net.Players[p.Info.Id] = p.Info;
			Log.Info("server: " + text + " (player " + p.Info.Id + ", " + p.Conn.Remote + ((result != 0L) ? (", steam " + result) : "") + ") joined; " + PlayerCount + "/" + Config.maxPlayers + " players");
			p.Conn.Send("sys.welcome", new MsgWelcome
			{
				you = p.Info.Id,
				server = Config.name,
				maxPlayers = Config.maxPlayers,
				players = PlayerList(),
				mods = (JToken)(object)ModList.ForWelcome(),
				modInfo = (JToken)(object)ModList.Describe(Config.WorkshopIdOf),
				build = GameBuild.Id,
				takeover = Locks.TakeoverSeconds,
				outings = Systems.Outings.Individual,
				ownership = Systems.Bays.Enabled
			});
			BroadcastRoster(p.Info.Id, -1, null);
			Net.RaisePlayerJoined(p.Info);
			Snapshot.SendTo(p.Info.Id, "join");
			PanelLink.MarkDirty("join");
		}
	}

	private static byte[] PasswordKey()
	{
		string text = Config?.password ?? "";
		if (text.Length == 0)
		{
			passwordKey = null;
			keyPassword = "";
			return null;
		}
		if (passwordKey != null && string.Equals(text, keyPassword, StringComparison.Ordinal))
		{
			return passwordKey;
		}
		bool flag = listening;
		long ms = Clock.Ms;
		passwordKey = Auth.DeriveKey(text, salt);
		keyPassword = text;
		Log.Info("server: password key ready (PBKDF2-SHA256, " + 20000 + " rounds, " + (Clock.Ms - ms) + " ms)" + (flag ? "; the password changed since the start" : ""));
		return passwordKey;
	}

	private static bool NameTaken(string n)
	{
		string b = Text.NameKey(n);
		foreach (Peer value in ByPlayer.Values)
		{
			if (string.Equals(Text.NameKey(value.Info.Name), b, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	internal static string CleanName(string n)
	{
		return Text.CleanName(n);
	}

	private static bool BuildOk(Peer p, MsgHello h)
	{
		string id = GameBuild.Id;
		string text = GameBuild.CleanId(h.build);
		string text2 = GameBuild.Clean(h.beta);
		if (text.Length == 0 || id.Length == 0 || text == id)
		{
			return true;
		}
		Log.Info("server: " + p.Conn.Remote + " runs a different game build (theirs " + text + ((text2.Length > 0) ? (", Steam beta '" + text2 + "'") : ", no beta") + ", ours " + id + ")" + ((text2.Length > 0) ? "" : "; let in with a warning"));
		if (text2.Length == 0)
		{
			return true;
		}
		Refuse(p, "build", "You are on Probably Stolen's beta (" + text2 + ") and this server runs a different build. In Steam, right-click Probably Stolen, open Properties > Betas and choose None, then join again.");
		return false;
	}

	private static bool ModsOk(Peer p, MsgHello h)
	{
		List<ModList.Info> list = ModList.ServerSet(Config.WorkshopIdOf);
		List<ModList.Info> list2 = ModList.Read(h.mods);
		if (list2 == null)
		{
			if (list.Count == 0)
			{
				return true;
			}
			Refuse(p, "mods", "This server runs mods (" + ModList.Names(list) + "). Get the same copy of " + Mod.Name + " as the host, then join again.");
			return false;
		}
		ModList.Diff diff = ModList.Compare(list, list2);
		if (diff.Missing.Count > 0 || (diff.Different.Count > 0 && Config.modsMustMatch))
		{
			Log.Info("server: " + p.Conn.Remote + " lacks mods: missing " + diff.Missing.Count + ", different " + diff.Different.Count + ((diff.Different.Count > 0 && !Config.modsMustMatch) ? " (another copy is let in; the missing ones are not)" : ""));
			if (!Config.modsMustMatch)
			{
				diff.Different.Clear();
			}
			Refuse(p, "mods", ModList.NeedsSentence(diff), (JToken)(object)ModList.NeedsList(diff));
			return false;
		}
		p.ModsNote = null;
		if (diff.Different.Count > 0)
		{
			p.ModsNote = ModList.DifferentLine(diff);
			Log.Info("server: " + p.Conn.Remote + " has another copy of " + ModList.Names(diff.Different) + "; let in with a warning (players must match copies only with modsMustMatch)");
		}
		if (diff.Extra.Count > 0 && Config.modsMustMatch)
		{
			Log.Info("server: " + p.Conn.Remote + " runs " + diff.Extra.Count + " mod(s) this server does not, and players must match");
			Refuse(p, "extramods", ModList.ExtraSentence(diff), (JToken)(object)ModList.ExtraList(diff));
			return false;
		}
		return true;
	}

	private static void Refuse(Peer p, string code, string reason, JToken mods)
	{
		Log.Info("server: refused " + p.Conn.Remote + " (" + code + "): " + reason);
		p.Conn.Send("sys.refuse", new MsgRefuse
		{
			code = code,
			reason = reason,
			mods = mods
		});
		p.Conn.CloseAfterFlush("refused: " + code);
	}

	private static void Refuse(Peer p, string code, string reason)
	{
		Log.Info("server: refused " + p.Conn.Remote + " (" + code + "): " + reason);
		p.Conn.Send("sys.refuse", new MsgRefuse
		{
			code = code,
			reason = reason
		});
		p.Conn.CloseAfterFlush("refused: " + code);
	}

	internal static List<PlayerInfo> PlayerList()
	{
		List<PlayerInfo> list = new List<PlayerInfo>();
		foreach (Peer value in ByPlayer.Values)
		{
			list.Add(value.Info);
		}
		list.Sort((PlayerInfo a, PlayerInfo b) => a.Id.CompareTo(b.Id));
		return list;
	}

	internal static void RosterChanged()
	{
		BroadcastRoster(-1, -1, null);
	}

	private static void BroadcastRoster(int joined, int left, string why)
	{
		MsgRoster payload = new MsgRoster
		{
			players = PlayerList(),
			joined = joined,
			left = left,
			why = why
		};
		byte[] frame = Wire.Encode("sys.roster", payload);
		foreach (Peer value in ByPlayer.Values)
		{
			value.Conn.Send(frame);
		}
	}

	/// Joins the host as player 1 over loopback, with no handshake or snapshot. Call after Start().
	internal static Peer AddHost(string name, ulong steamId)
	{
		Peer peer = new Peer
		{
			Conn = new Conn("host (this PC)"),
			State = PeerState.Ready,
			OpenedMs = Clock.Ms,
			ReadyMs = Clock.Ms,
			ModVersion = Mod.Version
		};
		string text = Text.Clip((name ?? "").Trim(), 24);
		if (text.Length == 0)
		{
			text = "Host";
		}
		peer.Info = new PlayerInfo
		{
			Id = nextPlayerId++,
			Name = text,
			SteamId = steamId
		};
		ByPlayer[peer.Info.Id] = peer;
		Net.Players[peer.Info.Id] = peer.Info;
		Net.LocalPlayer = peer.Info.Id;
		Log.Info("host: " + text + " is player " + peer.Info.Id + " on this server (loopback)");
		if (ByPlayer.Count > 1)
		{
			BroadcastRoster(peer.Info.Id, -1, null);
		}
		Net.RaisePlayerJoined(peer.Info);
		PanelLink.MarkDirty("join");
		return peer;
	}

	/// The host leaves its seat; the server keeps running.
	internal static bool RemoveHost(string why)
	{
		Peer peer = null;
		foreach (Peer value in ByPlayer.Values)
		{
			if (value.Conn != null && value.Conn.IsLoopback)
			{
				peer = value;
				break;
			}
		}
		if (peer == null || peer.Info == null)
		{
			return false;
		}
		ByPlayer.Remove(peer.Info.Id);
		Dispatch.Forget(peer.Info.Id);
		Log.ForgetPrefix("s" + peer.Info.Id + "|");
		Net.Players.Remove(peer.Info.Id);
		Log.Info("server: " + peer.Info.Name + " (player " + peer.Info.Id + ", the host) left: " + why + "; " + PlayerCount + "/" + Config.maxPlayers + " players; the server keeps running");
		Locks.ReleaseAll(peer.Info.Id);
		BroadcastRoster(-1, peer.Info.Id, "left");
		Net.RaisePlayerLeft(peer.Info);
		PanelLink.MarkDirty("leave");
		return true;
	}

	internal static void SendToJoined(byte[] frame, int except)
	{
		foreach (Peer value in ByPlayer.Values)
		{
			if (value.Info.Id != except && !value.Conn.IsLoopback)
			{
				value.Conn.Send(frame);
			}
		}
	}

	internal static void Tick()
	{
		long ms = Clock.Ms;
		List<Peer> list = null;
		foreach (Peer value in ByConn.Values)
		{
			if (value.State == PeerState.Handshake && ms - value.OpenedMs > 15000)
			{
				(list ?? (list = new List<Peer>())).Add(value);
			}
			else if (ms - value.Conn.LastRecvMs > 30000)
			{
				(list ?? (list = new List<Peer>())).Add(value);
			}
		}
		if (list != null)
		{
			foreach (Peer item in list)
			{
				item.Conn.Close((item.State == PeerState.Handshake) ? "no join request in 15 s" : "timed out (no data for 30 s)");
			}
		}
		if (ms - lastPingMs > 5000)
		{
			lastPingMs = ms;
			if (ByPlayer.Count > 0)
			{
				SendToJoined(Wire.Encode("sys.ping", new MsgPing
				{
					t = ms
				}), -1);
			}
		}
		if (ms - lastStatusMs > 2000)
		{
			lastStatusMs = ms;
			UpdateStatus();
		}
	}

	internal static void UpdateStatus()
	{
		if (Config != null)
		{
			Conn.StatusJson = ((JToken)PanelLink.Build(forPanel: false)).ToString((Formatting)0, Array.Empty<JsonConverter>());
		}
	}

	internal static bool Kick(int player)
	{
		if (!ByPlayer.TryGetValue(player, out var value))
		{
			return false;
		}
		if (value.Conn.IsLoopback)
		{
			Log.Info("server: the host cannot be kicked");
			return false;
		}
		Log.Info("server: removing " + value.Info.Name + " (player " + player + ", " + value.Conn.Remote + "): kicked by the server owner");
		value.Conn.Send("sys.refuse", new MsgRefuse
		{
			code = "kicked",
			reason = "Removed by the server owner."
		});
		value.Conn.CloseAfterFlush("kicked by the server owner");
		return true;
	}

	internal static void Goodbye(string reason)
	{
		byte[] frame = Wire.Encode("sys.bye", new MsgBye
		{
			reason = reason
		});
		foreach (Peer value in ByConn.Values)
		{
			value.Conn.Send(frame);
			value.Conn.CloseAfterFlush("server stopping");
		}
	}
}
