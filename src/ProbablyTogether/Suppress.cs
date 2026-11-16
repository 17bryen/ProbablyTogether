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
	}
}
