using System;
using System.Globalization;
using System.IO;
using System.Text;
using ProbablyTogether.Bots;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ProbablyTogether;

internal static class Announce
{
	internal const string Url = "https://api.survivalservers.com/probably_together/v1/announce";

	private const float EverySeconds = 60f;

	private const int TimeoutSeconds = 8;

	private const int MaxAnswerBytes = 16384;

	private static UnityWebRequest request;

	private static float nextAt = -1f;

	private static bool decided;

	private static bool allowed;

	private static string lastResult;

	internal static int Sent;

	internal static string LastResult => lastResult ?? "";

	[SystemTick]
	private static void Tick()
	{
		if (!Net.IsServer)
		{
			return;
		}
		if (request != null)
		{
			Poll();
		}
		else if (Server.Listening && !Server.Stopping && !ServerMode.Quitting && Server.Config != null && Allowed())
		{
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (!(nextAt >= 0f) || !(realtimeSinceStartup < nextAt))
			{
				nextAt = realtimeSinceStartup + 60f;
				Send(Server.Config.port);
			}
		}
	}

	private static bool Allowed()
	{
		if (decided)
		{
			return allowed;
		}
		decided = true;
		string text = null;
		string why;
		if (!string.IsNullOrEmpty(Args.BotArg))
		{
			text = "--br-bot is set";
		}
		else if (!string.IsNullOrEmpty(Args.SelfTest))
		{
			text = "--br-selftest is set";
		}
		else if (BotRunner.LabAllowed(out why))
		{
			text = "the data folder is a lab folder";
		}
		allowed = text == null;
		Log.Info(allowed ? ("server: server list: announcing TCP " + Server.Config.port + " to the public server list every " + 60 + " s") : ("server: server list: not announced (" + text + ")"));
		return allowed;
	}

	private static void Send(int port)
	{
		UnityWebRequest val = null;
		try
		{
			byte[] bytes = Encoding.UTF8.GetBytes("{\"port\":" + port.ToString(CultureInfo.InvariantCulture) + ",\"v\":1}");
			val = new UnityWebRequest("https://api.survivalservers.com/probably_together/v1/announce", "POST")
			{
				uploadHandler = (UploadHandler)new UploadHandlerRaw(bytes)
				{
					contentType = "application/json"
				},
				downloadHandler = (DownloadHandler)new DownloadHandlerBuffer(),
				timeout = 8
			};
			val.SetRequestHeader("Content-Type", "application/json");
			val.SetRequestHeader("User-Agent", "ProbablyTogether/0.2.8");
			val.SendWebRequest();
			request = val;
			Sent++;
		}
		catch (Exception ex)
		{
			try
			{
				if (val != null)
				{
					val.Dispose();
				}
			}
			catch
			{
			}
			request = null;
			Note("failed: " + Text.Clip(ex.Message, 160));
		}
	}

	private static void Poll()
	{
		UnityWebRequest val = request;
		if (!val.isDone)
		{
			if (val.downloadedBytes > 16384)
			{
				try
				{
					val.Abort();
					return;
				}
				catch
				{
					return;
				}
			}
			return;
		}
		request = null;
		string result;
		try
		{
			object text;
			if (val.downloadedBytes == 0 || val.downloadedBytes > 16384)
			{
				text = null;
			}
			else
			{
				DownloadHandler downloadHandler = val.downloadHandler;
				text = ((downloadHandler != null) ? downloadHandler.text : null);
			}
			JObject val2 = ParseAnswer((string)text);
			result = ((val2 != null && ((JToken)val2).Value<bool?>((object)"ok") == true) ? "listed" : ((val2 != null && val2["ok"] != null) ? ("refused: " + Text.Clip(((JToken)val2).Value<string>((object)"why") ?? "no reason given", 160) + " (HTTP " + val.responseCode + ")") : (((int)val.result != 1) ? ("failed: " + Text.Clip(val.error ?? ((object)val.result).ToString(), 160) + ((val.responseCode > 0) ? (" (HTTP " + val.responseCode + ")") : "")) : ("failed: unexpected answer (HTTP " + val.responseCode + ")"))));
		}
		catch (Exception ex)
		{
			result = "failed: " + Text.Clip(ex.Message, 160);
		}
		finally
		{
			try
			{
				val.Dispose();
			}
			catch
			{
			}
		}
		Note(result);
	}

	private static JObject ParseAnswer(string text)
	{
		if (string.IsNullOrEmpty(text) || text.Length > 16384)
		{
			return null;
		}
		try
		{
			using StringReader stringReader = new StringReader(text);
			JsonTextReader val = new JsonTextReader((TextReader)stringReader)
			{
				MaxDepth = 8,
				DateParseHandling = (DateParseHandling)0
			};
			try
			{
				JToken obj = JToken.ReadFrom((JsonReader)(object)val);
				return (JObject)(object)((obj is JObject) ? obj : null);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		catch
		{
			return null;
		}
	}

	private static void Note(string result)
	{
		if (!string.Equals(result, lastResult, StringComparison.Ordinal))
		{
			bool flag = lastResult == null;
			lastResult = result;
			if (result == "listed")
			{
				Log.Info("server: server list: " + (flag ? "announced" : "announced again") + "; the website lists the server once its STATUS check answers");
				return;
			}
			Log.Warn("server: server list: announce " + result + "; trying again every " + 60 + " s");
		}
	}
}
