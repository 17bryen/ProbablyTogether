using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

/// Takes the morning in place: items are checked against the server's fingerprint, the two save objects are loaded
/// into the running shop and the after-load steps run; anything unexpected falls back to a reload.
internal static class NightInPlace
{
	internal static int Mornings;

	internal static string LastWhyNot = "";

	/// Off: every morning reloads the shop.
	internal static bool Enabled = true;

	private static int nightDay = -1;

	private static bool clean;

	private static List<string> finalLines;

	internal static void NightStarts(int day)
	{
		nightDay = day;
		clean = true;
		finalLines = null;
	}

	internal static void Bad(string why)
	{
		if (clean)
		{
			LastWhyNot = why;
		}
		clean = false;
	}

	internal static void Final(int day, List<string> lines)
	{
		if (day == nightDay)
		{
			finalLines = lines;
		}
	}

	/// Takes the morning snapshot in place, or returns false so the copy reloads.
	internal static bool TryApply(MsgSnapshot m, string file)
	{
		try
		{
			string text = WhyNot(m);
			// A morning sent again later the same day (a resync) always reloads.
			nightDay = -1;
			finalLines = null;
			if (text != null)
			{
				LastWhyNot = text;
				Log.Info("night: this morning reloads the shop (" + text + ")");
				return false;
			}
			PlayerStore store = Game.Store;
			ES3.LoadInto("playerStore", file, store);
			ES3.LoadInto("storeStation", file, StoreStation.Instance);
			AfterLoad(store);
			store.saveSlotId = 990077;
			Mornings++;
			LastWhyNot = "";
			Log.Info("night: the morning of day " + m.day + " taken in place (no reload)");
			return true;
		}
		catch (Exception ex)
		{
			LastWhyNot = "threw " + ex.GetType().Name;
			Log.Error("night: taking the morning in place threw (the shop reloads): " + ex);
			return false;
		}
	}

	private static string WhyNot(MsgSnapshot m)
	{
		if (!Enabled)
		{
			return "switched off";
		}
		if (!Game.ShopLoaded || Game.Store == null || StoreStation.Instance == null)
		{
			return "no shop here";
		}
		if (nightDay < 0 || m.day != nightDay + 1)
		{
			return "no night here for day " + (m.day - 1);
		}
		if (!clean)
		{
			return "the night differed here: " + LastWhyNot;
		}
		if (finalLines == null)
		{
			return "the server's last fingerprint did not come";
		}
		Dictionary<int, string> dictionary = NightCheck.Lines();
		int num = 0;
		string text = null;
		foreach (string finalLine in finalLines)
		{
			int num2 = finalLine.IndexOf('|');
			if (num2 > 0 && int.TryParse(finalLine.Substring(0, num2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result != 0)
			{
				if (!dictionary.TryGetValue(result, out var value) || (value != finalLine && !OnlyTemperatures(finalLine, value)))
				{
					num++;
					text = text ?? ((value == null) ? ("not here: " + NightCheck.Head(finalLine)) : NightCheck.Explain(finalLine, value));
				}
				dictionary.Remove(result);
			}
		}
		dictionary.Remove(0);
		if (num > 0 || dictionary.Count > 0)
		{
			return "the items differ from the server's at the end of the night (" + num + " different, " + dictionary.Count + " only here; " + (text ?? ("only here: " + NightCheck.Head(dictionary.Values.First()))) + ")";
		}
		return null;
	}

	/// True when the lines differ only in temperatures, which the server keeps pushing and may be newer here.
	private static bool OnlyTemperatures(string server, string here)
	{
		string[] array = server.Split(new char[1] { '|' }, 5);
		string[] array2 = here.Split(new char[1] { '|' }, 5);
		if (array.Length < 5 || array2.Length < 5 || array[0] != array2[0] || array[1] != array2[1] || array[2] != array2[2] || array[3] != array2[3])
		{
			return false;
		}
		return WithoutTemperatures(array[4]) == WithoutTemperatures(array2[4]);
	}

	private static string WithoutTemperatures(string tags)
	{
		return string.Join(",", from t in tags.Split(',')
			where t.Length > 0 && t.Substring(0, Math.Max(0, t.IndexOf('='))).IndexOf("TEMP", StringComparison.Ordinal) < 0
			select t);
	}

		/// The game's LoadGame after-load steps, minus anything that builds or decodes items.
	private static void AfterLoad(PlayerStore ps)
	{
		ps.contrabandMarkupCritial = 150;
		StoreService.HealIntroducedAfterLoad();
		StoreClientData.HealButcherArrestAfterLoad();
		NetworkUpgrade.AddMissingAfterLoad();
		StoreReputation.AddMissingPerksAfterLoad();
		ps.difficultyId = Difficulty.ResolveId(ps.difficultyId, ps.isHardMode);
		ps.isHardMode = Difficulty.IsHardOrAbove();
		ps.LoadPower();
		Try("poster", StorePoster.LoadStorePoster);
		Try("shotgun", Shotgun.LoadShotgun);
		Try("pc", delegate
		{
			if (PCUIManager.Instance != null && PCUIManager.Instance.storePC != null)
			{
				PCUIManager.Instance.storePC.RefreshState();
			}
		});
		ps.savedItemFeatureList?.Clear();
		Try("always", ps.AlwayCall);
		Try("vending", delegate
		{
			StoreVendingMachine.Instance.gameObject.SetActive(ps.isVendingMachineUnlocked);
			if (ps.isVendingMachineUnlocked)
			{
				StoreVendingMachine.Instance.UpdateSprite();
			}
			StoreVendingFountain.Instance.gameObject.SetActive(ps.isVendingFountainUnlocked);
			if (ps.isVendingFountainUnlocked)
			{
				StoreVendingFountain.Instance.UpdateSprite();
			}
		});
		Try("permits", PermitHelper.ReapplyPermits);
		// End the night as the game does: lift the blackout and open the morning report.
		Try("blackout", DayNight.ClearBlackout);
		Try("report", delegate
		{
			if (ps.nightLogs != null && ps.nightLogs.Count > 0 && StartOfDayUIManager.Instance != null)
			{
				StartOfDayUIManager.Instance.ShowMorningReport();
			}
		});
	}

	private static void Try(string what, Action a)
	{
		try
		{
			a();
		}
		catch (Exception ex)
		{
			Log.Warn("night: after-load step '" + what + "' threw: " + ex.Message);
		}
	}
}
