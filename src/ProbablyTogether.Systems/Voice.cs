using System;
using System.Collections.Generic;
using System.IO;
using ProbablyTogether.Bots;
using ProbablyTogether.UI;
using Concentus.Structs;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ProbablyTogether.Systems;

public static class Voice
{
	private sealed class Speaker
	{
		public double Packets = 60.0;

		public double Bytes = 4800.0;

		public long LastMs = Clock.Ms;

		public long ConfigMs = -100000L;

		public bool Listen = true;

		public int Seq;
	}

	internal const int FramesPerMessage = 6;

	internal const double PacketsPerSec = 55.0;

	internal const double PacketBurst = 60.0;

	internal const double BytesPerSec = 4000.0;

	internal const double ByteBurst = 4800.0;

	internal const float MicIdleSeconds = 30f;

	private const string KeySave = "humangenome_accomplice.voiceKey";

	private const string OnSave = "humangenome_accomplice.voiceOn";

	private const string VolumeSave = "humangenome_accomplice.voiceVolume";

	public const string KeyBindOption = "voice_key";

	public const string Section = "voice";

	public const string HintText = "to talk to the shop.";

	public static float FakeToneHz;

	public static float FakeToneAmp = 0.3f;

	public static bool FakeTalk;

	public static int SentMessages;

	public static int SentPackets;

	public static int Spurts;

	public static int CaptureFailures;

	public static long SentBytes;

	public static float SentSeconds;

	private static bool inited;

	private static bool headless;

	private static readonly Dictionary<int, Speaker> speakers = new Dictionary<int, Speaker>();

	private static bool? serverSwitch;

	private static bool? labServerOverride;

	internal static int SrvMessages;

	internal static int SrvPackets;

	internal static int SrvRelayed;

	internal static int SrvDroppedRate;

	internal static int SrvDroppedBad;

	internal static int SrvDroppedOff;

	internal static int SrvDroppedLoading;

	internal static int SrvNotListening;

	internal static int SrvConfigs;

	internal static int SrvStateChanges;

	internal static int SrvStateLines;

	internal static int SrvStamped;

	private static bool enabled = true;

	private static bool prefsLoaded;

	private static float volume = 1f;

	private static KeyCode talkKey = (KeyCode)118;

	private static bool clientServerOn;

	private static bool configSeen;

	private static bool hintShown;

	private static bool active;

	private static bool talking;

	private static bool warnedNoMic;

	private static float lastTalkAt = -100f;

	private static float deniedUntil = -100f;

	private static float nextMicTry = -100f;

	private static int sendSeq;

	private static int speakingSig;

	private static readonly Dictionary<int, VoiceStream> streams = new Dictionary<int, VoiceStream>();

	private static readonly HashSet<int> muted = new HashSet<int>();

	private static readonly VoiceCapture capture = new VoiceCapture();

	private static OpusEncoder encoder;

	private static readonly List<short> pending = new List<short>();

	private static readonly List<short> discard = new List<short>();

	private static readonly List<string> outPackets = new List<string>();

	private static readonly short[] frame = new short[320];

	private static readonly byte[] scratch = new byte[640];

	private static bool optionsAdded;

	private static readonly HashSet<int> muteRows = new HashSet<int>();

	private static float muteRowsAt = -10f;

	public static bool ServerOn
	{
		get
		{
			if (!Net.IsServer)
			{
				if (clientServerOn)
				{
					return configSeen;
				}
				return false;
			}
			return ServerSwitch();
		}
	}

	public static bool ConfigSeen => configSeen;

	public static bool Enabled
	{
		get
		{
			LoadPrefs();
			return enabled;
		}
		set
		{
			SetEnabled(value);
		}
	}

	public static float Volume
	{
		get
		{
			LoadPrefs();
			return volume;
		}
		set
		{
			volume = Mathf.Clamp01(value);
			prefsLoaded = true;
			Save(delegate
			{
				ES3.Save<float>("humangenome_accomplice.voiceVolume", volume);
			});
		}
	}

	public static KeyCode TalkKey
	{
		get
		{
			LoadPrefs();
			return talkKey;
		}
		set
		{
			talkKey = (KeyCode)(((int)value == 0 || (int)value == 27) ? 118 : ((int)value));
			prefsLoaded = true;
			Save(delegate
			{
				ES3.Save<int>("humangenome_accomplice.voiceKey", (int)talkKey);
			});
		}
	}

	public static bool Talking => talking;

	public static string TalkKeyName
	{
		get
		{
			try
			{
				return KeyBindHelper.KeyName(TalkKey);
			}
			catch
			{
				return ((object)TalkKey).ToString();
			}
		}
	}

	public static string CaptureProblem => capture.Problem;

	public static bool CaptureRunning => capture.Running;

	public static bool UnityPlayback => VoiceAudioState.UnityClockSeen;

	public static bool NoAudioOutput => VoiceAudioState.NoAudioOutput;

	public static string CodecCheck => VoiceCodec.SelfTest();

	internal static int MuteRows => muteRows.Count;

	internal static int MutedCount => muted.Count;

	public static bool IsSpeaking(int player)
	{
		if (player != Net.LocalPlayer)
		{
			if (streams.TryGetValue(player, out var value))
			{
				return value.Speaking(Time.realtimeSinceStartup);
			}
			return false;
		}
		return talking;
	}

	public static bool IsMuted(int player)
	{
		if (player != Net.LocalPlayer)
		{
			return muted.Contains(player);
		}
		return false;
	}

	public static void SetMuted(int player, bool on)
	{
		if (player != Net.LocalPlayer && Net.Players.ContainsKey(player) && !(on ? (!muted.Add(player)) : (!muted.Remove(player))))
		{
			if (on && streams.TryGetValue(player, out var value))
			{
				value.Flush();
			}
			Log.Info("voice: " + Net.PlayerName(player) + (on ? " muted" : " unmuted") + " (this player only)");
			Presence.RosterChanged();
		}
	}

	public static string RosterMark(int player, out string plain)
	{
		plain = "";
		if (!Net.IsClient)
		{
			return "";
		}
		bool flag = IsSpeaking(player);
		bool flag2 = IsMuted(player);
		if (flag && !flag2)
		{
			plain = "talking";
			return "  <color=#" + Kit.Hex(Kit.Good) + ">" + Lang.Tr("talking") + "</color>";
		}
		if (flag2)
		{
			plain = (flag ? "muted, talking" : "muted");
			return "  <color=#" + Kit.Hex(Kit.Dim) + ">" + Lang.Tr(plain) + "</color>";
		}
		return "";
	}

	[SystemInit]
	internal static void Init()
	{
		if (inited)
		{
			return;
		}
		inited = true;
		headless = Args.Server || Net.IsDedicated;
		Net.On<MsgVoiceFrame>("voice.frame", ServerOnFrame, ClientOnFrame);
		Net.On<MsgVoiceState>("voice.state", ServerOnState, null);
		Net.On<MsgVoiceConfig>("voice.config", null, ClientOnConfig);
		Snapshot.ClientReady += ServerOnClientReady;
		Net.PlayerLeft += OnPlayerLeft;
		if (!headless)
		{
			Net.Disconnected += delegate
			{
				ClientReset();
			};
			Net.Denied += OnDenied;
		}
		Log.Info("voice: ready (" + (headless ? "server: relay only" : "client") + ")");
	}

	[SystemTick]
	internal static void Tick()
	{
		if (!inited || headless)
		{
			return;
		}
		EnsureOptions();
		if (!Net.IsClient)
		{
			if (active)
			{
				ClientReset();
			}
			return;
		}
		try
		{
			ClientTick();
		}
		catch (Exception ex)
		{
			if (Log.Allow("voice|tick", 10000L, out var skipped))
			{
				Log.Error("voice: tick failed" + Log.More(skipped) + ": " + ex);
			}
		}
	}

	private static Speaker SpeakerOf(int p)
	{
		if (!speakers.TryGetValue(p, out var value))
		{
			value = (speakers[p] = new Speaker());
		}
		return value;
	}

	private static bool ServerSwitch()
	{
		if (labServerOverride.HasValue)
		{
			return labServerOverride.Value;
		}
		if (!serverSwitch.HasValue)
		{
			string text = Args.Config;
			if (string.IsNullOrEmpty(text))
			{
				text = Paths.Under("server.json");
			}
			serverSwitch = ReadSwitch(text, out var why);
			Log.Info("server: voice is " + (serverSwitch.Value ? "on" : "off") + " (" + why + ")");
		}
		return serverSwitch.Value;
	}

	internal static bool ReadSwitch(string path, out string why)
	{
		try
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				why = "no server.json: the default, on";
				return true;
			}
			JObject obj = JObject.Parse(File.ReadAllText(path));
			JToken val = null;
			foreach (JProperty item in obj.Properties())
			{
				if (string.Equals(item.Name, "voice", StringComparison.OrdinalIgnoreCase))
				{
					val = item.Value;
				}
			}
			if (val == null)
			{
				why = "server.json has no voice key: the default, on";
				return true;
			}
			JTokenType type = val.Type;
			switch ((int)type - 6)
			{
			case 3:
				why = "server.json voice " + (((bool)val) ? "true" : "false");
				return (bool)val;
			case 0:
				why = "server.json voice " + (long)val;
				return (long)val != 0;
			case 2:
			{
				string text = (((string)val) ?? "").Trim().ToLowerInvariant();
				switch (text)
				{
				case "false":
				case "off":
				case "no":
				case "0":
					why = "server.json voice \"" + text + "\"";
					return false;
				case "true":
				case "on":
				case "yes":
				case "1":
					why = "server.json voice \"" + text + "\"";
					return true;
				}
				break;
			}
			}
			why = "server.json voice is not true or false (" + ((object)val.Type).ToString() + "): the default, on";
			return true;
		}
		catch (Exception ex)
		{
			why = "server.json unreadable (" + Text.Clip(ex.Message, 120) + "): the default, on";
			return true;
		}
	}

	internal static void LabSetServerOn(bool? on)
	{
		labServerOverride = on;
		Log.Info("server: voice switched " + (ServerSwitch() ? "on" : "off") + " (lab)");
		foreach (int item in new List<int>(Net.Players.Keys))
		{
			if (Net.IsPlayerLive(item))
			{
				SendConfig(item);
			}
		}
	}

	internal static bool LabListens(int player)
	{
		return SpeakerOf(player).Listen;
	}

	private static MsgVoiceConfig Config()
	{
		return new MsgVoiceConfig
		{
			on = ServerSwitch(),
			rate = 16000,
			ms = 20,
			max = 6,
			bytes = 160,
			perSec = 55
		};
	}

	private static void SendConfig(int player)
	{
		SpeakerOf(player).ConfigMs = Clock.Ms;
		SrvConfigs++;
		Net.ToPlayer(player, "voice.config", Config());
	}

	private static void ServerOnClientReady(int player)
	{
		if (Net.IsServer)
		{
			SendConfig(player);
		}
	}

	private static void ServerOnState(MsgVoiceState m, int from)
	{
		if (m == null)
		{
			return;
		}
		Speaker speaker = SpeakerOf(from);
		if (speaker.Listen != m.listen)
		{
			speaker.Listen = m.listen;
			SrvStateChanges++;
			if (Log.Allow("p" + from + "|voice-state", 10000L, out var skipped))
			{
				SrvStateLines++;
				Log.Info("server: " + Net.PlayerName(from) + " turned voice " + (m.listen ? "on" : "off") + " for themselves" + Log.More(skipped));
			}
		}
	}

	private static void ServerOnFrame(MsgVoiceFrame m, int from)
	{
		if (m == null)
		{
			return;
		}
		SrvMessages++;
		if (m.p != 0 && m.p != from)
		{
			SrvStamped++;
		}
		Speaker speaker = SpeakerOf(from);
		if (!ServerSwitch())
		{
			SrvDroppedOff++;
			if (Clock.Ms - speaker.ConfigMs > 5000)
			{
				SendConfig(from);
			}
			return;
		}
		if (!Net.IsPlayerLive(from))
		{
			SrvDroppedLoading++;
			return;
		}
		int num = ((m.f != null) ? m.f.Length : 0);
		if (num > 6 || (num == 0 && !m.e))
		{
			Bad(from, "voice.frame with " + num + " packets");
			return;
		}
		string[] array = new string[num];
		int num2 = 0;
		for (int i = 0; i < num; i++)
		{
			byte[] array2 = VoiceCodec.FromText(m.f[i]);
			if (array2 == null || !VoiceCodec.IsTwentyMsFrame(array2))
			{
				Bad(from, "voice.frame packet " + i + " is not one 20 ms Opus frame of at most " + 160 + " bytes");
				return;
			}
			num2 += array2.Length;
			array[i] = Convert.ToBase64String(array2);
		}
		long ms = Clock.Ms;
		double num3 = (double)Math.Max(0L, ms - speaker.LastMs) / 1000.0;
		speaker.LastMs = ms;
		speaker.Packets = Math.Min(60.0, speaker.Packets + num3 * 55.0);
		speaker.Bytes = Math.Min(4800.0, speaker.Bytes + num3 * 4000.0);
		if (speaker.Packets < (double)num || speaker.Bytes < (double)num2)
		{
			SrvDroppedRate += num;
			if (Log.Allow("p" + from + "|voice-rate", 10000L, out var skipped))
			{
				Log.Info("server: voice from " + Net.PlayerName(from) + " over the cap (" + 55.0 + " frames, " + 4000.0 + " bytes a second): dropped" + Log.More(skipped));
			}
			if (!m.e)
			{
				return;
			}
			array = new string[0];
		}
		else
		{
			speaker.Packets -= num;
			speaker.Bytes -= num2;
			SrvPackets += num;
		}
		MsgVoiceFrame msg = new MsgVoiceFrame
		{
			p = from,
			s = ++speaker.Seq,
			f = array,
			e = m.e
		};
		foreach (int item in new List<int>(Net.Players.Keys))
		{
			if (item != from && Net.IsPlayerLive(item))
			{
				if (!SpeakerOf(item).Listen)
				{
					SrvNotListening++;
					continue;
				}
				Net.ToPlayer(item, "voice.frame", msg);
				SrvRelayed++;
			}
		}
	}

	private static void Bad(int from, string what)
	{
		SrvDroppedBad++;
		if (Log.Allow("p" + from + "|voice-bad", 10000L, out var skipped))
		{
			Log.Warn("server: " + what + " from " + Net.PlayerName(from) + ": dropped" + Log.More(skipped));
		}
	}

	private static void OnPlayerLeft(PlayerInfo p)
	{
		if (p == null)
		{
			return;
		}
		if (Net.IsServer)
		{
			speakers.Remove(p.Id);
			return;
		}
		if (streams.TryGetValue(p.Id, out var value))
		{
			value.Destroy();
			streams.Remove(p.Id);
		}
		muted.Remove(p.Id);
		RemoveMuteRow(p.Id);
		Presence.RosterChanged();
	}

	internal static VoiceStream StreamOf(int player)
	{
		if (!streams.TryGetValue(player, out var value))
		{
			return null;
		}
		return value;
	}

	private static void LoadPrefs()
	{
		if (prefsLoaded)
		{
			return;
		}
		prefsLoaded = true;
		try
		{
			if (ES3.KeyExists("humangenome_accomplice.voiceOn"))
			{
				enabled = ES3.Load<bool>("humangenome_accomplice.voiceOn");
			}
			if (ES3.KeyExists("humangenome_accomplice.voiceVolume"))
			{
				volume = Mathf.Clamp01(ES3.Load<float>("humangenome_accomplice.voiceVolume"));
			}
			if (ES3.KeyExists("humangenome_accomplice.voiceKey"))
			{
				talkKey = (KeyCode)ES3.Load<int>("humangenome_accomplice.voiceKey");
			}
		}
		catch (Exception ex)
		{
			Log.Warn("voice: options not read (" + ex.Message + "); defaults used");
		}
		if ((int)talkKey == 0 || (int)talkKey == 27)
		{
			talkKey = (KeyCode)118;
		}
	}

	private static void Save(Action save)
	{
		try
		{
			save();
		}
		catch (Exception ex)
		{
			Log.Warn("voice: option not saved: " + ex.Message);
		}
	}

	private static void SetEnabled(bool on)
	{
		LoadPrefs();
		if (enabled == on)
		{
			return;
		}
		enabled = on;
		Save(delegate
		{
			ES3.Save<bool>("humangenome_accomplice.voiceOn", enabled);
		});
		Log.Info("voice: turned " + (on ? "on" : "off") + " in Options");
		if (!on)
		{
			if (talking)
			{
				EndTalk(Time.realtimeSinceStartup);
			}
			foreach (VoiceStream value in streams.Values)
			{
				value.Flush();
			}
			capture.Stop();
		}
		if (Net.IsClient && Net.IsLive && configSeen)
		{
			Net.ToServer("voice.state", new MsgVoiceState
			{
				listen = on
			});
		}
		Presence.RosterChanged();
	}

	private static void ClientOnConfig(MsgVoiceConfig m)
	{
		if (m == null)
		{
			return;
		}
		bool flag = clientServerOn && configSeen;
		clientServerOn = m.on;
		bool flag2 = !configSeen;
		configSeen = true;
		active = true;
		Log.Info("voice: server says voice is " + (m.on ? "on" : "off") + (flag2 ? "" : " (changed)"));
		if (flag2)
		{
			Net.ToServer("voice.state", new MsgVoiceState
			{
				listen = Enabled
			});
			Warm();
		}
		if (m.on && !Works())
		{
			Log.Error("voice: the codec self test failed (" + VoiceCodec.SelfTest() + "); voice stays off on this copy");
		}
		else if (m.on && Enabled && (!hintShown || !flag))
		{
			hintShown = true;
			Chat.Local("Hold " + TalkKeyName + " to talk to the shop.");
		}
		else if (!m.on && flag)
		{
			Chat.Local("Voice chat was turned off on this server.");
			if (talking)
			{
				EndTalk(Time.realtimeSinceStartup);
			}
		}
	}

	private static bool Works()
	{
		return VoiceCodec.Works;
	}

	private static void Warm()
	{
		try
		{
			Wire.Encode("voice.frame", new MsgVoiceFrame
			{
				s = 0,
				f = new string[1] { "AA==" },
				e = true
			});
			if (encoder == null && Works())
			{
				encoder = VoiceCodec.NewEncoder();
				VoiceCodec.Encode(encoder, frame, 0, scratch);
				encoder = VoiceCodec.NewEncoder();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("voice: warm-up failed: " + ex.Message);
		}
	}

	private static void ClientOnFrame(MsgVoiceFrame m)
	{
		if (m != null && m.p != Net.LocalPlayer && Net.Players.ContainsKey(m.p) && Ui.ShopOnline && Enabled && Works())
		{
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (!streams.TryGetValue(m.p, out var value))
			{
				value = (streams[m.p] = new VoiceStream(m.p));
			}
			bool flag = value.Speaking(realtimeSinceStartup);
			value.Receive(m, IsMuted(m.p), realtimeSinceStartup);
			active = true;
			if (value.Speaking(realtimeSinceStartup) != flag)
			{
				Presence.RosterChanged();
			}
		}
	}

	private static void OnDenied(string kind, string reason)
	{
		if (kind != null && kind.StartsWith("voice.", StringComparison.Ordinal))
		{
			deniedUntil = Time.realtimeSinceStartup + 10f;
			if (talking)
			{
				talking = false;
				pending.Clear();
				outPackets.Clear();
				Presence.RosterChanged();
			}
		}
	}

	private static bool KeyHeld()
	{
		if (Chat.OwnsKeyboard || KeyBindHelper.SuppressHotkeys || StoreUIManager.IsAnyTyping())
		{
			return false;
		}
		return Input.GetKey(TalkKey);
	}

	private static void ClientTick()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		bool shopOnline = Ui.ShopOnline;
		bool flag = shopOnline && ServerOn && Enabled && realtimeSinceStartup >= deniedUntil && (FakeTalk || KeyHeld()) && Works();
		if (flag && !talking)
		{
			BeginTalk(realtimeSinceStartup);
		}
		if (talking)
		{
			capture.Pull(pending, FakeToneHz, FakeToneAmp);
			EncodePending();
			if (flag)
			{
				SendFull(end: false);
			}
			else
			{
				EndTalk(realtimeSinceStartup);
			}
		}
		else if (capture.Running)
		{
			discard.Clear();
			capture.Pull(discard, FakeToneHz, FakeToneAmp);
			discard.Clear();
			if (realtimeSinceStartup - lastTalkAt > 30f || !shopOnline)
			{
				capture.Stop();
			}
		}
		if (streams.Count > 0)
		{
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			float num = Volume;
			foreach (VoiceStream value in streams.Values)
			{
				value.Pump((shopOnline && !IsMuted(value.Player)) ? num : 0f, realtimeSinceStartup, unscaledDeltaTime);
			}
		}
		if (realtimeSinceStartup - muteRowsAt > 1f)
		{
			muteRowsAt = realtimeSinceStartup;
			SyncMuteRows();
		}
		int num2 = (talking ? 1 : 0);
		int num3 = 2;
		foreach (KeyValuePair<int, VoiceStream> stream in streams)
		{
			if (stream.Value.Speaking(realtimeSinceStartup))
			{
				num2 |= num3;
			}
			num3 <<= 1;
		}
		if (num2 != speakingSig)
		{
			speakingSig = num2;
			Presence.RosterChanged();
		}
	}

	private static void BeginTalk(float now)
	{
		if (!capture.Running)
		{
			if (now < nextMicTry)
			{
				return;
			}
			if (!capture.Start(FakeToneHz))
			{
				CaptureFailures++;
				nextMicTry = now + 5f;
				Log.Info("voice: cannot send: " + capture.Problem);
				if (!warnedNoMic)
				{
					warnedNoMic = true;
					Chat.Local("Voice: " + capture.Problem + ", so nobody can hear you.");
				}
				return;
			}
			Log.Info("voice: capture started (" + (capture.UsingMic ? ("microphone at " + capture.DeviceRate + " Hz") : ("lab tone " + FakeToneHz + " Hz")) + ")");
		}
		if (encoder == null)
		{
			encoder = VoiceCodec.NewEncoder();
		}
		pending.Clear();
		outPackets.Clear();
		talking = true;
		active = true;
		Spurts++;
		Presence.RosterChanged();
	}

	private static void EncodePending()
	{
		int num = 0;
		while (pending.Count - num >= 320)
		{
			pending.CopyTo(num, frame, 0, 320);
			num += 320;
			byte[] array = null;
			try
			{
				array = VoiceCodec.Encode(encoder, frame, 0, scratch);
			}
			catch (Exception ex)
			{
				if (Log.Allow("voice|enc", 10000L, out var _))
				{
					Log.Warn("voice: encode failed: " + ex.Message);
				}
				encoder = null;
			}
			if (array == null)
			{
				if (encoder == null)
				{
					encoder = VoiceCodec.NewEncoder();
				}
			}
			else
			{
				outPackets.Add(Convert.ToBase64String(array));
			}
		}
		if (num > 0)
		{
			pending.RemoveRange(0, num);
		}
	}

	private static void SendFull(bool end)
	{
		while (outPackets.Count >= 6 || (end && outPackets.Count > 0))
		{
			int num = Math.Min(6, outPackets.Count);
			bool flag = end && num == outPackets.Count;
			Send(outPackets.GetRange(0, num).ToArray(), flag);
			outPackets.RemoveRange(0, num);
			if (flag)
			{
				return;
			}
		}
		if (end)
		{
			Send(new string[0], end: true);
		}
	}

	private static void EndTalk(float now)
	{
		if (pending.Count > 0)
		{
			while (pending.Count % 320 != 0)
			{
				pending.Add(0);
			}
			EncodePending();
		}
		pending.Clear();
		talking = false;
		lastTalkAt = now;
		if (Net.IsClient && Net.IsLive)
		{
			SendFull(end: true);
		}
		outPackets.Clear();
		Presence.RosterChanged();
	}

	private static void Send(string[] packets, bool end)
	{
		MsgVoiceFrame msgVoiceFrame = new MsgVoiceFrame
		{
			s = ++sendSeq,
			f = packets,
			e = end
		};
		SentMessages++;
		SentPackets += packets.Length;
		SentSeconds += (float)(packets.Length * 20) / 1000f;
		if (BotRunner.Enabled)
		{
			SentBytes += Wire.Encode("voice.frame", msgVoiceFrame).Length;
		}
		Net.ToServer("voice.frame", msgVoiceFrame);
	}

	private static void ClientReset()
	{
		if (talking)
		{
			talking = false;
			pending.Clear();
			outPackets.Clear();
		}
		capture.Stop();
		foreach (KeyValuePair<int, VoiceStream> stream in streams)
		{
			stream.Value.Destroy();
			RemoveMuteRow(stream.Key);
		}
		streams.Clear();
		foreach (int item in new List<int>(muteRows))
		{
			RemoveMuteRow(item);
		}
		muted.Clear();
		clientServerOn = (configSeen = (hintShown = (active = false)));
		deniedUntil = -100f;
		speakingSig = 0;
		encoder = null;
	}

	private static void EnsureOptions()
	{
		if (optionsAdded || !Ui.AtMenu)
		{
			return;
		}
		optionsAdded = true;
		try
		{
			GameOptionRegistry.EnsureVanilla();
			ModHelper.AddGameOptionSection("humangenome_accomplice", "voice", (Func<string>)(() => Lang.Tr("Co-op voice")));
			ModHelper.AddGameOptionToggle("humangenome_accomplice", "voice", "voice_on", (Func<string>)(() => Lang.Tr("Voice chat")), (Func<bool>)(() => Enabled), (Action<bool>)delegate(bool v)
			{
				Enabled = v;
			}, (Func<string>)(() => Lang.Tr("Hear the other players and talk to them (hold the talk key) on a co-op server.")));
			ModHelper.AddGameOptionSlider("humangenome_accomplice", "voice", "voice_volume", (Func<string>)(() => Lang.Tr("Voice volume")), (Func<float>)(() => Volume), (Action<float>)delegate(float v)
			{
				Volume = v;
			}, 0f, 1f, false, (Func<string>)null, (Func<float, string>)null);
			ModHelper.AddGameOptionKeyBind("humangenome_accomplice", "keybind", "voice_key", (Func<string>)(() => Lang.Tr("Co-op push to talk")), (Func<KeyCode>)(() => TalkKey), (Action<KeyCode>)delegate(KeyCode k)
			{
				TalkKey = k;
			}, (Func<string>)(() => Lang.Tr("Hold it to talk to everyone in the shop on a co-op server.")));
		}
		catch (Exception ex)
		{
			Log.Warn("voice: option rows not added: " + ex.Message);
		}
	}

	private static void SyncMuteRows()
	{
		if (!optionsAdded)
		{
			return;
		}
		foreach (int item in new List<int>(muteRows))
		{
			if (!Net.Players.ContainsKey(item))
			{
				RemoveMuteRow(item);
			}
		}
		foreach (KeyValuePair<int, PlayerInfo> player in Net.Players)
		{
			int id = player.Key;
			if (id == Net.LocalPlayer || muteRows.Contains(id))
			{
				continue;
			}
			string name = player.Value.Name;
			try
			{
				ModHelper.AddGameOptionToggle("humangenome_accomplice", "voice", "voice_mute_" + id, (Func<string>)(() => Lang.Trf("Mute {0}", Kit.Name(Net.Players.TryGetValue(id, out var value) ? value.Name : name))), (Func<bool>)(() => IsMuted(id)), (Action<bool>)delegate(bool v)
				{
					SetMuted(id, v);
				}, (Func<string>)(() => Lang.Tr("Only you stop hearing them, while they stay connected.")));
				muteRows.Add(id);
			}
			catch (Exception ex)
			{
				if (Log.Allow("voice|muterow", 60000L, out var _))
				{
					Log.Warn("voice: mute row not added: " + ex.Message);
				}
			}
		}
	}

	private static void RemoveMuteRow(int id)
	{
		if (!muteRows.Remove(id))
		{
			return;
		}
		try
		{
			ModHelper.RemoveGameOption("humangenome_accomplice", "voice_mute_" + id);
		}
		catch
		{
		}
	}
}
