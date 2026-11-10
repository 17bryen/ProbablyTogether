using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ProbablyTogether;

internal static class Dispatch
{
	internal sealed class Handler
	{
		public Type Type;

		public Action<object, int> OnServer;

		public Action<object> OnClient;
	}

	private sealed class Budget
	{
		public double Tokens = 100.0;

		public long LastMs = Clock.Ms;

		public long RefuseUntilMs;

		public int Charged;
	}

	internal static readonly Dictionary<string, Handler> Handlers = new Dictionary<string, Handler>();

	private static readonly HashSet<string> warned = new HashSet<string>();

	private const int MaxWarned = 256;

	private static readonly List<Func<int, string, string>> gates = new List<Func<int, string, string>>();

	private static long gateErrorMs = -100000L;

	private const long LogEveryMs = 10000L;

	internal const double BroadcastsPerSec = 10.0;

	internal const double BroadcastBurst = 100.0;

	internal const int BusyRefuseMs = 10000;

	internal const string BusyReason = "You are doing too much at once. Wait a few seconds, then carry on.";

	private static readonly Dictionary<int, Budget> budgets = new Dictionary<int, Budget>();

	private static int requester = -1;

	private static string requestKind = "";

	internal static int BudgetTrips;

	internal static int BudgetRefused;

	internal static int Requester => requester;

	internal static bool InRequest => requester > 0;

	internal static string RequestKind => requestKind;

	internal static int GateCount => gates.Count;

	internal static void AddGate(Func<int, string, string> gate)
	{
		if (gate != null && !gates.Contains(gate))
		{
			gates.Add(gate);
		}
	}

	internal static string Gate(int player, string kind)
	{
		for (int i = 0; i < gates.Count; i++)
		{
			string text;
			try
			{
				text = gates[i](player, kind);
			}
			catch (Exception ex)
			{
				long ms = Clock.Ms;
				if (ms - gateErrorMs > 10000)
				{
					gateErrorMs = ms;
					Log.Error("server: gate " + gates[i].Method.DeclaringType?.Name + "." + gates[i].Method.Name + " threw on " + kind + " (request let through): " + ex);
				}
				continue;
			}
			if (!string.IsNullOrEmpty(text))
			{
				if (text.Length <= 300)
				{
					return text;
				}
				return text.Substring(0, 300);
			}
		}
		return null;
	}

	internal static void Register<T>(string kind, Action<T, int> onServer, Action<T> onClient) where T : class
	{
		if (string.IsNullOrEmpty(kind) || kind.Length > 64)
		{
			throw new ArgumentException("bad message kind '" + kind + "'");
		}
		if (kind.StartsWith("sys.", StringComparison.Ordinal))
		{
			throw new ArgumentException("sys.* kinds belong to the core: " + kind);
		}
		if (Handlers.ContainsKey(kind))
		{
			throw new InvalidOperationException("duplicate message kind " + kind);
		}
		Handlers[kind] = new Handler
		{
			Type = typeof(T),
			OnServer = ((onServer == null) ? null : ((Action<object, int>)delegate(object o, int p)
			{
				onServer((T)o, p);
			})),
			OnClient = ((onClient == null) ? null : ((Action<object>)delegate(object o)
			{
				onClient((T)o);
			}))
		};
	}

	internal static void SendToServer(string kind, object msg)
	{
		if (Net.IsHost)
		{
			HostLoop.ToServer(kind, msg);
		}
		else if (!Net.IsClient || !Client.Connected)
		{
			WarnOnce("send:" + kind, "ToServer(" + kind + ") while not connected: dropped");
		}
		else
		{
			Client.Send(Wire.Encode(kind, msg));
		}
	}

	internal static void SendToAll(string kind, object msg, int except)
	{
		if (!Net.IsServer)
		{
			if (Net.IsClient)
			{
				WarnOnce("all:" + kind, "ToAll(" + kind + ") called on a client: only the server broadcasts");
			}
			return;
		}
		string json;
		byte[] frame = Wire.Encode(kind, msg, out json);
		except = DayLog.Record(kind, json, frame, except);
		int num = ((Net.Actor > 0) ? Net.Actor : requester);
		if (num > 0)
		{
			Charge(num, kind);
		}
		if (Server.PlayerCount != 0)
		{
			Server.SendToJoined(frame, except);
		}
		if (Net.IsHost && except != Net.LocalPlayer)
		{
			HostLoop.FromServer(kind, json);
		}
	}

	private static void Charge(int player, string kind)
	{
		long ms = Clock.Ms;
		if (!budgets.TryGetValue(player, out var value))
		{
			value = (budgets[player] = new Budget());
		}
		value.Tokens = Math.Min(100.0, value.Tokens + (double)(ms - value.LastMs) * 10.0 / 1000.0);
		value.LastMs = ms;
		value.Charged++;
		if (value.Tokens >= 1.0)
		{
			value.Tokens -= 1.0;
		}
		else if (ms >= value.RefuseUntilMs)
		{
			value.RefuseUntilMs = ms + 10000;
			BudgetTrips++;
			Log.Warn("server: " + Net.PlayerName(player) + " (player " + player + ") caused over " + 10.0 + " logged broadcasts a second (burst " + 100.0 + ", last " + kind + "); their requests are refused for " + 10 + " s; today's log has " + DayLog.Count + " message(s)");
		}
	}

	internal static bool Busy(int player)
	{
		if (budgets.TryGetValue(player, out var value))
		{
			return Clock.Ms < value.RefuseUntilMs;
		}
		return false;
	}

	internal static void SendToPlayer(int player, string kind, object msg)
	{
		Server.Peer value;
		if (!Net.IsServer)
		{
			if (Net.IsClient)
			{
				WarnOnce("one:" + kind, "ToPlayer(" + kind + ") called on a client: dropped");
			}
		}
		else if (Net.IsHost && player == Net.LocalPlayer)
		{
			string json;
			Wire.Encode(kind, msg, out json);
			HostLoop.FromServer(kind, json);
		}
		else if (Server.ByPlayer.TryGetValue(player, out value))
		{
			value.Conn.Send(Wire.Encode(kind, msg));
		}
	}

	internal static void OnServerFrame(int player, string kind, JToken d)
	{
		if (!Handlers.TryGetValue(kind, out var value) || value.OnServer == null)
		{
			if (Log.Allow("p" + player + "|nohandler|" + kind, 10000L, out var skipped))
			{
				Log.Warn("server: no handler for " + kind + " (from " + Net.PlayerName(player) + ")" + Log.More(skipped));
			}
			return;
		}
		if (Busy(player))
		{
			BudgetRefused++;
			Deny(player, kind, "You are doing too much at once. Wait a few seconds, then carry on.", "over the broadcast budget");
			return;
		}
		if (gates.Count > 0)
		{
			string text = Gate(player, kind);
			if (text != null)
			{
				Deny(player, kind, text, "refused by a gate");
				return;
			}
		}
		object obj;
		try
		{
			obj = ((value.Type == typeof(JToken)) ? d : Wire.To(d, value.Type));
		}
		catch (Exception ex)
		{
			if (Log.Allow("p" + player + "|unreadable|" + kind, 10000L, out var skipped2))
			{
				Log.Warn("server: unreadable " + kind + " from " + Net.PlayerName(player) + ": " + Text.Clip(ex.Message, 200) + Log.More(skipped2));
			}
			Strike(player, "unreadable " + kind);
			return;
		}
		if (obj == null)
		{
			if (Log.Allow("p" + player + "|empty|" + kind, 10000L, out var skipped3))
			{
				Log.Warn("server: empty " + kind + " from " + Net.PlayerName(player) + Log.More(skipped3));
			}
			return;
		}
		int num = requester;
		string text2 = requestKind;
		requester = player;
		requestKind = kind;
		try
		{
			value.OnServer(obj, player);
		}
		catch (Exception ex2)
		{
			if (Log.Allow("p" + player + "|threw|" + kind, 10000L, out var skipped4))
			{
				Log.Error("server: handler " + kind + " from " + Net.PlayerName(player) + " threw" + Log.More(skipped4) + ": " + ex2);
			}
			Strike(player, kind + " handler threw");
		}
		finally
		{
			requester = num;
			requestKind = text2;
		}
	}

	private static void Deny(int player, string kind, string why, string what)
	{
		if (Log.Allow("p" + player + "|denied|" + kind, 10000L, out var skipped))
		{
			Log.Info("server: " + kind + " from " + Net.PlayerName(player) + " " + what + ": " + why + Log.More(skipped));
		}
		SendToPlayer(player, "sys.denied", new MsgDenied
		{
			kind = kind,
			reason = why
		});
	}

	private static void Strike(int player, string why)
	{
		if (Server.ByPlayer.TryGetValue(player, out var value))
		{
			value.Conn.Strike(why);
		}
	}

	internal static void Forget(int player)
	{
		budgets.Remove(player);
		Log.ForgetPrefix("p" + player + "|");
	}

	internal static void OnClientFrame(string kind, JToken d)
	{
		if (Systems.Outings.DropsWhilePrivate(kind))
		{
			return;
		}
		if (!Handlers.TryGetValue(kind, out var value) || value.OnClient == null)
		{
			WarnOnce("cli?" + kind, "client: no handler for " + kind);
			return;
		}
		object obj;
		try
		{
			obj = ((value.Type == typeof(JToken)) ? d : Wire.To(d, value.Type));
		}
		catch (Exception ex)
		{
			if (Log.Allow("c|unreadable|" + kind, 10000L, out var skipped))
			{
				Log.Warn("client: unreadable " + kind + ": " + Text.Clip(ex.Message, 200) + Log.More(skipped));
			}
			return;
		}
		if (obj == null)
		{
			return;
		}
		try
		{
			value.OnClient(obj);
		}
		catch (Exception ex2)
		{
			if (Log.Allow("c|threw|" + kind, 10000L, out var skipped2))
			{
				Log.Error("client: handler " + kind + " threw" + Log.More(skipped2) + ": " + ex2);
			}
		}
	}

	private static void WarnOnce(string key, string text)
	{
		if (warned.Count >= 256)
		{
			if (warned.Count == 256 && warned.Add("\0full"))
			{
				Log.Warn("further one-time warnings are not logged (" + 256 + " distinct already)");
			}
		}
		else if (warned.Add(key))
		{
			Log.Warn(text);
		}
	}
}
