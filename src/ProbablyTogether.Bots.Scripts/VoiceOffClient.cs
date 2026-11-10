using System;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class VoiceOffClient : VoiceBotBase
{
	private int sent0;

	public override string Name => "voice-off";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer);
		WaitFor("shop-live", 240f, () => Ui.ShopOnline, () => "state " + Net.ClientState);
		WaitFor("config", 30f, () => Voice.ConfigSeen, () => "voice.config seen " + Voice.ConfigSeen);
		Check("server-off", () => !Voice.ServerOn, () => "server says on: " + Voice.ServerOn);
		WaitFor("two-live", 300f, () => Net.Players.Count == 2 && Net.IsPlayerLive(Net.LocalPlayer) && Net.IsPlayerLive(VoiceBotBase.Other()), () => Net.Players.Count + " player(s)");
		Delay("settle", 2f);
		Check("no-hint", () => !Chat.Lines.Any((Chat.Line l) => l.Text.Contains("to talk to the shop.") || l.Text.Contains("Voice chat was turned off")), () => "chat: " + string.Join(" / ", Chat.Lines.Select((Chat.Line l) => l.Text)));
		Do("hold-talk", delegate
		{
			sent0 = Voice.SentMessages;
			Voice.FakeToneHz = 440f;
			Voice.FakeTalk = true;
			return "talk switch held";
		});
		Delay("held", 2f);
		Check("sent-nothing", delegate
		{
			Voice.FakeTalk = false;
			return Voice.SentMessages == sent0 && !Voice.Talking && !Voice.CaptureRunning;
		}, () => "sent " + (Voice.SentMessages - sent0) + ", talking " + Voice.Talking + ", capture " + Voice.CaptureRunning);
		Do("raw-frame", delegate
		{
			byte[] inArray = VoiceCodec.Encode(VoiceCodec.NewEncoder(), new short[320], 0, new byte[640]);
			Net.ToServer("voice.frame", new MsgVoiceFrame
			{
				s = 1,
				f = new string[1] { Convert.ToBase64String(inArray) },
				e = true
			});
			return "one valid frame sent around the client's own gate";
		});
		Delay("refused", 1.5f);
		Check("nothing-heard", () => Net.Players.Keys.All((int p) => p == Net.LocalPlayer || Voice.StreamOf(p) == null), () => "no stream from anyone");
		Cmd("mark-done", () => "mark:off-done-" + Net.LocalPlayer, (string r) => r == "ok");
	}
}
