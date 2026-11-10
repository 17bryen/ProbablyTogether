using System;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class VoiceOffline : VoiceBotBase
{
	private int sent0;

	public override string Name => "voice-offline";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 300f;

	protected override void Build()
	{
		WaitFor("menu", 120f, () => Ui.AtMenu, () => "scene " + BotProbes.Scene);
		Check("offline", () => !Net.IsOnline && Net.Role == ProbablyTogether.Role.Off, () => "role " + Net.Role);
		WaitFor("option-rows", 10f, () => VoiceBotBase.Option("voice_on") != null && VoiceBotBase.Option("voice_volume") != null && VoiceBotBase.Option("voice_key") != null, () => "voice_on " + (VoiceBotBase.Option("voice_on") != null) + ", volume " + (VoiceBotBase.Option("voice_volume") != null) + ", key " + (VoiceBotBase.Option("voice_key") != null));
		Check("codec", () => Voice.CodecCheck.StartsWith("ok", StringComparison.Ordinal), () => Voice.CodecCheck);
		Do("hold-talk", delegate
		{
			sent0 = Voice.SentMessages;
			Voice.FakeToneHz = 440f;
			Voice.FakeTalk = true;
			return "talk switch held offline";
		});
		Delay("held", 2f);
		Check("nothing-offline", delegate
		{
			Voice.FakeTalk = false;
			return !Voice.Talking && !Voice.CaptureRunning && Voice.SentMessages == sent0 && Voice.Spurts == 0 && Voice.MuteRows == 0 && !AnyVoiceObject();
		}, () => "talking " + Voice.Talking + ", capture " + Voice.CaptureRunning + ", sent " + (Voice.SentMessages - sent0) + ", spurts " + Voice.Spurts + ", mute rows " + Voice.MuteRows + ", audio objects " + AnyVoiceObject());
	}

	private static bool AnyVoiceObject()
	{
		GameObject[] array = Resources.FindObjectsOfTypeAll<GameObject>();
		foreach (GameObject val in array)
		{
			if ((Object)(object)val != (Object)null && ((Object)val).name.StartsWith("humangenome_accomplice_voice_", StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}
}
