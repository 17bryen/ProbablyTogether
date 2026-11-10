using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public abstract class VoiceBotBase : BotScript
{
	private int sentPk0;

	private int sentMsg0;

	private long sentB0;

	private bool sawOwnMark;

	protected int pkt0;

	protected int played0;

	protected int muted0;

	protected int msgs0;

	protected int und0;

	protected bool sawMark;

	protected static double rms2;

	protected static double outRms2;

	protected static string F(double v, string fmt = "F1")
	{
		return v.ToString(fmt, CultureInfo.InvariantCulture);
	}

	protected BotStep Cmd(string name, Func<string> cmd, Func<string, bool> ok, float timeout = 20f, Action<string> keep = null)
	{
		int id = 0;
		float sentAt = 0f;
		string text = "";
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			string text2 = VoiceBotNet.Take(id);
			if (text2 == null)
			{
				if (st.Elapsed - sentAt > 3f)
				{
					sentAt = st.Elapsed;
					VoiceBotNet.Send(text, id);
				}
				return StepStatus.Running;
			}
			keep?.Invoke(text2);
			st.Detail = text + " -> " + text2;
			return ok(text2) ? StepStatus.Pass : StepStatus.Fail;
		}, delegate
		{
			text = cmd();
			id = VoiceBotNet.Send(text);
			sentAt = 0f;
		});
		botStep.TimeoutDetail = () => "no answer to '" + text + "'";
		return botStep;
	}

	protected void Mark(string mark)
	{
		Cmd("mark-" + mark, () => "mark:" + mark, (string r) => r == "ok");
	}

	protected void WaitMark(string mark, float timeout = 120f)
	{
		int id = -1;
		float last = -10f;
		Step("wait-" + mark, timeout, delegate(BotStep st)
		{
			if (id >= 0)
			{
				string text = VoiceBotNet.Take(id);
				if (text != null)
				{
					id = -1;
					if (text == "yes")
					{
						st.Detail = "mark " + mark + " set (" + F(st.Elapsed) + " s)";
						return StepStatus.Pass;
					}
				}
			}
			if (id < 0 && st.Elapsed - last >= 0.5f)
			{
				last = st.Elapsed;
				id = VoiceBotNet.Send("has:" + mark);
			}
			return StepStatus.Running;
		}).TimeoutDetail = () => "mark " + mark + " never set";
	}

	protected static int Other()
	{
		foreach (int key in Net.Players.Keys)
		{
			if (key != Net.LocalPlayer)
			{
				return key;
			}
		}
		return -1;
	}

	protected static string Row(int id)
	{
		if (!Presence.RosterTexts.TryGetValue(id, out var value))
		{
			return "(no row)";
		}
		return value;
	}

	protected static bool RowSays(int id, string word)
	{
		return Row(id).EndsWith("| " + word, StringComparison.Ordinal);
	}

	protected static GameOptionRegistry.Entry Option(string id)
	{
		return GameOptionRegistry.Find(ModHelper.GameOptionId("bellun_probably_together", id));
	}

	protected void Prelude(string me, string other)
	{
		Do("output-probe", delegate
		{
			VoiceStream.OutputProbe = true;
			return "reading what Unity's mixer plays from each voice AudioSource (GetOutputData, every 0.1 s)";
		});
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer);
		WaitFor("shop-live", 240f, () => Ui.ShopOnline, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
		WaitFor("two-live", 300f, () => Net.Players.Count == 2 && Net.IsPlayerLive(Net.LocalPlayer) && Net.IsPlayerLive(Other()) && Presence.RosterTexts.Count == 2, () => Net.Players.Count + " player(s)");
		WaitFor("server-voice-on", 30f, () => Voice.ServerOn, () => "server says on: " + Voice.ServerOn);
		Check("codec", () => Voice.CodecCheck.StartsWith("ok", StringComparison.Ordinal), () => Voice.CodecCheck);
		Check("talk-hint", () => Chat.Lines.Any((Chat.Line l) => l.Text.Contains("Hold " + Voice.TalkKeyName + " to talk to the shop.")), () => "chat: " + string.Join(" / ", from l in Chat.Lines
			select l.Text into t
			where t.Contains("talk") || t.Contains("chat")
			select t));
		WaitFor("option-rows", 5f, () => Option("voice_on") != null && Option("voice_volume") != null && Option("voice_key") != null && Option("voice_mute_" + Other()) != null, () => "voice_on " + (Option("voice_on") != null) + ", volume " + (Option("voice_volume") != null) + ", key " + (Option("voice_key") != null) + ", mute row for " + Other() + " " + (Option("voice_mute_" + Other()) != null));
		Check("talk-key", delegate
		{
			GameOptionRegistry.Entry val = Option("voice_key");
			if (val == null || (int)val.getKey() != 118)
			{
				return false;
			}
			foreach (GameOptionRegistry.Section section in GameOptionRegistry.Sections)
			{
				foreach (GameOptionRegistry.Entry entry in section.entries)
				{
					if ((int)entry.kind == 3 && entry != val && entry.getKey != null && (int)entry.getKey() == 118)
					{
						return false;
					}
				}
			}
			return true;
		}, delegate
		{
			List<string> list = new List<string>();
			foreach (GameOptionRegistry.Section section2 in GameOptionRegistry.Sections)
			{
				foreach (GameOptionRegistry.Entry entry2 in section2.entries)
				{
					if ((int)entry2.kind == 3 && entry2.getKey != null)
					{
						list.Add(entry2.id.Substring(entry2.id.LastIndexOf(':') + 1) + "=" + ((object)entry2.getKey()).ToString());
					}
				}
			}
			return "talk key " + Voice.TalkKeyName + "; every key bind row: " + string.Join(", ", list);
		});
		Mark(me + "-ready");
		WaitMark(other + "-ready");
	}

	protected void Talk(string name, float hz, float seconds, bool checkSent = true)
	{
		Step(name, seconds + 15f, delegate(BotStep st)
		{
			if (st.Elapsed < seconds)
			{
				if (RowSays(Net.LocalPlayer, "talking"))
				{
					sawOwnMark = true;
				}
				return StepStatus.Running;
			}
			Voice.FakeTalk = false;
			if (Voice.Talking)
			{
				return StepStatus.Running;
			}
			int num = Voice.SentPackets - sentPk0;
			int num2 = Voice.SentMessages - sentMsg0;
			long num3 = Voice.SentBytes - sentB0;
			double num4 = (double)(num * 20) / 1000.0;
			double num5 = ((num4 > 0.0) ? ((double)num3 / num4) : 0.0);
			st.Detail = "talked " + F(seconds) + " s of " + F(hz, "F0") + " Hz: sent " + num2 + " messages, " + num + " frames (" + F(num4, "F2") + " s of audio), " + num3 + " bytes on the wire = " + F(num5, "F0") + " bytes/s; own roster mark seen " + sawOwnMark;
			if (!checkSent)
			{
				return StepStatus.Pass;
			}
			return ((double)num >= (double)(seconds * 50f) * 0.8 && (double)num <= (double)(seconds * 50f) * 1.25 + 10.0 && num5 > 0.0 && num5 < 5000.0 && sawOwnMark) ? StepStatus.Pass : StepStatus.Fail;
		}, delegate
		{
			sentPk0 = Voice.SentPackets;
			sentMsg0 = Voice.SentMessages;
			sentB0 = Voice.SentBytes;
			sawOwnMark = false;
			Voice.FakeToneHz = hz;
			Voice.FakeTalk = true;
		}).TimeoutDetail = () => "still talking";
	}

	internal VoiceStream S(int p)
	{
		return Voice.StreamOf(p);
	}

	protected void ListenStart(string name, Func<int> speaker)
	{
		Do(name, delegate
		{
			VoiceStream voiceStream = S(speaker());
			pkt0 = voiceStream?.PacketsIn ?? 0;
			played0 = (int)(voiceStream?.Played ?? 0);
			muted0 = voiceStream?.PacketsMuted ?? 0;
			msgs0 = voiceStream?.MessagesIn ?? 0;
			und0 = voiceStream?.Underruns ?? 0;
			voiceStream?.TapReset();
			voiceStream?.OutReset();
			return "ready for " + Net.PlayerName(speaker()) + ": packets so far " + pkt0 + ", played samples " + played0;
		});
	}

	protected void SeeMark(string name, Func<int> speaker, string word, float timeout = 20f)
	{
		WaitFor(name, timeout, () => RowSays(speaker(), word), () => "row '" + Row(speaker()) + "'");
	}

	protected void Heard(string name, Func<int> who, float hz, float seconds, Func<double, bool> rmsOk, bool keepRms = false, Func<double, bool> outRmsOk = null)
	{
		Step(name, 10f, delegate(BotStep st)
		{
			int num = who();
			VoiceStream voiceStream = S(num);
			if (voiceStream == null)
			{
				st.Detail = "no stream from player " + num;
				if (!(st.Elapsed > 5f))
				{
					return StepStatus.Running;
				}
				return StepStatus.Fail;
			}
			if (voiceStream.Queued > 0 || voiceStream.Speaking(Time.realtimeSinceStartup))
			{
				return StepStatus.Running;
			}
			int num2 = voiceStream.PacketsIn - pkt0;
			int num3 = (int)(voiceStream.Played - played0);
			int num4 = voiceStream.Underruns - und0;
			float[] array = voiceStream.TapRecent(16000);
			int num5 = Math.Min(array.Length / 4, 1600);
			double num6 = VoiceMeter.Rms(array, num5, array.Length - num5);
			float num7 = VoiceMeter.Dominant(array, num5, array.Length - num5, 16000, 200, 2000, 5);
			int outRate = voiceStream.OutRate;
			double outPeakRms = voiceStream.OutPeakRms;
			double num8 = ((voiceStream.OutLoud > 0) ? (voiceStream.OutLoudRmsSum / (double)voiceStream.OutLoud) : 0.0);
			float num9 = ((voiceStream.OutPeak != null && outRate > 0) ? VoiceMeter.Dominant(voiceStream.OutPeak, 0, voiceStream.OutPeak.Length, outRate, 200, 2000, 5) : 0f);
			if (keepRms)
			{
				rms2 = num6;
				outRms2 = outPeakRms;
			}
			bool flag = Voice.UnityPlayback && !voiceStream.VirtualClock;
			st.Detail = "from " + Net.PlayerName(num) + ": " + num2 + " frames, " + (voiceStream.MessagesIn - msgs0) + " messages, played " + num3 + " samples (" + F((double)num3 / 16000.0, "F2") + " s), tap rms " + F(num6, "F3") + ", strongest " + F(num7, "F0") + " Hz (sent " + F(hz, "F0") + "), bad " + voiceStream.PacketsBad + ", underruns " + num4 + ", stalls " + voiceStream.Stalls + "; Unity output: " + voiceStream.OutSnaps + " blocks of " + 4096 + " read, " + voiceStream.OutLoud + " with sound, loudest rms " + F(outPeakRms, "F3") + " (mean " + F(num8, "F3") + "; other channel " + F(voiceStream.OutPeakRms1, "F3") + "), strongest " + F(num9, "F0") + " Hz at a " + outRate + " Hz mixer, " + ((object)AudioSettings.speakerMode).ToString() + ", game master volume " + F(AudioListener.volume, "F2") + ", mean/expected " + F((num6 > 0.0) ? (num8 / (num6 * (double)AudioListener.volume * 0.7071)) : 0.0, "F2") + ", loudest/expected " + F((num6 > 0.0) ? (outPeakRms / (num6 * (double)AudioListener.volume * 0.7071)) : 0.0, "F2") + ((voiceStream.OutProblem.Length > 0) ? (", read failed: " + voiceStream.OutProblem) : "") + "; playback " + (flag ? "Unity audio (play head moved)" : ((Voice.NoAudioOutput || voiceStream.VirtualClock) ? "by the clock (this copy has no audio output)" : "not started"));
			bool num10 = (double)num2 >= (double)(seconds * 50f) * 0.8 && (double)num3 >= (double)(seconds * 16000f) * 0.75 && rmsOk(num6) && Math.Abs(num7 - hz) <= 15f && voiceStream.PacketsBad == 0;
			double num11 = num6 * (double)AudioListener.volume * 0.7071;
			bool flag2 = flag && (float)voiceStream.OutLoud >= seconds * 4f && num11 > 0.005 && num8 / num11 > 0.75 && num8 / num11 < 1.3 && outPeakRms / num11 > 0.8 && outPeakRms / num11 < 1.45 && Math.Abs(voiceStream.OutPeakRms1 - outPeakRms) < 0.1 * outPeakRms && Math.Abs(num9 - hz) <= 15f && (outRmsOk == null || outRmsOk(outPeakRms));
			return (num10 && flag2 && num4 <= 3) ? StepStatus.Pass : StepStatus.Fail;
		}).TimeoutDetail = () => "the stream never finished playing";
		WaitFor(name + "-mark-gone", 3f, () => !RowSays(who(), "talking"), () => "row '" + Row(who()) + "'");
	}
}
