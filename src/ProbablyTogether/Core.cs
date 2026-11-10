using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

internal static class Core
{
	private static bool menuLogged;

	private static bool menuJobsDone;

	private static float menuSince = -1f;

	internal static void Start()
	{
		if (Net.IsServer)
		{
			ServerMode.Start();
		}
	}

	internal static void Tick()
	{
		DrainInbox();
		if (Net.IsHost)
		{
			HostLoop.Drain();
		}
		if (Net.IsServer)
		{
			ServerMode.Tick();
			Server.Tick();
			Snapshot.ServerTick();
			Locks.Tick();
			Digest.ServerTick();
		}
		else
		{
			Client.Tick();
			Snapshot.ClientTick();
		}
		MenuJobs();
		SelfTest.Tick();
		SystemHooks.Tick();
	}

	private static void DrainInbox()
	{
		long ms = Clock.Ms;
		int num = 0;
		int num2 = ((Net.IsClient && Snapshot.IsLoading) ? 50000 : 1000);
		InboxItem x;
		while (Inbox.Queue.TryDequeue(out x))
		{
			try
			{
				Route(x);
			}
			catch (Exception ex)
			{
				string text = ((x.Kind == null) ? "-" : Text.Clip(x.Kind, 64));
				if (Log.Allow("inbox|" + x.Event.ToString() + "|" + text, 10000L, out var skipped))
				{
					Log.Error("handling " + x.Event.ToString() + " " + text + Log.More(skipped) + ": " + ex);
				}
				if (x.Conn != null && x.Conn.ServerSide && x.Event == InboxEvent.Frame)
				{
					x.Conn.Strike("frame " + text + " threw");
				}
			}
			if (++num >= num2 || Clock.Ms - ms > 40)
			{
				break;
			}
		}
	}

	private static void Route(InboxItem x)
	{
		if (x.Event == InboxEvent.ConnectFailed)
		{
			Client.OnConnectFailed(x.Text);
			return;
		}
		Conn conn = x.Conn;
		if (conn.ServerSide)
		{
			if (!Net.IsServer)
			{
				conn.Close("not a server");
				return;
			}
			switch (x.Event)
			{
			case InboxEvent.Opened:
				Server.OnOpened(conn);
				break;
			case InboxEvent.Closed:
				Server.OnClosed(conn, x.Text);
				break;
			case InboxEvent.Frame:
				Server.OnFrame(conn, x.Kind, x.Data);
				break;
			}
		}
		else
		{
			switch (x.Event)
			{
			case InboxEvent.Opened:
				Client.OnOpened(conn);
				break;
			case InboxEvent.Closed:
				Client.OnClosed(conn, x.Text);
				break;
			case InboxEvent.Frame:
				Client.OnFrame(conn, x.Kind, x.Data);
				break;
			}
		}
	}

	private static void MenuJobs()
	{
		if (!(Boot.Scene == "EmporiumMenu") || !((Object)(object)NewGameData.Instance != (Object)null))
		{
			menuSince = -1f;
			return;
		}
		if (menuSince < 0f)
		{
			menuSince = Time.realtimeSinceStartup;
		}
		if (!menuLogged)
		{
			menuLogged = true;
			Log.Info("main menu up after " + Time.realtimeSinceStartup.ToString("F1") + " s; data folder " + Paths.Root + "; " + Paths.RewrittenSites + " call site(s) redirected");
			SelfTest.AtMenu();
		}
		if (!menuJobsDone && !(Time.realtimeSinceStartup - menuSince < 2f))
		{
			menuJobsDone = true;
			if (!Net.IsServer && !string.IsNullOrEmpty(Args.Join))
			{
				Net.Connect(Args.Join, Args.Password ?? "", Args.Name);
			}
			if (!string.IsNullOrEmpty(Args.BotArg))
			{
				Hooks.StartBot(Args.BotArg);
			}
		}
	}

	internal static void Shutdown(string why)
	{
		if (Net.IsServer)
		{
			Server.Stopping = true;
			Server.Goodbye(why);
			Server.StopListening();
		}
		else if (Client.Connected)
		{
			Client.Disconnect(why);
		}
	}
}
