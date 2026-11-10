using System;
using System.Linq;
using System.Threading;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class VoiceA : VoiceBotBase
{
	private int b;

	private int sent0;

	private string stats0 = "";

	private int stalls0;

	private int staleMax;

	private int staleChecks;

	private int preStale;

	private int preLead;

	private int outBefore;

	private bool slept;

	public override string Name => "voice-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		Prelude("a", "b");
		Do("roles", delegate
		{
			b = VoiceBotBase.Other();
			return "I am " + Net.PlayerName(Net.LocalPlayer) + " (" + Net.LocalPlayer + "), the other is " + Net.PlayerName(b) + " (" + b + ")";
		});
		WaitMark("b-listen1");
		Talk("talk1", 660f, 3f);
		Mark("a-talk1-done");
		ListenStart("listen2", () => b);
		Mark("a-listen2");
		SeeMark("mark2", () => b, "talking");
		WaitMark("b-talk2-done");
		Heard("heard2", () => b, 440f, 3f, (double r) => r > 0.08, keepRms: true);
		Do("stall-at", delegate
		{
			VoiceStream voiceStream = S(b);
			stalls0 = voiceStream.Stalls;
			voiceStream.OutReset();
			staleMax = 0;
			staleChecks = 0;
			slept = false;
			return "stalls so far " + stalls0;
		});
		Mark("a-stall");
		WaitFor("stall-playing", 15f, () => S(b).Playing && S(b).Speaking(Time.realtimeSinceStartup), () => "b playing " + S(b).Playing);
		Step("stall", 20f, delegate(BotStep st)
		{
			VoiceStream voiceStream = S(b);
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (voiceStream.Playing)
			{
				int lead;
				int num = voiceStream.ClipStale(out lead);
				staleChecks++;
				if (num > staleMax)
				{
					staleMax = num;
				}
				if (!slept)
				{
					preLead = lead;
				}
			}
			if (!slept && st.Elapsed >= 0.8f)
			{
				slept = true;
				outBefore = voiceStream.OutLoud;
				preStale = staleMax;
				Thread.Sleep(1200);
				return StepStatus.Running;
			}
			if (!slept || voiceStream.Speaking(realtimeSinceStartup) || voiceStream.Queued > 0)
			{
				return StepStatus.Running;
			}
			st.Detail = "clip checked " + staleChecks + " times while playing: most samples outside the fresh lead that were not silent " + staleMax + " (before the stall " + preStale + ", lead then " + VoiceBotBase.F((double)preLead * 1000.0 / 16000.0, "F0") + " ms); stalls " + (voiceStream.Stalls - stalls0) + "; Unity output blocks with sound before the stall " + outBefore + ", after " + (voiceStream.OutLoud - outBefore) + ", underruns " + voiceStream.Underruns;
			return (staleMax == 0 && staleChecks >= 10 && voiceStream.Stalls > stalls0 && voiceStream.OutLoud - outBefore >= 3) ? StepStatus.Pass : StepStatus.Fail;
		}).TimeoutDetail = () => "b never stopped talking";
		WaitMark("b-talk2b-done");
		Do("mute", delegate
		{
			VoiceBotBase.Option("voice_mute_" + b).setBool(obj: true);
			VoiceStream voiceStream = S(b);
			pkt0 = voiceStream.PacketsIn;
			played0 = (int)voiceStream.Played;
			muted0 = voiceStream.PacketsMuted;
			voiceStream.OutReset();
			return "muted " + Net.PlayerName(b) + " via Options; muted now " + Voice.IsMuted(b) + " (" + Voice.MutedCount + " muted)";
		});
		Check("row-muted", () => VoiceBotBase.RowSays(b, "muted"), () => "row '" + VoiceBotBase.Row(b) + "'");
		Mark("a-muted");
		SeeMark("mark3", () => b, "muted, talking");
		WaitMark("b-talk3-done");
		Step("muted-silent", 10f, delegate(BotStep st)
		{
			VoiceStream voiceStream = S(b);
			if (voiceStream.Speaking(Time.realtimeSinceStartup))
			{
				return StepStatus.Running;
			}
			int num = voiceStream.PacketsIn - pkt0;
			int num2 = voiceStream.PacketsMuted - muted0;
			int num3 = (int)(voiceStream.Played - played0);
			st.Detail = "while muted: " + num + " frames arrived, " + num2 + " muted, " + num3 + " samples played; Unity output blocks with sound " + voiceStream.OutLoud + " of " + voiceStream.OutSnaps;
			return (num >= 80 && num2 == num && num3 == 0 && voiceStream.OutLoud == 0) ? StepStatus.Pass : StepStatus.Fail;
		});
		Do("unmute", delegate
		{
			VoiceBotBase.Option("voice_mute_" + b).setBool(obj: false);
			return "unmuted; muted now " + Voice.IsMuted(b);
		});
		Check("row-unmuted", () => !VoiceBotBase.RowSays(b, "muted"), () => "row '" + VoiceBotBase.Row(b) + "'");
		Do("volume-half", delegate
		{
			VoiceBotBase.Option("voice_volume").setFloat(0.5f);
			return "voice volume " + Voice.Volume;
		});
		ListenStart("listen4", () => b);
		Mark("a-vol");
		WaitMark("b-talk4-done");
		Heard("heard4", () => b, 440f, 2f, (double r) => VoiceBotBase.rms2 > 0.0 && r / VoiceBotBase.rms2 > 0.4 && r / VoiceBotBase.rms2 < 0.6, keepRms: false, (double o) => VoiceBotBase.outRms2 > 0.0 && o / VoiceBotBase.outRms2 > 0.4 && o / VoiceBotBase.outRms2 < 0.6);
		Do("volume-full", delegate
		{
			VoiceBotBase.Option("voice_volume").setFloat(1f);
			return "voice volume " + Voice.Volume;
		});
		Do("voice-off", delegate
		{
			VoiceBotBase.Option("voice_on").setBool(obj: false);
			VoiceStream voiceStream = S(b);
			msgs0 = voiceStream.MessagesIn;
			return "voice " + Voice.Enabled;
		});
		Cmd("server-sees-off", () => "listen:" + Net.LocalPlayer, (string r) => r == "0");
		Cmd("stats5", () => "stats", (string r) => true, 20f, delegate(string r)
		{
			stats0 = r;
		});
		Mark("a-off");
		Do("talk-while-off", delegate
		{
			sent0 = Voice.SentMessages;
			Voice.FakeToneHz = 500f;
			Voice.FakeTalk = true;
			return "talk switch held while voice is off";
		});
		WaitMark("b-talk5-done");
		Delay("drain5", 1f);
		Step("off-silent", 0f, delegate(BotStep st)
		{
			Voice.FakeTalk = false;
			VoiceStream voiceStream = S(b);
			st.Detail = "messages from " + Net.PlayerName(b) + " while off: " + (voiceStream.MessagesIn - msgs0) + "; messages sent while off: " + (Voice.SentMessages - sent0) + "; talking " + Voice.Talking;
			return (voiceStream.MessagesIn == msgs0 && Voice.SentMessages == sent0 && !Voice.Talking) ? StepStatus.Pass : StepStatus.Fail;
		});
		Cmd("server-skipped", () => "stats", (string r) => VoiceBotNet.StatValue(r, "notListening") > VoiceBotNet.StatValue(stats0, "notListening"));
		Do("voice-on", delegate
		{
			VoiceBotBase.Option("voice_on").setBool(obj: true);
			return "voice " + Voice.Enabled;
		});
		Cmd("server-sees-on", () => "listen:" + Net.LocalPlayer, (string r) => r == "1");
		Cmd("stats5b", () => "stats", (string r) => true, 20f, delegate(string r)
		{
			stats0 = r;
		});
		Do("flip-flood", delegate
		{
			for (int i = 0; i < 40; i++)
			{
				Net.ToServer("voice.state", new MsgVoiceState
				{
					listen = (i % 2 == 1)
				});
			}
			return "40 raw voice.state messages in one frame, off/on alternately, ending on";
		});
		Delay("flip-settle", 1f);
		Cmd("flips-quiet", () => "stats", (string r) => VoiceBotNet.StatValue(r, "stateChanges") - VoiceBotNet.StatValue(stats0, "stateChanges") >= 39 && VoiceBotNet.StatValue(r, "stateLines") - VoiceBotNet.StatValue(stats0, "stateLines") <= 1);
		Cmd("flips-end-on", () => "listen:" + Net.LocalPlayer, (string r) => r == "1");
		Check("flips-connected", () => Net.IsLive && Net.LastRefuseCode == null, () => "live " + Net.IsLive + ", refused " + (Net.LastRefuseCode ?? "no"));
		Cmd("stats6a", () => "stats", (string r) => true, 20f, delegate(string r)
		{
			stats0 = r;
		});
		Do("spoof-at", delegate
		{
			VoiceStream voiceStream = S(b);
			pkt0 = voiceStream.PacketsIn;
			msgs0 = voiceStream.MessagesIn;
			muted0 = voiceStream.PacketsBad;
			return "from b so far: " + msgs0 + " messages, " + pkt0 + " frames, " + muted0 + " bad";
		});
		Mark("a-spoof");
		WaitMark("b-spoof-done");
		WaitFor("spoof-arrived", 5f, () => S(b).MessagesIn > msgs0, () => "messages from b since: " + (S(b).MessagesIn - msgs0) + "; a stream under my own id: " + (S(Net.LocalPlayer) != null));
		Step("spoof-stamped", 0f, delegate(BotStep st)
		{
			VoiceStream voiceStream = S(b);
			int num = voiceStream.MessagesIn - msgs0;
			int num2 = voiceStream.PacketsIn - pkt0;
			int num3 = voiceStream.PacketsBad - muted0;
			st.Detail = "b's frame with p=" + Net.LocalPlayer + " (my id) arrived as b's: " + num + " message, " + num2 + " frame, " + num3 + " bad; a stream under my own id: " + (S(Net.LocalPlayer) != null);
			return (num == 1 && num2 == 1 && num3 == 0 && S(Net.LocalPlayer) == null) ? StepStatus.Pass : StepStatus.Fail;
		});
		Cmd("server-stamped", () => "stats", (string r) => VoiceBotNet.StatValue(r, "stamped") - VoiceBotNet.StatValue(stats0, "stamped") == 1 && VoiceBotNet.StatValue(r, "rate") == 0 && VoiceBotNet.StatValue(r, "relayed") > VoiceBotNet.StatValue(stats0, "relayed"));
		Cmd("stats6", () => "stats", (string r) => true, 20f, delegate(string r)
		{
			stats0 = r;
		});
		Do("listen6-at", delegate
		{
			VoiceStream voiceStream = S(b);
			pkt0 = voiceStream.PacketsIn;
			msgs0 = voiceStream.MessagesIn;
			return "frames from b so far " + pkt0;
		});
		Mark("a-caps");
		WaitMark("b-caps-done");
		Delay("drain6", 1.5f);
		Step("capped", 0f, delegate(BotStep st)
		{
			int num = S(b).PacketsIn - pkt0;
			st.Detail = "b sent 240 frames in one go plus junk; " + num + " frames reached me (the cap is a " + 60.0 + "-frame burst plus " + 55.0 + "/s); a stream under my own id: " + (S(Net.LocalPlayer) != null) + "; b still live " + Net.IsPlayerLive(b);
			return ((double)num <= 75.0 && num >= 1 && S(Net.LocalPlayer) == null && Net.IsPlayerLive(b)) ? StepStatus.Pass : StepStatus.Fail;
		});
		Cmd("server-dropped", () => "stats", (string r) => VoiceBotNet.StatValue(r, "rate") - VoiceBotNet.StatValue(stats0, "rate") >= 150 && VoiceBotNet.StatValue(stats0, "rate") == 0 && VoiceBotNet.StatValue(r, "bad") - VoiceBotNet.StatValue(stats0, "bad") >= 4);
		Cmd("server-off", () => "off", (string r) => r == "ok off");
		WaitFor("off-here", 10f, () => !Voice.ServerOn, () => "server on " + Voice.ServerOn);
		Check("off-line", () => Chat.Lines.Any((Chat.Line l) => l.Text.Contains("Voice chat was turned off on this server.")), () => "last chat '" + ((Chat.Lines.Count > 0) ? Chat.Lines[Chat.Lines.Count - 1].Text : "") + "'");
		Do("listen7-at", delegate
		{
			VoiceStream voiceStream = S(b);
			msgs0 = voiceStream.MessagesIn;
			return "messages from b so far " + msgs0;
		});
		Mark("a-srv-off");
		WaitMark("b-srv-off-done");
		Check("nothing-while-off", () => S(b).MessagesIn == msgs0, () => "messages from b while the server was off: " + (S(b).MessagesIn - msgs0));
		Cmd("server-json", () => "json", (string r) => r.StartsWith("ok on", StringComparison.Ordinal));
		WaitFor("on-here", 10f, () => Voice.ServerOn, () => "server on " + Voice.ServerOn);
		Check("hint-again", () => Chat.Lines.Count > 0 && Chat.Lines.Skip(Math.Max(0, Chat.Lines.Count - 3)).Any((Chat.Line l) => l.Text.Contains("to talk to the shop.")), () => "last chat '" + ((Chat.Lines.Count > 0) ? Chat.Lines[Chat.Lines.Count - 1].Text : "") + "'");
		Mark("a-srv-on");
		Mark("a-done");
	}
}
