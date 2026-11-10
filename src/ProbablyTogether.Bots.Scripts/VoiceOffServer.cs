using System;
using System.IO;
using System.Linq;
using ProbablyTogether.Systems;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ProbablyTogether.Bots.Scripts;

public sealed class VoiceOffServer : VoiceBotBase
{
	public override string Name => "voice-off-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		Step("write-switch", 0f, delegate(BotStep st)
		{
			string text = Args.Config;
			if (string.IsNullOrEmpty(text))
			{
				text = Paths.Under("server.json");
			}
			try
			{
				JObject val = (JObject)(File.Exists(text) ? ((object)JObject.Parse(File.ReadAllText(text))) : ((object)new JObject()));
				val["voice"] = (JToken)(false);
				File.WriteAllText(text, ((JToken)val).ToString((Formatting)0, Array.Empty<JsonConverter>()));
				st.Detail = "server.json now " + ((JToken)val).ToString((Formatting)0, Array.Empty<JsonConverter>());
				return StepStatus.Pass;
			}
			catch (Exception ex)
			{
				st.Detail = "cannot write " + text + ": " + ex.Message;
				return StepStatus.Fail;
			}
		});
		Check("switch-off", () => !Voice.ServerOn, () => "server voice on: " + Voice.ServerOn);
		WaitFor("shop", 300f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene);
		WaitFor("two-live", 300f, () => Net.Players.Keys.Count(Net.IsPlayerLive) >= 2, () => Net.Players.Count + " player(s)");
		WaitFor("clients-done", 300f, () => VoiceBotNet.HasMark("off-done-1") && VoiceBotNet.HasMark("off-done-2"), () => VoiceBotNet.Stats());
		Step("counters", 0f, delegate(BotStep st)
		{
			st.Detail = VoiceBotNet.Stats();
			return (Voice.SrvDroppedOff >= 2 && Voice.SrvRelayed == 0 && Voice.SrvPackets == 0) ? StepStatus.Pass : StepStatus.Fail;
		});
	}
}
