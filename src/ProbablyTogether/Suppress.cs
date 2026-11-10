using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

internal static class Suppress
{
	private static object tutorialFor;

	private static bool serverLogged;

	private static bool hooked;

	internal static int BlockedAchievements;

	internal static int BlockedDevTools;

	private static readonly MethodInfo consoleDeactivate = AccessTools.Method("QFSW.QC.QuantumConsole:Deactivate", (Type[])null, (Type[])null);

	private static readonly MethodInfo consoleIsActive = AccessTools.PropertyGetter("QFSW.QC.QuantumConsole:IsActive");

	private static readonly Type consoleType = AccessTools.TypeByName("QFSW.QC.QuantumConsole");

	internal static bool DevToolAllowed(string what)
	{
		if (!Net.IsClient)
		{
			return true;
		}
		BlockedDevTools++;
		if (BlockedDevTools <= 3 || BlockedDevTools % 50 == 0)
		{
			Log.Info("client: " + what + " blocked while connected to a server (" + BlockedDevTools + ")");
		}
		return false;
	}

	internal static void Hook()
	{
		if (!hooked)
		{
			hooked = true;
			Snapshot.Loaded += OnClientLoaded;
		}
	}

	internal static void Tick()
	{
		if (!Net.IsDedicated || Net.HostAway)
		{
			return;
		}
		if (!serverLogged)
		{
			serverLogged = true;
			Log.Info("server: Steam achievements, analytics and the tutorial are off on this dedicated server");
		}
		EmporiumAnalytics.isAnalyticsInitialized = false;
		PlayerStore store = Game.Store;
		if (store == null || store == tutorialFor || !Game.ShopLoaded)
		{
			return;
		}
		tutorialFor = store;
		if (store.tutorialEnable)
		{
			Log.Info("server: tutorial turned off for this run (no player sees the server's overlays)");
		}
		store.tutorialEnable = false;
		try
		{
			TutorialUIManager instance = TutorialUIManager.Instance;
			if (instance != null)
			{
				instance.CloseAllTutorial();
			}
		}
		catch
		{
		}
	}

	private static void OnClientLoaded()
	{
		if (!Net.IsClient || consoleType == null || consoleDeactivate == null)
		{
			return;
		}
		try
		{
			Object[] array = Object.FindObjectsOfType(consoleType);
			bool flag = default(bool);
			foreach (Object obj in array)
			{
				if (!(consoleIsActive == null))
				{
					object obj2 = consoleIsActive.Invoke(obj, null);
					int num;
					if (obj2 is bool)
					{
						flag = (bool)obj2;
						num = 1;
					}
					else
					{
						num = 0;
					}
					if (((uint)num & (flag ? 1u : 0u)) == 0)
					{
						continue;
					}
				}
				consoleDeactivate.Invoke(obj, null);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("closing the dev console: " + ex.Message);
		}
	}
}
