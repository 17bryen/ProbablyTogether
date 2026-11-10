using System;
using System.Collections.Generic;

namespace ProbablyTogether;

public static class Net
{
	private sealed class ApplyScope : IDisposable
	{
		private readonly int prevActor;

		private bool done;

		internal ApplyScope(int prev)
		{
			prevActor = prev;
		}

		public void Dispose()
		{
			if (!done)
			{
				done = true;
				applyDepth--;
				actorPlus1 = prevActor;
			}
		}
	}

	public static readonly Dictionary<int, PlayerInfo> Players = new Dictionary<int, PlayerInfo>();

	[ThreadStatic]
	private static int applyDepth;

	[ThreadStatic]
	private static int actorPlus1;

	public static Role Role { get; internal set; } = Role.Off;

	/// Authority that validates, applies and broadcasts requests: a dedicated server or a host.
	public static bool IsServer => Role == Role.Server || Role == Role.Host;

	/// Has a local player acting through requests. On a host, calls made inside a server request handler
	/// take the server path.
	public static bool IsClient => Role == Role.Client || (Role == Role.Host && !Dispatch.InRequest);

	public static bool IsHost => Role == Role.Host;

	public static bool IsDedicated => Role == Role.Server;

	public static bool IsRemote => Role == Role.Client;

	public static bool IsOnline => Role != Role.Off;

	public static int LocalPlayer { get; internal set; }

	public static bool Applying => applyDepth > 0;

	public static int Actor => actorPlus1 - 1;

	public static int Requester => Dispatch.Requester;

	public static string RequestKind => Dispatch.RequestKind;

	public static string LastRefuseCode => Client.LastRefuseCode;

	internal static List<ModList.Info> LastRefuseMods => Client.LastRefuseMods;

	public static string LastRefuseReason => Client.LastRefuseReason;

	public static string LastDeniedKind { get; internal set; }

	public static string LastDeniedReason { get; internal set; }

	public static ClientState ClientState => Client.State;

	public static bool IsLive
	{
		get
		{
			if (IsHost)
			{
				return Server.ShopReady;
			}
			if (!IsClient)
			{
				if (IsServer)
				{
					return Server.ShopReady;
				}
				return false;
			}
			return Client.State == ClientState.Ready;
		}
	}

	public static string ServerName
	{
		get
		{
			if (!IsServer)
			{
				return Client.ServerName;
			}
			return Server.Config?.name ?? "";
		}
	}

	public static string ServerAddress => Client.Address;

	public static int MaxPlayers
	{
		get
		{
			if (!IsServer)
			{
				return Client.MaxPlayers;
			}
			return Server.Config?.maxPlayers ?? 0;
		}
	}

	public static string LastDisconnectReason { get; private set; }

	public static event Action<string, string> Denied;

	public static event Action<PlayerInfo> PlayerJoined;

	public static event Action<PlayerInfo> PlayerLeft;

	public static event Action<string> Disconnected;

	public static IDisposable Apply()
	{
		applyDepth++;
		return new ApplyScope(actorPlus1);
	}

	public static IDisposable Apply(int actor)
	{
		ApplyScope result = new ApplyScope(actorPlus1);
		applyDepth++;
		actorPlus1 = ((actor >= 0) ? (actor + 1) : 0);
		return result;
	}

	public static void AddServerGate(Func<int, string, string> gate)
	{
		Dispatch.AddGate(gate);
	}

	public static void On<T>(string kind, Action<T, int> onServer, Action<T> onClient) where T : class
	{
		Dispatch.Register(kind, onServer, onClient);
	}

	public static void ToServer<T>(string kind, T msg) where T : class
	{
		Dispatch.SendToServer(kind, msg);
	}

	public static void ToAll<T>(string kind, T msg) where T : class
	{
		Dispatch.SendToAll(kind, msg, -1);
	}

	public static void ToAllExcept<T>(int player, string kind, T msg) where T : class
	{
		Dispatch.SendToAll(kind, msg, player);
	}

	public static void ToPlayer<T>(int player, string kind, T msg) where T : class
	{
		Dispatch.SendToPlayer(player, kind, msg);
	}

	public static bool IsPlayerLive(int player)
	{
		if (IsServer)
		{
			if (player != 0)
			{
				if (Server.ByPlayer.TryGetValue(player, out var value))
				{
					return value.State == Server.PeerState.Ready;
				}
				return false;
			}
			return true;
		}
		if (IsClient)
		{
			return Players.ContainsKey(player);
		}
		return false;
	}

	public static void Connect(string address, string password = "", string name = null)
	{
		if (IsServer)
		{
			Log.Warn("Connect called on the server");
		}
		else
		{
			Client.Connect(address, password, name);
		}
	}

	public static void Disconnect()
	{
		if (!IsServer)
		{
			Client.Disconnect("left");
		}
	}

	public static void Echo(string text)
	{
		if (IsClient)
		{
			Client.Send(Wire.Encode("sys.echo", new MsgEcho
			{
				text = text,
				sentMs = Clock.Ms
			}));
		}
	}

	public static string PlayerName(int id)
	{
		if (id != 0 || !IsServer)
		{
			if (!Players.TryGetValue(id, out var value))
			{
				return "player " + id;
			}
			return value.Name;
		}
		return "server";
	}

	internal static void EnterSession(int you, List<PlayerInfo> players)
	{
		Role = Role.Client;
		LocalPlayer = you;
		Players.Clear();
		if (players == null)
		{
			return;
		}
		foreach (PlayerInfo player in players)
		{
			Players[player.Id] = player;
		}
	}

	internal static void LeaveSession()
	{
		if (Role == Role.Client)
		{
			Role = Role.Off;
		}
		LocalPlayer = 0;
		Players.Clear();
		Locks.ClientClear();
	}

	internal static void ApplyRoster(MsgRoster r)
	{
		Dictionary<int, PlayerInfo> dictionary = new Dictionary<int, PlayerInfo>(Players);
		Players.Clear();
		if (r.players != null)
		{
			foreach (PlayerInfo player in r.players)
			{
				Players[player.Id] = player;
			}
		}
		foreach (PlayerInfo value in Players.Values)
		{
			if (!dictionary.ContainsKey(value.Id))
			{
				Log.Info("client: " + value.Name + " joined");
				RaisePlayerJoined(value);
			}
		}
		foreach (PlayerInfo value2 in dictionary.Values)
		{
			if (!Players.ContainsKey(value2.Id))
			{
				Log.Info("client: " + value2.Name + " left" + ((r.why != null) ? (" (" + r.why + ")") : ""));
				RaisePlayerLeft(value2);
			}
		}
	}

	internal static void RaisePlayerJoined(PlayerInfo p)
	{
		try
		{
			Net.PlayerJoined?.Invoke(p);
		}
		catch (Exception ex)
		{
			Log.Error("PlayerJoined handler: " + ex);
		}
	}

	internal static void RaisePlayerLeft(PlayerInfo p)
	{
		try
		{
			Net.PlayerLeft?.Invoke(p);
		}
		catch (Exception ex)
		{
			Log.Error("PlayerLeft handler: " + ex);
		}
	}

	internal static void RaiseDenied(string kind, string reason)
	{
		LastDeniedKind = kind ?? "";
		LastDeniedReason = reason ?? "";
		try
		{
			Net.Denied?.Invoke(LastDeniedKind, LastDeniedReason);
		}
		catch (Exception ex)
		{
			Log.Error("Denied handler: " + ex);
		}
	}

	internal static void RaiseDisconnected(string why)
	{
		LastDisconnectReason = why;
		try
		{
			Net.Disconnected?.Invoke(why);
		}
		catch (Exception ex)
		{
			Log.Error("Disconnected handler: " + ex);
		}
	}
}
