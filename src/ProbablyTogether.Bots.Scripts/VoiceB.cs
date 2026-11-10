using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using Concentus.Structs;

namespace ProbablyTogether.Bots.Scripts;

public sealed class VoiceB : VoiceBotBase
{
	private int a;

	private int sent0;

	private int deniedBefore;

	public override string Name => "voice-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		Prelude("b", "a");
		Do("roles", delegate
		{
			a = VoiceBotBase.Other();
			return "I am " + Net.PlayerName(Net.LocalPlayer) + " (" + Net.LocalPlayer + "), the other is " + Net.PlayerName(a) + " (" + a + ")";
		});
		ListenStart("listen1", () => a);
		Mark("b-listen1");
		SeeMark("mark1", () => a, "talking");
		WaitMark("a-talk1-done");
		Heard("heard1", () => a, 660f, 3f, (double r) => r > 0.08);
		WaitMark("a-listen2");
		Talk("talk2", 440f, 3f);
		Mark("b-talk2-done");
		WaitMark("a-stall");
		Talk("talk2b", 440f, 3f);
		Mark("b-talk2b-done");
		WaitMark("a-muted");
		Talk("talk3", 550f, 2f);
		Mark("b-talk3-done");
		WaitMark("a-vol");
		Talk("talk4", 440f, 2f);
		Mark("b-talk4-done");
		WaitMark("a-off");
		Talk("talk5", 440f, 2f);
		Mark("b-talk5-done");
		WaitMark("a-spoof");
		Do("spoof", delegate
		{
			short[] array = new short[320];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = (short)(9000.0 * Math.Sin(Math.PI * 600.0 * (double)i / 16000.0));
			}
			byte[] inArray = VoiceCodec.Encode(VoiceCodec.NewEncoder(), array, 0, new byte[640]);
			Net.ToServer("voice.frame", new MsgVoiceFrame
			{
				p = a,
				s = 8000,
				f = new string[1] { Convert.ToBase64String(inArray) },
				e = true
			});
			return "one 300 Hz frame with p=" + a + " (a's id) sent under the cap";
		});
		Mark("b-spoof-done");
		WaitMark("a-caps");
		Step("flood", 0f, delegate(BotStep st)
		{
			deniedBefore = Notices.DeniedShown;
			OpusEncoder enc = VoiceCodec.NewEncoder();
			short[] array = new short[320];
			byte[] scratch = new byte[640];
			double num = 0.0;
			List<string> list = new List<string>();
			for (int i = 0; i < 240; i++)
			{
				for (int j = 0; j < array.Length; j++)
				{
					array[j] = (short)(9000.0 * Math.Sin(num));
					num += Math.PI * 3.0 / 80.0;
				}
				list.Add(Convert.ToBase64String(VoiceCodec.Encode(enc, array, 0, scratch)));
			}
			for (int k = 0; k < 40; k++)
			{
				Net.ToServer("voice.frame", new MsgVoiceFrame
				{
					s = 9000 + k,
					f = list.GetRange(k * 6, 6).ToArray()
				});
			}
			string text = list[0];
			byte[] array2 = new byte[400];
			array2[0] = 8;
			byte[] array3 = Convert.FromBase64String(text);
			array3[0] = (byte)((array3[0] & -4) | 1);
			Net.ToServer("voice.frame", new MsgVoiceFrame
			{
				s = 9100,
				f = list.GetRange(0, 7).ToArray()
			});
			Net.ToServer("voice.frame", new MsgVoiceFrame
			{
				s = 9101,
				f = new string[1] { Convert.ToBase64String(array2) }
			});
			Net.ToServer("voice.frame", new MsgVoiceFrame
			{
				s = 9102,
				f = new string[1] { "!!not base64!!" }
			});
			Net.ToServer("voice.frame", new MsgVoiceFrame
			{
				s = 9103,
				f = new string[1] { Convert.ToBase64String(array3) }
			});
			Net.ToServer("voice.frame", new MsgVoiceFrame
			{
				p = a,
				s = 9104,
				f = new string[1] { text },
				e = true
			});
			st.Detail = "sent 40 messages x 6 frames (4.8 s of audio) in one frame, then 7 frames, a 400-byte packet, bad base64, a two-frame TOC, and an end frame with p=" + a;
			return StepStatus.Pass;
		});
		Delay("flood-settle", 1.5f);
		Check("still-connected", () => Net.IsLive && Net.LastRefuseCode == null && Notices.DeniedShown == deniedBefore, () => "live " + Net.IsLive + ", refused " + (Net.LastRefuseCode ?? "no") + ", denied notices " + (Notices.DeniedShown - deniedBefore));
		Mark("b-caps-done");
		WaitMark("a-srv-off");
		WaitFor("off-here", 10f, () => !Voice.ServerOn, () => "server on " + Voice.ServerOn);
		Check("hint-gone", () => Chat.Lines.Any((Chat.Line l) => l.Text.Contains("Voice chat was turned off on this server.")), () => "chat has the off line");
		Do("talk-while-server-off", delegate
		{
			sent0 = Voice.SentMessages;
			Voice.FakeToneHz = 440f;
			Voice.FakeTalk = true;
			return "talk switch held";
		});
		Delay("held", 1.5f);
		Check("sent-nothing", delegate
		{
			Voice.FakeTalk = false;
			return Voice.SentMessages == sent0 && !Voice.Talking;
		}, () => "messages sent while the server is off: " + (Voice.SentMessages - sent0));
		Cmd("stats7", () => "stats", (string r) => true, 20f, delegate(string r)
		{
			Vars["off0"] = VoiceBotNet.StatValue(r, "off").ToString(CultureInfo.InvariantCulture);
		});
		Do("raw-frame", delegate
		{
			byte[] inArray = VoiceCodec.Encode(VoiceCodec.NewEncoder(), new short[320], 0, new byte[640]);
			Net.ToServer("voice.frame", new MsgVoiceFrame
			{
				s = 9200,
				f = new string[1] { Convert.ToBase64String(inArray) },
				e = true
			});
			return "one valid frame sent around the client's own gate";
		});
		Cmd("server-refused", () => "stats", (string r) => VoiceBotNet.StatValue(r, "off") > int.Parse(Vars["off0"], CultureInfo.InvariantCulture));
		Mark("b-srv-off-done");
		WaitMark("a-srv-on");
		WaitFor("on-here", 10f, () => Voice.ServerOn, () => "server on " + Voice.ServerOn);
		Mark("b-done");
	}
}
