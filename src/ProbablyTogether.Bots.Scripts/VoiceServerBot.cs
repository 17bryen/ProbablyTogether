using System;
using System.IO;
using System.Linq;
using System.Text;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class VoiceServerBot : VoiceBotBase
{
	public override string Name => "voice-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop", 300f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene);
		Check("lab-channel", () => BotRunner.Enabled, () => "voice.bot registered: " + BotRunner.Enabled);
		Check("switch-default", () => Voice.ServerOn, () => "server.json voice: " + Voice.ServerOn);
		Step("switch-file", 0f, delegate(BotStep st)
		{
			string path = Path.Combine(Args.Data ?? ".", "voice-switch-test.json");
			(string, bool)[] obj = new(string, bool)[9]
			{
				(null, true),
				("{}", true),
				("{\"voice\": true}", true),
				("{\"voice\": false}", false),
				("{\"Voice\": false}", false),
				("{\"voice\": 0}", false),
				("{\"voice\": \"off\"}", false),
				("{\"voice\": \"maybe\"}", true),
				("{\"voice\": fals", true)
			};
			StringBuilder stringBuilder = new StringBuilder();
			bool flag = true;
			(string, bool)[] array = obj;
			for (int i = 0; i < array.Length; i++)
			{
				(string, bool) tuple = array[i];
				try
				{
					if (File.Exists(path))
					{
						File.Delete(path);
					}
					if (tuple.Item1 != null)
					{
						File.WriteAllText(path, tuple.Item1);
					}
				}
				catch (Exception ex)
				{
					st.Detail = "cannot write the test file: " + ex.Message;
					return StepStatus.Fail;
				}
				string why;
				bool flag2 = Voice.ReadSwitch(path, out why);
				flag &= flag2 == tuple.Item2;
				stringBuilder.Append(tuple.Item1 ?? "(no file)").Append(" -> ").Append(flag2 ? "on" : "off")
					.Append((flag2 == tuple.Item2) ? "" : " WRONG")
					.Append("; ");
			}
			try
			{
				File.Delete(path);
			}
			catch
			{
			}
			st.Detail = stringBuilder.ToString();
			return flag ? StepStatus.Pass : StepStatus.Fail;
		});
		WaitFor("two-live", 300f, () => Net.Players.Keys.Count(Net.IsPlayerLive) >= 2, () => Net.Players.Count + " player(s)");
		Do("log-line", delegate
		{
			Chat.Announce("Voice lab: today's log must never hold a voice frame.");
			return "a system chat line, so today's log is not empty (" + DayLog.Count + " message(s))";
		});
		WaitFor("clients-done", 600f, () => VoiceBotNet.HasMark("a-done") && VoiceBotNet.HasMark("b-done"), () => VoiceBotNet.Stats());
		Step("counters", 0f, delegate(BotStep st)
		{
			st.Detail = VoiceBotNet.Stats();
			return (Voice.SrvRelayed > 0 && Voice.SrvDroppedRate >= 100 && Voice.SrvDroppedBad >= 4 && Voice.SrvDroppedOff >= 1 && Voice.SrvNotListening >= 1 && Voice.SrvStamped >= 1 && Voice.SrvStateChanges >= 40 && Voice.SrvStateLines <= 4) ? StepStatus.Pass : StepStatus.Fail;
		});
		Step("not-in-day-log", 0f, delegate(BotStep st)
		{
			int num = 0;
			int num2 = 0;
			foreach (byte[] frame in DayLog.Frames)
			{
				num++;
				if (frame != null && frame.Length >= 4)
				{
					Wire.ParseHeader(frame, out var length, out var gz);
					byte[] bytes = (gz ? Wire.Gunzip(frame.Skip(4).ToArray(), 67108864) : frame.Skip(4).Take(length).ToArray());
					if (Encoding.UTF8.GetString(bytes).Contains("\"k\":\"voice."))
					{
						num2++;
					}
				}
			}
			string path = Path.Combine(Args.Data ?? ".", "daylog.jsonl");
			int num3 = 0;
			string text2;
			try
			{
				if (File.Exists(path))
				{
					string text;
					using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
					{
						using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
						text = streamReader.ReadToEnd();
					}
					num3 = CountOf(text, "\"k\":\"voice.");
					text2 = text.Length + " chars";
				}
				else
				{
					text2 = "no file";
				}
			}
			catch (Exception ex)
			{
				text2 = "unreadable: " + ex.Message;
			}
			st.Detail = "today's log: " + num + " message(s), " + num2 + " voice; daylog.jsonl " + text2 + ", " + num3 + " voice";
			return (num2 == 0 && num3 == 0 && num > 0 && text2.EndsWith("chars", StringComparison.Ordinal)) ? StepStatus.Pass : StepStatus.Fail;
		});
	}

	private static int CountOf(string s, string what)
	{
		int num = 0;
		int startIndex = 0;
		while ((startIndex = s.IndexOf(what, startIndex, StringComparison.Ordinal)) >= 0)
		{
			num++;
			startIndex += what.Length;
		}
		return num;
	}
}
