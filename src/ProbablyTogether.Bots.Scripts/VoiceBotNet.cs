using System;
using System.Collections.Generic;
using System.Globalization;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public static class VoiceBotNet
{
	private static int nextId;

	private static readonly Dictionary<int, string> replies = new Dictionary<int, string>();

	private static readonly HashSet<string> marks = new HashSet<string>();

	private static bool registered;

	[SystemInit(Order = 10)]
	internal static void Init()
	{
		if (registered || !BotRunner.Enabled)
		{
			return;
		}
		registered = true;
		Net.On<MsgVoiceBot>("voice.bot", ServerOnCmd, null);
		Net.On("voice.bot.r", null, delegate(MsgVoiceBotReply m)
		{
			if (m != null && !replies.ContainsKey(m.id))
			{
				replies[m.id] = m.r ?? "";
			}
		});
	}

	public static int Send(string cmd, int id = 0)
	{
		if (id == 0)
		{
			id = ++nextId;
		}
		Net.ToServer("voice.bot", new MsgVoiceBot
		{
			id = id,
			cmd = cmd
		});
		return id;
	}

	public static string Take(int id)
	{
		if (!replies.TryGetValue(id, out var value))
		{
			return null;
		}
		replies.Remove(id);
		return value;
	}

	internal static bool HasMark(string m)
	{
		return marks.Contains(m);
	}

	private static void ServerOnCmd(MsgVoiceBot m, int from)
	{
		string text = m?.cmd ?? "";
		string r;
		if (text.StartsWith("mark:", StringComparison.Ordinal))
		{
			marks.Add(text.Substring(5));
			r = "ok";
		}
		else if (text.StartsWith("has:", StringComparison.Ordinal))
		{
			r = (marks.Contains(text.Substring(4)) ? "yes" : "no");
		}
		else
		{
			switch (text)
			{
			case "off":
				Voice.LabSetServerOn(false);
				r = "ok off";
				break;
			case "on":
				Voice.LabSetServerOn(true);
				r = "ok on";
				break;
			case "json":
				Voice.LabSetServerOn(null);
				r = "ok " + (Voice.ServerOn ? "on" : "off") + " (server.json)";
				break;
			case "stats":
				r = Stats();
				break;
			default:
			{
				r = ((!text.StartsWith("listen:", StringComparison.Ordinal)) ? ("unknown " + text) : ((!int.TryParse(text.Substring(7), out var result)) ? "?" : (Voice.LabListens(result) ? "1" : "0")));
				break;
			}
			}
		}
		Net.ToPlayer(from, "voice.bot.r", new MsgVoiceBotReply
		{
			id = (m?.id ?? 0),
			r = r
		});
	}

	internal static string Stats()
	{
		return "messages=" + Voice.SrvMessages + " packets=" + Voice.SrvPackets + " relayed=" + Voice.SrvRelayed + " rate=" + Voice.SrvDroppedRate + " bad=" + Voice.SrvDroppedBad + " off=" + Voice.SrvDroppedOff + " loading=" + Voice.SrvDroppedLoading + " notListening=" + Voice.SrvNotListening + " configs=" + Voice.SrvConfigs + " stamped=" + Voice.SrvStamped + " stateChanges=" + Voice.SrvStateChanges + " stateLines=" + Voice.SrvStateLines;
	}

	internal static int StatValue(string stats, string key)
	{
		string[] array = (stats ?? "").Split(' ');
		foreach (string text in array)
		{
			int num = text.IndexOf('=');
			if (num > 0 && text.Substring(0, num) == key && int.TryParse(text.Substring(num + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				return result;
			}
		}
		return -1;
	}
}
