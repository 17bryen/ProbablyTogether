using System;
using System.Collections.Generic;
using ProbablyTogether.Bots;
using Concentus.Structs;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

internal sealed class VoiceStream
{
	internal const int ClipSeconds = 2;

	internal const int ClipLen = 32000;

	internal const int QueueCap = 16000;

	internal const int Prebuffer = 2400;

	private const double StallSeconds = 0.5;

	private const int TapLen = 64000;

	internal readonly int Player;

	internal int PacketsIn;

	internal int PacketsBad;

	internal int PacketsMuted;

	internal int MessagesIn;

	internal int Gaps;

	internal int Underruns;

	internal int Dropped;

	internal long Played;

	internal float LastPacketAt = -100f;

	internal bool Ended = true;

	internal int LastSeq = -1;

	private OpusDecoder decoder;

	private readonly short[] pcm = new short[320];

	private readonly float[] queue = new float[16000];

	private int qHead;

	private int qCount;

	private float firstQueuedAt;

	private GameObject go;

	private AudioSource src;

	private AudioClip clip;

	private bool playing;

	private long writeHead;

	private long playHead;

	private long clearedTo;

	private int lastTimeSamples;

	private double lastClockAt;

	private double stuckSince;

	private double lastPumpAt;

	internal int Stalls;

	private bool virtualClock;

	private readonly float[] chunk = new float[2048];

	private float smoothedDt = 0.02f;

	private float idleSince = -1f;

	private readonly float[] tap = new float[64000];

	private int tapHead;

	private int tapCount;

	private static readonly float[] Zeros = new float[4096];

	private readonly Dictionary<int, float[]> parts = new Dictionary<int, float[]>();

	internal static bool OutputProbe;

	internal const int OutLen = 4096;

	private float[] outBuf;

	private float[] outBuf1;

	private float nextProbeAt;

	internal int OutSnaps;

	internal int OutLoud;

	internal double OutPeakRms;

	internal double OutLoudRmsSum;

	internal double OutPeakRms1;

	internal float[] OutPeak;

	internal string OutProblem = "";

	private float[] clipCopy;

	internal int Queued => qCount;

	internal bool Playing => playing;

	internal bool VirtualClock => virtualClock;

	internal int OutRate => AudioSettings.outputSampleRate;

	internal int TapCount => tapCount;

	internal VoiceStream(int player)
	{
		Player = player;
	}

	internal bool Speaking(float now)
	{
		if (!Ended)
		{
			return now - LastPacketAt < 0.35f;
		}
		return false;
	}

	internal void Receive(MsgVoiceFrame m, bool muted, float now)
	{
		MessagesIn++;
		if (LastSeq >= 0 && m.s != LastSeq + 1)
		{
			Gaps++;
		}
		LastSeq = m.s;
		int num = ((m.f != null) ? m.f.Length : 0);
		if (num > 0)
		{
			LastPacketAt = now;
			Ended = false;
		}
		if (m.e)
		{
			Ended = true;
		}
		for (int i = 0; i < num; i++)
		{
			byte[] array = VoiceCodec.FromText(m.f[i]);
			PacketsIn++;
			if (array == null || !VoiceCodec.IsTwentyMsFrame(array))
			{
				PacketsBad++;
				continue;
			}
			if (muted)
			{
				PacketsMuted++;
				continue;
			}
			int num2 = 0;
			try
			{
				if (decoder == null)
				{
					decoder = VoiceCodec.NewDecoder();
				}
				num2 = VoiceCodec.Decode(decoder, array, pcm);
			}
			catch
			{
				num2 = 0;
			}
			if (num2 != 320)
			{
				PacketsBad++;
				decoder = null;
				continue;
			}
			if (qCount == 0)
			{
				firstQueuedAt = now;
			}
			for (int j = 0; j < num2; j++)
			{
				Push((float)pcm[j] / 32768f);
			}
		}
	}

	private void Push(float v)
	{
		if (qCount == 16000)
		{
			qHead = (qHead + 1) % 16000;
			qCount--;
			Dropped++;
		}
		queue[(qHead + qCount) % 16000] = v;
		qCount++;
	}

	internal void Flush()
	{
		qHead = 0;
		qCount = 0;
	}

	internal void Pump(float gain, float now, float dt)
	{
		smoothedDt += (Mathf.Clamp(dt, 0.001f, 0.5f) - smoothedDt) * 0.1f;
		if (!playing && (qCount == 0 || (qCount < 2400 && !Ended && now - firstQueuedAt < 0.25f) || !StartPlayback()))
		{
			return;
		}
		while (qCount > 8000)
		{
			qHead = (qHead + 1) % 16000;
			qCount--;
			Dropped++;
		}
		double realtimeSinceStartupAsDouble = Time.realtimeSinceStartupAsDouble;
		bool flag = realtimeSinceStartupAsDouble - lastPumpAt > 0.5;
		lastPumpAt = realtimeSinceStartupAsDouble;
		long num = 0L;
		if (!virtualClock)
		{
			int num2 = 0;
			try
			{
				num2 = (((Object)(object)src != (Object)null) ? src.timeSamples : 0);
			}
			catch
			{
			}
			int num3 = num2 - lastTimeSamples;
			if (num3 < 0)
			{
				num3 += 32000;
			}
			lastTimeSamples = num2;
			if (num3 > 0)
			{
				num = num3;
				stuckSince = realtimeSinceStartupAsDouble;
				VoiceAudioState.UnityClockSeen = true;
			}
			else if (!AudioListener.pause && !VoiceAudioState.UnityClockSeen && realtimeSinceStartupAsDouble - stuckSince > 0.5)
			{
				virtualClock = true;
				VoiceAudioState.NoAudioOutput = true;
				if (!VoiceAudioState.LoggedNoOutput)
				{
					VoiceAudioState.LoggedNoOutput = true;
					Log.Info("voice: this copy plays no sound (the AudioSource's play head never moved: no audio output); voice is drained by the clock");
				}
				lastClockAt = realtimeSinceStartupAsDouble;
			}
		}
		if (virtualClock)
		{
			num = (long)((realtimeSinceStartupAsDouble - lastClockAt) * 16000.0);
			lastClockAt += (double)num / 16000.0;
		}
		playHead += num;
		if (flag && !virtualClock)
		{
			Stalls++;
			WriteZeros(0L, 32000);
			writeHead = (clearedTo = playHead);
		}
		if (writeHead < playHead)
		{
			if (qCount > 0)
			{
				Underruns++;
			}
			writeHead = playHead;
		}
		if (playHead - clearedTo > 32000)
		{
			clearedTo = playHead - 32000;
		}
		if (clearedTo < playHead)
		{
			WriteZeros(clearedTo, (int)(playHead - clearedTo));
			clearedTo = playHead;
		}
		int num4 = (int)(Mathf.Clamp(smoothedDt * 2.5f, 0.08f, 0.35f) * 16000f);
		long num5 = playHead + num4;
		while (writeHead < num5)
		{
			int num6 = (int)Math.Min(num5 - writeHead, chunk.Length);
			Take(chunk, num6, gain);
			Write(writeHead, chunk, num6);
			writeHead += num6;
		}
		if (OutputProbe && !virtualClock)
		{
			ProbeOutput(now);
		}
		if (qCount != 0 || (!Ended && !(now - LastPacketAt > 0.6f)))
		{
			idleSince = -1f;
			return;
		}
		if (idleSince < 0f)
		{
			idleSince = now;
		}
		if (now - idleSince > 0.5f)
		{
			StopPlayback();
		}
	}

	private void Take(float[] buf, int n, float gain)
	{
		int i;
		for (i = 0; i < n; i++)
		{
			if (qCount <= 0)
			{
				break;
			}
			float num = queue[qHead] * gain;
			qHead = (qHead + 1) % 16000;
			qCount--;
			buf[i] = num;
			tap[tapHead] = num;
			tapHead = (tapHead + 1) % 64000;
			if (tapCount < 64000)
			{
				tapCount++;
			}
			Played++;
		}
		for (; i < n; i++)
		{
			buf[i] = 0f;
		}
	}

	private void WriteZeros(long at, int n)
	{
		if (!virtualClock && !((Object)(object)clip == (Object)null))
		{
			n = Math.Min(n, 32000);
			while (n > 0)
			{
				int num = Math.Min(n, Zeros.Length);
				Write(at, Zeros, num);
				at += num;
				n -= num;
			}
		}
	}

	private void Write(long at, float[] data, int n)
	{
		if (virtualClock || (Object)(object)clip == (Object)null || n <= 0)
		{
			return;
		}
		int num = (int)(at % 32000);
		try
		{
			if (num + n <= 32000)
			{
				SetData(data, 0, n, num);
				return;
			}
			int num2 = 32000 - num;
			SetData(data, 0, num2, num);
			SetData(data, num2, n - num2, 0);
		}
		catch (Exception ex)
		{
			if (!VoiceAudioState.LoggedWriteError)
			{
				VoiceAudioState.LoggedWriteError = true;
				Log.Warn("voice: writing the playback clip failed: " + ex.Message);
			}
		}
	}

	private void SetData(float[] data, int from, int n, int clipOffset)
	{
		if (!parts.TryGetValue(n, out var value))
		{
			if (parts.Count > 32)
			{
				parts.Clear();
			}
			value = (parts[n] = new float[n]);
		}
		Array.Copy(data, from, value, 0, n);
		clip.SetData(value, clipOffset);
	}

	private bool StartPlayback()
	{
		try
		{
			if ((Object)(object)go == (Object)null)
			{
				go = new GameObject("bellun_probably_together_voice_" + Player);
				Object.DontDestroyOnLoad((Object)(object)go);
				src = go.AddComponent<AudioSource>();
				src.playOnAwake = false;
				src.loop = true;
				src.spatialBlend = 0f;
				src.volume = 1f;
				src.priority = 16;
				clip = AudioClip.Create("bellun_probably_together_voice_" + Player, 32000, 1, 16000, false);
				src.clip = clip;
			}
			virtualClock = VoiceAudioState.NoAudioOutput;
			writeHead = (playHead = (clearedTo = 0L));
			WriteZeros(0L, 32000);
			lastTimeSamples = 0;
			stuckSince = (lastClockAt = (lastPumpAt = Time.realtimeSinceStartupAsDouble));
			src.timeSamples = 0;
			src.Play();
			playing = true;
			idleSince = -1f;
			return true;
		}
		catch (Exception ex)
		{
			if (!VoiceAudioState.LoggedStartError)
			{
				VoiceAudioState.LoggedStartError = true;
				Log.Warn("voice: playback could not start: " + ex.Message);
			}
			return false;
		}
	}

	private void StopPlayback()
	{
		playing = false;
		try
		{
			if ((Object)(object)src != (Object)null)
			{
				src.Stop();
			}
		}
		catch
		{
		}
	}

	internal void Destroy()
	{
		StopPlayback();
		try
		{
			if ((Object)(object)clip != (Object)null)
			{
				Object.Destroy((Object)(object)clip);
			}
		}
		catch
		{
		}
		try
		{
			if ((Object)(object)go != (Object)null)
			{
				Object.Destroy((Object)(object)go);
			}
		}
		catch
		{
		}
		clip = null;
		src = null;
		go = null;
	}

	private void ProbeOutput(float now)
	{
		if (!BotRunner.Enabled || (Object)(object)src == (Object)null || now < nextProbeAt)
		{
			return;
		}
		nextProbeAt = now + 0.1f;
		if (outBuf == null)
		{
			outBuf = new float[4096];
			outBuf1 = new float[4096];
		}
		try
		{
			src.GetOutputData(outBuf, 0);
			src.GetOutputData(outBuf1, 1);
		}
		catch (Exception ex)
		{
			OutProblem = ex.Message;
			return;
		}
		OutSnaps++;
		double num = VoiceMeter.Rms(outBuf, 0, 4096);
		if (num > 0.02)
		{
			OutLoud++;
			OutLoudRmsSum += num;
		}
		if (num > OutPeakRms)
		{
			OutPeakRms = num;
			OutPeakRms1 = VoiceMeter.Rms(outBuf1, 0, 4096);
			if (OutPeak == null)
			{
				OutPeak = new float[4096];
			}
			Array.Copy(outBuf, OutPeak, 4096);
		}
	}

	internal void OutReset()
	{
		OutSnaps = (OutLoud = 0);
		OutPeakRms = (OutLoudRmsSum = (OutPeakRms1 = 0.0));
		OutPeak = null;
	}

	internal int ClipStale(out int lead)
	{
		lead = (int)(writeHead - playHead);
		if (!playing || virtualClock || (Object)(object)clip == (Object)null || lead < 0 || lead > 32000)
		{
			return 0;
		}
		if (clipCopy == null)
		{
			clipCopy = new float[32000];
		}
		clip.GetData(clipCopy, 0);
		int num = (int)(playHead % 32000);
		int num2 = 0;
		for (int i = lead; i < 32000; i++)
		{
			if (clipCopy[(num + i) % 32000] != 0f)
			{
				num2++;
			}
		}
		return num2;
	}

	internal void TapReset()
	{
		tapHead = 0;
		tapCount = 0;
	}

	internal float[] TapRecent(int count)
	{
		count = Math.Min(count, tapCount);
		float[] array = new float[count];
		int num = (tapHead - count + 64000) % 64000;
		for (int i = 0; i < count; i++)
		{
			array[i] = tap[(num + i) % 64000];
		}
		return array;
	}
}
