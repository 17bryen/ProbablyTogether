using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether.Bots;

[HarmonyPatch(typeof(EmporiumOption), "ApplyFpsCap")]
internal static class BotFps
{
	private static int fps;

	private static bool Prepare()
	{
		if (fps == 0)
		{
			fps = -1;
			string text = Args.Get("--br-fps");
			if (text != null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result >= 1 && result <= 120)
			{
				fps = result;
			}
			if (fps > 0)
			{
				Apply();
				Log.Info("lab frame cap " + fps + " fps (--br-fps)");
			}
		}
		return fps > 0;
	}

	private static void Postfix()
	{
		Apply();
	}

	private static void Apply()
	{
		QualitySettings.vSyncCount = 0;
		Application.targetFrameRate = fps;
	}
}
