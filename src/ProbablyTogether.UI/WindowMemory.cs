using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace ProbablyTogether.UI;

/// Keeps each player's own window places, tabs and open windows across the morning reload, in a file per shop.
/// Looks only: shop state is not changed.
internal static class WindowMemory
{
	private sealed class Win
	{
		public float x;

		public float y;

		public string tabs = "";
	}

	private sealed class Saved
	{
		public Dictionary<int, Win> pos = new Dictionary<int, Win>();

		/// Open when last looked at, bottom to top.
		public List<int> open = new List<int>();
	}

	private static Saved mine;

	private static string mineRun;

	internal static int Reopened;

	internal static string LastSkipped = "";

	/// Only a non-server client; the host keeps its windows through the night.
	private static bool Applies => Net.IsOnline && Net.IsClient && !Net.IsServer;

	private static string Run => Game.Store?.runID ?? "";

	private static string FileFor(string run)
	{
		string text = new string((run ?? "").Where(char.IsLetterOrDigit).ToArray());
		return Paths.Under("ProbablyTogether/windows/" + ((text.Length > 0) ? text : "none") + ".json");
	}

	private static Saved Mine()
	{
		string run = Run;
		if (mine != null && mineRun == run)
		{
			return mine;
		}
		mine = new Saved();
		mineRun = run;
		try
		{
			string path = FileFor(run);
			if (run.Length > 0 && System.IO.File.Exists(path))
			{
				mine = JsonConvert.DeserializeObject<Saved>(System.IO.File.ReadAllText(path)) ?? new Saved();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("windows: reading this shop's window layout threw: " + ex.Message);
			mine = new Saved();
		}
		return mine;
	}

	private static void Write()
	{
		if (mine == null || string.IsNullOrEmpty(mineRun))
		{
			return;
		}
		try
		{
			string path = FileFor(mineRun);
			System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));
			System.IO.File.WriteAllText(path, JsonConvert.SerializeObject(mine));
		}
		catch (Exception ex)
		{
			Log.Warn("windows: writing this shop's window layout threw: " + ex.Message);
		}
	}

	private static void Note(GameItem item, PixelWindow w)
	{
		if (item == null || w == null || item.contentWindow != w)
		{
			return;
		}
		Vector2 anchoredPosition = w.rectTransform.anchoredPosition;
		string tabs = "";
		try
		{
			tabs = item.GetContentTabsSelection() ?? "";
		}
		catch
		{
		}
		Mine().pos[item.GetUniqueID()] = new Win
		{
			x = anchoredPosition.x,
			y = anchoredPosition.y,
			tabs = tabs
		};
	}

	/// The game remembers a window's place: keep this player's own copy.
	internal static void Remembered(GameItem item, PixelWindow window)
	{
		if (Applies && !DayLog.Replaying && Game.ShopLoaded && window != null && window.IsVisible())
		{
			Note(item, window);
		}
	}

	/// A player's copy is about to reload the shop: what is open now, and where.
	internal static void BeforeLoad()
	{
		if (!Applies || !Game.ShopLoaded)
		{
			return;
		}
		try
		{
			Saved saved = Mine();
			List<int> list = new List<int>();
			foreach (PixelWindow item in (WindowsHandler.current?.visibleWindows ?? new List<PixelWindow>()).ToList())
			{
				if (item == null || !item.IsVisible())
				{
					continue;
				}
				foreach (GameItem parentItem in item.parentItems)
				{
					if (parentItem != null && parentItem.contentWindow == item)
					{
						Note(parentItem, item);
						int uniqueID = parentItem.GetUniqueID();
						if (!list.Contains(uniqueID))
						{
							list.Add(uniqueID);
						}
					}
				}
			}
			saved.open = list;
			Write();
			Log.Info("windows: " + list.Count + " window(s) open before the shop reloads; " + saved.pos.Count + " place(s) of this player's kept");
		}
		catch (Exception ex)
		{
			Log.Warn("windows: noting the open windows threw: " + ex.Message);
		}
	}

	/// The shop has loaded and the day caught up: this player's places over the host's, the windows open again.
	internal static void AfterLoad()
	{
		Reopened = 0;
		LastSkipped = "";
		if (!Applies || !Game.ShopLoaded)
		{
			return;
		}
		try
		{
			Saved saved = Mine();
			PlayerStore store = Game.Store;
			if (store.contentWindowPositions == null)
			{
				store.contentWindowPositions = new Dictionary<int, Vector2>();
			}
			foreach (KeyValuePair<int, Win> item in saved.pos)
			{
				store.contentWindowPositions[item.Key] = new Vector2(item.Value.x, item.Value.y);
			}
			if (saved.open.Count == 0 || ItemMouseDoubleClickHandler.current == null)
			{
				return;
			}
			Dictionary<int, GameItem> dictionary = new Dictionary<int, GameItem>();
			foreach (GameItem item2 in Items.All())
			{
				if (item2 != null)
				{
					dictionary[item2.GetUniqueID()] = item2;
				}
			}
			List<string> list = new List<string>();
			using (ItemsUse.Quiet())
			{
				foreach (int item3 in saved.open)
				{
					if (!dictionary.TryGetValue(item3, out var value) || value.contentWindow == null)
					{
						list.Add(item3.ToString(CultureInfo.InvariantCulture) + " gone");
						continue;
					}
					try
					{
						if (!value.contentWindow.IsVisible())
						{
							ItemMouseDoubleClickHandler.current.OpenContentAction(value, Vector2.zero);
						}
						if (!value.contentWindow.IsVisible())
						{
							list.Add(value.identifier + " would not open");
							continue;
						}
						if (saved.pos.TryGetValue(item3, out var value2))
						{
							value.contentWindow.rectTransform.anchoredPosition = new Vector2(value2.x, value2.y);
							value.contentWindow.FixBounds();
							if (!string.IsNullOrEmpty(value2.tabs))
							{
								value.ApplyContentTabsSelection(value2.tabs);
							}
						}
						value.contentWindow.ToFront();
						Reopened++;
					}
					catch (Exception ex)
					{
						list.Add(value.identifier + " threw " + ex.GetType().Name);
					}
				}
			}
			LastSkipped = string.Join(", ", list);
			Log.Info("windows: " + Reopened + " of " + saved.open.Count + " window(s) open again where they were" + ((list.Count > 0) ? (" (not: " + LastSkipped + ")") : ""));
		}
		catch (Exception ex2)
		{
			Log.Warn("windows: opening the windows again threw: " + ex2.Message);
		}
	}
}

/// The game notes a window's place on close and save; the player's copy keeps its own.
[HarmonyPatch(typeof(GameItem), "RememberContentWindowPositionIfMine")]
internal static class WindowMemoryRememberPatch
{
	private static void Prefix(GameItem __instance, PixelWindow window)
	{
		try
		{
			WindowMemory.Remembered(__instance, window);
		}
		catch
		{
		}
	}
}
