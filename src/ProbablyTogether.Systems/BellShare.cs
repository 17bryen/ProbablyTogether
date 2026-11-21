using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

public sealed class MsgBellRing
{
	public int by;
}

/// Shares the counter bell's look and sound: a ring goes to the server, which passes it on to every other copy.
internal static class BellShare
{
	private static bool inited;

	private static StoreBell bell;

	private static float releaseAt = -1f;

	private static readonly Dictionary<int, float> lastRing = new Dictionary<int, float>();

	internal static int Heard;

	internal static int Relayed;

	[SystemInit]
	private static void Init()
	{
		if (inited)
		{
			return;
		}
		inited = true;
		Net.On<MsgBellRing>("bell.ring", ServerRing, null);
		Net.On<MsgBellRing>("bell.rang", null, ClientRang);
	}

	/// This player rang the bell (the game already played it here).
	internal static void Rang()
	{
		if (Net.IsOnline && Net.IsLive && Net.HasLocalPlayer && !Net.Applying && !DayLog.Replaying && !Outings.Private)
		{
			Net.ToServer("bell.ring", new MsgBellRing());
		}
	}

	private static void ServerRing(MsgBellRing m, int from)
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (lastRing.TryGetValue(from, out var value) && realtimeSinceStartup - value < 0.3f)
		{
			return;
		}
		lastRing[from] = realtimeSinceStartup;
		MsgBellRing msg = new MsgBellRing
		{
			by = from
		};
		foreach (int item in new List<int>(Net.Players.Keys))
		{
			// The host's own player is not a connection, so include it explicitly (HostLoop passes "bell.rang" on).
			if (item != from && (Net.IsPlayerLive(item) || (Net.IsHost && item == Net.LocalPlayer)))
			{
				Net.ToPlayer(item, "bell.rang", msg);
				Relayed++;
			}
		}
	}

	private static void ClientRang(MsgBellRing m)
	{
		if (!Game.ShopLoaded || DayLog.Replaying || (m != null && m.by == Net.LocalPlayer))
		{
			return;
		}
		try
		{
			if ((Object)(object)bell == (Object)null)
			{
				bell = Object.FindObjectOfType<StoreBell>();
			}
			if ((Object)(object)bell == (Object)null)
			{
				return;
			}
			Heard++;
			if ((Object)(object)bell.audioSource != (Object)null)
			{
				bell.audioSource.Play();
			}
			if ((Object)(object)bell.buttonRenderer != (Object)null && (Object)(object)bell.spriteClick != (Object)null)
			{
				bell.buttonRenderer.sprite = bell.spriteClick;
				releaseAt = Time.realtimeSinceStartup + 0.18f;
			}
		}
		catch (Exception ex)
		{
			Log.Warn("bell: ringing another player's bell here threw: " + ex.Message);
		}
	}

	[SystemTick]
	private static void Tick()
	{
		if (releaseAt < 0f || Time.realtimeSinceStartup < releaseAt)
		{
			return;
		}
		releaseAt = -1f;
		try
		{
			if ((Object)(object)bell != (Object)null && (Object)(object)bell.buttonRenderer != (Object)null && (Object)(object)bell.spriteNormal != (Object)null && bell.buttonRenderer.sprite == bell.spriteClick)
			{
				bell.buttonRenderer.sprite = bell.spriteNormal;
			}
		}
		catch
		{
		}
	}
}

/// When the bell rang here (not under the game's lock), the others hear it too.
[HarmonyPatch(typeof(StoreBell), "RingBell")]
internal static class BellSharePatch
{
	private static void Postfix()
	{
		try
		{
			PlayerStore instance = PlayerStore.Instance;
			if (instance != null && instance.lockNextBellFlag <= 0)
			{
				BellShare.Rang();
			}
		}
		catch
		{
		}
	}
}
