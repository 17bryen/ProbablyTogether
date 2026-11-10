using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using ProbablyTogether.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ProbablyTogether;

internal static class PanelLink
{
	internal const string CommandsFile = "commands.txt";

	internal const string StatusFile = "status.json";

	private const int MaxCommandBytes = 65536;

	private const int MaxLinesPerPass = 50;

	private const int MaxLineChars = 300;

	private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private static float nextPoll;

	private static long nextPeriodicMs;

	private static string pendingWhy;

	private static int writtenDay = -1;

	private static string writtenState = "";

	private static bool writeFailLogged;

	private static bool readFailLogged;

	internal static long LastSaveUnix;

	internal static int CommandsRun;

	internal static string LastCommand = "";

	internal static string LastResult = "";

	internal static int StatusWrites;

	internal static string LastStatusWhy = "";

	internal static string DataFolder => (Paths.Root ?? "").Replace('/', Path.DirectorySeparatorChar);

	internal static void MarkDirty(string why)
	{
		if (Net.IsServer)
		{
			pendingWhy = ((pendingWhy == null) ? why : (pendingWhy.Contains(why) ? pendingWhy : (pendingWhy + ", " + why)));
			Server.UpdateStatus();
		}
	}

	internal static void NoteSaved(string what)
	{
		LastSaveUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		MarkDirty("save");
	}

	internal static void NoteSavedAt(DateTime utc)
	{
		try
		{
			LastSaveUnix = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
		}
		catch
		{
		}
	}

	internal static void Tick()
	{
		if (!Net.IsServer || Server.Config == null)
		{
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (realtimeSinceStartup >= nextPoll)
		{
			nextPoll = realtimeSinceStartup + 1f;
			if (!ServerMode.Quitting)
			{
				PollCommands();
			}
		}
		string statusState = ServerMode.StatusState;
		int day = Game.Day;
		if (statusState != writtenState)
		{
			MarkDirty("state " + statusState);
		}
		else if (day > 0 && writtenDay > 0 && day != writtenDay)
		{
			MarkDirty("day change");
		}
		if (pendingWhy != null || Clock.Ms >= nextPeriodicMs)
		{
			WriteNow(pendingWhy);
		}
	}

	internal static JObject Build(bool forPanel)
	{
		ServerConfig config = Server.Config;
		bool flag = (config?.password?.Length).GetValueOrDefault() > 0;
		JArray val = new JArray();
		foreach (PlayerInfo item in Server.PlayerList())
		{
			val.Add((JToken)((forPanel || !flag) ? new JObject
			{
				["id"] = (JToken)(item.Id),
				["name"] = (JToken)(item.Name ?? "")
			} : new JObject { ["id"] = (JToken)(item.Id) }));
		}
		PlayerStore val2 = (Game.ShopLoaded ? Game.Store : null);
		string text = "";
		if (val2 != null)
		{
			try
			{
				text = val2.startType.ToString();
			}
			catch
			{
			}
		}
		JObject val3 = new JObject
		{
			["state"] = (JToken)(ServerMode.StatusState),
			["name"] = (JToken)(config?.name ?? ""),
			["players"] = (JToken)(object)val,
			["playerCount"] = (JToken)(((JContainer)val).Count),
			["maxPlayers"] = (JToken)(config?.maxPlayers ?? 0),
			["day"] = (JToken)(Game.Day),
			["cash"] = (JToken)(val2?.playerCash ?? 0),
			["scenario"] = (JToken)(text),
			["ending"] = (JToken)(ServerMode.Ending ?? "")
		};
		if (forPanel)
		{
			val3["lastSave"] = (JToken)(LastSaveUnix);
		}
		val3["version"] = (JToken)(Mod.Version);
		val3["protocol"] = (JToken)(2);
		val3["gameVersion"] = (JToken)(Application.version);
		val3["passworded"] = (JToken)(flag);
		if (forPanel)
		{
			val3["data"] = (JToken)(DataFolder);
		}
		return val3;
	}

	internal static void WriteNow(string why)
	{
		nextPeriodicMs = Clock.Ms + 10000;
		Server.UpdateStatus();
		string text = ((JToken)Build(forPanel: true)).ToString((Formatting)0, Array.Empty<JsonConverter>());
		string text2 = Paths.Under("status.json");
		string text3 = text2 + ".tmp";
		try
		{
			File.WriteAllText(text3, text + "\n", Utf8NoBom);
			if (File.Exists(text2))
			{
				File.Replace(text3, text2, null);
			}
			else
			{
				File.Move(text3, text2);
			}
		}
		catch (Exception ex)
		{
			if (!writeFailLogged)
			{
				writeFailLogged = true;
				Log.Warn("server: could not write status.json (" + ex.Message + "); retrying every second");
			}
			nextPeriodicMs = Clock.Ms + 1000;
			if (pendingWhy == null)
			{
				pendingWhy = why ?? "retry";
			}
			return;
		}
		if (writeFailLogged)
		{
			writeFailLogged = false;
			Log.Info("server: status.json written again");
		}
		StatusWrites++;
		pendingWhy = null;
		writtenState = ServerMode.StatusState;
		writtenDay = Game.Day;
		if (why != null)
		{
			LastStatusWhy = why;
			Log.Info("server: status.json written (" + why + "): " + text);
		}
		else
		{
			LastStatusWhy = "every 10 s";
		}
	}

	private static void PollCommands()
	{
		string text = Paths.Under("commands.txt");
		string text2 = text + ".run";
		List<string> list;
		try
		{
			if (!File.Exists(text2))
			{
				if (!File.Exists(text) || new FileInfo(text).Length == 0L)
				{
					return;
				}
				File.Move(text, text2);
			}
			list = ReadLines(text2);
			File.Delete(text2);
			if (!File.Exists(text))
			{
				using (new FileStream(text, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
				{
				}
			}
			readFailLogged = false;
		}
		catch (Exception ex)
		{
			if (!readFailLogged)
			{
				readFailLogged = true;
				Log.Warn("server: could not take commands.txt (" + ex.Message + "); retrying every second");
			}
			return;
		}
		foreach (string item in list)
		{
			RunLogged(item);
		}
	}

	private static List<string> ReadLines(string file)
	{
		List<string> list = new List<string>();
		byte[] array = File.ReadAllBytes(file);
		int num = Math.Min(array.Length, 65536);
		if (array.Length > 65536)
		{
			Log.Warn("server: commands.txt was " + array.Length + " bytes; only the first " + 65536 + " are read");
		}
		int num2 = ((num >= 3 && array[0] == 239 && array[1] == 187 && array[2] == 191) ? 3 : 0);
		string[] array2 = Encoding.UTF8.GetString(array, num2, num - num2).Split('\n');
		for (int i = 0; i < array2.Length; i++)
		{
			string text = array2[i].TrimEnd('\r').Trim();
			if (text.Length != 0)
			{
				if (list.Count >= 50)
				{
					Log.Warn("server: commands.txt had more than " + 50 + " lines; the rest were dropped");
					break;
				}
				list.Add(text);
			}
		}
		return list;
	}

	private static void RunLogged(string line)
	{
		string text = Printable(line);
		string text2;
		try
		{
			text2 = Run(line);
		}
		catch (Exception ex)
		{
			text2 = "failed: " + ex.Message;
			Log.Error("server: command '" + text + "' threw: " + ex);
		}
		CommandsRun++;
		LastCommand = text;
		LastResult = text2;
		Log.Info("command: " + text + " -> " + text2);
	}

	internal static string Run(string line)
	{
		line = (line ?? "").Trim();
		if (line.Length > 300)
		{
			line = line.Substring(0, 300);
		}
		int num = line.IndexOf(' ');
		string text = ((num < 0) ? line : line.Substring(0, num)).ToLowerInvariant();
		string text2 = ((num < 0) ? "" : line.Substring(num + 1).Trim());
		if (ServerMode.Quitting && text != "")
		{
			return "refused: the server is stopping";
		}
		switch (text)
		{
		case "say":
			return Say(text2);
		case "kick":
			return Kick(text2);
		case "save":
			if (text2.Length != 0)
			{
				return "unknown command";
			}
			return ServerMode.SaveCommand();
		case "newrun":
			if (text2.Length == 0)
			{
				return ServerMode.NewRunCommand(force: false);
			}
			if (text2.Equals("force", StringComparison.OrdinalIgnoreCase))
			{
				return ServerMode.NewRunCommand(force: true);
			}
			return "unknown command";
		default:
			return "unknown command";
		}
	}

	private static string Say(string text)
	{
		string text2 = Chat.Clean(text);
		if (text2.Length == 0)
		{
			return "nothing to say";
		}
		Chat.AnnounceOwner(text2);
		int playerCount = Server.PlayerCount;
		return "sent to " + playerCount + " player" + ((playerCount == 1) ? "" : "s") + ((text2.Length < text.Trim().Length) ? (" (trimmed to " + text2.Length + " characters)") : "");
	}

	private static string Kick(string who)
	{
		if (who.Length == 0)
		{
			return "kick whom? (a player id or exact name)";
		}
		PlayerInfo playerInfo = null;
		if (int.TryParse(who, NumberStyles.None, CultureInfo.InvariantCulture, out var result) && Net.Players.TryGetValue(result, out var value))
		{
			playerInfo = value;
		}
		if (playerInfo == null)
		{
			foreach (PlayerInfo item in Server.PlayerList())
			{
				if (string.Equals(item.Name, who, StringComparison.Ordinal))
				{
					playerInfo = item;
					break;
				}
			}
		}
		if (playerInfo == null)
		{
			foreach (PlayerInfo item2 in Server.PlayerList())
			{
				if (string.Equals(item2.Name, who, StringComparison.OrdinalIgnoreCase))
				{
					playerInfo = item2;
					break;
				}
			}
		}
		if (playerInfo == null)
		{
			return "no such player (" + Server.PlayerCount + " connected)";
		}
		if (!Server.Kick(playerInfo.Id))
		{
			return "no such player";
		}
		return "kicked " + playerInfo.Name + " (player " + playerInfo.Id + ")";
	}

	private static string Printable(string s)
	{
		StringBuilder stringBuilder = new StringBuilder(Math.Min(s.Length, 300));
		foreach (char c in s)
		{
			stringBuilder.Append(char.IsControl(c) ? ' ' : c);
			if (stringBuilder.Length >= 300)
			{
				break;
			}
		}
		return stringBuilder.ToString();
	}
}
