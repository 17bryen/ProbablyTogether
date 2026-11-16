using System;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

namespace ProbablyTogether;

internal static class Boot
{
	internal static bool Started;

	internal static ModManifest Manifest;

	internal const string MenuScene = "EmporiumMenu";

	internal const string ShopScene = "InventoryScene";

	internal static string Scene
	{
		get
		{
			try
			{
				Scene activeScene = SceneManager.GetActiveScene();
				return activeScene.name;
			}
			catch
			{
				return "";
			}
		}
	}

	internal static void Init(ModManifest manifest)
	{
		Manifest = manifest;
	}

	internal static void Enable()
	{
		if (!Started)
		{
			Started = true;
			Log.Info(Mod.Name + " " + Mod.Version + " (protocol " + Protocol.Version + ", based on the upstream mod 0.2.8) on game " + Application.version + "; switches: " + Args.Describe());
			if (Paths.Redirecting)
			{
				// A copy with its own data folder keeps its options file there: set the save library's default file.
				try
				{
					ES3Settings.defaultSettings.path = Paths.File("SaveFile.es3");
				}
				catch (System.Exception ex)
				{
					Log.Error("data folder: the options file could not be pointed at " + Paths.Root + ": " + ex.Message);
				}
				Log.Info("data folder: " + Paths.Root + " (saves, save index and settings; the account folder " + Paths.Original + " keeps only the mod list and the studio's trust records)");
			}
			GameObject val = new GameObject("ProbablyTogether.Driver");
			Object.DontDestroyOnLoad((Object)val);
			val.AddComponent<Driver>();
			if (Args.Server)
			{
				Net.Role = Role.Server;
			}
			else if (Args.Host)
			{
				Net.Role = Role.Host;
				Paths.KeepSettings = true;
			}
			ModList.Enable();
			SystemHooks.Init();
			Core.Start();
		}
	}

	internal static void Disable()
	{
		try
		{
			Core.Shutdown("mod disabled");
		}
		catch (Exception ex)
		{
			Log.Warn("shutdown: " + ex.Message);
		}
		try
		{
			ModList.Disable();
		}
		catch
		{
		}
	}
}
