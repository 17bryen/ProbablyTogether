using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

internal sealed class VoiceCapture
{
	private string device;

	private AudioClip clip;

	private int clipLen;

	private int channels;

	private int lastPos;

	private double fakePhase;

	private double fakeLast = -1.0;

	private double resPos;

	private double lp;

	private float resPrev;

	internal bool Running { get; private set; }

	internal bool UsingMic { get; private set; }

	internal string Problem { get; private set; } = "";

	internal int DeviceRate { get; private set; }

	internal bool Start(float fakeHz)
	{
		if (Running)
		{
			return true;
		}
		Problem = "";
		if (fakeHz > 0f)
		{
			UsingMic = false;
			fakeLast = -1.0;
			Running = true;
			DeviceRate = 16000;
			return true;
		}
		try
		{
			string[] devices = Microphone.devices;
			if (devices == null || devices.Length == 0)
			{
				Problem = "no microphone found";
				return false;
			}
			device = null;
			int num = 0;
			int num2 = 0;
			try
			{
				Microphone.GetDeviceCaps(device, out num, out num2);
			}
			catch
			{
			}
			int num3 = ((num == 0 && num2 == 0) ? 16000 : Mathf.Clamp(16000, num, num2));
			clip = Microphone.Start(device, true, 1, num3);
			if ((Object)(object)clip == (Object)null)
			{
				Problem = "the microphone did not start";
				return false;
			}
			clipLen = clip.samples;
			channels = Math.Max(1, clip.channels);
			DeviceRate = ((clip.frequency > 0) ? clip.frequency : num3);
			lastPos = 0;
			resPos = 0.0;
			lp = 0.0;
			resPrev = 0f;
			UsingMic = true;
			Running = true;
			return true;
		}
		catch (Exception ex)
		{
			Problem = "microphone: " + ex.Message;
			return false;
		}
	}

	internal void Stop()
	{
		if (!Running)
		{
			return;
		}
		Running = false;
		if (UsingMic)
		{
			try
			{
				Microphone.End(device);
			}
			catch
			{
			}
		}
		if ((Object)(object)clip != (Object)null)
		{
			try
			{
				Object.Destroy((Object)(object)clip);
			}
			catch
			{
			}
			clip = null;
		}
		UsingMic = false;
	}

	internal void Pull(List<short> output, float fakeHz, float fakeAmp)
	{
		if (!Running)
		{
			return;
		}
		if (!UsingMic)
		{
			PullFake(output, fakeHz, fakeAmp);
			return;
		}
		int position;
		try
		{
			position = Microphone.GetPosition(device);
		}
		catch
		{
			return;
		}
		if (position < 0 || clipLen <= 0)
		{
			return;
		}
		int num = position - lastPos;
		if (num < 0)
		{
			num += clipLen;
		}
		if (num != 0)
		{
			float[] array = new float[num * channels];
			try
			{
				clip.GetData(array, lastPos);
			}
			catch
			{
				return;
			}
			lastPos = position;
			Resample(array, num, output);
		}
	}

	private void PullFake(List<short> output, float hz, float amp)
	{
		double realtimeSinceStartupAsDouble = Time.realtimeSinceStartupAsDouble;
		if (fakeLast < 0.0)
		{
			fakeLast = realtimeSinceStartupAsDouble - 0.02;
		}
		int num = (int)((realtimeSinceStartupAsDouble - fakeLast) * 16000.0);
		if (num <= 0)
		{
			return;
		}
		if (num > 16000)
		{
			fakeLast = realtimeSinceStartupAsDouble - 1.0;
			num = 16000;
		}
		fakeLast += (double)num / 16000.0;
		double num2 = Math.PI * 2.0 * (double)hz / 16000.0;
		for (int i = 0; i < num; i++)
		{
			output.Add((short)(Math.Max(-1.0, Math.Min(1.0, (double)amp * Math.Sin(fakePhase))) * 32767.0));
			fakePhase += num2;
			if (fakePhase > Math.PI * 2.0)
			{
				fakePhase -= Math.PI * 2.0;
			}
		}
	}

	private void Resample(float[] interleaved, int frames, List<short> output)
	{
		int deviceRate = DeviceRate;
		if (deviceRate == 16000)
		{
			for (int i = 0; i < frames; i++)
			{
				output.Add(ToShort(Mix(interleaved, i)));
			}
			return;
		}
		double num = (double)deviceRate / 16000.0;
		double num2 = ((deviceRate > 16000) ? Math.Min(1.0, Math.PI * 14000.0 / (double)deviceRate) : 1.0);
		for (int j = 0; j < frames; j++)
		{
			lp += num2 * ((double)Mix(interleaved, j) - lp);
			float num3 = (float)lp;
			while (resPos <= 1.0)
			{
				output.Add(ToShort(resPrev + (num3 - resPrev) * (float)resPos));
				resPos += num;
			}
			resPos -= 1.0;
			resPrev = num3;
		}
	}

	private float Mix(float[] a, int frame)
	{
		if (channels == 1)
		{
			return a[frame];
		}
		float num = 0f;
		for (int i = 0; i < channels; i++)
		{
			num += a[frame * channels + i];
		}
		return num / (float)channels;
	}

	private static short ToShort(float v)
	{
		return (short)(Mathf.Clamp(v, -1f, 1f) * 32767f);
	}
}
