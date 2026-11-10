using System;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch(typeof(PlayerStore), "WriteSlotFile")]
internal static class WriteSlotFilePatch
{
	private static bool Prefix(PlayerStore __instance)
	{
		if (!Snapshot.Capturing)
		{
			return true;
		}
		try
		{
			ES3Writer val = ES3Writer.Create(new ES3Settings("probably_together/snapshot.es3", (ES3Settings)null));
			try
			{
				val.Write<PlayerStore>("playerStore", (object)__instance);
				val.Write<StoreStation>("storeStation", (object)StoreStation.Instance);
				val.Save();
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		catch (Exception captureError)
		{
			Snapshot.CaptureError = captureError;
		}
		return false;
	}
}
