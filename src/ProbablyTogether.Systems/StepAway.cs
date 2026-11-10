using System;
using ProbablyTogether.UI;
using UnityEngine;

namespace ProbablyTogether.Systems;

/// A key that releases the counter lock so another player can take the customer.
internal static class StepAway
{
	private const string SaveKey = "bellun_probably_together.stepAwayKey";

	private static KeyCode key = KeyCode.C;

	private static bool keyLoaded;

	private static bool keyBindAdded;

	private static string lastTakenKeyLog;

	private static float lastPressAt = -10f;

	public static KeyCode Key
	{
		get
		{
			LoadKey();
			return key;
		}
		set
		{
			if (!Pointing.KeyboardKey(value))
			{
				Log.Info("stepaway: key " + value.ToString() + " refused (a keyboard key only)");
				return;
			}
			key = value;
			keyLoaded = true;
			try
			{
				ES3.Save<int>(SaveKey, (int)value);
			}
			catch (Exception ex)
			{
				Log.Warn("stepaway: key not saved: " + ex.Message);
			}
		}
	}

	private static void LoadKey()
	{
		if (keyLoaded)
		{
			return;
		}
		keyLoaded = true;
		try
		{
			if (ES3.KeyExists(SaveKey))
			{
				key = (KeyCode)ES3.Load<int>(SaveKey);
			}
		}
		catch
		{
			key = KeyCode.C;
		}
		if (!Pointing.KeyboardKey(key))
		{
			key = KeyCode.C;
		}
	}

	private static void EnsureKeyBind()
	{
		if (keyBindAdded || !Ui.AtMenu)
		{
			return;
		}
		keyBindAdded = true;
		try
		{
			GameOptionRegistry.EnsureVanilla();
			ModHelper.AddGameOptionKeyBind("bellun_probably_together", "keybind", "stepaway", (Func<string>)(() => Lang.Tr("Co-op step away")), (Func<KeyCode>)(() => Key), (Action<KeyCode>)delegate(KeyCode k)
			{
				Key = k;
			}, (Func<string>)(() => Lang.Tr("On a co-op server: leave the counter so another player can take the customer.")));
		}
		catch (Exception ex)
		{
			Log.Warn("stepaway: key bind row not added: " + ex.Message);
		}
	}

	[SystemTick]
	private static void Tick()
	{
		if (Net.IsDedicated || Args.Server)
		{
			return;
		}
		EnsureKeyBind();
		if (!Net.IsClient || !Net.IsLive || !Game.ShopLoaded)
		{
			return;
		}
		KeyCode k = Key;
		if ((int)k == 0 || !Input.GetKeyDown(k) || Pointing.GameBusy())
		{
			return;
		}
		string taken = Pointing.GameBindUsing(k);
		if (taken != null)
		{
			if (lastTakenKeyLog != taken)
			{
				lastTakenKeyLog = taken;
				Log.Info("stepaway: the key " + k.ToString() + " is also the game's '" + taken + "' key: the game's action wins (pick another key in Options)");
			}
			return;
		}
		if (Time.realtimeSinceStartup - lastPressAt < 0.5f)
		{
			return;
		}
		lastPressAt = Time.realtimeSinceStartup;
		if (!Negotiation.LocalHoldsCounter)
		{
			Notices.Show(Lang.Tr("You are not at the counter."), "stepaway");
			return;
		}
		Customers.RequestReleaseCounter();
		Notices.Show(Lang.Tr("You stepped away from the counter."), "stepaway");
		Log.Info("stepaway: asked the server to release the counter");
	}
}
