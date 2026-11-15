using System;
using UnityEngine;

namespace ProbablyTogether.UI;

public static class Ui
{
	private static bool inited;

	internal static bool Headless;

	private static ClientState lastState;

	private static bool? runInBackgroundBefore;

	private static bool modsChecked;

	internal static bool InShop => Game.ShopLoaded;

	internal static bool AtMenu => Boot.Scene == "EmporiumMenu";

	internal static bool ShopOnline
	{
		get
		{
			if (Net.HasLocalPlayer && Net.IsLive)
			{
				return InShop;
			}
			return false;
		}
	}

	[SystemInit]
	internal static void Init()
	{
		if (inited)
		{
			return;
		}
		inited = true;
		Headless = Args.Server || Net.IsDedicated;
		try
		{
			Chat.Register();
			Presence.Register();
			Net.PlayerJoined += OnPlayerJoined;
			Net.PlayerLeft += OnPlayerLeft;
			Net.Disconnected += OnDisconnected;
			Snapshot.Loaded += OnSnapshotLoaded;
			Snapshot.ClientReady += Presence.ServerOnClientReady;
			Locks.Changed += OnLockChanged;
			if (!Headless)
			{
				ClientHooks();
			}
			Log.Info("ui: presence and menus ready (" + (Headless ? "server: relays only, no UI" : "client") + ")");
		}
		catch (Exception ex)
		{
			Log.Error("ui: init failed: " + ex);
		}
	}

	private static void ClientHooks()
	{
		try
		{
			Security.AimChanged += Presence.OnAimChanged;
		}
		catch (Exception ex)
		{
			Log.Warn("ui: aim label not hooked: " + ex.Message);
		}
		try
		{
			Notices.ClientInit();
		}
		catch (Exception ex2)
		{
			Log.Warn("ui: notices not hooked: " + ex2.Message);
		}
		try
		{
			NewRun.ClientInit();
		}
		catch (Exception ex3)
		{
			Log.Warn("ui: new-run plate not hooked: " + ex3.Message);
		}
	}

	private static void OnPlayerJoined(PlayerInfo p)
	{
		if (!Headless && Net.HasLocalPlayer && p != null && p.Id != Net.LocalPlayer)
		{
			Chat.Local(Kit.Name(p.Name) + " joined the shop.");
		}
	}

	private static void OnPlayerLeft(PlayerInfo p)
	{
		if (p != null)
		{
			if (Net.IsServer)
			{
				Presence.ServerForget(p.Id);
				Chat.ServerForget(p.Id);
			}
			else if (!Headless && Net.HasLocalPlayer && p.Id != Net.LocalPlayer)
			{
				Presence.ClientForget(p.Id);
				Chat.Local(Kit.Name(p.Name) + " left the shop.");
			}
		}
	}

	private static void OnDisconnected(string why)
	{
		if (!Headless)
		{
			modsChecked = false;
			Presence.ClientReset();
			Chat.ClientReset();
			NewRun.Reset();
			Notices.ClientReset();
			JoinMenu.OnDisconnected(why);
		}
	}

	private static void OnSnapshotLoaded()
	{
		if (!Headless)
		{
			JoinMenu.OnJoined();
			Chat.OnShopLoaded();
			WarnAboutMods();
			Presence.OnShopLoaded();
		}
	}

	private static void WarnAboutMods()
	{
		if (!modsChecked)
		{
			modsChecked = true;
			string text = ModList.ExtraModsLine(Client.ServerMods);
			if (text != null)
			{
				Log.Info("ui: mod warning shown: " + text);
				Chat.Local(text);
			}
			string serverBuild = Client.ServerBuild;
			string id = GameBuild.Id;
			if (serverBuild.Length > 0 && id.Length > 0 && serverBuild != id)
			{
				string text2 = "This server runs a different build of Probably Stolen, so parts of the game may not match the other players. Make sure the game is up to date in Steam" + ((GameBuild.Beta.Length > 0) ? " and not on a beta (Properties > Betas > None)." : ".");
				Log.Info("ui: build warning shown (server " + serverBuild + ", mine " + id + ((GameBuild.Beta.Length > 0) ? (", beta " + GameBuild.Beta) : "") + "): " + text2);
				Chat.Local(text2);
			}
		}
	}

	private static void OnLockChanged(string name, int holder)
	{
		if (!Headless && Net.HasLocalPlayer)
		{
			Presence.OnLockChanged(name, holder);
		}
	}

	[SystemTick]
	internal static void Tick()
	{
		if (!inited || Headless)
		{
			return;
		}
		try
		{
			ClientTick();
		}
		catch (Exception e)
		{
			UiErrors.Report("tick", e);
		}
	}

	private static void ClientTick()
	{
		ClientState clientState = Net.ClientState;
		if (clientState != lastState)
		{
			if (clientState == ClientState.Loading)
			{
				ClearStaticPauses();
			}
			if (clientState != ClientState.Idle && !runInBackgroundBefore.HasValue)
			{
				runInBackgroundBefore = Application.runInBackground;
				Application.runInBackground = true;
			}
			else if (clientState == ClientState.Idle && runInBackgroundBefore.HasValue)
			{
				Application.runInBackground = runInBackgroundBefore.Value;
				runInBackgroundBefore = null;
			}
			lastState = clientState;
		}
		Kit.Tick();
		JoinMenu.Tick();
		HostFlow.Tick();
		ShotTour.Tick();
		UpdateNotice.Tick();
		LoadingScreen.Tick();
		Overlay.Tick();
		Presence.Tick();
		Chat.Tick();
		ReadyBar.Tick();
		NewRun.Tick();
		if (ShopOnline)
		{
			Notices.Tick();
		}
	}

	private static void ClearStaticPauses()
	{
		try
		{
			Interactable.isPaused = false;
			TooltipContainer.isPaused = false;
		}
		catch
		{
		}
	}
}
