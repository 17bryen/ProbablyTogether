using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ProbablyTogether;

/// Host mode: the host's requests go straight into the server dispatch. Broadcasts are not applied to the host;
/// only presentation and refusal kinds reach its client handlers.
internal static class HostLoop
{
	private struct Pending
	{
		public string Kind;
		public string Json;
	}

	private static readonly Queue<Pending> inbox = new Queue<Pending>();

	/// Client handlers that are safe to run on the host: they present or relay, they do not change shop state.
	private static readonly HashSet<string> wanted = new HashSet<string>(StringComparer.Ordinal)
	{
		"chat.line", "chat.notice", "presence.cursor", "presence.state", "ping.show", "ping.no",
		"voice.frame", "voice.config", "crew.tally", "help.checked",
		"customers.refused", "dayNight.refused", "dayNight.readyChanged", "dayNight.newRunVotes",
		"dialogue.refused", "econ.refused", "production.refused", "sec.refused", "items.reject", "items.use.refused", "machine.reject",
		"outing.go", "spec.refused", "perks.refused", "repick.offer"
	};

	internal static int Delivered;

	internal static int Dropped;

	internal static int Sent;

	internal static bool Wants(string kind)
	{
		return kind != null && (wanted.Contains(kind) || kind.StartsWith("bot.", StringComparison.Ordinal));
	}

	/// A request from the host's own player: served on this frame by the server dispatch.
	internal static void ToServer(string kind, object msg)
	{
		if (!Net.IsHost)
		{
			return;
		}
		string json;
		Wire.Encode(kind, msg, out json);
		JToken val;
		try
		{
			val = JObject.Parse(json)["d"];
		}
		catch (Exception ex)
		{
			Log.Warn("host: could not re-read " + kind + " for the server: " + ex.Message);
			return;
		}
		Sent++;
		Dispatch.OnServerFrame(Net.LocalPlayer, kind, val);
	}

	/// Something the server sent to the host (a broadcast or a message to the host player).
	internal static void FromServer(string kind, string json)
	{
		if (!Net.IsHost)
		{
			return;
		}
		if (kind == "sys.denied" || Wants(kind))
		{
			inbox.Enqueue(new Pending
			{
				Kind = kind,
				Json = json
			});
		}
		else
		{
			Dropped++;
		}
	}

	/// sys.* frames the server code sends straight to the host peer's connection.
	internal static void Sys(string kind, object payload)
	{
		switch (kind)
		{
		case "sys.refuse":
		{
			MsgRefuse msgRefuse = payload as MsgRefuse;
			Log.Warn("host: the server tried to refuse the host (" + (msgRefuse?.code ?? "?") + ": " + (msgRefuse?.reason ?? "") + "); ignored");
			break;
		}
		case "sys.welcome":
		case "sys.roster":
		case "sys.ping":
		case "sys.bye":
		case "sys.snapshot":
		case "sys.replay":
		case "sys.lock":
		case "sys.digest":
			break;
		default:
			if (Log.Allow("host|sys|" + kind, 10000L, out var skipped))
			{
				Log.Info("host: " + kind + " to the host peer: nothing to do" + Log.More(skipped));
			}
			break;
		}
	}

	/// Delivered once a frame from Core.Tick, after the network inbox, so a server handler never re-enters a client handler.
	internal static void Drain()
	{
		int num = 0;
		while (inbox.Count > 0 && num++ < 1000)
		{
			Pending pending = inbox.Dequeue();
			JToken val;
			try
			{
				val = JObject.Parse(pending.Json)["d"];
			}
			catch (Exception ex)
			{
				Log.Warn("host: could not re-read " + pending.Kind + ": " + ex.Message);
				continue;
			}
			Delivered++;
			if (pending.Kind == "sys.denied")
			{
				MsgDenied msgDenied = ((val != null) ? Wire.To<MsgDenied>(val) : null);
				if (msgDenied != null)
				{
					if (Log.Allow("c|denied|" + msgDenied.kind, 2000L, out var skipped))
					{
						Log.Info("host: the server refused " + Text.Clip(msgDenied.kind, 64) + ": " + Text.Clip(msgDenied.reason, 300) + Log.More(skipped));
					}
					Net.RaiseDenied(msgDenied.kind, msgDenied.reason);
				}
			}
			else
			{
				Dispatch.OnClientFrame(pending.Kind, val);
			}
		}
	}

	internal static string Describe()
	{
		return "sent " + Sent + ", delivered " + Delivered + ", dropped " + Dropped + ", queued " + inbox.Count;
	}
}
