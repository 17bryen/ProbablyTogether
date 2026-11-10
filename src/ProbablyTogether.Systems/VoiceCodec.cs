using System;
using Concentus.Celt;
using Concentus.Enums;
using Concentus.Structs;

namespace ProbablyTogether.Systems;

internal static class VoiceCodec
{
	internal const int Rate = 16000;

	internal const int FrameMs = 20;

	internal const int FrameSamples = 320;

	internal const int Bitrate = 16000;

	internal const int Complexity = 3;

	internal const int MaxPacketBytes = 160;

	private const uint FftTableHash = 2843151509u;

	private const uint MdctTableHash = 811266497u;

	private static string selfTest;

	internal static bool Works => SelfTest().StartsWith("ok", StringComparison.Ordinal);

	internal static OpusEncoder NewEncoder()
	{
		OpusEncoder opusEncoder = OpusEncoder.Create(16000, 1, OpusApplication.OPUS_APPLICATION_VOIP);
		opusEncoder.Bitrate = 16000;
		opusEncoder.UseVBR = false;
		opusEncoder.Complexity = 3;
		opusEncoder.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
		return opusEncoder;
	}

	internal static OpusDecoder NewDecoder()
	{
		return OpusDecoder.Create(16000, 1);
	}

	internal static byte[] Encode(OpusEncoder enc, short[] pcm, int offset, byte[] scratch)
	{
		int num = enc.Encode(pcm, offset, 320, scratch, 0, scratch.Length);
		if (num <= 0 || num > 160)
		{
			return null;
		}
		byte[] array = new byte[num];
		Buffer.BlockCopy(scratch, 0, array, 0, num);
		return array;
	}

	internal static int Decode(OpusDecoder dec, byte[] packet, short[] output)
	{
		if (!IsTwentyMsFrame(packet))
		{
			return 0;
		}
		int num = dec.Decode(packet, 0, packet.Length, output, 0, 320);
		if (num != 320)
		{
			return 0;
		}
		return num;
	}

	internal static bool IsTwentyMsFrame(byte[] p)
	{
		if (p == null || p.Length < 1 || p.Length > 160)
		{
			return false;
		}
		byte num = p[0];
		int num2 = num >> 3;
		switch (num & 3)
		{
		case 1:
		case 2:
			return false;
		case 3:
			if (p.Length < 2 || (p[1] & 0x3F) != 1)
			{
				return false;
			}
			break;
		}
		if (num2 < 12)
		{
			return num2 % 4 == 1;
		}
		if (num2 < 16)
		{
			return num2 % 2 == 1;
		}
		return num2 % 4 == 3;
	}

	internal static byte[] FromText(string s)
	{
		if (string.IsNullOrEmpty(s) || s.Length > 216)
		{
			return null;
		}
		try
		{
			return Convert.FromBase64String(s);
		}
		catch
		{
			return null;
		}
	}

	internal static string SelfTest()
	{
		if (selfTest != null)
		{
			return selfTest;
		}
		try
		{
			uint num = Fnv(Tables.fft_twiddles48000_960);
			uint num2 = Fnv(Tables.mdct_twiddles960);
			if (num != 2843151509u || num2 != 811266497)
			{
				return selfTest = "generated tables differ (fft 0x" + num.ToString("X8") + ", mdct 0x" + num2.ToString("X8") + ")";
			}
			OpusEncoder enc = NewEncoder();
			OpusDecoder dec = NewDecoder();
			short[] array = new short[320];
			short[] array2 = new short[320];
			byte[] scratch = new byte[320];
			double num3 = 0.0;
			double num4 = 0.0;
			double num5 = 0.0;
			int num6 = 0;
			int num7 = 25;
			float[] array3 = new float[num7 * 320];
			for (int i = 0; i < num7; i++)
			{
				for (int j = 0; j < 320; j++)
				{
					float num8 = (float)(0.3 * Math.Sin(num3));
					num3 += Math.PI / 8.0;
					array[j] = (short)(num8 * 32767f);
					num4 += (double)(num8 * num8);
				}
				byte[] array4 = Encode(enc, array, 0, scratch);
				if (array4 == null)
				{
					return selfTest = "the encoder failed on frame " + i;
				}
				if (!IsTwentyMsFrame(array4))
				{
					return selfTest = "the encoder made a packet that is not one 20 ms frame (TOC 0x" + array4[0].ToString("X2") + ")";
				}
				num6 += array4.Length;
				if (Decode(dec, array4, array2) != 320)
				{
					return selfTest = "the decoder failed on frame " + i;
				}
				for (int k = 0; k < 320; k++)
				{
					float num9 = (array3[i * 320 + k] = (float)array2[k] / 32768f);
					if (i >= 5)
					{
						num5 += (double)(num9 * num9);
					}
				}
			}
			double num10 = Math.Sqrt(num4 / (double)(num7 * 320));
			double num11 = Math.Sqrt(num5 / (double)((num7 - 5) * 320));
			float num12 = VoiceMeter.Dominant(array3, 1600, array3.Length - 1600, 16000, 100, 4000, 10);
			if (num11 < num10 * 0.5 || Math.Abs(num12 - 1000f) > 30f)
			{
				return selfTest = "a 1 kHz tone came back as rms " + num11.ToString("F3") + " at " + num12.ToString("F0") + " Hz";
			}
			return selfTest = "ok: tables match, 1 kHz tone rms " + num10.ToString("F3") + " -> " + num11.ToString("F3") + " at " + num12.ToString("F0") + " Hz, " + num6 / num7 + " bytes a frame";
		}
		catch (Exception ex)
		{
			return selfTest = "threw " + ex.GetType().Name + ": " + ex.Message;
		}
	}

	private static uint Fnv(short[] a)
	{
		uint num = 2166136261u;
		foreach (short num2 in a)
		{
			num ^= (ushort)num2;
			num *= 16777619;
		}
		return num;
	}
}
